using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace FlashBeat.Gameplay
{
    /// <summary>Result of one yt-dlp invocation.</summary>
    public sealed class YtDlpOutput
    {
        public int ExitCode;
        public string Stdout = "";
        public string Stderr = "";

        public bool Ok => ExitCode == 0;

        public string ErrorSummary
        {
            get
            {
                var err = (Stderr ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
                if (err.Length > 240) err = err.Substring(0, 240) + "...";
                return $"exit={ExitCode} stderr={err}";
            }
        }
    }

    /// <summary>One line of `yt-dlp -F` output.</summary>
    public sealed class YtDlpFormat
    {
        public string Id = "";
        public string Ext = "";
        public string Resolution = "";
        public string Vcodec = "";
        public string Acodec = "";

        public bool HasVideo => !string.IsNullOrEmpty(Vcodec) && Vcodec != "none" && Vcodec != "audio only";
        public bool HasAudio => !string.IsNullOrEmpty(Acodec) && Acodec != "none" && Acodec != "video only";
        public bool IsCombined => HasVideo && HasAudio;
        public bool IsAvc1 => Vcodec != null && Vcodec.StartsWith("avc1", StringComparison.OrdinalIgnoreCase);
        public bool IsMp4Audio => Acodec != null && Acodec.StartsWith("mp4a", StringComparison.OrdinalIgnoreCase);

        public int Height
        {
            get
            {
                if (string.IsNullOrEmpty(Resolution)) return 0;
                var parts = Resolution.Split('x');
                int h;
                if (parts.Length == 2 && int.TryParse(parts[1], out h)) return h;
                return 0;
            }
        }

        public override string ToString()
        {
            return $"{Id} {Ext} {Resolution} v={Vcodec} a={Acodec}";
        }
    }

    // Resolves a YouTube videoId to a playable media file by spawning yt-dlp.exe.
    //
    // Why yt-dlp and not the bundled InvidiousVideoPlayer:
    //   The youtube-player package routes through Invidious, whose public
    //   instance list has decayed — 0/13 hosts return valid JSON today. yt-dlp
    //   talks to YouTube directly.
    //
    // Why the media is downloaded to a local cache instead of streamed by URL:
    //   Media Foundation (MFCreateSourceReaderFromURL) rejects googlevideo.com
    //   URLs with 0x80070005 (access denied) because the request needs the
    //   same User-Agent / po-token context yt-dlp used, and Unity's VideoPlayer
    //   cannot send custom HTTP headers. Downloading the exact same bytes with
    //   yt-dlp and playing the local file sidesteps that limitation entirely.
    //   The cached file is reused on the next run of the same song.
    //
    // Install yt-dlp: https://github.com/yt-dlp/yt-dlp/releases (Windows: "yt-dlp.exe" / macOS: "yt-dlp")
    //   The resolver auto-detects yt-dlp from PATH, the mise install tree,
    //   common Python install locations and a project-bundled
    //   Assets/StreamingAssets/yt-dlp/yt-dlp.exe. On macOS/Linux the
    //   extension-less binary name is probed as well.
    //   Override the path via the FLASHBEAT_YTDLP_PATH environment variable.
    //
    // Deno is used as the JavaScript runtime for yt-dlp (YouTube's challenge
    // solver needs a JS engine). When a deno binary is found it is passed
    // explicitly as `--js-runtimes deno:<path>`; override with FLASHBEAT_DENO_PATH.
    public static class YouTubeStreamResolver
    {
        public const string WatchUrlFormat = "https://www.youtube.com/watch?v={0}";

        static bool IsWindows =>
            System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                System.Runtime.InteropServices.OSPlatform.Windows);

        // ---------------------------------------------------------------
        // yt-dlp / deno discovery
        // ---------------------------------------------------------------

        static string cachedYtDlpPath;
        static bool ytDlpPathResolved;
        static string cachedDenoPath;
        static bool denoPathResolved;
        static string cachedFfmpegPath;
        static bool ffmpegPathResolved;

        /// <summary>
        /// Serializes the lazy tool resolution: the first caller starts the
        /// background probe and every later caller shares the same Task, so
        /// the candidate list is probed exactly once per process (issue #642).
        /// </summary>
        static readonly object resolutionGate = new object();
        static Task<string> ytDlpPathTask;
        static Task<string> denoPathTask;
        static Task<string> ffmpegPathTask;

        /// <summary>
        /// Timeout of the per-candidate "--version" probe. A binary that does
        /// not answer within this window is killed and skipped.
        /// </summary>
        public const int ProbeTimeoutMs = 20000;

        /// <summary>
        /// Test seam (EditMode): when > 0 it replaces <see cref="ProbeTimeoutMs"/>
        /// so the timeout-kill path can be verified without waiting 20 s.
        /// The game never sets it - a test restores 0 in its TearDown.
        /// </summary>
        public static int ProbeTimeoutMsOverride;

        /// <summary>
        /// Test seam for the EditMode tests: when set, it replaces the
        /// per-candidate version probe so tool discovery can be verified
        /// without real binaries (same contract as
        /// <see cref="YouTubeMediaCache.DownloadOverride"/> - the game never
        /// sets it, a test restores null in its TearDown, issue #642).
        /// </summary>
        public static Func<string, string, bool> CandidateProbeOverride;

        /// <summary>
        /// Runs <paramref name="probe"/> on the thread pool. The probe bodies
        /// only use System.IO / environment variables / Process - no Unity API -
        /// so they are safe off the main thread (issue #642 / #570).
        /// </summary>
        static Task<string> RunResolution(Func<string> probe)
        {
            return Task.Run(() =>
            {
                try { return probe(); }
                catch (Exception e)
                {
                    Debug.LogWarning("[YouTubeStreamResolver] tool resolution failed: " + e.Message);
                    return null;
                }
            });
        }

        /// <summary>
        /// Test seam (EditMode): clears the cached tool resolution so a test
        /// can re-resolve with a different PATH / FLASHBEAT_* environment.
        /// The game never calls it.
        /// </summary>
        public static void ResetToolResolutionForTests()
        {
            lock (resolutionGate)
            {
                ytDlpPathTask = null;
                denoPathTask = null;
                ffmpegPathTask = null;
                ytDlpPathResolved = false;
                cachedYtDlpPath = null;
                denoPathResolved = false;
                cachedDenoPath = null;
                ffmpegPathResolved = false;
                cachedFfmpegPath = null;
            }
        }

        /// <summary>
        /// Resolves the yt-dlp binary off the calling thread (issue #642).
        ///
        /// The discovery probes spawn `yt-dlp --version`-style processes; run
        /// synchronously on the Unity main thread they froze the game for up
        /// to <see cref="ProbeTimeoutMs"/> per candidate on machines without
        /// the tools (#570). The result is cached process-wide and concurrent
        /// callers share one probe. Only the Unity-API reads
        /// (Application.streamingAssetsPath / dataPath) stay on the calling
        /// thread - they are captured before the work moves to the pool.
        /// </summary>
        public static Task<string> ResolveYtDlpPathAsync()
        {
            lock (resolutionGate)
            {
                if (ytDlpPathTask == null)
                {
                    string streamingAssetsPath = Application.streamingAssetsPath;
                    string dataPath = Application.dataPath;
                    ytDlpPathTask = RunResolution(() => ResolveYtDlpPathCore(streamingAssetsPath, dataPath));
                }
                return ytDlpPathTask;
            }
        }

        static string ResolveYtDlpPathCore(string streamingAssetsPath, string dataPath)
        {
            if (ytDlpPathResolved) return cachedYtDlpPath;
            ytDlpPathResolved = true;

            // 1. Explicit override
            var pref = Environment.GetEnvironmentVariable("FLASHBEAT_YTDLP_PATH");
            if (!string.IsNullOrEmpty(pref) && File.Exists(pref))
            {
                cachedYtDlpPath = pref;
                return cachedYtDlpPath;
            }

            // 1.5. 同梱ツール (issue #795): 配布 ZIP にだけ存在する製品管理下の
            //      バイナリ。利用者の PATH や開発環境に依存しないため最優先で使う。
            //      FLASHBEAT_*_PATH の明示指定だけがこれより優先される。
            EnsureBundledToolsRunnable(streamingAssetsPath);
            EnsureBundledToolsRunnable(Path.Combine(dataPath, "StreamingAssets"));
            var bundledHit = FirstUsable(BundledToolCandidates(streamingAssetsPath, dataPath, YtDlpBundledNames()), "--version");
            if (bundledHit != null)
            {
                cachedYtDlpPath = bundledHit;
                return cachedYtDlpPath;
            }

            // 2. PATH (a launcher that exists but does not work - the mise shims -
            //    is skipped, the candidate has to answer to --version)
            var pathHit = FirstUsable(WhereOnPathAll("yt-dlp.exe"), "--version");
            if (pathHit != null)
            {
                cachedYtDlpPath = pathHit;
                return cachedYtDlpPath;
            }

            // 3. mise install tree (the mise *shim* is unusable when no version
            //    is pinned, so look at the real install directories).
            var miseHit = FindInVersionedDir("mise", "installs", "yt-dlp", "yt-dlp.exe");
            if (miseHit != null)
            {
                cachedYtDlpPath = miseHit;
                return cachedYtDlpPath;
            }

            // 4. Common Windows Python install locations
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var candidates = new List<string>();
            foreach (var ver in new[] { "314", "313", "312", "311", "310" })
            {
                candidates.Add(Path.Combine(appData, "Python", "Python" + ver, "Scripts", "yt-dlp.exe"));
                candidates.Add(Path.Combine(localAppData, "Programs", "Python", "Python" + ver, "Scripts", "yt-dlp.exe"));
                candidates.Add(@"C:\Python" + ver + @"\Scripts\yt-dlp.exe");
            }
            candidates.Add(Path.Combine(streamingAssetsPath, "yt-dlp", "yt-dlp.exe"));
            candidates.Add(Path.Combine(dataPath, "StreamingAssets", "yt-dlp", "yt-dlp.exe"));

            // 5. Linux/macOS fallbacks
            string[] unixCandidates = { "/usr/local/bin/yt-dlp", "/usr/bin/yt-dlp", "/opt/homebrew/bin/yt-dlp" };
            foreach (var c in unixCandidates)
            {
                candidates.Add(c);
            }

            cachedYtDlpPath = FirstUsable(candidates, "--version");
            return cachedYtDlpPath;
        }

        /// <summary>
        /// Resolves the deno binary off the calling thread (issue #642) - same
        /// background-probe contract as <see cref="ResolveYtDlpPathAsync"/>.
        /// </summary>
        public static Task<string> ResolveDenoPathAsync()
        {
            lock (resolutionGate)
            {
                if (denoPathTask == null)
                {
                    // Application.* は呼び出しスレッドでしか読めないため、
                    // プールへ移す前にここで捕捉する (yt-dlp と同じ契約)。
                    string streamingAssetsPath = Application.streamingAssetsPath;
                    string dataPath = Application.dataPath;
                    denoPathTask = RunResolution(() => ResolveDenoPathCore(streamingAssetsPath, dataPath));
                }
                return denoPathTask;
            }
        }

        static string ResolveDenoPathCore(string streamingAssetsPath, string dataPath)
        {
            if (denoPathResolved) return cachedDenoPath;
            denoPathResolved = true;

            var pref = Environment.GetEnvironmentVariable("FLASHBEAT_DENO_PATH");
            if (!string.IsNullOrEmpty(pref) && File.Exists(pref))
            {
                cachedDenoPath = pref;
                return cachedDenoPath;
            }

            // 同梱ツール (issue #795): yt-dlp と同じく製品管理下のバイナリを優先する。
            EnsureBundledToolsRunnable(streamingAssetsPath);
            EnsureBundledToolsRunnable(Path.Combine(dataPath, "StreamingAssets"));
            var bundledHit = FirstUsable(BundledToolCandidates(streamingAssetsPath, dataPath, DenoBundledNames()), "--version");
            if (bundledHit != null)
            {
                cachedDenoPath = bundledHit;
                return cachedDenoPath;
            }

            var pathHit = FirstUsable(WhereOnPathAll("deno.exe"), "--version");
            if (pathHit != null)
            {
                cachedDenoPath = pathHit;
                return cachedDenoPath;
            }

            var miseHit = FindInVersionedDir("mise", "installs", "deno", "deno.exe");
            if (miseHit != null)
            {
                cachedDenoPath = miseHit;
                return cachedDenoPath;
            }

            var miseBinHit = FindInVersionedDir("mise", "installs", "deno", "bin", "deno.exe");
            if (miseBinHit != null)
            {
                cachedDenoPath = miseBinHit;
                return cachedDenoPath;
            }

            // macOS / Linux: the binary has no ".exe" suffix and the mise tree
            // lives in ~/.local/share, not in the Windows LocalApplicationData.
            var unixPathHit = FirstUsable(WhereOnPathAll("deno"), "--version");
            if (unixPathHit != null)
            {
                cachedDenoPath = unixPathHit;
                return cachedDenoPath;
            }

            string[] unixCandidates =
            {
                "/opt/homebrew/bin/deno",
                "/usr/local/bin/deno",
                "/usr/bin/deno",
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile) + "/.deno/bin/deno",
            };
            var unixHit = FirstUsable(unixCandidates, "--version");
            if (unixHit != null)
            {
                cachedDenoPath = unixHit;
                return cachedDenoPath;
            }

            var miseUnixHit = FindInUnixMiseDir("deno", "deno");
            if (miseUnixHit != null)
            {
                cachedDenoPath = miseUnixHit;
            }

            return cachedDenoPath;
        }

        /// <summary>
        /// Finds a tool in the unix mise install tree (~/.local/share/mise/installs/&lt;tool&gt;/&lt;version&gt;/&lt;exe&gt;).
        /// The newest version wins.
        /// </summary>
        static string FindInUnixMiseDir(string tool, string exeName)
        {
            string home;
            try
            {
                home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
            }
            catch { return null; }
            if (string.IsNullOrEmpty(home)) { return null; }

            string root = Path.Combine(home, ".local", "share", "mise", "installs", tool);
            if (!Directory.Exists(root)) { return null; }

            string best = null;
            DateTime bestTime = DateTime.MinValue;
            try
            {
                foreach (var dir in Directory.GetDirectories(root))
                {
                    var candidate = Path.Combine(dir, exeName);
                    if (!File.Exists(candidate)) { continue; }
                    var written = File.GetLastWriteTimeUtc(candidate);
                    if (written > bestTime)
                    {
                        bestTime = written;
                        best = candidate;
                    }
                }
            }
            catch { }
            return best;
        }

        /// <summary>
        /// Locates ffmpeg. yt-dlp needs it to merge the video-only and the
        /// audio-only stream of YouTube into one mp4 - that merged file is the
        /// only way the YouTube sound reaches Unity's VideoPlayer (a separate
        /// audio-only m4a is refused: "Failed to recognize video file format").
        /// Override with FLASHBEAT_FFMPEG_PATH.
        /// </summary>
        public static Task<string> ResolveFfmpegPathAsync()
        {
            lock (resolutionGate)
            {
                if (ffmpegPathTask == null)
                {
                    ffmpegPathTask = RunResolution(ResolveFfmpegPathCore);
                }
                return ffmpegPathTask;
            }
        }

        static string ResolveFfmpegPathCore()
        {
            if (ffmpegPathResolved) return cachedFfmpegPath;
            ffmpegPathResolved = true;

            var pref = Environment.GetEnvironmentVariable("FLASHBEAT_FFMPEG_PATH");
            if (!string.IsNullOrEmpty(pref) && File.Exists(pref))
            {
                cachedFfmpegPath = pref;
                return cachedFfmpegPath;
            }

            var pathHit = FirstUsable(WhereOnPathAll("ffmpeg.exe"), "-version");
            if (pathHit != null)
            {
                cachedFfmpegPath = pathHit;
                return cachedFfmpegPath;
            }

            var miseHit = FindInVersionedDir("mise", "installs", "ffmpeg", "bin", "ffmpeg.exe");
            if (miseHit != null)
            {
                cachedFfmpegPath = miseHit;
                return cachedFfmpegPath;
            }

            var miseFlat = FindInVersionedDir("mise", "installs", "ffmpeg", "ffmpeg.exe");
            if (miseFlat != null)
            {
                cachedFfmpegPath = miseFlat;
                return cachedFfmpegPath;
            }

            string[] candidates =
            {
                @"C:\Program Files (x86)\Common Files\Adobe\CEP\extensions\Aeviewer\bin\Win\ffmpeg.exe",
                @"C:\ffmpeg\bin\ffmpeg.exe",
                "/usr/local/bin/ffmpeg",
                "/opt/homebrew/bin/ffmpeg",
            };
            foreach (var candidate in candidates)
            {
                try { if (File.Exists(candidate)) { cachedFfmpegPath = candidate; return cachedFfmpegPath; } }
                catch { }
            }

            return null;
        }

        /// <summary>
        /// Returns the first candidate that exists and answers to <paramref name="versionArgs"/>.
        /// A launcher that exists but does not work (the mise shims) is skipped.
        /// </summary>
        static string FirstUsable(IEnumerable<string> candidates, string versionArgs)
        {
            foreach (var candidate in candidates)
            {
                if (string.IsNullOrEmpty(candidate)) continue;
                try { if (!File.Exists(candidate)) continue; } catch { continue; }
                if (IsShim(candidate)) continue;
                if (ProbeCandidate(candidate, versionArgs)) return candidate;
            }
            return null;
        }

        /// <summary>
        /// The mise shims are launchers that abort with "No version is set for shim"
        /// when the tool has no pinned version - they must not be used.
        /// </summary>
        static bool IsShim(string path)
        {
            var normalized = path.Replace('\\', '/').ToLowerInvariant();
            return normalized.Contains("/mise/shims/") || normalized.Contains("/.mise/shims/");
        }

        // ---------------------------------------------------------------
        // Bundled tools (issue #795)
        // ---------------------------------------------------------------

        static bool IsMacOS =>
            System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                System.Runtime.InteropServices.OSPlatform.OSX);

        /// <summary>
        /// Expands bundled-tool relative paths under StreamingAssets. The release
        /// packaging scripts (tools/release/package-macos.sh / package-win64.ps1)
        /// copy script/fetch-media-tools.sh output into
        /// &lt;build&gt;/StreamingAssets/tools/&lt;relative path&gt;. Shipping targets
        /// are macOS (universal) and Windows x64 only; other platforms get an empty
        /// list and fall through to the PATH / mise / system search as before.
        /// </summary>
        static IEnumerable<string> BundledToolCandidates(string streamingAssetsPath, string dataPath, string[] relativePaths)
        {
            if (relativePaths == null) yield break;
            foreach (var rel in relativePaths)
            {
                yield return Path.Combine(streamingAssetsPath, "tools", rel);
                yield return Path.Combine(dataPath, "StreamingAssets", "tools", rel);
            }
        }

        /// <summary>yt-dlp_macos is a universal Mach-O; Windows ships yt-dlp.exe.</summary>
        static string[] YtDlpBundledNames()
        {
            if (IsWindows) return new[] { "windows-x64/yt-dlp.exe" };
            if (IsMacOS) return new[] { "macos/yt-dlp" };
            return new string[0];
        }

        /// <summary>Deno ships per-arch; the macOS build is universal so the arch key is required.</summary>
        static string[] DenoBundledNames()
        {
            if (IsWindows) return new[] { "windows-x64/deno.exe" };
            if (IsMacOS)
            {
                var arch = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture;
                var key = arch == System.Runtime.InteropServices.Architecture.Arm64 ? "macos-arm64" : "macos-x86_64";
                return new[] { key + "/deno" };
            }
            return new string[0];
        }

        /// <summary>
        /// macOS: removes the quarantine xattr from the bundled tools tree so
        /// Gatekeeper does not kill the child binaries (issue #795). A ZIP
        /// downloaded in a browser propagates com.apple.quarantine to every
        /// extracted file - including binaries nested inside FlashBeat.app -
        /// and a quarantined unsigned/adhoc-signed Mach-O is killed (SIGKILL)
        /// on exec even when the parent app is already running. Stripping the
        /// attribute from our own shipped tools inside our own bundle is the
        /// distributor-side fix; /usr/bin/xattr is a system binary and always
        /// runs. Best effort: a read-only (translocated) bundle simply keeps
        /// the old behaviour.
        /// </summary>
        static void EnsureBundledToolsRunnable(string streamingAssetsPath)
        {
            if (!IsMacOS || string.IsNullOrEmpty(streamingAssetsPath)) return;
            var toolsDir = Path.Combine(streamingAssetsPath, "tools");
            try { if (!Directory.Exists(toolsDir)) return; } catch { return; }
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "/usr/bin/xattr",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                psi.ArgumentList.Add("-dr");
                psi.ArgumentList.Add("com.apple.quarantine");
                psi.ArgumentList.Add(toolsDir);
                using (var p = Process.Start(psi))
                {
                    if (p == null) return;
                    if (!p.WaitForExit(5000)) { try { p.Kill(); } catch { } return; }
                }
            }
            catch { /* best effort: the version probe simply fails as before */ }
        }

        /// <summary>
        /// The per-candidate version probe. The EditMode tests replace it via
        /// <see cref="CandidateProbeOverride"/> so discovery can be verified
        /// without real binaries; the game always uses <see cref="RunsSuccessfully"/>.
        /// </summary>
        static bool ProbeCandidate(string executable, string arguments)
        {
            var probe = CandidateProbeOverride;
            if (probe != null) return probe(executable, arguments);
            return RunsSuccessfully(executable, arguments);
        }

        /// <summary>
        /// The per-candidate version probe: runs <paramref name="executable"/>
        /// with <paramref name="arguments"/>, drains its output asynchronously
        /// and returns true only on exit code 0 within the probe timeout. A
        /// candidate that does not answer in time is killed so no orphaned
        /// child is left behind (issue #642). Public so the EditMode tests can
        /// verify the timeout-kill contract with a real process.
        /// </summary>
        public static bool RunsSuccessfully(string executable, string arguments)
        {
            int timeoutMs = ProbeTimeoutMsOverride > 0 ? ProbeTimeoutMsOverride : ProbeTimeoutMs;
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = executable,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };

                using (var proc = Process.Start(psi))
                {
                    if (proc == null) return false;
                    // Drain asynchronously: a synchronous ReadToEnd here would
                    // block until the child exits, so the timeout below could
                    // never fire for a hung candidate (and a child filling the
                    // pipe buffer would deadlock before it ever exits).
                    var stdoutTask = proc.StandardOutput.ReadToEndAsync();
                    var stderrTask = proc.StandardError.ReadToEndAsync();
                    if (!proc.WaitForExit(timeoutMs))
                    {
                        // Issue #642: a candidate that does not answer in time
                        // used to be abandoned still running (the mise shim
                        // hang was the motivating case). Kill it so the probe
                        // leaves no orphaned child behind.
                        try { proc.Kill(); } catch { }
                        Debug.LogWarning($"[YouTubeStreamResolver] probe timed out after {timeoutMs} ms, killed: {executable}");
                        return false;
                    }
                    // The probe only needs the exit code; bound the drain so a
                    // grandchild holding the pipes open cannot hang us either.
                    try { Task.WhenAll(stdoutTask, stderrTask).Wait(3000); }
                    catch { }
                    return proc.ExitCode == 0;
                }
            }
            catch
            {
                return false;
            }
        }

        static string FindInVersionedDir(string toolDir, string installs, string tool, params string[] tail)
        {
            // tail の最終要素は ".exe" 付きで渡される。Windows 以外では
            // 拡張子なしのバイナリ名に読み替える (mise は拡張子なしで配置する)。
            if (!IsWindows && tail.Length > 0)
            {
                tail = (string[])tail.Clone();
                tail[tail.Length - 1] = Path.GetFileNameWithoutExtension(tail[tail.Length - 1]);
            }

            string root;
            try
            {
                root = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    toolDir, installs, tool);
                if (!Directory.Exists(root)) return null;
            }
            catch { return null; }

            string best = null;
            DateTime bestTime = DateTime.MinValue;
            try
            {
                foreach (var dir in Directory.GetDirectories(root))
                {
                    var candidate = Path.Combine(new[] { dir }.Concat(tail).ToArray());
                    if (!File.Exists(candidate)) continue;
                    var written = File.GetLastWriteTimeUtc(candidate);
                    if (written > bestTime)
                    {
                        bestTime = written;
                        best = candidate;
                    }
                }
            }
            catch { }
            return best;
        }

        /// <summary>All candidates of the executable on the PATH, in PATH order.
        /// exeName は ".exe" 付きで渡される。Windows 以外では同名の拡張子なしバイナリ
        /// (homebrew / mise / pipx が置くもの) も候補にする。</summary>
        static List<string> WhereOnPathAll(string exeName)
        {
            var hits = new List<string>();
            var path = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(path)) return hits;
            foreach (var dir in path.Split(Path.PathSeparator))
            {
                if (string.IsNullOrEmpty(dir)) continue;
                try
                {
                    var candidate = Path.Combine(dir, exeName);
                    if (File.Exists(candidate))
                    {
                        hits.Add(candidate);
                        continue;
                    }
                    if (!IsWindows)
                    {
                        var unix = Path.Combine(dir, Path.GetFileNameWithoutExtension(exeName));
                        if (File.Exists(unix)) hits.Add(unix);
                    }
                }
                catch { }
            }
            return hits;
        }

        // ---------------------------------------------------------------
        // process helpers
        // ---------------------------------------------------------------

        public static string Quote(string argument)
        {
            if (argument == null) return "\"\"";
            if (argument.Length > 0 && argument.IndexOfAny(new[] { ' ', '\t', '"' }) < 0) return argument;
            return "\"" + argument.Replace("\"", "\\\"") + "\"";
        }

        /// <summary>
        /// Runs yt-dlp with the given arguments and captures stdout/stderr.
        /// Never throws; the exit code is reported in the result.
        /// </summary>
        public static async Task<YtDlpOutput> RunAsync(string arguments, int timeoutMs = 120000, CancellationToken cancellationToken = default)
        {
            // Issue #642: tool discovery runs on the thread pool; awaiting it
            // here keeps the probe off the Unity main thread.
            var exePath = await ResolveYtDlpPathAsync().ConfigureAwait(false);
            if (exePath == null)
            {
                return new YtDlpOutput
                {
                    ExitCode = -1,
                    Stderr = "yt-dlp.exe not found (set FLASHBEAT_YTDLP_PATH or install yt-dlp)"
                };
            }

            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

            var deno = await ResolveDenoPathAsync().ConfigureAwait(false);
            if (!string.IsNullOrEmpty(deno))
            {
                // yt-dlp needs a JS runtime for YouTube's challenge solver.
                psi.EnvironmentVariables["PATH"] = Path.GetDirectoryName(deno) + Path.PathSeparator + psi.EnvironmentVariables["PATH"];
            }

            var result = new YtDlpOutput { ExitCode = -1 };
            using (var proc = new Process { StartInfo = psi })
            {
                try
                {
                    proc.Start();
                }
                catch (Exception e)
                {
                    result.Stderr = "failed to start yt-dlp: " + e.Message;
                    return result;
                }

                var stdoutTask = proc.StandardOutput.ReadToEndAsync();
                var stderrTask = proc.StandardError.ReadToEndAsync();

                // Issue #430: WaitForExit は呼び出しスレッドをプロセス終了まで
                // 止める。RunAsync は Unity メインスレッド (MusicManager の
                // コルーチン) から呼ばれるため、yt-dlp -F (数秒) やダウンロード
                // (最大 180 秒) の全時間がそのままゲームのフリーズになっていた。
                // 待機だけをスレッドプールへ逃がして即座に制御を返す。
                // (Process のハンドル操作のみ。Unity API は触らない)
                // Issue #613: WaitForExit の失敗を「タイムアウト」と誤分類しない。
                // 例外を保持し、プロセスが既に終了していれば結果は読めるが、
                // 生きていれば kill して実際の原因を Stderr に残す。
                Exception waitError = null;
                await Task.Run(() =>
                {
                    using (cancellationToken.Register(() => { try { if (!proc.HasExited) proc.Kill(); } catch { } }))
                    {
                        try { proc.WaitForExit(timeoutMs); }
                        catch (Exception e) { waitError = e; }
                    }
                }).ConfigureAwait(false);

                if (waitError != null && !proc.HasExited)
                {
                    try { proc.Kill(); } catch { }
                    result.ExitCode = -3;
                    result.Stderr = $"yt-dlp wait failed after {timeoutMs} ms: {waitError.Message}";
                    return result;
                }

                if (!proc.HasExited)
                {
                    try { proc.Kill(); } catch { }
                    result.ExitCode = -2;
                    result.Stderr = $"yt-dlp timed out after {timeoutMs} ms";
                    return result;
                }

                result.ExitCode = proc.ExitCode;
                result.Stdout = await DrainAsync(stdoutTask).ConfigureAwait(false);
                result.Stderr = await DrainAsync(stderrTask).ConfigureAwait(false);
            }

            return result;
        }

        static async Task<string> DrainAsync(Task<string> reader)
        {
            var finished = await Task.WhenAny(reader, Task.Delay(3000)).ConfigureAwait(false);
            if (finished == reader)
            {
                try { return reader.Result; }
                catch (Exception e)
                {
                    // stderr/stdout の読み取り失敗で診断情報を失わないよう記録する
                    // (issue #613)。戻り値の "" は「出力なし」の意味を保つ。
                    Debug.LogWarning($"[YouTubeStreamResolver] output read failed: {e.Message}");
                    return "";
                }
            }
            return "";
        }

        static async Task<string> CommonArgumentsAsync()
        {
            var args = new StringBuilder();
            args.Append("--no-warnings --no-playlist --no-progress --newline --ignore-config ");
            var deno = await ResolveDenoPathAsync().ConfigureAwait(false);
            if (!string.IsNullOrEmpty(deno))
            {
                args.Append("--js-runtimes ");
                args.Append(Quote("deno:" + deno));
                args.Append(' ');
            }
            return args.ToString();
        }

        // ---------------------------------------------------------------
        // yt-dlp operations
        // ---------------------------------------------------------------

        /// <summary>Lists the formats YouTube exposes for the video (evidence + format selection).</summary>
        public static async Task<YtDlpOutput> ListFormatsAsync(string videoId, CancellationToken cancellationToken = default)
        {
            var args = await CommonArgumentsAsync().ConfigureAwait(false) + "-F " + Quote(string.Format(WatchUrlFormat, videoId));
            return await RunAsync(args, 60000, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Resolves the direct googlevideo.com URL for a format selector.</summary>
        public static async Task<YtDlpOutput> GetStreamUrlAsync(string videoId, string formatSelector, CancellationToken cancellationToken = default)
        {
            var args = await CommonArgumentsAsync().ConfigureAwait(false) + "-f " + Quote(formatSelector) + " -g " + Quote(string.Format(WatchUrlFormat, videoId));
            return await RunAsync(args, 60000, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Downloads one format to <paramref name="outputTemplate"/>. yt-dlp appends the
        /// container extension, so the caller looks the produced file up afterwards.
        ///
        /// A selector with "+" (video itag + audio itag) makes yt-dlp mux both
        /// streams into one mp4; that needs ffmpeg, so it is passed explicitly.
        /// </summary>
        public static async Task<YtDlpOutput> DownloadAsync(string videoId, string formatSelector, string outputTemplate, CancellationToken cancellationToken = default)
        {
            var args = await CommonArgumentsAsync().ConfigureAwait(false) + "--no-part -f " + Quote(formatSelector);
            if (formatSelector != null && formatSelector.Contains("+"))
            {
                args += " --merge-output-format mp4";
                var ffmpeg = await ResolveFfmpegPathAsync().ConfigureAwait(false);
                if (!string.IsNullOrEmpty(ffmpeg))
                {
                    args += " --ffmpeg-location " + Quote(ffmpeg);
                }
            }
            args += " -o " + Quote(outputTemplate) + " " + Quote(string.Format(WatchUrlFormat, videoId));
            return await RunAsync(args, 180000, cancellationToken).ConfigureAwait(false);
        }

        // ---------------------------------------------------------------
        // format parsing
        // ---------------------------------------------------------------

        /// <summary>
        /// The resolution column is either "640x360" or the two word placeholder
        /// "audio only".
        /// </summary>
        static string ReadResolution(string[] head)
        {
            if (head.Length <= 2) return "";
            if (head[2].IndexOf('x') >= 0) return head[2];
            if (head.Length > 3 && head[3] == "only") return head[2] + " " + head[3];
            return head[2];
        }

        /// <summary>
        /// Reads the codec column. yt-dlp writes the placeholders "audio only" and
        /// "video only" as two words, so the token layout depends on the row type.
        /// </summary>
        static void ReadCodecs(YtDlpFormat format, string[] tail)
        {
            if (tail.Length == 0) return;

            if (tail[0] == "audio" && tail.Length > 1 && tail[1] == "only")
            {
                format.Vcodec = "audio only";
                format.Acodec = tail.Length > 2 ? tail[2] : "";
                return;
            }

            format.Vcodec = tail[0];
            if (tail.Length > 2)
            {
                if (tail[2] == "video" && tail.Length > 3 && tail[3] == "only")
                {
                    format.Acodec = "video only";
                }
                else
                {
                    format.Acodec = tail[2];
                }
            }
        }

        /// <summary>
        /// Parses the table printed by `yt-dlp -F`. Unknown lines are ignored, so a
        /// yt-dlp version that changes the layout degrades to "no formats" instead
        /// of throwing.
        /// </summary>
        public static List<YtDlpFormat> ParseFormats(string listOutput)
        {
            var formats = new List<YtDlpFormat>();
            if (string.IsNullOrEmpty(listOutput)) return formats;

            foreach (var rawLine in listOutput.Split('\n'))
            {
                var line = rawLine.Trim('\r', ' ', '\t');
                if (line.Length == 0) continue;
                if (line.StartsWith("ID ", StringComparison.Ordinal)) continue;   // header
                if (line.StartsWith("-", StringComparison.Ordinal)) continue;      // separator

                var columns = line.Split('|');
                if (columns.Length < 3) continue;

                var head = columns[0].Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (head.Length < 2) continue;

                var format = new YtDlpFormat
                {
                    Id = head[0],
                    Ext = head[1],
                    Resolution = ReadResolution(head),
                };

                var tail = columns[2].Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                ReadCodecs(format, tail);

                formats.Add(format);
            }

            return formats;
        }
    }
}
