using System.Globalization;
using UnityEngine;

namespace FlashBeat.UI.V6.Result
{
    /// <summary>
    /// v6 リザルト画面 (Issue #217) の純ロジック。Unity API への依存は Mathf のみ
    /// (EditMode テストから直接検証できる)。グレード算出は既存の Assessment 判定
    /// (<see cref="FlashBeat.UI.V5.ResultView.RankFor"/>) と同一の閾値を使い、
    /// v5 リザルトの表示と 1:1 に保つ。寸法は design v6 の 1728x972 基準。
    /// </summary>
    public static class V6ResultLogic
    {
        /// <summary>design v6 共通フレームの基準サイズ。</summary>
        public const float DesignW = 1728f;
        public const float DesignH = 972f;

        /// <summary>スコア満点 (V5 SelectViewLogic.ScoreMax と同じ基準)。</summary>
        public const int ScoreMax = FlashBeat.UI.V5.SelectViewLogic.ScoreMax;

        /// <summary>スコアゲージの全長 (design px, zcSHh の Score Gauge)。</summary>
        public const float GaugeW = 824f;

        /// <summary>
        /// design px → 実ピクセルの倍率。実画面 3 解像度
        /// (1920x1080 / 2560x1440 / 2560x1664) でフレームが収まるよう
        /// min(W/1728, H/972) 倍する (V6SelectViewLogic.DesignScale と同じ方式)。
        /// </summary>
        public static float DesignScale(int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                return 1f;
            }

            return Mathf.Min((float)width / DesignW, (float)height / DesignH);
        }

        /// <summary>
        /// スコアからグレード文字列を返す。既存の Assessment 判定
        /// (V5 ResultView.RankFor) と同じ閾値 (1:1 維持)。スコアが無い (0 以下) ときは "–"。
        /// </summary>
        public static string GradeOf(int score)
        {
            if (score <= 0)
            {
                return "–";
            }

            return FlashBeat.UI.V5.ResultView.RankFor(score);
        }

        /// <summary>スコア表示。3桁カンマ区切り (デザイン表記 988,513)。</summary>
        public static string FormatScore(int value)
        {
            return value.ToString("N0", CultureInfo.InvariantCulture);
        }

        /// <summary>ハイスコア差分。更新時は "+66,633"、それ以外は空文字 (V5 と同じ)。</summary>
        public static string DeltaLabel(int prevBest, int score)
        {
            if (score > prevBest)
            {
                return "+" + FormatScore(score - prevBest);
            }

            return string.Empty;
        }

        /// <summary>
        /// スコアゲージの Fill 幅 (design px)。スコアは満点 ScoreMax に対する比率で
        /// 全長 GaugeW に写像される (design 実測: 988,513 → 815px)。
        /// </summary>
        public static float GaugeFillWidth(int score)
        {
            return GaugeRatio(score) * GaugeW;
        }

        /// <summary>
        /// スコアゲージの Best マーカー位置 (design px, 左端基準)。
        /// design 実測: 前回最高 921,880 → x=760。
        /// </summary>
        public static float GaugeBestX(int bestScore)
        {
            return GaugeRatio(bestScore) * GaugeW;
        }

        /// <summary>曲長表示 (秒 → "m:ss")。無効値は "–"。</summary>
        public static string DurationText(float seconds)
        {
            string text = FlashBeat.UI.V5.SelectViewLogic.DurationText(seconds);
            return string.IsNullOrEmpty(text) ? "–" : text;
        }

        private static float GaugeRatio(int score)
        {
            if (score <= 0)
            {
                return 0f;
            }

            return Mathf.Clamp01((float)score / ScoreMax);
        }
    }
}
