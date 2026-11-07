using System.Globalization;

namespace FlashBeat.UI.V5
{
    /// <summary>
    /// Game HUD / Pause の純ロジック。Unity API に依存しないので
    /// EditMode テストから直接呼べる (V5GameHudTests 参照)。
    /// 値は design/flashbeat-ui.pen v5 + GManager の実装から。
    /// </summary>
    public static class GameHudLogic
    {
        /// <summary>スコア表示の桁区切り ("N0")。デザインは 1,012,340。</summary>
        public static string FormatScore(int score)
        {
            return score.ToString("N0", CultureInfo.InvariantCulture);
        }

        /// <summary>コンボボーナス表示。デザインは +12,300 / +21,500。
        /// 正確な式は .pen から復元できないため combo*50 を近似に使う (報告済み)。</summary>
        public static long BonusTotal(int combo)
        {
            return combo * 50L;
        }

        /// <summary>次のボーナス里程碑。300 コンボごと。</summary>
        public static int NextBonusMilestone(int combo)
        {
            return (combo / 300 + 1) * 300;
        }

        /// <summary>次のボーナスの報酬 (デザイン: 300→+3,000)。</summary>
        public static int NextBonusReward(int milestone)
        {
            return milestone * 10;
        }

        /// <summary>ランク文字。SS>=1,000,000 / S>=950,000 / A>=900,000 / B>=700,000 / C。</summary>
        public static string RankText(int score)
        {
            if (score >= 1_000_000) return "SS";
            if (score >= 950_000) return "S";
            if (score >= 900_000) return "A";
            if (score >= 700_000) return "B";
            return "C";
        }

        /// <summary>スコアレールの塗り幅 (px)。レール幅 600、塗りは 530 まで。</summary>
        public static float RailFillWidth(int score, float railWidth)
        {
            return Clamp01(score / 1_000_000f) * (railWidth - 70f);
        }

        /// <summary>スコアレールの + ゾーンの塗り幅 (px)。1,000,000〜1,100,000 の帯。</summary>
        public static float RailPlusWidth(int score, float railWidth)
        {
            if (score <= 1_000_000) return 0f;
            return Clamp01((score - 1_000_000) / 100_000f) * 70f;
        }

        /// <summary>スコアレールの Best マーカーの x (px)。</summary>
        public static float RailBestX(int hiscore, float railWidth)
        {
            return Clamp01(hiscore / 1_000_000f) * (railWidth - 70f);
        }

        private static float Clamp01(float v)
        {
            if (v < 0f) return 0f;
            if (v > 1f) return 1f;
            return v;
        }
    }
}
