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

    [Test]
    public void ProducedFileWithoutExtensionIsIgnored()
    {
        // Unity's VideoPlayer derives the container from the file name, so a file
        // without extension cannot be played and must not be handed back.
        WriteFile(Path.Combine("songA", "video"), 3000);
        WriteFile(Path.Combine("songA", "audio"), 1000);

        Assert.IsNull(YouTubeMediaCache.FindProducedFile(Path.Combine(root, "songA"), "video"));
        Assert.IsNull(YouTubeMediaCache.FindProducedFile(Path.Combine(root, "songA"), "audio"));
    }

    [Test]
    public void ProducedFileWithMediaExtensionIsPicked()
    {
        WriteFile(Path.Combine("songA", "video.mp4"), 3000);

        string found = YouTubeMediaCache.FindProducedFile(Path.Combine(root, "songA"), "video");

        Assert.IsNotNull(found);
        Assert.AreEqual("video.mp4", Path.GetFileName(found));
    }

    [Test]
    public void PartialDownloadFilesAreIgnored()
    {
        WriteFile(Path.Combine("songA", "video.mp4.part"), 500);
        WriteFile(Path.Combine("songA", "video.mp4.ytdl"), 10);

        Assert.IsNull(YouTubeMediaCache.FindProducedFile(Path.Combine(root, "songA"), "video"));
    }

    // --- Issue #403: 容量取得のコスト ---

    [Test]
    public void SizeScanIsRememberedUntilItIsInvalidated()
    {
        MediaCacheManager.ResetSizeCacheForAudit();
        WriteFile(Path.Combine("songA", "video.mp4"), 1000);

        Assert.AreEqual(1000L, MediaCacheManager.GetCacheSizeBytes(root));
        Assert.AreEqual(1, MediaCacheManager.SizeScanCount, "最初の取得で 1 回舐める");

        // 2 回目はディスクを舐めない (Option を開き直すたびの同期 I/O を無くす)。
        Assert.AreEqual(1000L, MediaCacheManager.GetCacheSizeBytes(root));
        Assert.AreEqual(1, MediaCacheManager.SizeScanCount);

        // ファイルが増えても、明示的に無効化するまでは前の値 (契約)。
        WriteFile(Path.Combine("songB", "video.mp4"), 2000);
        Assert.AreEqual(1000L, MediaCacheManager.GetCacheSizeBytes(root));

        MediaCacheManager.InvalidateSize(root);
        Assert.AreEqual(3000L, MediaCacheManager.GetCacheSizeBytes(root));
        Assert.AreEqual(2, MediaCacheManager.SizeScanCount);
    }

    [Test]
    public void ClearInvalidatesTheRememberedSize()
    {
        MediaCacheManager.ResetSizeCacheForAudit();
        WriteFile(Path.Combine("songA", "video.mp4"), 1000);
        WriteFile(Path.Combine("songB", "video.mp4"), 2000);

        Assert.AreEqual(3000L, MediaCacheManager.GetCacheSizeBytes(root));

        Assert.IsTrue(MediaCacheManager.Clear("songA", root));
        // Clear が無効化するので、次の取得は実際の 2000 を返す
        // (古い 3000 を返さない)。
        Assert.AreEqual(2000L, MediaCacheManager.GetCacheSizeBytes(root));
    }

    [Test]
    public void ClearAllInvalidatesTheRememberedSize()
    {
        MediaCacheManager.ResetSizeCacheForAudit();
        WriteFile(Path.Combine("songA", "video.mp4"), 1000);

        Assert.AreEqual(1000L, MediaCacheManager.GetCacheSizeBytes(root));
        Assert.AreEqual(1, MediaCacheManager.ClearAll(root));
        Assert.AreEqual(0L, MediaCacheManager.GetCacheSizeBytes(root));
    }

    [Test]
    public void ResetSizeCacheForAudit_DoesNotChangeTheValueItReports()
    {
        WriteFile(Path.Combine("songA", "video.mp4"), 1000);
        Assert.AreEqual(1000L, MediaCacheManager.GetCacheSizeBytes(root));

        MediaCacheManager.ResetSizeCacheForAudit();
        Assert.AreEqual(1000L, MediaCacheManager.GetCacheSizeBytes(root),
            "キャッシュを捨てても実ファイルの合計は同じ");
    }

    [Test]
    public void ScanSizeBytesAlwaysReadsTheDisk()
    {
        WriteFile(Path.Combine("songA", "video.mp4"), 1000);
        Assert.AreEqual(1000L, MediaCacheManager.GetCacheSizeBytes(root));

        WriteFile(Path.Combine("songB", "video.mp4"), 500);
        Assert.AreEqual(1500L, MediaCacheManager.ScanSizeBytes(root),
            "計測用の Scan はスナップショットを経由しない");
    }
}
