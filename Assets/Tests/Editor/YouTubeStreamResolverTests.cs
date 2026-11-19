using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FlashBeat.Gameplay;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// YouTubeStreamResolver (Issue #642 / #570) の契約テスト。
///
///   - ツール探索 (yt-dlp / deno / ffmpeg の --version プローブ) は
///     呼び出しスレッドではなくスレッドプールで走る
///   - 同時呼び出しは 1 回のプローブを共有し、結果はプロセス内でキャッシュされる
///   - 候補の優先順位 (環境変数 → PATH → …) は従来どおり
///   - プローブがタイムアウトしたら子プロセスを kill する
///
/// 実バイナリには依存しない: PATH の先頭に空の偽実行ファイルを置き、
/// <see cref="YouTubeStreamResolver.CandidateProbeOverride"/> でプローブを
/// 迂回する (YouTubeMediaCache.DownloadOverride と同じ隔離方法)。
/// </summary>
public class YouTubeStreamResolverTests
{
    string tempDir;
    string oldPath;
    string oldYtDlp;
    string oldDeno;
    string oldFfmpeg;

    [SetUp]
    public void SetUp()
    {
        tempDir = Path.Combine(Path.GetTempPath(), "fb642-resolver-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        oldPath = Environment.GetEnvironmentVariable("PATH");
        oldYtDlp = Environment.GetEnvironmentVariable("FLASHBEAT_YTDLP_PATH");
        oldDeno = Environment.GetEnvironmentVariable("FLASHBEAT_DENO_PATH");
        oldFfmpeg = Environment.GetEnvironmentVariable("FLASHBEAT_FFMPEG_PATH");

        // 明示オーバーライドが残っていると候補走査を通らないので空にする。
        Environment.SetEnvironmentVariable("FLASHBEAT_YTDLP_PATH", "");
        Environment.SetEnvironmentVariable("FLASHBEAT_DENO_PATH", "");
        Environment.SetEnvironmentVariable("FLASHBEAT_FFMPEG_PATH", "");

        YouTubeStreamResolver.CandidateProbeOverride = null;
        YouTubeStreamResolver.ProbeTimeoutMsOverride = 0;
        YouTubeStreamResolver.ResetToolResolutionForTests();
    }

    [TearDown]
    public void TearDown()
    {
        YouTubeStreamResolver.CandidateProbeOverride = null;
        YouTubeStreamResolver.ProbeTimeoutMsOverride = 0;
        YouTubeStreamResolver.ResetToolResolutionForTests();
        Environment.SetEnvironmentVariable("PATH", oldPath);
        Environment.SetEnvironmentVariable("FLASHBEAT_YTDLP_PATH", oldYtDlp);
        Environment.SetEnvironmentVariable("FLASHBEAT_DENO_PATH", oldDeno);
        Environment.SetEnvironmentVariable("FLASHBEAT_FFMPEG_PATH", oldFfmpeg);

        try { Directory.Delete(tempDir, true); } catch { }
    }

    /// <summary>
    /// PATH の先頭に偽のツール実行ファイルを置く。中身は空でよい -
    /// CandidateProbeOverride がプロセス起動の代わりに成否を返す。
    /// "ffmpeg.exe" という名前は Windows / Unix の両方で候補になる
    /// (WhereOnPathAll は拡張子なし名も別途探すが、.exe 側が先に来る)。
    /// </summary>
    string InstallFakeToolOnPath(string exeName)
    {
        string dir = Path.Combine(tempDir, "bin-" + exeName);
        Directory.CreateDirectory(dir);
        string exe = Path.Combine(dir, exeName);
        File.WriteAllBytes(exe, new byte[] { 0x7f, 0x45, 0x4c, 0x46 }); // 存在すれば十分 (probe は override)
        Environment.SetEnvironmentVariable("PATH", dir + Path.PathSeparator + oldPath);
        return dir;
    }

    [Test]
    public async Task ResolveFfmpegPathAsync_ProbesOnThreadPool_AndCachesTheResult()
    {
        string dir = InstallFakeToolOnPath("ffmpeg.exe");
        string expected = Path.Combine(dir, "ffmpeg.exe");

        int callerThread = Thread.CurrentThread.ManagedThreadId;
        int probeThread = -1;
        bool probeOnPool = false;
        int probeCalls = 0;
        YouTubeStreamResolver.CandidateProbeOverride = (exe, args) =>
        {
            probeCalls++;
            probeThread = Thread.CurrentThread.ManagedThreadId;
            probeOnPool = Thread.CurrentThread.IsThreadPoolThread;
            return string.Equals(exe, expected, StringComparison.Ordinal);
        };

        // 同時呼び出しは 1 回のプローブを共有する (resolutionGate + Task)。
        var first = YouTubeStreamResolver.ResolveFfmpegPathAsync();
        var second = YouTubeStreamResolver.ResolveFfmpegPathAsync();
        string path = await first;
        string path2 = await second;

        Assert.AreEqual(expected, path, "PATH 先頭の候補が選ばれていない");
        Assert.AreEqual(path, path2);
        Assert.AreEqual(1, probeCalls, "同時呼び出しでプローブが二重に走った");
        Assert.IsTrue(probeOnPool, "バージョンプローブが呼び出しスレッドで走っている (issue #642)");
        Assert.AreNotEqual(callerThread, probeThread);

        // キャッシュ済み: 3 回目はプローブを増やさない。
        string path3 = await YouTubeStreamResolver.ResolveFfmpegPathAsync();
        Assert.AreEqual(expected, path3);
        Assert.AreEqual(1, probeCalls, "解決結果がキャッシュされていない");
    }

    [Test]
    public async Task ResolveYtDlpPathAsync_ProbesOnThreadPool_AndKeepsCandidateOrder()
    {
        string dir = InstallFakeToolOnPath("yt-dlp.exe");
        string expected = Path.Combine(dir, "yt-dlp.exe");

        bool probeOnPool = false;
        YouTubeStreamResolver.CandidateProbeOverride = (exe, args) =>
        {
            probeOnPool = Thread.CurrentThread.IsThreadPoolThread;
            return string.Equals(exe, expected, StringComparison.Ordinal);
        };

        string path = await YouTubeStreamResolver.ResolveYtDlpPathAsync();

        Assert.AreEqual(expected, path);
        Assert.IsTrue(probeOnPool, "yt-dlp のプローブが呼び出しスレッドで走っている");
    }

    [Test]
    public async Task ResolveYtDlpPathAsync_EnvironmentOverrideStillWins()
    {
        // FLASHBEAT_YTDLP_PATH は従来どおり最優先 (プローブすら通らない)。
        string fake = Path.Combine(tempDir, "yt-dlp-custom");
        File.WriteAllBytes(fake, new byte[] { 1 });
        Environment.SetEnvironmentVariable("FLASHBEAT_YTDLP_PATH", fake);
        YouTubeStreamResolver.ResetToolResolutionForTests();

        string path = await YouTubeStreamResolver.ResolveYtDlpPathAsync();
        Assert.AreEqual(fake, path);
    }

    [Test]
    public void RunsSuccessfully_ReturnsTrueForAQuickSuccessfulExit()
    {
        string exe, args;
        if (IsWindows())
        {
            exe = "cmd.exe";
            args = "/c exit 0";
        }
        else
        {
            exe = "/bin/sh";
            args = "-c \"exit 0\"";
        }

        Assert.IsTrue(YouTubeStreamResolver.RunsSuccessfully(exe, args));
    }

    [Test]
    public void RunsSuccessfully_TimesOutAndKillsTheHungProcess()
    {
        // タイムアウトを短くして、ハングした候補が kill されることを実プロセスで
        // 確認する。子プロセスが生き延びると末尾のコマンドが sentinel を書くので、
        // 「false が返った」だけでなく「プロセスが死んだ」ことまで検証できる。
        YouTubeStreamResolver.ProbeTimeoutMsOverride = 400;
        string sentinel = Path.Combine(tempDir, "probe-survived.txt");

        string exe, args;
        if (IsWindows())
        {
            // ping -n 3 は約 2 秒。kill されなければ & の先の echo が sentinel を書く。
            exe = "cmd.exe";
            args = "/c ping -n 3 127.0.0.1 > nul & echo survived > \"" + sentinel + "\"";
        }
        else
        {
            exe = "/bin/sh";
            args = "-c \"sleep 2; touch '" + sentinel + "'\"";
        }

        bool ok = YouTubeStreamResolver.RunsSuccessfully(exe, args);
        Assert.IsFalse(ok, "タイムアウトしたプローブは失敗を返す");

        // sleep/ping の完了を待ってから sentinel を見る (kill が無ければ書かれる)。
        Thread.Sleep(3000);
        Assert.IsFalse(File.Exists(sentinel),
            "タイムアウトしたプローブの子プロセスが kill されず生き残っている (issue #642)");
    }

    static bool IsWindows()
    {
        return Application.platform == RuntimePlatform.WindowsEditor
            || Application.platform == RuntimePlatform.WindowsPlayer;
    }
}
