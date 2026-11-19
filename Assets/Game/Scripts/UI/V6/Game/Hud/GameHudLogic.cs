using System.Globalization;

namespace FlashBeat.UI.V6.Game.Hud
{
    /// <summary>
    /// ゲーム HUD / ポーズの純ロジック (Issue #740: 旧 V5 GameHudLogic から
    /// 製品で生きているメンバーだけを v6 HUD 層へ移設)。
    /// Unity API に依存しないので EditMode テストから直接呼べる。
    ///
    /// かつては v5 HUD のスコアレール幅 / コンボボーナス近似式も持っていたが、
    /// それらは v5 GameHudView 専用で、v6 HUD は <c>V6HudScore</c> /
    /// <c>V6HudLogic</c> が同じ責務を持つため撤去した。
    /// </summary>
    public static class GameHudLogic
    {
        /// <summary>スコア表示の桁区切り ("N0")。デザインは 1,012,340。</summary>
        public static string FormatScore(int score)
        {
            return score.ToString("N0", CultureInfo.InvariantCulture);
        }

        /// <summary>ランク文字。SS&gt;=1,000,000 / S&gt;=950,000 / A&gt;=900,000 / B&gt;=700,000 / C。</summary>
        public static string RankText(int score)
        {
            if (score >= 1_000_000) return "SS";
            if (score >= 950_000) return "S";
            if (score >= 900_000) return "A";
            if (score >= 700_000) return "B";
            return "C";
        }
    }
}
