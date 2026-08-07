using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace FlashBeat.Gameplay
{
    // Resolves a YouTube videoId to a direct googlevideo.com stream URL by
    // spawning a yt-dlp.exe subprocess. The returned URL is valid for ~6h
    // (until the `expire=` parameter), so call just-in-time, not at Awake.
    //
    // Why yt-dlp and not the bundled InvidiousVideoPlayer:
    //   The youtube-player package routes through Invidious, whose public
    //   instance list has decayed — 0/13 hosts return valid JSON today. yt-dlp
    //   talks to YouTube directly and returns a googlevideo.com URL that any
    //   video player can stream.
    //
    // Install yt-dlp: https://github.com/yt-dlp/yt-dlp/releases (Windows: "yt-dlp.exe")
    //   The resolver auto-detects yt-dlp.exe from PATH, common Python install
    //   locations, and a project-bundled Assets/StreamingAssets/yt-dlp/yt-dlp.exe.
    //   Override the path via Edit > Project Settings > FlashBeat > YouTube.
    public static class YouTubeStreamResolver
    {
        // 18 = 360p mp4 (video+audio combined). Highest universally-available
        // format YouTube exposes for the vast majority of videos. Mirrors the
        // package's default itag=22 fallback (since 22 isn't always present).
        public const string DefaultFormat = "18";

        public static async Task<string> GetStreamUrlAsync(string videoId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(videoId))
                throw new ArgumentException("videoId is null or empty", nameof(videoId));

            var exePath = ResolveYtDlpPath();
            if (exePath == null)
            {
                throw new FileNotFoundException(
                    "yt-dlp.exe not found. Install from https://github.com/yt-dlp/yt-dlp/releases " +
                    "and ensure it's on PATH, or set the path in Edit > Project Settings > FlashBeat > YouTube.");
            }

            var youtubeUrl = $"https://www.youtube.com/watch?v={videoId}";
            var args = $"--no-warnings --no-part -f {DefaultFormat} -g \"{youtubeUrl}\"";

            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            string stdout = null;
            string stderr = null;
            int exitCode = -1;

            using (var proc = new Process { StartInfo = psi, EnableRaisingEvents = true })
            {
                var stdoutTask = new TaskCompletionSource<string>();
                var stderrTask = new TaskCompletionSource<string>();
                proc.OutputDataReceived += (s, e) => { if (e.Data != null) stdoutTask.TrySetResult(e.Data); else stdoutTask.TrySetResult(""); };
                proc.ErrorDataReceived += (s, e) => { if (e.Data != null) stderrTask.TrySetResult(e.Data); else stderrTask.TrySetResult(""); };

                proc.Start();
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();

                using (cancellationToken.Register(() => { try { if (!proc.HasExited) proc.Kill(); } catch { } }))
                {
                    await Task.Run(() => proc.WaitForExit(30000), cancellationToken);
                }

                // Drain background readers
                stdout = await Task.WhenAny(stdoutTask.Task, Task.Delay(500)) == stdoutTask.Task ? stdoutTask.Task.Result : "";
                stderr = await Task.WhenAny(stderrTask.Task, Task.Delay(500)) == stderrTask.Task ? stderrTask.Task.Result : "";

                exitCode = proc.ExitCode;
            }

            if (exitCode != 0)
            {
                throw new InvalidOperationException(
                    $"yt-dlp exited {exitCode} for videoId={videoId}. stderr: {Truncate(stderr, 300)}");
            }

            var url = (stdout ?? "").Trim();
            if (string.IsNullOrEmpty(url) || !url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"yt-dlp returned no URL for videoId={videoId}. stdout: {Truncate(stdout, 200)} stderr: {Truncate(stderr, 200)}");
            }

            return url;
        }

        public static string ResolveYtDlpPath()
        {
            // 1. User override via EditorPrefs / PlayerPrefs
            var pref = Environment.GetEnvironmentVariable("FLASHBEAT_YTDLP_PATH");
            if (!string.IsNullOrEmpty(pref) && File.Exists(pref)) return pref;

            // 2. PATH
            var pathHit = WhereOnPath("yt-dlp.exe");
            if (pathHit != null) return pathHit;

            // 3. Common Windows Python install locations
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string[] candidates = {
                Path.Combine(appData, "Python", "Python314", "Scripts", "yt-dlp.exe"),
                Path.Combine(appData, "Python", "Python313", "Scripts", "yt-dlp.exe"),
                Path.Combine(appData, "Python", "Python312", "Scripts", "yt-dlp.exe"),
                Path.Combine(appData, "Python", "Python311", "Scripts", "yt-dlp.exe"),
                Path.Combine(appData, "Python", "Python310", "Scripts", "yt-dlp.exe"),
                Path.Combine(localAppData, "Programs", "Python", "Python314", "Scripts", "yt-dlp.exe"),
                Path.Combine(localAppData, "Programs", "Python", "Python313", "Scripts", "yt-dlp.exe"),
                Path.Combine(localAppData, "Programs", "Python", "Python312", "Scripts", "yt-dlp.exe"),
                Path.Combine(localAppData, "Programs", "Python", "Python311", "Scripts", "yt-dlp.exe"),
                Path.Combine(localAppData, "Programs", "Python", "Python310", "Scripts", "yt-dlp.exe"),
                @"C:\Python314\Scripts\yt-dlp.exe",
                @"C:\Python313\Scripts\yt-dlp.exe",
                @"C:\Python312\Scripts\yt-dlp.exe",
                @"C:\Python311\Scripts\yt-dlp.exe",
                @"C:\Python310\Scripts\yt-dlp.exe",
                Path.Combine(Application.streamingAssetsPath, "yt-dlp", "yt-dlp.exe"),
                Path.Combine(Application.dataPath, "StreamingAssets", "yt-dlp", "yt-dlp.exe"),
            };
            foreach (var c in candidates)
            {
                try { if (File.Exists(c)) return c; } catch { }
            }

            // 4. Linux/macOS fallbacks
            string[] unixCandidates = { "/usr/local/bin/yt-dlp", "/usr/bin/yt-dlp", "/opt/homebrew/bin/yt-dlp" };
            foreach (var c in unixCandidates)
            {
                try { if (File.Exists(c)) return c; } catch { }
            }

            return null;
        }

        static string WhereOnPath(string exeName)
        {
            var path = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(path)) return null;
            foreach (var dir in path.Split(Path.PathSeparator))
            {
                if (string.IsNullOrEmpty(dir)) continue;
                try
                {
                    var candidate = Path.Combine(dir, exeName);
                    if (File.Exists(candidate)) return candidate;
                }
                catch { }
            }
            return null;
        }

        static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Replace("\n", " ").Replace("\r", " ");
            return s.Length <= max ? s : s.Substring(0, max) + "...";
        }
    }
}
