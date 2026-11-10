using UnityEngine;

namespace FlashBeat.Gameplay
{
    /// <summary>
    /// Windows の Media Foundation で動画を開けなかったときの復旧判定 (Issue #13)。
    ///
    /// **この分岐は Windows 実機で未検証** (0x80070005 = E_ACCESSDENIED の原因は
    /// 期限切れ URL / URL 長 / Range 非対応のいずれかで、実機ログだけでは決まらない)。
    /// macOS の経路は変えない: <see cref="ShouldRetryVideoWithFreshUrl"/> は
    /// isWindows が false なら常に false を返す。
    ///
    /// 判定だけを持つ純粋ロジック (MonoBehaviour にしない)。実機が無くても
    /// EditMode で固定できるようにするため、プラットフォームは引数で受け取る。
    /// </summary>
    public static class WindowsMediaRecovery
    {
        /// <summary>URL を解決し直して再試行する回数の上限。</summary>
        public const int MaxFreshUrlAttempts = 1;

        /// <summary>
        /// 解決済み URL が失効している可能性があるとき、URL を 1 度だけ解決し直して
        /// 再 prepare するか。Windows 以外では常に false (macOS の挙動を変えない)。
        /// </summary>
        public static bool ShouldRetryVideoWithFreshUrl(bool isWindows, int attemptsSoFar)
        {
            if (!isWindows) { return false; }
            return attemptsSoFar < MaxFreshUrlAttempts;
        }

        /// <summary>
        /// 映像が開けなかったときにプレイヤーへ出す一言。
        /// 出す文言が無い場合 (macOS で映像も音声も無い等) は空文字を返す。
        /// </summary>
        public static string VideoFailureHint(bool isWindows, bool hasBundledAudio)
        {
            if (!hasBundledAudio)
            {
                return "音声も準備できませんでした。";
            }

            if (isWindows)
            {
                return "動画を開けませんでした (Media Foundation)。音声で続行します。";
            }

            return "";
        }

        /// <summary>
        /// 実行中のプラットフォームが Windows のプレイヤーか。
        /// (Editor での動作確認では false を返す = macOS と同じ経路)
        /// </summary>
        public static bool CurrentPlatformIsWindows()
        {
            return Application.platform == RuntimePlatform.WindowsPlayer;
        }
    }
}
