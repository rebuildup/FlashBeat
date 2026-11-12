using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FlashBeat.Gameplay
{
    /// <summary>
    /// Owns the local media cache: the YouTube media that <c>yt-dlp</c> downloads
    /// for a song.
    ///
    /// The cache lives in <c>persistentDataPath/FlashBeatMedia</c> and nowhere
    /// else - not in the repository, not in Resources and not in StreamingAssets -
    /// so that no third party media ends up in the distributed build. The Option
    /// screen shows how much space it uses and can delete it.
    /// </summary>
    public static class MediaCacheManager
    {
        public const string CacheFolderName = "FlashBeatMedia";

        /// <summary>Root of the cache: %USERPROFILE%\AppData\LocalLow\...\FlashBeatMedia</summary>
        public static string RootDirectory
        {
            get { return Path.Combine(Application.persistentDataPath, CacheFolderName); }
        }

        /// <summary>Folder that holds the media of one song.</summary>
        public static string DirectoryFor(string videoId)
        {
            return YouTubeMediaCache.CacheDirectoryFor(videoId);
        }

        /// <summary>Video ids that currently have a cache folder.</summary>
        public static List<string> GetCachedVideoIds(string root = null)
        {
            var ids = new List<string>();
            var directory = root ?? RootDirectory;
            try
            {
                if (!Directory.Exists(directory)) return ids;
                foreach (var dir in Directory.GetDirectories(directory))
                {
                    ids.Add(Path.GetFileName(dir));
                }
            }
            catch { }
            ids.Sort(StringComparer.Ordinal);
            return ids;
        }

        /// <summary>Number of songs in the cache.</summary>
        public static int GetCachedSongCount(string root = null)
        {
            return GetCachedVideoIds(root).Count;
        }

        /// <summary>
        /// Total size of the cache in bytes.
        ///
        /// Issue #403: 以前は呼ぶたびに <see cref="Directory.GetFiles(string,string,SearchOption)"/>
        /// で全ファイルを列挙し <see cref="FileInfo"/> を作っていた。Option を開くたびに
        /// 同期 I/O + 一時配列 + GC が発生する。ここでは
        ///
        ///   - 同じ root に対する結果を **スナップショット** として覚える
        ///   - 無効化は「このクラスが cache を書き換えたとき」に行う
        ///     (<see cref="Clear"/> / <see cref="ClearAll"/> / <see cref="InvalidateSize"/>)
        ///   - スナップショットは <see cref="SizeSnapshotSeconds"/> 秒で陳腐化する
        ///
        /// という形にして、表示のたびにディスクを舐めない。
        /// 外部（yt-dlp のダウンロード完了）がファイルを増やしたときは
        /// ダウンロードを完了した側が <see cref="InvalidateSize"/> を呼ぶ。
        /// TTL は「外部の書き換えを永遠に見逃さない」ための保険であり、
        /// 陳腐化しても最短でも TTL 秒に 1 回は舐め直す（それ以上古い値は返らない）。
        /// </summary>
        public static long GetCacheSizeBytes(string root = null)
        {
            var directory = root ?? RootDirectory;
            string snapshotKey = directory;
            if (sizeSnapshot.TryGetValue(snapshotKey, out var snap)
                && Time.unscaledTime - snap.Stamp < SizeSnapshotSeconds)
            {
                return snap.Bytes;
            }

            long total = ScanSizeBytes(directory);
            sizeSnapshot[snapshotKey] = (total, Time.unscaledTime);
            SizeScanCount++;
            return total;
        }

        /// <summary>スナップショットの有効期間 (秒)。</summary>
        public const float SizeSnapshotSeconds = 30f;

        /// <summary>
        /// ディスクを実際に舐めて合計を出す (キャッシュを経由しない)。
        /// 計測・検証用。
        /// </summary>
        public static long ScanSizeBytes(string root = null)
        {
            long total = 0;
            var directory = root ?? RootDirectory;
            try
            {
                if (!Directory.Exists(directory)) return 0;

                // Directory.GetFiles は全パスの配列を一度に確保する。
                // ファイル数が多いキャッシュで大きな一時配列を作らないよう、
                // ディレクトリを辿りながら 1 件ずつ数える。
                total += SizeOfFilesIn(directory);
                foreach (var sub in Directory.GetDirectories(directory))
                {
                    total += SizeOfTree(sub);
                }
            }
            catch { }
            return total;
        }

        private static long SizeOfTree(string directory)
        {
            long total = SizeOfFilesIn(directory);
            try
            {
                foreach (var sub in Directory.GetDirectories(directory))
                {
                    total += SizeOfTree(sub);
                }
            }
            catch { }
            return total;
        }

        private static long SizeOfFilesIn(string directory)
        {
            long total = 0;
            try
            {
                foreach (var file in Directory.GetFiles(directory))
                {
                    total += SizeOf(file);
                }
            }
            catch { }
            return total;
        }

        /// <summary>1 ファイルのサイズ (読めないときは 0)。</summary>
        private static long SizeOf(string path)
        {
            try
            {
                return new FileInfo(path).Length;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// 容量のスナップショットを捨てる。次回 <see cref="GetCacheSizeBytes"/> は
        /// ディスクを舐め直す。キャッシュを書き換えた側 (ダウンロード完了・削除) が呼ぶ。
        /// </summary>
        public static void InvalidateSize(string root = null)
        {
            if (root == null)
            {
                sizeSnapshot.Clear();
                return;
            }

            sizeSnapshot.Remove(root);
        }

        /// <summary>このプロセスで実際にディスクを舐めた回数 (キャッシュ ヒットは含まない)。</summary>
        public static int SizeScanCount { get; private set; }

        /// <summary>テスト用: スナップショットと計測点を戻す。</summary>
        public static void ResetSizeCacheForAudit()
        {
            sizeSnapshot.Clear();
            SizeScanCount = 0;
        }

        private static readonly Dictionary<string, (long Bytes, float Stamp)> sizeSnapshot =
            new Dictionary<string, (long Bytes, float Stamp)>(StringComparer.Ordinal);

        /// <summary>Deletes the media of one song. Returns true when something was removed.</summary>
        public static bool Clear(string videoId, string root = null)
        {
            if (string.IsNullOrEmpty(videoId)) return false;

            var directory = root ?? RootDirectory;
            var songFolder = Path.Combine(directory, videoId.Replace('/', '_'));
            var removed = DeleteFolder(songFolder);

            // Issue #403: 容量のスナップショットを古くしない。
            if (removed) InvalidateSize(root);

            // Deleting the cache is the explicit refetch way out (issue #184
            // review B2): without the reset a spent refetch budget would keep
            // blocking the download for this song even after the deletion.
            if (removed) MediaRefetchPolicy.Reset(videoId);

            return removed;
        }

        /// <summary>Deletes the whole cache. Returns the number of removed song folders.</summary>
        public static int ClearAll(string root = null)
        {
            var directory = root ?? RootDirectory;
            var removed = 0;
            try
            {
                if (!Directory.Exists(directory)) return 0;
                foreach (var dir in Directory.GetDirectories(directory))
                {
                    if (DeleteFolder(dir))
                    {
                        removed++;
                        // The folder name is the video id (CacheDirectoryFor
                        // replaces the path separators): its refetch budget is
                        // spent, the deletion has to restore it (issue #184
                        // review B2).
                        MediaRefetchPolicy.Reset(Path.GetFileName(dir));
                    }
                }
            }
            catch { }

            // Issue #403: 削除したら容量のスナップショットを捨てる。
            if (removed > 0) InvalidateSize(root);
            return removed;
        }

        static bool DeleteFolder(string path)
        {
            try
            {
                if (!Directory.Exists(path)) return false;
                Directory.Delete(path, true);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[MediaCacheManager] cannot delete '{path}': {e.Message}");
                return false;
            }
        }

        /// <summary>Human readable size, e.g. "12.4 MB".</summary>
        public static string FormatSize(long bytes)
        {
            if (bytes < 0) bytes = 0;
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0f:F1} KB";
            if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0f * 1024.0f):F1} MB";
            return $"{bytes / (1024.0f * 1024.0f * 1024.0f):F2} GB";
        }
    }
}
