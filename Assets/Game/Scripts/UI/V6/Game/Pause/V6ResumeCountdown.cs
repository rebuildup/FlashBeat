using UnityEngine;
using FlashBeat.Gameplay;
using FlashBeat.UI.V5;
using FlashBeat.UI.V6.Game.Hud;

namespace FlashBeat.UI.V6.Game.Pause
{
    /// <summary>
    /// 再開カウントダウンの純ロジック (Issue #382)。0〜1 秒が 3、1〜2 秒が 2、
    /// 2〜3 秒が 1。3 秒経過で演奏を再開する。
    /// </summary>
    public static class V6ResumeCountdownLogic
    {
        public const float DurationSeconds = 3f;
        public const int StartNumber = 3;

        /// <summary>経過秒に対応する表示数字。範囲外は 3 で始まり 1 で止まる。</summary>
        public static int NumberAt(float elapsed)
        {
            if (elapsed < 0f)
            {
                return StartNumber;
            }

            int remaining = StartNumber - Mathf.FloorToInt(elapsed);
            return Mathf.Clamp(remaining, 1, StartNumber);
        }

        /// <summary>カウントダウンが終わり、演奏を再開してよいか。</summary>
        public static bool IsFinished(float elapsed)
        {
            return elapsed >= DurationSeconds;
        }

        /// <summary>
        /// 1 フレームで進める上限 (秒)。シェーダ初回コンパイル等の長い
        /// フレーム飛びで数字 (3 → 2 → 1) を飛ばさないようにする (Issue #382)。
        /// 各数字は 1 秒表示なので、その十分の一に抑える。
        /// </summary>
        public const float MaxFrameAdvanceSeconds = 0.1f;

        /// <summary>経過秒へ 1 フレーム分の delta を足す。上限超過と負値は丸める。</summary>
        public static float AdvanceElapsed(float elapsed, float deltaSeconds)
        {
            float step = Mathf.Min(Mathf.Max(deltaSeconds, 0f), MaxFrameAdvanceSeconds);
            return elapsed + step;
        }
    }

    /// <summary>
    /// ポーズから再開するとき、歌詞スロットに 3 → 2 → 1 を出してから演奏を再開する
    /// (Issue #382)。カウントダウン中は <see cref="PauseGate"/> を閉じないため、
    /// メディア時計・判定・譜面進行は止まったまま。3 秒経過時に PauseGate.Exit が
    /// 判定基準時刻を再同期する。Esc はカウントダウンを取り消してポーズへ戻る。
    /// </summary>
    public sealed class V6ResumeCountdown : MonoBehaviour
    {
        /// <summary>カウントダウン中か。統合側はこの間、ポーズの開閉入力を渡さない。</summary>
        public static bool IsRunning { get; private set; }

        private V6PauseView pause;
        private V6GameHudView hud;
        private float elapsed;
        private int shownNumber;
        private bool firstFrameDone;

        /// <summary>ポーズ画面を閉じた直後に呼ぶ。数字の表示と再開をこの 1 個が担う。</summary>
        public static void Begin(V6PauseView pauseView)
        {
            Abort();
            var go = new GameObject("V6ResumeCountdown");
            var countdown = go.AddComponent<V6ResumeCountdown>();
            countdown.pause = pauseView;
            countdown.hud = FindObjectOfType<V6GameHudView>();
            countdown.elapsed = 0f;
            countdown.shownNumber = 0;
            IsRunning = true;
            countdown.Show(V6ResumeCountdownLogic.StartNumber);
        }

        /// <summary>進行中のカウントダウンを破棄する (演奏は再開しない)。</summary>
        public static void Abort()
        {
            if (!IsRunning)
            {
                return;
            }

            IsRunning = false;
            var existing = FindObjectOfType<V6ResumeCountdown>();
            if (existing != null)
            {
                if (existing.hud != null)
                {
                    existing.hud.HideResumeCountdown();
                }

                V5GameUi.DestroyNow(existing.gameObject);
            }
        }

        private void Update()
        {
            // カウントダウンを作ったフレームでは Back を見ない。ポーズ画面を
            // Esc で閉じた場合、そのキーの Down がこのフレームに残っていて、
            // 表示された瞬間にポーズへ戻ってしまう。
            bool ignoreBackThisFrame = !firstFrameDone;
            firstFrameDone = true;

            elapsed = V6ResumeCountdownLogic.AdvanceElapsed(elapsed, Time.unscaledDeltaTime);
            Show(V6ResumeCountdownLogic.NumberAt(elapsed));

            if (!ignoreBackThisFrame && KeyActions.IsDown(KeyActions.KeyAction.Back))
            {
                CancelBackToPause();
                return;
            }

            if (V6ResumeCountdownLogic.IsFinished(elapsed))
            {
                Finish();
            }
        }

        /// <summary>
        /// シーン遷移などで外から破棄されたときも状態を残さない。残すと
        /// 次のシーンで「カウントダウン中」のままポーズ入力が抑止される。
        /// </summary>
        private void OnDestroy()
        {
            if (hud != null)
            {
                hud.HideResumeCountdown();
            }

            IsRunning = false;
        }

        private void Show(int number)
        {
            if (number == shownNumber)
            {
                return;
            }

            shownNumber = number;
            if (hud != null)
            {
                hud.ShowResumeCountdown(number);
            }
        }

        /// <summary>Esc: 数字を消し、ポーズ画面へ戻す。譜面時刻は固定したまま。</summary>
        private void CancelBackToPause()
        {
            IsRunning = false;
            if (hud != null)
            {
                hud.HideResumeCountdown();
            }

            V5GameUi.DestroyNow(gameObject);
            if (pause != null)
            {
                pause.Open();
            }
        }

        /// <summary>3 秒経過: 表示を消し、判定基準時刻を再同期して演奏を再開する。</summary>
        private void Finish()
        {
            IsRunning = false;
            if (hud != null)
            {
                hud.HideResumeCountdown();
            }

            PauseGate.Exit();
            V5GameUi.DestroyNow(gameObject);
        }
    }
}
