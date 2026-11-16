using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FlashBeat.Gameplay;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// MediaPrefetcher (Issue #377) の契約テスト。
///
///   - キャッシュ済みの曲は形式解決もダウンロードも走らない
///   - 形式解決 → 計画 → ダウンロードで TryGetReadyFiles が温まる
///   - 同じ曲の多重要求は 1 本の先読みにまとまる
///   - 別の曲への切替は前の先読みをキャンセルする
///   - 失敗はカウントするだけで再試行しない
///
/// yt-dlp / ffmpeg は <see cref="MediaPrefetcher.FormatsOverride"/> と
/// <see cref="YouTubeMediaCache.DownloadOverride"/> で迂回する
/// (MediaCacheCompletionTests と同じ隔離方法。videoId は実行ごとに一意)。
/// </summary>
public class MediaPrefetcherTests
{
    readonly List<string> videoIds = new List<string>();

    [SetUp]
    public void SetUp()
    {
        MediaPrefetcher.ResetForAudit();
        MediaPrefetcher.FormatsOverride = null;
        YouTubeMediaCache.DownloadOverride = null;
    }

    [TearDown]
    public void TearDown()
    {
        MediaPrefetcher.ResetForAudit();
        MediaPrefetcher.FormatsOverride = null;
        YouTubeMediaCache.DownloadOverride = null;
        foreach (string id in videoIds)
        {
            try { Directory.Delete(YouTubeMediaCache.CacheDirectoryFor(id), true); } catch { }
            MediaRefetchPolicy.Reset(id);
        }
        videoIds.Clear();
    }

    string NewVideoId()
    {
        string id = "prefetch-test-" + Guid.NewGuid().ToString("N");
        videoIds.Add(id);
        return id;
    }

    /// <summary>映像+音声を持つ progressive mp4 (SelectPlan の結合枝)。</summary>
    static List<YtDlpFormat> CombinedFormats()
    {
        return new List<YtDlpFormat>
        {
            new YtDlpFormat
            {
                Id = "22", Ext = "mp4", Resolution = "1280x720",
                Vcodec = "avc1.64001f", Acodec = "mp4a.40.2",
            },
        };
    }

    /// <summary>偽ダウンロード: テンプレートを mp4 として書き出して成功を返す。</summary>
    static void InstallFakeDownload()
    {
        YouTubeMediaCache.DownloadOverride = (id, selector, template, token) =>
        {
            File.WriteAllBytes(template.Replace("%(ext)s", "mp4"), new byte[64]);
            return Task.FromResult(new YtDlpOutput { ExitCode = 0 });
        };
    }

    [Test]
    public async Task Request_EmptyVideoId_IsANoOp()
    {
        await MediaPrefetcher.Request(null);
        await MediaPrefetcher.Request("");
        Assert.AreEqual(0, MediaPrefetcher.StartedCount);
    }

    [Test]
    public async Task Request_CachedMedia_SkipsFormatResolutionAndDownload()
    {
        string videoId = NewVideoId();
        string dir = YouTubeMediaCache.CacheDirectoryFor(videoId);
        Directory.CreateDirectory(dir);

        // canMerge のどちらでもヒットするよう、結合と分離ペアの両方を
        // 完了ファイルとして置く。
        foreach (string prefix in new[] { YouTubeMediaCache.MergedFilePrefix, YouTubeMediaCache.VideoFilePrefix, YouTubeMediaCache.AudioFilePrefix })
        {
            string path = Path.Combine(dir, prefix + ".mp4");
            File.WriteAllBytes(path, new byte[32]);
            File.WriteAllText(YouTubeMediaCache.DoneMarkerPath(path), "32");
        }

        bool formatsCalled = false;
        MediaPrefetcher.FormatsOverride = (id, token) =>
        {
            formatsCalled = true;
            return Task.FromResult(CombinedFormats());
        };

        await MediaPrefetcher.Request(videoId);

        Assert.IsFalse(formatsCalled, "キャッシュ済みなら形式解決を呼ばない");
        Assert.AreEqual(1, MediaPrefetcher.CacheHitCount);
        Assert.AreEqual(0, MediaPrefetcher.StartedCount);
    }

    [Test]
    public async Task Request_ResolvesDownloads_AndWarmsTheCache()
    {
        string videoId = NewVideoId();
        InstallFakeDownload();
        MediaPrefetcher.FormatsOverride = (id, token) => Task.FromResult(CombinedFormats());

        await MediaPrefetcher.Request(videoId);

        Assert.AreEqual(1, MediaPrefetcher.StartedCount);
        Assert.AreEqual(1, MediaPrefetcher.CompletedCount);
        Assert.AreEqual(0, MediaPrefetcher.FailedCount);

        // 温まった曲への再要求はキャッシュヒット (形式解決もダウンロードも走らない)。
        bool formatsCalled = false;
        MediaPrefetcher.FormatsOverride = (id, token) =>
        {
            formatsCalled = true;
            return Task.FromResult(CombinedFormats());
        };
        await MediaPrefetcher.Request(videoId);
        Assert.IsFalse(formatsCalled, "温め済みの曲は再解決しない");
        Assert.AreEqual(1, MediaPrefetcher.StartedCount, "先読みは 1 回だけ");
    }

    [Test]
    public async Task Request_SameVideoIdWhileInFlight_SharesOneRun()
    {
        string videoId = NewVideoId();
        InstallFakeDownload();

        var gate = new TaskCompletionSource<List<YtDlpFormat>>();
        int formatCalls = 0;
        MediaPrefetcher.FormatsOverride = (id, token) =>
        {
            formatCalls++;
            return gate.Task;
        };

        Task first = MediaPrefetcher.Request(videoId);
        Task second = MediaPrefetcher.Request(videoId);

        Assert.AreEqual(1, MediaPrefetcher.StartedCount, "同じ曲の多重要求は 1 本にまとまる");
        Assert.AreSame(first, second, "同じ完了 Task を返す");

        gate.SetResult(CombinedFormats());
        await first;
        Assert.AreEqual(1, formatCalls);
        Assert.AreEqual(1, MediaPrefetcher.CompletedCount);
    }

    [Test]
    public async Task Request_DifferentVideoId_CancelsThePreviousPrefetch()
    {
        string first = NewVideoId();
        string second = NewVideoId();
        InstallFakeDownload();

        MediaPrefetcher.FormatsOverride = (id, token) =>
        {
            if (id == first)
            {
                // キャンセルを token 経由で観測する (Task.Delay のキャンセルで OCE)。
                return Task.Run(async () =>
                {
                    await Task.Delay(Timeout.Infinite, token);
                    return new List<YtDlpFormat>();
                }, token);
            }
            return Task.FromResult(CombinedFormats());
        };

        Task firstRun = MediaPrefetcher.Request(first);
        Assert.AreEqual(first, MediaPrefetcher.InFlightVideoId);

        Task secondRun = MediaPrefetcher.Request(second);
        await firstRun;
        await secondRun;

        Assert.AreEqual(1, MediaPrefetcher.CancelledCount, "切替で前の先読みはキャンセルされる");
        Assert.AreEqual(1, MediaPrefetcher.CompletedCount, "新しい曲の先読みは完了する");
        Assert.IsNull(MediaPrefetcher.InFlightVideoId);
    }

    [Test]
    public async Task Request_NoFormats_CountsFailure_AndDoesNotRetry()
    {
        string videoId = NewVideoId();
        MediaPrefetcher.FormatsOverride = (id, token) => Task.FromResult(new List<YtDlpFormat>());

        await MediaPrefetcher.Request(videoId);
        await MediaPrefetcher.Request(videoId);

        Assert.AreEqual(1, MediaPrefetcher.StartedCount);
        Assert.AreEqual(1, MediaPrefetcher.FailedCount);
        Assert.AreEqual(0, MediaPrefetcher.CompletedCount);
    }
}
