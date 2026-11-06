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
    // Install yt-dlp: https://github.com/yt-dlp/yt-dlp/releases (Windows: "yt-dlp.exe")
    //   The resolver auto-detects yt-dlp.exe from PATH, the mise install tree,
    //   common Python install locations and a project-bundled
    //   Assets/StreamingAssets/yt-dlp/yt-dlp.exe.
    //   Override the path via the FLASHBEAT_YTDLP_PATH environment variable.
    //
    // Deno is used as the JavaScript runtime for yt-dlp (YouTube's challenge
    // solver needs a JS engine). When a deno.exe is found it is passed
    // explicitly as `--js-runtimes deno:<path>`; override with FLASHBEAT_DENO_PATH.
    public static class YouTubeStreamResolver
    {
        public const string WatchUrlFormat = "https://www.youtube.com/watch?v={0}";

        // ---------------------------------------------------------------
        // yt-dlp / deno discovery
        // ---------------------------------------------------------------

        static string cachedYtDlpPath;
        static bool ytDlpPathResolved;
        static string cachedDenoPath;
        static bool denoPathResolved;

        public static string ResolveYtDlpPath()
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
            candidates.Add(Path.Combine(Application.streamingAssetsPath, "yt-dlp", "yt-dlp.exe"));
            candidates.Add(Path.Combine(Application.dataPath, "StreamingAssets", "yt-dlp", "yt-dlp.exe"));

            // 5. Linux/macOS fallbacks
            string[] unixCandidates = { "/usr/local/bin/yt-dlp", "/usr/bin/yt-dlp", "/opt/homebrew/bin/yt-dlp" };
            foreach (var c in unixCandidates)
            {
                candidates.Add(c);
            }

            cachedYtDlpPath = FirstUsable(candidates, "--version");
            return cachedYtDlpPath;
        }

        public static string ResolveDenoPath()
        {
            if (denoPathResolved) return cachedDenoPath;
            denoPathResolved = true;

            var pref = Environment.GetEnvironmentVariable("FLASHBEAT_DENO_PATH");
            if (!string.IsNullOrEmpty(pref) && File.Exists(pref))
            {
                cachedDenoPath = pref;
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
            }

            return cachedDenoPath;
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
                if (RunsSuccessfully(candidate, versionArgs)) return candidate;
            }
            return null;
        }

        /// <summary>
        /// The mise shims are launchers that abort with "No version is set for shim"
        /// when the tool has no pinned version - they must not be used.
        /// </summary>
        static bool IsShim(string path)
        {
            var normalized = path.Replace('/', '\\').ToLowerInvariant();
            return normalized.Contains("\\mise\\shims\\") || normalized.Contains("\\.mise\\shims\\");
        }

        static bool RunsSuccessfully(string executable, string arguments)
        {
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
                    proc.StandardOutput.ReadToEnd();
                    proc.StandardError.ReadToEnd();
                    if (!proc.WaitForExit(20000)) return false;
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

        /// <summary>All candidates of the executable on the PATH, in PATH order.</summary>
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
                    if (File.Exists(candidate)) hits.Add(candidate);
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
            var exePath = ResolveYtDlpPath();
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

            var deno = ResolveDenoPath();
            if (!string.IsNullOrEmpty(deno))
            {
                // yt-dlp needs a JS runtime for YouTube's challenge solver.
                psi.EnvironmentVariables["PATH"] = Path.GetDirectoryName(deno) + ";" + psi.EnvironmentVariables["PATH"];
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

                using (cancellationToken.Register(() => { try { if (!proc.HasExited) proc.Kill(); } catch { } }))
                {
                    try { proc.WaitForExit(timeoutMs); }
                    catch { }
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
                try { return reader.Result; } catch { return ""; }
            }
            return "";
        }

        static string CommonArguments()
        {
            var args = new StringBuilder();
            args.Append("--no-warnings --no-playlist --no-progress --newline --ignore-config ");
            var deno = ResolveDenoPath();
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
        public static Task<YtDlpOutput> ListFormatsAsync(string videoId, CancellationToken cancellationToken = default)
        {
            var args = CommonArguments() + "-F " + Quote(string.Format(WatchUrlFormat, videoId));
            return RunAsync(args, 60000, cancellationToken);
        }

        /// <summary>Resolves the direct googlevideo.com URL for a format selector.</summary>
        public static Task<YtDlpOutput> GetStreamUrlAsync(string videoId, string formatSelector, CancellationToken cancellationToken = default)
        {
            var args = CommonArguments() + "-f " + Quote(formatSelector) + " -g " + Quote(string.Format(WatchUrlFormat, videoId));
            return RunAsync(args, 60000, cancellationToken);
        }

        /// <summary>
        /// Downloads one format to <paramref name="outputTemplate"/>. yt-dlp appends the
        /// container extension, so the caller looks the produced file up afterwards.
        /// </summary>
        public static Task<YtDlpOutput> DownloadAsync(string videoId, string formatSelector, string outputTemplate, CancellationToken cancellationToken = default)
        {
            var args = CommonArguments() + "--no-part -f " + Quote(formatSelector) +
                       " -o " + Quote(outputTemplate) + " " + Quote(string.Format(WatchUrlFormat, videoId));
            return RunAsync(args, 180000, cancellationToken);
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
