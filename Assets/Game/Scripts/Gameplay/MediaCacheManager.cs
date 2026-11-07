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

        /// <summary>Total size of the cache in bytes.</summary>
        public static long GetCacheSizeBytes(string root = null)
        {
            long total = 0;
            var directory = root ?? RootDirectory;
            try
            {
                if (!Directory.Exists(directory)) return 0;
                foreach (var file in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
                {
                    var info = new FileInfo(file);
                    total += info.Length;
                }
            }
            catch { }
            return total;
        }

        /// <summary>Deletes the media of one song. Returns true when something was removed.</summary>
        public static bool Clear(string videoId, string root = null)
        {
            if (string.IsNullOrEmpty(videoId)) return false;

            var directory = root ?? RootDirectory;
            var songFolder = Path.Combine(directory, videoId.Replace('/', '_'));
            var removed = DeleteFolder(songFolder);

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
