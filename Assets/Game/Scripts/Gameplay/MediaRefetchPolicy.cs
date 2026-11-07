using UnityEngine;

namespace FlashBeat.Gameplay
{
    /// <summary>
    /// Counts how often the cached media of a video was dropped because the
    /// player could not open it (issue #184).
    ///
    /// The budget bounds the automatic downloads per video:
    /// <see cref="YouTubeMediaCache.EnsureFileAsync"/> fetches only while
    /// <see cref="MayRefetch"/> holds. When the player cannot open a cached
    /// file, MusicManager strips its completion marker and counts the attempt
    /// no matter the budget - a marked but unplayable file must never block the
    /// next download. Past the budget the Option screen's cache deletion
    /// (<see cref="MediaCacheManager.Clear"/>, <c>ClearAll</c>) is the explicit
    /// way out: it deletes the files and resets the budget. The count lives in
    /// PlayerPrefs (it has to survive the scene reload of a Retry) and is
    /// cleared as soon as the song is playable again.
    /// </summary>
    public static class MediaRefetchPolicy
    {
        /// <summary>How often a broken cache entry may be dropped and fetched again.</summary>
        public const int MaxAttemptsPerVideo = 2;

        const string KeyPrefix = "FlashBeat.MediaRefetchCount.";

        static string Key(string videoId)
        {
            return KeyPrefix + (string.IsNullOrEmpty(videoId) ? "unknown" : videoId.Replace('/', '_'));
        }

        /// <summary>How often the cache entry of the video was dropped so far.</summary>
        public static int CountFor(string videoId)
        {
            return PlayerPrefs.GetInt(Key(videoId), 0);
        }

        /// <summary>True while the broken cache entry may still be dropped for another download.</summary>
        public static bool MayRefetch(string videoId)
        {
            return CountFor(videoId) < MaxAttemptsPerVideo;
        }

        /// <summary>Records that the cache entry was dropped and is fetched again.</summary>
        public static void CountRefetch(string videoId)
        {
            PlayerPrefs.SetInt(Key(videoId), CountFor(videoId) + 1);
            PlayerPrefs.Save();
        }

        /// <summary>Clears the count - the song is playable again.</summary>
        public static void Reset(string videoId)
        {
            PlayerPrefs.DeleteKey(Key(videoId));
            PlayerPrefs.Save();
        }
    }
}
