using System;
using System.Collections.Generic;
using System.Globalization;
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

        /// <summary>
        /// The video-only selector to fall back to when the merged download fails
        /// (no ffmpeg on the machine, for example). The picture still plays, the
        /// YouTube sound is then missing - the game keeps running.
        /// </summary>
        public string FallbackVideoSelector = "";

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

        /// <summary>
        /// The merged file (video and YouTube sound in one mp4) gets its own
        /// prefix, so a picture-only file of an earlier revision is never reused
        /// as if it carried the sound.
        /// </summary>
        public const string MergedFilePrefix = "merged";

        /// <summary>
        /// Suffix of the completion marker. A media file only counts as a cache
        /// hit when its marker exists and records the very size of the file on
        /// disk - a download that was interrupted (process killed, power loss)
        /// leaves the media file without a marker or with a stale size, so it is
        /// fetched again instead of breaking the playback (issue #184).
        /// </summary>
        public const string DoneMarkerSuffix = ".done";

        /// <summary>
        /// Test seam for the EditMode tests: when set, it replaces the yt-dlp
        /// download so the cache logic can be verified without a network. The
        /// game never sets it - a test restores null in its TearDown.
        /// </summary>
        public static Func<string, string, string, CancellationToken, Task<YtDlpOutput>> DownloadOverride;

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
        ///   2. otherwise the video-only H.264 mp4 (<= 720p) and the audio-only
        ///      AAC/m4a track are **merged into one mp4** with yt-dlp + ffmpeg, so
        ///      the YouTube sound reaches Unity through the same VideoPlayer.
        ///      Unity's VideoPlayer cannot open an audio-only container
        ///      ("Failed to recognize video file format for: …m4a"), so the split
        ///      plan of an earlier revision lost the YouTube sound completely;
        ///   3. without ffmpeg on the machine the merge is impossible, then the
        ///      split plan is used (picture only).
        ///
        /// YouTube dropped the progressive itag 18/22 for many videos (verified with
        /// `yt-dlp -F` on the FlashBeat song list on 2026-11-06), so branch 2 is the
        /// one that actually runs today.
        /// </summary>
        public static MediaFormatPlan SelectPlan(IList<YtDlpFormat> formats)
        {
            return SelectPlan(formats, canMerge: true);
        }

        /// <summary>
        /// <paramref name="canMerge"/> tells whether ffmpeg is available on the
        /// machine. Without it the two streams cannot be muxed into one file.
        /// </summary>
        public static MediaFormatPlan SelectPlan(IList<YtDlpFormat> formats, bool canMerge)
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

            // 4. The two streams are muxed into one mp4 when ffmpeg is around:
            //    that is the only way the YouTube sound reaches Unity's VideoPlayer.
            if (canMerge && bestVideo != null && bestAudio != null)
            {
                plan.Combined = true;
                plan.VideoSelector = bestVideo.Id + "+" + bestAudio.Id;
                plan.AudioSelector = "";
                plan.FallbackVideoSelector = bestVideo.Id;
                plan.Summary = $"merged mp4 (video itag={bestVideo.Id} {bestVideo.Resolution} + audio itag={bestAudio.Id})";
            }
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

            // Only a completed download is a cache hit: the file has a marker and
            // the marker matches the size on disk. A file that a killed process
            // left halfway (with or without the final name) is ignored here.
            var cached = FindCompletedFile(dir, filePrefix);
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

            // Nothing usable is in the cache: drop whatever an interrupted run
            // left behind, so the file this run produces is the only one that can
            // be found afterwards.
            RemoveProducedFiles(dir, filePrefix);

            // The extension matters: Unity's VideoPlayer refuses a file whose
            // container it cannot derive from the name ("Failed to recognize video
            // file format"), so yt-dlp has to write video.mp4 and not just "video".
            var template = Path.Combine(dir, filePrefix + ".%(ext)s");
            var run = DownloadOverride != null
                ? await DownloadOverride(videoId, selector, template, cancellationToken).ConfigureAwait(false)
                : await YouTubeStreamResolver.DownloadAsync(videoId, selector, template, cancellationToken).ConfigureAwait(false);
            if (!run.Ok)
            {
                // A half downloaded file must not be reused as a cache hit later.
                RemoveProducedFiles(dir, filePrefix);
                result.Detail = $"yt-dlp download failed ({run.ErrorSummary})";
                return result;
            }

            var produced = FindProducedFile(dir, filePrefix);
            if (produced == null)
            {
                RemoveProducedFiles(dir, filePrefix);
                result.Detail = "yt-dlp reported success but produced no file";
                return result;
            }

            // An empty file cannot be opened by the VideoPlayer and would poison
            // the cache: reject it here so the next attempt downloads again.
            long producedBytes = 0;
            try { producedBytes = new FileInfo(produced).Length; } catch { }
            if (producedBytes <= 0)
            {
                RemoveProducedFiles(dir, filePrefix);
                result.Detail = "the downloaded file is empty";
                return result;
            }

            // The download (and the ffmpeg merge, when one ran) is complete: the
            // marker makes the file a cache hit for every later call.
            MarkCompleted(produced);

            result.Ok = true;
            result.Path = produced;
            result.Detail = Path.GetFileName(produced);
            return result;
        }

        /// <summary>
        /// Finds the file yt-dlp produced for the given prefix. Only files with a
        /// media extension are accepted - Unity's VideoPlayer derives the
        /// container from the file name, so a file without extension is useless.
        /// </summary>
        public static string FindProducedFile(string directory, string filePrefix)
        {
            return NewestProducedFile(directory, filePrefix, completedOnly: false);
        }

        /// <summary>
        /// Finds the newest <em>completed</em> cache file for the given prefix:
        /// a media file whose completion marker exists and records the size of
        /// the file on disk. Everything an interrupted download left halfway is
        /// skipped, so it is fetched again instead of played (issue #184).
        /// </summary>
        public static string FindCompletedFile(string directory, string filePrefix)
        {
            return NewestProducedFile(directory, filePrefix, completedOnly: true);
        }

        /// <summary>Path of the completion marker that belongs to a media file.</summary>
        public static string DoneMarkerPath(string mediaPath)
        {
            return mediaPath + DoneMarkerSuffix;
        }

        /// <summary>
        /// True when the media file is there, is not empty, and its completion
        /// marker records exactly the size of the file on disk.
        /// </summary>
        public static bool IsCompletedCacheFile(string mediaPath)
        {
            try
            {
                var info = new FileInfo(mediaPath);
                if (!info.Exists || info.Length <= 0) return false;

                var marker = DoneMarkerPath(mediaPath);
                if (!File.Exists(marker)) return false;

                long recorded;
                if (!long.TryParse(File.ReadAllText(marker).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out recorded)) return false;
                return recorded == info.Length;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Writes the completion marker for a fully downloaded file. The marker
        /// records the byte length, so a file that is written halfway (or filled
        /// again by a later run) is detected by <see cref="IsCompletedCacheFile"/>.
        /// </summary>
        public static void MarkCompleted(string mediaPath)
        {
            try
            {
                var info = new FileInfo(mediaPath);
                if (!info.Exists || info.Length <= 0) return;
                File.WriteAllText(DoneMarkerPath(mediaPath), info.Length.ToString(CultureInfo.InvariantCulture));
            }
            catch
            {
                // Without a marker the file simply is not a cache hit - the next
                // call fetches the media again, nothing breaks.
            }
        }

        /// <summary>
        /// Deletes the media file and its completion marker. MusicManager calls
        /// this when the player cannot open a cached file, so the next attempt
        /// (Retry) fetches the media again instead of reusing the broken file.
        /// </summary>
        public static void InvalidateProducedFile(string mediaPath)
        {
            try { if (File.Exists(mediaPath)) File.Delete(mediaPath); } catch { }
            try { if (File.Exists(DoneMarkerPath(mediaPath))) File.Delete(DoneMarkerPath(mediaPath)); } catch { }
        }

        static string NewestProducedFile(string directory, string filePrefix, bool completedOnly)
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
                    if (!MediaExtensions.Contains(ext)) continue;
                    if (completedOnly && !IsCompletedCacheFile(path)) continue;
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

        /// <summary>Deletes every cached file of the given prefix (a failed download leaves nothing behind).</summary>
        public static void RemoveProducedFiles(string directory, string filePrefix)
        {
            try
            {
                if (!Directory.Exists(directory)) return;
                foreach (var path in Directory.GetFiles(directory))
                {
                    var name = Path.GetFileName(path);
                    if (!name.StartsWith(filePrefix, StringComparison.OrdinalIgnoreCase)) continue;
                    try { File.Delete(path); } catch { }
                }
            }
            catch { }
        }

        static readonly HashSet<string> MediaExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4", ".m4v", ".webm", ".mkv", ".mov", ".m4a", ".mp3", ".ogg", ".opus", ".aac", ".wav"
        };
    }
}
