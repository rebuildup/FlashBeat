using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FlashBeat.Gameplay;
using NUnit.Framework;

/// <summary>
/// Issue #642: 結合ダウンロードが失敗して映像のみフォールバックに落ちたとき、
/// 分離音声も取得されることを固定する。
///
/// バグの形: 結合プラン (Combined) は AudioSelector を持たないので
/// HasAudioFile == false。フォールバックで combined=false にしても
/// 音声ブランチが skip され、再生が無音になっていた。
///
/// 検証は 2 段:
///   - YouTubeMediaCache.PlanAfterMergedFallback が同じ映像ストリーム +
///     音声トラックを選ぶ純粋な契約
///   - MediaPrefetcher のフォールバック経路が実際に音声ファイルまで温め、
///     かつ分離ペアを「結合の再試行を妨げない」形で残すこと
///     (映像のみファイルは video prefix のまま = 次回は結合をやり直せる)。
///
/// yt-dlp / ffmpeg は FormatsOverride / DownloadOverride /
/// CandidateProbeOverride で迂回する (MediaPrefetcherTests と同じ隔離方法)。
/// </summary>
public class MediaFallbackPlanTests
{
    string tempDir;
    string oldPath;
    string oldFfmpeg;
    readonly List<string> videoIds = new List<string>();

    [SetUp]
    public void SetUp()
    {
        tempDir = Path.Combine(Path.GetTempPath(), "fb642-fallback-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        oldPath = Environment.GetEnvironmentVariable("PATH");
        oldFfmpeg = Environment.GetEnvironmentVariable("FLASHBEAT_FFMPEG_PATH");
        Environment.SetEnvironmentVariable("FLASHBEAT_FFMPEG_PATH", "");

        MediaPrefetcher.ResetForAudit();
        MediaPrefetcher.FormatsOverride = null;
        YouTubeMediaCache.DownloadOverride = null;
        YouTubeStreamResolver.CandidateProbeOverride = null;
        YouTubeStreamResolver.ResetToolResolutionForTests();
    }

    [TearDown]
    public void TearDown()
    {
        MediaPrefetcher.ResetForAudit();
        MediaPrefetcher.FormatsOverride = null;
        YouTubeMediaCache.DownloadOverride = null;
        YouTubeStreamResolver.CandidateProbeOverride = null;
        YouTubeStreamResolver.ProbeTimeoutMsOverride = 0;
        YouTubeStreamResolver.ResetToolResolutionForTests();
        Environment.SetEnvironmentVariable("PATH", oldPath);
        Environment.SetEnvironmentVariable("FLASHBEAT_FFMPEG_PATH", oldFfmpeg);

        foreach (string id in videoIds)
        {
            try { Directory.Delete(YouTubeMediaCache.CacheDirectoryFor(id), true); } catch { }
            MediaRefetchPolicy.Reset(id);
        }
        videoIds.Clear();
        try { Directory.Delete(tempDir, true); } catch { }
    }

    string NewVideoId()
    {
        string id = "fallback-test-" + Guid.NewGuid().ToString("N");
        videoIds.Add(id);
        return id;
    }

    /// <summary>結合形式を持たない映像+音声のフォーマット列 (分離結合の対象)。</summary>
    static List<YtDlpFormat> SplitCapableFormats()
    {
        return new List<YtDlpFormat>
        {
            new YtDlpFormat
            {
                Id = "137", Ext = "mp4", Resolution = "1280x720",
                Vcodec = "avc1.64001f", Acodec = "none",
            },
            new YtDlpFormat
            {
                Id = "140", Ext = "m4a", Resolution = "",
                Vcodec = "none", Acodec = "mp4a.40.2",
            },
        };
    }

    /// <summary>
    /// ffmpeg が「いる」ことにする: PATH 先頭に偽実行ファイルを置き、
    /// プローブを override で成功させる。canMerge=true が確定的になる。
    /// </summary>
    void ForceFfmpegAvailable()
    {
        string dir = Path.Combine(tempDir, "bin-ffmpeg");
        Directory.CreateDirectory(dir);
        string exe = Path.Combine(dir, "ffmpeg.exe");
        File.WriteAllBytes(exe, new byte[] { 1 });
        Environment.SetEnvironmentVariable("PATH", dir + Path.PathSeparator + oldPath);

        YouTubeStreamResolver.CandidateProbeOverride = (path, args) =>
            string.Equals(path, exe, StringComparison.Ordinal);
        YouTubeStreamResolver.ResetToolResolutionForTests();
    }

    [Test]
    public void MergedPlan_HasNoAudioFile_AndRecordsTheFallbackSelector()
    {
        var merged = YouTubeMediaCache.SelectPlan(SplitCapableFormats(), canMerge: true);

        Assert.IsTrue(merged.Combined);
        Assert.IsFalse(merged.HasAudioFile,
            "結合プランは音声セレクタを持たない - フォールバック時にこれが罠になる");
        Assert.AreEqual("137", merged.FallbackVideoSelector);
        StringAssert.Contains("+", merged.VideoSelector);
    }

    [Test]
    public void PlanAfterMergedFallback_SelectsTheSameVideoPlusItsAudio()
    {
        var formats = SplitCapableFormats();
        var merged = YouTubeMediaCache.SelectPlan(formats, canMerge: true);

        var split = YouTubeMediaCache.PlanAfterMergedFallback(formats);

        Assert.IsFalse(split.Combined);
        Assert.IsTrue(split.HasAudioFile,
            "フォールバック後のプランは分離音声を取得できなければならない (無音バグの本体)");
        Assert.AreEqual(merged.FallbackVideoSelector, split.VideoSelector,
            "フォールバックは結合プランが記録した映像ストリームと同じものを選ぶ");
        Assert.AreEqual("140", split.AudioSelector);
    }

    /// <summary>
    /// 結合ダウンロードが失敗 → 映像のみフォールバック → 分離音声も取得、
    /// までを MediaPrefetcher の実経路で通す。完了判定は「分離ペアが
    /// 使える形で残り、結合ファイルは無い (次回結合を再試行できる)」こと。
    /// </summary>
    [Test]
    public async Task Prefetcher_MergedFallback_AlsoWarmsTheAudioTrack()
    {
        ForceFfmpegAvailable();
        string videoId = NewVideoId();

        MediaPrefetcher.FormatsOverride = (id, token) => Task.FromResult(SplitCapableFormats());
        YouTubeMediaCache.DownloadOverride = (id, selector, template, token) =>
        {
            // 結合セレクタ ("137+140") は ffmpeg 不在を模して失敗させる。
            // 映像のみ / 音声のみは成功させてファイルを置く。
            if (selector != null && selector.Contains("+"))
            {
                return Task.FromResult(new YtDlpOutput { ExitCode = 1, Stderr = "merge failed (no ffmpeg)" });
            }
            File.WriteAllBytes(template.Replace("%(ext)s", "mp4"), new byte[64]);
            return Task.FromResult(new YtDlpOutput { ExitCode = 0 });
        };

        await MediaPrefetcher.Request(videoId);

        Assert.AreEqual(1, MediaPrefetcher.StartedCount);
        Assert.AreEqual(1, MediaPrefetcher.CompletedCount,
            "フォールバックで温めきったのに失敗カウントされている");
        Assert.AreEqual(0, MediaPrefetcher.FailedCount);

        string dir = YouTubeMediaCache.CacheDirectoryFor(videoId);
        ReadyMedia ready;
        Assert.IsTrue(YouTubeMediaCache.TryGetReadyFiles(videoId, out ready, canMerge: false),
            "映像+音声のペアが完了ファイルとして残っていない");
        Assert.IsTrue(ready.HasAudio, "フォールバック経路で音声が取得されていない (無音のまま)");
        Assert.IsFalse(ready.Combined);

        // 分離ペアは canMerge:true の環境では再利用されない = 次回実行時に
        // 結合ダウンロードが再試行される (video prefix のまま残る契約)。
        Assert.IsFalse(YouTubeMediaCache.TryGetReadyFiles(videoId, out _, canMerge: true),
            "分離ペアが結合環境で再利用可能になっている - 結合の再試行が阻害される");
        Assert.IsNull(YouTubeMediaCache.FindCompletedFile(dir, YouTubeMediaCache.MergedFilePrefix),
            "失敗したはずの結合ファイルが完了扱いで残っている");

        // 温め済みの曲への再要求はキャッシュ扱いで再実行しない。
        await MediaPrefetcher.Request(videoId);
        Assert.AreEqual(1, MediaPrefetcher.StartedCount, "温め済みの曲が再先読みされた");
    }
}
