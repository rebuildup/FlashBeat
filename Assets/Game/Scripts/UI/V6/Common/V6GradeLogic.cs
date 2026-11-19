using FlashBeat.Gameplay;

namespace FlashBeat.UI.V6.Common
{
    /// <summary>
    /// v6 画面共通のスコア満点とグレード帯 (Issue #758 / ADR-0034)。
    ///
    /// scoringVersion ごとの満点:
    /// - v1–v3: 1,000,000 (プレイ全体を 1 つの正規化スコアに畳み込む。ADR-0028/0029)
    /// - v4:   2,000,000 (判定由来の基礎スコア ≤1M + コンボ等のボーナス ≤1M。
    ///   ADR-0033)
    ///
    /// v4 のグレード帯は v1–v3 の帯をスコア空間の倍化 (1M → 2M) に合わせて
    /// 2 倍したもの: SS = 満点のみ / S ≥ 95% / A ≥ 90% / B ≥ 80% / 残り C。
    /// 「ノーミスならだれでも SS」にはならず、SS は全ノーツ Perfect かつ
    /// フルコンボ終了 (ちょうど 2,000,000) のみ — v3 の「ちょうど
    /// 1,000,000 = SS」と同じ厳しさ。
    ///
    /// B の下限 1,600,000 は miss を含むプレイの理論上限 (約 1.53M) を
    /// 上回るため、「B 以上は実質ノーミスクリア」という v1–v3 の実効条件
    /// (miss ありの最高スコアは 700,000 未満で実効 C だった) を維持する。
    /// </summary>
    public static class V6GradeLogic
    {
        /// <summary>scoring v1–v3 のスコア満点。</summary>
        public const int ScoreMaxLegacy = 1000000;

        /// <summary>scoring v4 のスコア満点 (<see cref="Scoring.ScoreMaxV4"/> と同じ 2,000,000)。</summary>
        public const int ScoreMaxV4 = Scoring.ScoreMaxV4;

        /// <summary>v4 グレード S の下限 (満点の 95%)。</summary>
        public const int GradeV4S = 1900000;

        /// <summary>v4 グレード A の下限 (満点の 90%)。</summary>
        public const int GradeV4A = 1800000;

        /// <summary>
        /// v4 グレード B の下限 (満点の 80%)。
        /// miss を含むプレイはボーナス側 (コンボ継続・ノーミス・終了加点) が
        /// 丸ごと失われるため、基礎スコアが満点に近くても合計は約 1.53M で
        /// 頭打ちになる。この境界の上に来るのは実質ノーミスのクリアだけ。
        /// </summary>
        public const int GradeV4B = 1600000;

        /// <summary>
        /// その scoringVersion で到達し得るスコア満点。スコアゲージ等の
        /// 正規化に使う。v4 未満は <see cref="ScoreMaxLegacy"/>、
        /// v4 以降は <see cref="ScoreMaxV4"/>。
        /// </summary>
        public static int ScoreMaxOf(int scoringVersion)
        {
            return scoringVersion >= Scoring.Version4 ? ScoreMaxV4 : ScoreMaxLegacy;
        }

        /// <summary>
        /// scoringVersion に応じたグレード (SS/S/A/B/C)。
        /// Result 画面・ゲーム HUD・登録モーダルなど RankFor / RankText 系の
        /// 帯を使う画面向け: v4 以降は <see cref="GradeOfV4"/>、v1–v3 は
        /// V5 ResultView.RankFor の閾値をそのまま使う
        /// (Select 画面系は B 境界が異なる従来帯を持つため、
        /// <see cref="FlashBeat.UI.V6.Select.Data.V6SelectDataLogic.GradeOf"/>
        /// 側で別途ディスパッチする)。
        /// </summary>
        public static string GradeOf(int score, int scoringVersion)
        {
            if (scoringVersion >= Scoring.Version4)
            {
                return GradeOfV4(score);
            }

            return FlashBeat.UI.V5.ResultView.RankFor(score);
        }

        /// <summary>
        /// scoring v4 のグレード (SS/S/A/B/C)。SS は満点 2,000,000 のみ。
        /// 0 以下も "C" (V5 ResultView.RankFor / GameHudLogic.RankText と
        /// 同じ扱い。空表示への丸めは各画面側が行う)。
        /// </summary>
        public static string GradeOfV4(int score)
        {
            if (score >= ScoreMaxV4)
            {
                return "SS";
            }

            if (score >= GradeV4S)
            {
                return "S";
            }

            if (score >= GradeV4A)
            {
                return "A";
            }

            if (score >= GradeV4B)
            {
                return "B";
            }

            return "C";
        }
    }
}
