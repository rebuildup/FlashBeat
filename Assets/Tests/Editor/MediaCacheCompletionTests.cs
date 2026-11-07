using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FlashBeat.Gameplay;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Completion marking of the media cache (issue #184):
///
///   * a download that was interrupted (process killed, power loss) leaves a
///     file without a completion marker or with a stale size - it must never be
///     adopted as a cache hit;
///   * a cache entry that the player cannot open is dropped, so the next
///     attempt (Retry) fetches the media again;
///   * the re-fetch is bounded (MediaRefetchPolicy), the Option screen's cache
///     deletion stays the explicit way out past the limit.
/// </summary>
public class MediaCacheCompletionTests
{
    string videoId;
    string dir;
    int downloadCalls;

    [SetUp]
    public void SetUp()
    {
        videoId = "test-" + Guid.NewGuid().ToString("N");
        dir = YouTubeMediaCache.CacheDirectoryFor(videoId);
        Directory.CreateDirectory(dir);
        downloadCalls = 0;
    }

    [TearDown]
    public void TearDown()
    {
        YouTubeMediaCache.DownloadOverride = null;
        try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
        MediaRefetchPolicy.Reset(videoId);
    }

    // --- helpers -----------------------------------------------------------

    /// <summary>Writes a cache file and (optionally) its completion marker.</summary>
    void WriteCacheFile(string fileName, int bytes, bool marked, long markerSize = -1)
    {
        var path = Path.Combine(dir, fileName);
        File.WriteAllBytes(path, new byte[bytes]);
        if (marked)
        {
            var recorded = markerSize >= 0 ? markerSize : bytes;
            File.WriteAllText(YouTubeMediaCache.DoneMarkerPath(path), recorded.ToString());
        }
    }

    /// <summary>
    /// The fake download: writes the file yt-dlp would have produced (the output
    /// template with %(ext)s resolved) and reports success.
    /// </summary>
    Func<string, string, string, CancellationToken, Task<YtDlpOutput>> FakeDownload(int bytes)
    {
        return (id, selector, template, token) =>
        {
            downloadCalls++;
            File.WriteAllBytes(template.Replace("%(ext)s", "mp4"), new byte[bytes]);
            return Task.FromResult(new YtDlpOutput { ExitCode = 0 });
        };
    }

    Func<string, string, string, CancellationToken, Task<YtDlpOutput>> FailingDownload(string stderr)
    {
        return (id, selector, template, token) =>
        {
            downloadCalls++;
            return Task.FromResult(new YtDlpOutput { ExitCode = 1, Stderr = stderr });
        };
    }

    // --- 未完成キャッシュが採用されないこと -----------------------------------

    [Test]
    public void ZeroByteCacheFileIsNotAdoptedAndIsFetchedAgain()
    {
        // A killed download can leave an empty file with the final name: it must
        // not be a cache hit, the media has to be fetched again.
        WriteCacheFile("video.mp4", 0, marked: false);

        YouTubeMediaCache.DownloadOverride = FakeDownload(1024);
        var result = YouTubeMediaCache.EnsureFileAsync(videoId, "18", "video").GetAwaiter().GetResult();

        Assert.IsTrue(result.Ok, result.Detail);
        Assert.IsFalse(result.FromCache, "a zero byte file must not be a cache hit");
        Assert.AreEqual(1, downloadCalls, "the broken cache entry must be fetched again");

        // The freshly fetched file is marked complete: the next call reuses it.
        var again = YouTubeMediaCache.EnsureFileAsync(videoId, "18", "video").GetAwaiter().GetResult();
        Assert.IsTrue(again.Ok, again.Detail);
        Assert.IsTrue(again.FromCache, "the completed cache is adopted on the next call");
        Assert.AreEqual(1, downloadCalls, "the completed cache is reused without a new download");
    }

    [Test]
    public void CacheFileWithoutMarkerIsNotAdopted()
    {
        // The marker is written only after yt-dlp reported success: a file that
        // was written halfway has none and is fetched again.
        WriteCacheFile("video.mp4", 1024, marked: false);

        YouTubeMediaCache.DownloadOverride = FakeDownload(2048);
        var result = YouTubeMediaCache.EnsureFileAsync(videoId, "18", "video").GetAwaiter().GetResult();

        Assert.IsTrue(result.Ok, result.Detail);
        Assert.IsFalse(result.FromCache);
        Assert.AreEqual(1, downloadCalls);
    }

    [Test]
    public void CacheFileWithMismatchedMarkerIsNotAdopted()
    {
        // The marker records the size of the finished download - a file whose
        // marker does not match the bytes on disk was written halfway.
        WriteCacheFile("video.mp4", 500, marked: true, markerSize: 1000);

        YouTubeMediaCache.DownloadOverride = FakeDownload(1000);
        var result = YouTubeMediaCache.EnsureFileAsync(videoId, "18", "video").GetAwaiter().GetResult();

        Assert.IsTrue(result.Ok, result.Detail);
        Assert.IsFalse(result.FromCache);
        Assert.AreEqual(1, downloadCalls);
    }

    [Test]
    public void IntermediateMergeFileIsNotAdopted()
    {
        // While yt-dlp merges two streams it writes "merged.f137.mp4": a killed
        // process leaves that intermediate behind, it must not become a hit.
        WriteCacheFile("merged.f137.mp4", 700, marked: false);

        YouTubeMediaCache.DownloadOverride = FakeDownload(4096);
        var result = YouTubeMediaCache.EnsureFileAsync(videoId, "137+140", "merged").GetAwaiter().GetResult();

        Assert.IsTrue(result.Ok, result.Detail);
        Assert.IsFalse(result.FromCache);
        Assert.AreEqual(1, downloadCalls);
        Assert.AreEqual("merged.mp4", Path.GetFileName(result.Path));
    }

    [Test]
    public void CompletedCacheIsAdoptedWithoutADownload()
    {
        WriteCacheFile("video.mp4", 1024, marked: true);

        YouTubeMediaCache.DownloadOverride = FakeDownload(1024); // would be counted if called
        var result = YouTubeMediaCache.EnsureFileAsync(videoId, "18", "video").GetAwaiter().GetResult();

        Assert.IsTrue(result.Ok, result.Detail);
        Assert.IsTrue(result.FromCache);
        Assert.AreEqual(0, downloadCalls, "a completed cache entry must not trigger a download");
        StringAssert.EndsWith("video.mp4", result.Path);
    }

    // --- 取得に失敗したもの・空のものは確定しない -----------------------------

    [Test]
    public void FailedDownloadLeavesNoAdoptableCacheFile()
    {
        WriteCacheFile("video.mp4", 1024, marked: false); // stale leftover of an interrupted run

        YouTubeMediaCache.DownloadOverride = FailingDownload("network gone");
        var result = YouTubeMediaCache.EnsureFileAsync(videoId, "18", "video").GetAwaiter().GetResult();

        Assert.IsFalse(result.Ok);
        StringAssert.Contains("yt-dlp download failed", result.Detail);
        Assert.AreEqual(0, Directory.GetFiles(dir).Length,
            "a failed download must leave nothing behind that could be adopted later");
    }

    [Test]
    public void EmptyProducedFileIsRejected()
    {
        YouTubeMediaCache.DownloadOverride = FakeDownload(0);
        var result = YouTubeMediaCache.EnsureFileAsync(videoId, "18", "video").GetAwaiter().GetResult();

        Assert.IsFalse(result.Ok, "a zero byte file cannot be opened by the VideoPlayer");
        StringAssert.Contains("empty", result.Detail);
        Assert.AreEqual(0, Directory.GetFiles(dir).Length);
    }

    // --- 再生失敗時の無効化 (Retry で再取得が走る) ----------------------------

    [Test]
    public void InvalidateRemovesTheFileAndItsMarkerSoRetryFetchesAgain()
    {
        WriteCacheFile("video.mp4", 1024, marked: true);
        var path = Path.Combine(dir, "video.mp4");
        Assert.IsTrue(YouTubeMediaCache.IsCompletedCacheFile(path));

        // What MusicManager does when the VideoPlayer cannot open the file.
        YouTubeMediaCache.InvalidateProducedFile(path);

        Assert.IsFalse(File.Exists(path));
        Assert.IsFalse(File.Exists(YouTubeMediaCache.DoneMarkerPath(path)));
        Assert.IsNull(YouTubeMediaCache.FindCompletedFile(dir, "video"),
            "after the invalidation there is no cache hit - the next attempt downloads again");
    }

    [Test]
    public void CompletedFileIsFoundAndAnUnmarkedOneIsNot()
    {
        WriteCacheFile("video.mp4", 1024, marked: true);
        WriteCacheFile("video.webm", 999, marked: false); // newer, but never completed

        var completed = YouTubeMediaCache.FindCompletedFile(dir, "video");

        Assert.IsNotNull(completed);
        Assert.AreEqual("video.mp4", Path.GetFileName(completed));
        Assert.IsTrue(YouTubeMediaCache.IsCompletedCacheFile(completed));
        Assert.IsFalse(YouTubeMediaCache.IsCompletedCacheFile(Path.Combine(dir, "video.webm")));
    }

    // --- 回数制限付き再取得 ---------------------------------------------------

    [Test]
    public void RefetchIsBoundedAndResetWhenTheSongIsPlayableAgain()
    {
        Assert.IsTrue(MediaRefetchPolicy.MayRefetch(videoId));

        MediaRefetchPolicy.CountRefetch(videoId);
        Assert.AreEqual(1, MediaRefetchPolicy.CountFor(videoId));
        Assert.IsTrue(MediaRefetchPolicy.MayRefetch(videoId));

        MediaRefetchPolicy.CountRefetch(videoId);
        Assert.IsFalse(MediaRefetchPolicy.MayRefetch(videoId),
            "past the limit the Option screen's cache deletion is the explicit way out");

        MediaRefetchPolicy.Reset(videoId);
        Assert.AreEqual(0, MediaRefetchPolicy.CountFor(videoId));
        Assert.IsTrue(MediaRefetchPolicy.MayRefetch(videoId));
    }

    [Test]
    public void RefetchCountSurvivesASceneReload()
    {
        // A Retry reloads the GameScene: the count lives in PlayerPrefs, so the
        // bound holds across attempts instead of starting from zero each time.
        MediaRefetchPolicy.CountRefetch(videoId);

        Assert.AreEqual(1, PlayerPrefs.GetInt("FlashBeat.MediaRefetchCount." + videoId, -1));
    }
}
