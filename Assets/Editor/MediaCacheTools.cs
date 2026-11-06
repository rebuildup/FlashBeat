using System;
using System.Threading;
using UnityEditor;
using UnityEngine;
using FlashBeat.Gameplay;

/// <summary>
/// Editor tools for the local media cache (the YouTube media that yt-dlp
/// downloads for the songs).
///
/// * Tools > FlashBeat > Clear Media Cache - deletes the cache.
/// * Tools > FlashBeat > Pre-cache all songs - downloads the media of every song
///   into the cache so that the exhibition build starts without waiting for the
///   network. The same method can be run headless:
///   Unity -batchmode -quit -projectPath . -executeMethod MediaCacheTools.PrecacheAllSongs
/// </summary>
public static class MediaCacheTools
{
    [MenuItem("Tools/FlashBeat/Clear Media Cache")]
    public static void ClearMediaCache()
    {
        long bytes = MediaCacheManager.GetCacheSizeBytes();
        int songs = MediaCacheManager.GetCachedSongCount();

        bool ok = EditorUtility.DisplayDialog(
            "Clear media cache",
            $"Delete {songs} cached song(s) ({MediaCacheManager.FormatSize(bytes)}) from\n{MediaCacheManager.RootDirectory}?",
            "Delete",
            "Cancel");
        if (!ok) return;

        int removed = MediaCacheManager.ClearAll();
        Debug.Log($"[FlashBeat] media cache cleared: {removed} song(s), {bytes} bytes freed");
    }

    [MenuItem("Tools/FlashBeat/Pre-cache all songs")]
    public static void PrecacheAllSongsMenu()
    {
        PrecacheAllSongs();
    }

    /// <summary>
    /// Downloads the media of every song into the cache. Safe to call in batch mode.
    /// </summary>
    public static void PrecacheAllSongs()
    {
        var songs = GManager.Songs;
        int cached = 0;
        int failed = 0;

        for (int i = 1; i < songs.Length; i++)
        {
            var song = songs[i];
            if (song == null || string.IsNullOrEmpty(song.videoId))
            {
                continue;
            }

            try
            {
                var formatsOut = YouTubeStreamResolver.ListFormatsAsync(song.videoId).GetAwaiter().GetResult();
                var formats = YouTubeStreamResolver.ParseFormats(formatsOut.Ok ? formatsOut.Stdout : "");
                if (formats.Count == 0)
                {
                    Debug.LogWarning($"[FlashBeat] pre-cache '{song.title}' failed: no formats ({formatsOut.ErrorSummary})");
                    failed++;
                    continue;
                }

                var plan = YouTubeMediaCache.SelectPlan(formats);
                var video = YouTubeMediaCache.EnsureFileAsync(song.videoId, plan.VideoSelector, YouTubeMediaCache.VideoFilePrefix).GetAwaiter().GetResult();
                if (!video.Ok)
                {
                    Debug.LogWarning($"[FlashBeat] pre-cache '{song.title}' failed: {video.Detail}");
                    failed++;
                    continue;
                }

                var detail = video.Detail;
                if (plan.HasAudioFile)
                {
                    var audio = YouTubeMediaCache.EnsureFileAsync(song.videoId, plan.AudioSelector, YouTubeMediaCache.AudioFilePrefix).GetAwaiter().GetResult();
                    detail += audio.Ok ? $" + {audio.Detail}" : $" (audio failed: {audio.Detail})";
                }

                Debug.Log($"[FlashBeat] pre-cache '{song.title}' [{song.videoId}]: {detail} ({plan.Summary})");
                cached++;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[FlashBeat] pre-cache '{song.title}' failed: {e.Message}");
                failed++;
            }
        }

        long bytes = MediaCacheManager.GetCacheSizeBytes();
        Debug.Log($"[FlashBeat] pre-cache finished: {cached} song(s) cached, {failed} failed, cache size {MediaCacheManager.FormatSize(bytes)}");

        if (Application.isBatchMode)
        {
            EditorApplication.Exit(failed > 0 ? 1 : 0);
        }
    }
}
