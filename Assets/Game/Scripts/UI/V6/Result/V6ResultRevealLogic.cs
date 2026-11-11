using UnityEngine;

namespace FlashBeat.UI.V6.Result
{
    /// <summary>
    /// Result の段階表示 (Issue #384) の時間割とイージング。設計値は
    /// <c>docs/design/v6/review-2026-11-11-decisions.md</c> §7.1 の表をそのまま持つ。
    /// Unity API は Mathf だけなので EditMode テストから直接呼べる。
    ///
    /// 時刻は Result を表示した瞬間を 0 とした秒。各段は
    /// 「開始時刻 / 長さ」の組で表し、アルファは 0→1 を EaseOut で進める。
    /// </summary>
    public static class V6ResultRevealLogic
    {
        // トロフィー: 0.6→1.0 のスケール + フェードイン。
        public const float TrophyStart = 0f;
        public const float TrophyDuration = 0.18f;
        public const float TrophyScaleFrom = 0.6f;

        // グレード文字: 0.9→1.0 のスケール + フェードイン。
        public const float GradeStart = 0.12f;
        public const float GradeDuration = 0.18f;
        public const float GradeScaleFrom = 0.9f;

        // スコア。ラベルはフェードのみ、値は 0 から最終値へカウントアップ。
        public const float ScoreLabelStart = 0.30f;
        public const float ScoreLabelDuration = 0.12f;
        public const float ScoreCountStart = 0.30f;
        public const float ScoreCountDuration = 0.90f;

        // 前回最高。
        public const float BestStart = 1.25f;
        public const float BestDuration = 0.18f;

        // 判定内訳 5 列。60ms 間隔で 1 列ずつ。
        public const float JudgementStart = 1.40f;
        public const float JudgementStep = 0.06f;
        public const float JudgementDuration = 0.18f;
        public const int JudgementColumns = 5;

        // スコアゲージの塗り。
        public const float GaugeStart = 1.45f;
        public const float GaugeDuration = 0.50f;

        // 曲情報とランキング。
        public const float SongInfoStart = 1.75f;
        public const float SongInfoDuration = 0.20f;

        // 登録欄 (登録モーダル)。表示し終えた時点で名前欄へフォーカスする。
        public const float RegisterStart = 2.00f;
        public const float RegisterDuration = 0.18f;

        /// <summary>全段が表示し終わる時刻。Space スキップの判定にも使う。</summary>
        public const float TotalSeconds = RegisterStart + RegisterDuration;

        /// <summary>入力カーソルの点滅半周期 (design 530ms)。</summary>
        public const float CaretHalfCycleSeconds = 0.53f;

        /// <summary>登録成功チップの表示時間 (design 決定 §7.3)。</summary>
        public const float ChipFadeInSeconds = 0.18f;
        public const float ChipHoldSeconds = 2f;
        public const float ChipFadeOutSeconds = 0.3f;

        /// <summary>開始時刻からの進行度 (0..1、範囲外はクランプ)。</summary>
        public static float Progress(float elapsed, float start, float duration)
        {
            if (duration <= 0f)
            {
                return elapsed >= start ? 1f : 0f;
            }

            return Mathf.Clamp01((elapsed - start) / duration);
        }

        /// <summary>EaseOut (2 次)。減速して終わる。</summary>
        public static float EaseOut(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - (1f - t) * (1f - t);
        }

        /// <summary>ある段のアルファ (0..1)。</summary>
        public static float Alpha(float elapsed, float start, float duration)
        {
            return EaseOut(Progress(elapsed, start, duration));
        }

        public static float TrophyScale(float elapsed)
        {
            return Mathf.Lerp(TrophyScaleFrom, 1f, EaseOut(Progress(elapsed, TrophyStart, TrophyDuration)));
        }

        public static float GradeScale(float elapsed)
        {
            return Mathf.Lerp(GradeScaleFrom, 1f, EaseOut(Progress(elapsed, GradeStart, GradeDuration)));
        }

        /// <summary>判定列 index (0..4) のアルファ。</summary>
        public static float JudgementAlpha(float elapsed, int index)
        {
            return Alpha(elapsed, JudgementStart + JudgementStep * index, JudgementDuration);
        }

        /// <summary>ゲージの塗り率 (0..1)。最終値にこの率を掛ける。</summary>
        public static float GaugeProgress(float elapsed)
        {
            return EaseOut(Progress(elapsed, GaugeStart, GaugeDuration));
        }

        /// <summary>カウントアップ中の表示スコア。</summary>
        public static int CountedScore(int finalScore, float elapsed)
        {
            if (finalScore <= 0)
            {
                return 0;
            }

            return Mathf.RoundToInt(finalScore * EaseOut(Progress(elapsed, ScoreCountStart, ScoreCountDuration)));
        }

        /// <summary>登録欄を出し始める時刻に達したか。</summary>
        public static bool ShouldShowRegister(float elapsed)
        {
            return elapsed >= RegisterStart;
        }

        /// <summary>段階表示が終わったか。</summary>
        public static bool IsComplete(float elapsed)
        {
            return elapsed >= TotalSeconds;
        }
    }

    /// <summary>
    /// 登録送信の状態 (Idle → Submitting → Succeeded / Failed)。二重送信を
    /// 状態として 1 箇所に閉じ込める (Issue #384)。失敗だけが再試行を許す。
    /// </summary>
    public sealed class V6RegistrationGate
    {
        public bool InFlight { get; private set; }

        public bool Succeeded { get; private set; }

        public bool Failed { get; private set; }

        /// <summary>送信を開始してよいか (送信中・登録済みは不可)。</summary>
        public bool CanStart
        {
            get { return !InFlight && !Succeeded; }
        }

        /// <summary>送信を開始する。開始できなければ false。</summary>
        public bool Begin()
        {
            if (!CanStart)
            {
                return false;
            }

            InFlight = true;
            Failed = false;
            return true;
        }

        public void Succeed()
        {
            InFlight = false;
            Failed = false;
            Succeeded = true;
        }

        public void Fail()
        {
            InFlight = false;
            Failed = true;
        }
    }
}
