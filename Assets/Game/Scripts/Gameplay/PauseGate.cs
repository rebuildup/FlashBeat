using UnityEngine;

namespace FlashBeat.Gameplay
{
    /// <summary>
    /// ポーズゲートの純粋ロジック (EditMode テスト対象)。
    /// Unity の静的状態 (GManager) や Time.time に直接触らない。
    /// </summary>
    public static class PauseGateLogic
    {
        /// <summary>ポーズ中は判定・スコア加算を行わない。</summary>
        public static bool CanJudge(bool isPaused)
        {
            return !isPaused;
        }

        /// <summary>ポーズ開始時に固定する譜面時刻 (メディア時刻)。現在値をそのまま凍結する。</summary>
        public static float FrozenMediaTime(float currentStepTime)
        {
            return currentStepTime;
        }

        /// <summary>
        /// 再開時の判定基準時刻 (<c>GManager.StartTime</c>)。
        /// ポーズ中は壁時計 (<c>Time.time</c>) だけが進み、メディアは止まっている。
        /// 戻り値を StartTime へ書くと、再開直後の <c>Time.time - StartTime</c> が
        /// 固定中の譜面時刻に一致する — ノーツ位置が飛ばない (Issue #224)。
        /// </summary>
        public static float ResumeJudgeStartTime(float wallClockTime, float frozenMediaTime)
        {
            return wallClockTime - frozenMediaTime;
        }
    }

    /// <summary>
    /// ポーズ中の共有ゲート (Issue #224)。
    ///
    /// v6 (<see cref="FlashBeat.UI.V6.Game.Pause.V6PauseView"/>) と v5
    /// (<c>FlashBeat.UI.V5.PauseMenuView</c>) の両方のポーズ画面が Open / Close で
    /// Enter / Exit を呼ぶ。開閉状態の正本は各ビューの IsOpen のまま (ここは
    /// 「メディアと判定を止めるか」の 1 ビットだけを持つ)。
    ///
    /// - Enter: 譜面時刻 (<c>GManager.step_time</c>) を固定する。MusicManager /
    ///   Judge / Notes はこの値 (または IsPaused) を見て停止する。
    /// - Exit: 判定基準時刻 (<c>GManager.StartTime</c>) を壁時計へ再合わせする。
    ///   ポーズ中に壁時計だけ進んだぶんを詰めないと、再開直後にノーツ位置が飛ぶ。
    /// - メディア (AudioSource / VideoPlayer) の Pause / 再開は MusicManager 側が
    ///   実メンバー (songVideo / audioS / streamAudioPlayer) に対して行う。
    /// </summary>
    public static class PauseGate
    {
        /// <summary>ポーズ中か。true の間はメディア時計・判定・譜面進行を止める。</summary>
        public static bool IsPaused
        {
            get;
            private set;
        }

        /// <summary>ポーズ開始時に固定した譜面時刻 (メディア時刻)。</summary>
        public static float FrozenMediaTime
        {
            get;
            private set;
        }

        /// <summary>ポーズを開始する。譜面時刻を固定する (冪等)。</summary>
        public static void Enter()
        {
            if (IsPaused)
            {
                return;
            }
            IsPaused = true;
            FrozenMediaTime = PauseGateLogic.FrozenMediaTime(GManager.step_time);
            GManager.step_time = FrozenMediaTime;
        }

        /// <summary>
        /// ポーズを終了 (再開) する。判定基準時刻を壁時計へ再合わせする (冪等)。
        /// </summary>
        public static void Exit()
        {
            if (!IsPaused)
            {
                return;
            }
            IsPaused = false;
            GManager.StartTime = PauseGateLogic.ResumeJudgeStartTime(Time.time, FrozenMediaTime);
            GManager.step_time = FrozenMediaTime;
        }

        /// <summary>
        /// シーンをまたぐハードリセット (GameScene の読み直しから呼ぶ)。
        /// ポーズ中にシーンを抜けた (リトライ / リタイア) ときにゲートを閉じる。
        /// </summary>
        public static void HardReset()
        {
            IsPaused = false;
            FrozenMediaTime = 0f;
        }
    }
}
