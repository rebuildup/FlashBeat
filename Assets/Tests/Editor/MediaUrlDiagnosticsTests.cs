using NUnit.Framework;
using FlashBeat.Gameplay;

namespace FlashBeat.Tests.Editor
{
    /// <summary>
    /// Issue #13: Windows の Media Foundation が動画 URL を開けない問題の診断。
    ///
    /// ここで固定するのは「ログに何を出すか」と「Windows だけ再試行するか」の
    /// 2 点。実機の挙動 (0x80070005 の原因) は Windows 実機が無いと決まらないので、
    /// このテストはその判断材料を壊さないためのもの。
    /// </summary>
    public class MediaUrlDiagnosticsTests
    {
        // --- Summarize: 値を出さない ---

        [Test]
        public void Summarize_NeverLeaksQueryValues()
        {
            const string url =
                "https://rr3---sn-abc123.googlevideo.com/videoplayback?expire=1799999999" +
                "&ip=203.0.113.7&sig=SECRETSIGNATURE&range=0-12345&itag=18";

            var text = MediaUrlDiagnostics.Summarize(url);

            Assert.IsFalse(text.Contains("1799999999"), "expire の値が出ている: " + text);
            Assert.IsFalse(text.Contains("203.0.113.7"), "ip の値が出ている: " + text);
            Assert.IsFalse(text.Contains("SECRETSIGNATURE"), "sig の値が出ている: " + text);
            Assert.IsFalse(text.Contains("0-12345"), "range の値が出ている: " + text);
        }

        [Test]
        public void Summarize_KeepsQueryKeyNames()
        {
            const string url = "https://example.com/v.mp4?b=2&a=1&itag=18";

            var text = MediaUrlDiagnostics.Summarize(url);

            Assert.IsTrue(text.Contains("queryKeys=[a,b,itag]"), text);
        }

        [Test]
        public void Summarize_ReportsLengthAndScheme()
        {
            var url = "https://example.com/v.mp4?x=" + new string('y', 500);

            var text = MediaUrlDiagnostics.Summarize(url);

            Assert.IsTrue(text.Contains("len=" + url.Length), text);
            Assert.IsTrue(text.Contains("scheme=https"), text);
        }

        [Test]
        public void Summarize_StaysWithinTheLineBudget()
        {
            var url = "https://example.com/v.mp4?" + string.Join("&", MakePairs(80));

            var text = MediaUrlDiagnostics.Summarize(url);

            Assert.LessOrEqual(text.Length, MediaUrlDiagnostics.MaxLength, text);
            Assert.LessOrEqual(MediaUrlDiagnostics.MaxLength, 400);
        }

        [Test]
        public void Summarize_LimitsTheNumberOfQueryKeys()
        {
            var url = "https://example.com/v.mp4?" + string.Join("&", MakePairs(100));

            var text = MediaUrlDiagnostics.Summarize(url);

            Assert.IsTrue(text.Contains("…+"), "上限を超えた分が示されていない: " + text);
        }

        [Test]
        public void Summarize_ReportsMissingExtension()
        {
            // Unity の WindowsVideoMedia は拡張子が無いと形式を仮定する。
            // その状態かどうかをログで分かるようにしておく。
            var text = MediaUrlDiagnostics.Summarize("https://example.com/videoplayback?itag=18");

            Assert.IsTrue(text.Contains("pathExtension=<none>"), text);
        }

        [Test]
        public void Summarize_HandlesNullAndEmpty()
        {
            Assert.AreEqual("url=<empty>", MediaUrlDiagnostics.Summarize(null));
            Assert.AreEqual("url=<empty>", MediaUrlDiagnostics.Summarize(""));
            Assert.AreEqual("url=<empty>", MediaUrlDiagnostics.Summarize("   "));
        }

        [Test]
        public void Summarize_HandlesLocalPathsWithoutThrowing()
        {
            var text = MediaUrlDiagnostics.Summarize("Musics/song.mp4");

            Assert.IsTrue(text.Contains("shape=<not-absolute>"), text);
            Assert.IsTrue(text.Contains("hasExtension=true"), text);
        }

        private static string[] MakePairs(int count)
        {
            var pairs = new string[count];
            for (int i = 0; i < count; i++)
            {
                pairs[i] = "k" + i.ToString("00") + "=value" + i;
            }
            return pairs;
        }

        // --- WindowsMediaRecovery ---

        [Test]
        public void ShouldRetryVideoWithFreshUrl_OnlyOnWindowsAndOnlyOnce()
        {
            Assert.IsTrue(WindowsMediaRecovery.ShouldRetryVideoWithFreshUrl(true, 0));
            Assert.IsFalse(WindowsMediaRecovery.ShouldRetryVideoWithFreshUrl(true, 1));
            Assert.IsFalse(WindowsMediaRecovery.ShouldRetryVideoWithFreshUrl(false, 0));
            Assert.IsFalse(WindowsMediaRecovery.ShouldRetryVideoWithFreshUrl(false, 1));
        }

        [Test]
        public void VideoFailureHint_Windows_WithBundledAudio_ContinuesWithSound()
        {
            var hint = WindowsMediaRecovery.VideoFailureHint(true, true);

            Assert.IsTrue(hint.Contains("Media Foundation"), hint);
            Assert.IsTrue(hint.Contains("音声で続行"), hint);
        }

        [Test]
        public void VideoFailureHint_WithoutAnyAudio_ReportsBothMissing()
        {
            var hint = WindowsMediaRecovery.VideoFailureHint(true, false);

            Assert.AreEqual("音声も準備できませんでした。", hint);
        }

        [Test]
        public void VideoFailureHint_MacOS_WithAudio_IsSilent()
        {
            // macOS は既存挙動を変えない (ヒントを出さない)。
            Assert.AreEqual("", WindowsMediaRecovery.VideoFailureHint(false, true));
        }

        [Test]
        public void MaxFreshUrlAttempts_IsOne()
        {
            Assert.AreEqual(1, WindowsMediaRecovery.MaxFreshUrlAttempts);
        }
    }
}
