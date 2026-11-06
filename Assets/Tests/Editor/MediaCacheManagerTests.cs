using System;
using System.IO;
using FlashBeat.Gameplay;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// The media cache must stay inside persistentDataPath (never in the repository,
/// in Resources or in StreamingAssets) and it must be deletable song by song and
/// as a whole.
/// </summary>
public class MediaCacheManagerTests
{
    string root;

    [SetUp]
    public void SetUp()
    {
        root = Path.Combine(Path.GetTempPath(), "flashbeat-cache-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
    }

    [TearDown]
    public void TearDown()
    {
        try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch { }
    }

    void WriteFile(string relativePath, int bytes)
    {
        var path = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, new byte[bytes]);
    }

    [Test]
    public void CacheLivesInPersistentDataPathOnly()
    {
        string cacheRoot = MediaCacheManager.RootDirectory;

        Assert.IsTrue(cacheRoot.StartsWith(Application.persistentDataPath),
            $"the cache must live under persistentDataPath, got '{cacheRoot}'");
        Assert.AreEqual(MediaCacheManager.CacheFolderName, Path.GetFileName(cacheRoot));
        Assert.IsFalse(cacheRoot.Contains("StreamingAssets"));
        Assert.IsFalse(cacheRoot.Contains("Resources"));
    }

    [Test]
    public void SongFolderIsInsideTheCacheRoot()
    {
        string songFolder = YouTubeMediaCache.CacheDirectoryFor("DeKLpgzh-qQ");

        Assert.IsTrue(songFolder.StartsWith(MediaCacheManager.RootDirectory));
        Assert.AreEqual("DeKLpgzh-qQ", Path.GetFileName(songFolder));
    }

    [Test]
    public void SizeAndSongCountReflectTheCachedFiles()
    {
        WriteFile(Path.Combine("songA", "video.mp4"), 1000);
        WriteFile(Path.Combine("songA", "audio.m4a"), 500);
        WriteFile(Path.Combine("songB", "video.mp4"), 2000);

        Assert.AreEqual(3500L, MediaCacheManager.GetCacheSizeBytes(root));
        Assert.AreEqual(2, MediaCacheManager.GetCachedSongCount(root));
    }

    [Test]
    public void ClearRemovesOnlyTheRequestedSong()
    {
        WriteFile(Path.Combine("songA", "video.mp4"), 1000);
        WriteFile(Path.Combine("songB", "video.mp4"), 2000);

        Assert.IsTrue(MediaCacheManager.Clear("songA", root));
        Assert.IsFalse(Directory.Exists(Path.Combine(root, "songA")));
        Assert.IsTrue(Directory.Exists(Path.Combine(root, "songB")));
        Assert.AreEqual(2000L, MediaCacheManager.GetCacheSizeBytes(root));
        Assert.AreEqual(1, MediaCacheManager.GetCachedSongCount(root));
    }

    [Test]
    public void ClearOnAnUnknownSongReportsNothingRemoved()
    {
        WriteFile(Path.Combine("songB", "video.mp4"), 2000);

        Assert.IsFalse(MediaCacheManager.Clear("does-not-exist", root));
        Assert.AreEqual(1, MediaCacheManager.GetCachedSongCount(root));
    }

    [Test]
    public void ClearAllRemovesEveryCachedSong()
    {
        WriteFile(Path.Combine("songA", "video.mp4"), 1000);
        WriteFile(Path.Combine("songB", "video.mp4"), 2000);
        WriteFile(Path.Combine("songC", "video.mp4"), 3000);

        int removed = MediaCacheManager.ClearAll(root);

        Assert.AreEqual(3, removed);
        Assert.AreEqual(0L, MediaCacheManager.GetCacheSizeBytes(root));
        Assert.AreEqual(0, MediaCacheManager.GetCachedSongCount(root));
    }

    [Test]
    public void SizeIsFormattedForHumans()
    {
        Assert.AreEqual("0 B", MediaCacheManager.FormatSize(0));
        Assert.AreEqual("512 B", MediaCacheManager.FormatSize(512));
        Assert.AreEqual("1.0 KB", MediaCacheManager.FormatSize(1024));
        Assert.AreEqual("5.0 MB", MediaCacheManager.FormatSize(5 * 1024 * 1024));
    }
}
