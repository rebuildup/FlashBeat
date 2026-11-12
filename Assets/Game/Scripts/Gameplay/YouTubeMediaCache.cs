using System;
using System.Collections.Concurrent;
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

        /// <summary>A file that a previous run already downloaded and completed.</summary>
        public static MediaFileResult FromCompletedCache(string path)
        {
            return new MediaFileResult { Ok = true, Path = path, FromCache = true, Detail = "cache" };
        }
    }

    /// <summary>
    /// The files of one video that are already on disk and completed. A run that
    /// finds this set does not have to resolve the formats with yt-dlp.
    /// </summary>
    public sealed class ReadyMedia
    {
        public string VideoPath = "";
        public string AudioPath = "";
        public bool Combined;

        public bool HasAudio => !Combined && !string.IsNullOrEmpty(AudioPath);

        public string Summary => Combined ? "merged" : "video+audio";
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
        /// The cache prefix the picture file of a plan has to be stored under.
        /// A merged file (picture + YouTube sound in one mp4) uses its own
        /// prefix, a picture-only file the video prefix. The game and the
        /// pre-cache tool must agree on it, otherwise the pre-cached file is
        /// not found at runtime (or a merged file is reused as if it were a
        /// picture-only one).
        /// </summary>
        public static string PrefixForPlan(MediaFormatPlan plan)
        {
            return plan != null && plan.Combined ? MergedFilePrefix : VideoFilePrefix;
        }

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

        /// <summary>
        /// One pipeline run at a time per video (issue #184 review B1). Keyed by
        /// the cache directory, so two requests for the same video are serialized
        /// and the cleanup of the second can never delete the file the first one
        /// is still downloading.
        /// </summary>
        static readonly ConcurrentDictionary<string, SemaphoreSlim> DownloadGates =
            new ConcurrentDictionary<string, SemaphoreSlim>(StringComparer.Ordinal);

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

            // One pipeline run at a time per video (issue #184 review B1): a
            // second request for the same video waits here and then finds the
            // file the first run finalized - its own RemoveProducedFiles can
            // never delete an in-flight download of another request.
            var gate = DownloadGates.GetOrAdd(dir, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // Only a completed download is a cache hit: the file has a marker
                // and the marker matches the size on disk. A file that a killed
                // process left halfway (with or without the final name) is ignored
                // here. The check runs inside the gate, so it cannot race a
                // download that is still running.
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

                // A download would follow, but only while the refetch budget
                // lasts (issue #184 review B2). Past the budget the Option
                // screen's cache deletion is the explicit way out - it resets
                // the budget. A completed cache entry above still plays.
                if (!MediaRefetchPolicy.MayRefetch(videoId))
                {
                    result.Detail = "media refetch limit reached - delete the media cache in the Option screen to fetch it again";
                    return result;
                }

                // Nothing usable is in the cache: drop whatever an interrupted
                // run left behind, so the file this run produces is the only one
                // that can be found afterwards.
                RemoveProducedFiles(dir, filePrefix);

                // The extension matters: Unity's VideoPlayer refuses a file whose
                // container it cannot derive from the name ("Failed to recognize video
                // file format"), so yt-dlp has to write video.mp4 and not just "video".
                var template = Path.Combine(dir, filePrefix + ".%(ext)s");
                YtDlpOutput run;
                try
                {
                    run = DownloadOverride != null
                        ? await DownloadOverride(videoId, selector, template, cancellationToken).ConfigureAwait(false)
                        : await YouTubeStreamResolver.DownloadAsync(videoId, selector, template, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    // A cancelled or crashed download (the cancellation token of
                    // a scene teardown kills yt-dlp) must not leave a half
                    // written file with the final name behind. The failure is
                    // reported as a result, not thrown: the callers poll
                    // IsCompleted in Unity coroutines and would never see a
                    // faulted task's exception.
                    RemoveProducedFiles(dir, filePrefix);
                    result.Detail = "download did not finish: " + e.Message;
                    return result;
                }

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
            finally
            {
                gate.Release();
            }
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

        /// <summary>
        /// The files of <paramref name="videoId"/> that a previous run left in the
        /// cache, if they are complete enough to play without asking YouTube again
        /// (issue #377). The set is either one merged file or the video/audio pair;
        /// a picture file without its sound file is a miss, because the plan that
        /// would need the sound (ffmpeg present) can then only be known from the
        /// format probe.
        ///
        /// This is the single place that decides what counts as a reusable cache
        /// entry. The cache survives restarts on purpose (the media is large and
        /// the same videoId always resolves to the same song), so the rule is
        /// "completed marker still matches the file on disk" - a chart or catalogue
        /// update does not invalidate media because the file is identified by the
        /// YouTube video id, not by the chart version.
        /// </summary>
        /// <param name="videoId">The YouTube video id.</param>
        /// <param name="media">The reusable set.</param>
        public static bool TryGetReadyFiles(string videoId, out ReadyMedia media)
        {
            // ffmpeg の有無は呼び出し側が決める (試すたびに PATH を引かない)。
            // 省略時は「結合できない前提」= 分離ペアも再利用可。
            return TryGetReadyFiles(videoId, out media, canMerge: false);
        }

        /// <summary>
        /// <see cref="TryGetReadyFiles(string, out ReadyMedia)"/> の実体
        /// (PR #387 レビュー指摘)。
        ///
        /// <paramref name="canMerge"/> が true の環境では、分離ペア
        /// (video.mp4 + audio.m4a) を再利用しない: 結合できる環境なら
        /// 結合 mp4 を作るのが正しい形で、分離ペアをそのまま使うと
        /// Unity の VideoPlayer が開けない音声コンテナを掴んだまま
        /// 「音が出ない」状態で固定される。結合済みがあれば当然それを使う。
        /// </summary>
        public static bool TryGetReadyFiles(string videoId, out ReadyMedia media, bool canMerge)
        {
            media = null;
            if (string.IsNullOrEmpty(videoId)) return false;

            string directory = CacheDirectoryFor(videoId);

            string merged = FindCompletedFile(directory, MergedFilePrefix);
            if (!string.IsNullOrEmpty(merged))
            {
                media = new ReadyMedia { VideoPath = merged, Combined = true };
                return true;
            }

            string video = FindCompletedFile(directory, VideoFilePrefix);
            if (string.IsNullOrEmpty(video)) return false;

            string audio = FindCompletedFile(directory, AudioFilePrefix);
            if (string.IsNullOrEmpty(audio)) return false;

            if (canMerge)
            {
                // 結合できる環境で分離ペアを採用しない (形式解決へ進み、
                // yt-dlp に結合 mp4 を作らせる)。
                return false;
            }

            media = new ReadyMedia { VideoPath = video, AudioPath = audio, Combined = false };
            return true;
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
