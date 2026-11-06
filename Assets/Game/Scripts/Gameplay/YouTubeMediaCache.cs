using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace FlashBeat.Gameplay
{
    /// <summary>
    /// Which files have to be fetched for a song so that Unity's VideoPlayer can
    /// show the picture and hear the sound.
    /// </summary>
    public sealed class MediaFormatPlan
    {
        /// <summary>True when a single progressive file carries both video and audio.</summary>
        public bool Combined;

        /// <summary>yt-dlp format selector for the picture file.</summary>
        public string VideoSelector = "";

        /// <summary>yt-dlp format selector for the sound file (empty when <see cref="Combined"/>).</summary>
        public string AudioSelector = "";

        /// <summary>Human readable summary used for the log evidence.</summary>
        public string Summary = "";

        public bool HasAudioFile => !Combined && !string.IsNullOrEmpty(AudioSelector);
    }

    /// <summary>Result of a cache lookup / download.</summary>
    public sealed class MediaFileResult
    {
        public bool Ok;
        public string Path = "";
        public string Detail = "";
        public bool FromCache;
    }

    // Downloads the YouTube media for a song into a local cache and hands back the
    // file paths. Local files are used because Media Foundation cannot read
    // googlevideo.com URLs (0x80070005, see YouTubeStreamResolver).
    public static class YouTubeMediaCache
    {
        public const string VideoFilePrefix = "video";
        public const string AudioFilePrefix = "audio";

        public static string CacheRoot
        {
            get { return Path.Combine(Application.persistentDataPath, "FlashBeatMedia"); }
        }

        public static string CacheDirectoryFor(string videoId)
        {
            var safe = string.IsNullOrEmpty(videoId) ? "unknown" : videoId.Replace('/', '_');
            return Path.Combine(CacheRoot, safe);
        }

        /// <summary>
        /// Chooses the formats to download.
        ///
        /// Preference order:
        ///   1. a progressive mp4 with H.264 video *and* audio (itag 22 / 18) — one
        ///      file, Unity plays picture and sound from it;
        ///   2. otherwise a video-only H.264 mp4 (<= 720p) plus an audio-only
        ///      AAC/m4a track, which are merged in the player (two VideoPlayers).
        ///
        /// YouTube dropped the progressive itag 18/22 for many videos (verified with
        /// `yt-dlp -F` on the FlashBeat song list on 2026-11-06), so branch 2 is the
        /// one that actually runs today.
        /// </summary>
        public static MediaFormatPlan SelectPlan(IList<YtDlpFormat> formats)
        {
            var plan = new MediaFormatPlan();

            if (formats == null || formats.Count == 0)
            {
                plan.VideoSelector = "bv[ext=mp4][height<=720]";
                plan.AudioSelector = "ba[ext=m4a]";
                plan.Summary = "fallback selectors (format list unavailable)";
                return plan;
            }

            // 1. progressive mp4 with both tracks
            YtDlpFormat bestCombined = null;
            foreach (var f in formats)
            {
                if (!f.IsCombined) continue;
                if (f.Ext != "mp4") continue;
                if (!f.IsAvc1) continue;
                if (f.Height > 720) continue;
                if (bestCombined == null || f.Height > bestCombined.Height) bestCombined = f;
            }
            if (bestCombined != null)
            {
                plan.Combined = true;
                plan.VideoSelector = bestCombined.Id;
                plan.Summary = $"combined mp4/H.264 itag={bestCombined.Id} {bestCombined.Resolution}";
                return plan;
            }

            // 2. video-only H.264 mp4 (largest <= 720p)
            YtDlpFormat bestVideo = null;
            foreach (var f in formats)
            {
                if (!f.HasVideo || f.HasAudio) continue;
                if (f.Ext != "mp4") continue;
                if (!f.IsAvc1) continue;
                if (f.Height > 720) continue;
                if (bestVideo == null || f.Height > bestVideo.Height) bestVideo = f;
            }

            // 3. audio-only AAC in an m4a container (largest)
            YtDlpFormat bestAudio = null;
            foreach (var f in formats)
            {
                if (f.HasVideo || !f.HasAudio) continue;
                if (f.Ext != "m4a") continue;
                if (!f.IsMp4Audio) continue;
                if (bestAudio == null || FormatRank(f) > FormatRank(bestAudio)) bestAudio = f;
            }

            plan.VideoSelector = bestVideo != null ? bestVideo.Id : "bv[ext=mp4][height<=720]";
            plan.AudioSelector = bestAudio != null ? bestAudio.Id : "ba[ext=m4a]";
            plan.Summary = bestVideo != null
                ? $"video-only mp4/H.264 itag={bestVideo.Id} {bestVideo.Resolution}"
                : "video fallback selector";
            plan.Summary += bestAudio != null
                ? $" + audio-only m4a/AAC itag={bestAudio.Id}"
                : " + audio fallback selector";
            return plan;
        }

        /// <summary>Numeric itag, used to prefer the higher bitrate audio track.</summary>
        static int FormatRank(YtDlpFormat format)
        {
            int value;
            return int.TryParse(format.Id, out value) ? value : 0;
        }

        /// <summary>
        /// Returns the cached file for the given prefix, downloading it with yt-dlp
        /// when it is missing. The produced file keeps yt-dlp's own extension
        /// (video.mp4 / audio.m4a).
        /// </summary>
        public static async Task<MediaFileResult> EnsureFileAsync(string videoId, string selector, string filePrefix, CancellationToken cancellationToken = default)
        {
            var result = new MediaFileResult();
            var dir = CacheDirectoryFor(videoId);

            try
            {
                Directory.CreateDirectory(dir);
            }
            catch (Exception e)
            {
                result.Detail = "cannot create cache dir: " + e.Message;
                return result;
            }

            var cached = FindProducedFile(dir, filePrefix);
            if (cached != null)
            {
                result.Ok = true;
                result.Path = cached;
                result.FromCache = true;
                result.Detail = Path.GetFileName(cached);
                return result;
            }

            if (string.IsNullOrEmpty(selector))
            {
                result.Detail = "no format selector";
                return result;
            }

            var template = Path.Combine(dir, filePrefix);
            var run = await YouTubeStreamResolver.DownloadAsync(videoId, selector, template, cancellationToken);
            if (!run.Ok)
            {
                result.Detail = $"yt-dlp download failed ({run.ErrorSummary})";
                return result;
            }

            var produced = FindProducedFile(dir, filePrefix);
            if (produced == null)
            {
                result.Detail = "yt-dlp reported success but produced no file";
                return result;
            }

            result.Ok = true;
            result.Path = produced;
            result.Detail = Path.GetFileName(produced);
            return result;
        }

        /// <summary>Finds the file yt-dlp produced for the given prefix (ignores .part/.ytdl leftovers).</summary>
        public static string FindProducedFile(string directory, string filePrefix)
        {
            try
            {
                if (!Directory.Exists(directory)) return null;
                string best = null;
                DateTime bestTime = DateTime.MinValue;
                foreach (var path in Directory.GetFiles(directory))
                {
                    var name = Path.GetFileName(path);
                    if (!name.StartsWith(filePrefix, StringComparison.OrdinalIgnoreCase)) continue;
                    var ext = Path.GetExtension(name).ToLowerInvariant();
                    if (ext == ".part" || ext == ".ytdl" || ext == ".temp") continue;
                    var written = File.GetLastWriteTimeUtc(path);
                    if (written > bestTime)
                    {
                        bestTime = written;
                        best = path;
                    }
                }
                return best;
            }
            catch
            {
                return null;
            }
        }
    }
}
