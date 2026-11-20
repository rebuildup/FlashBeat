using FlashBeat.Gameplay;

namespace FlashBeat.UI.V6.Common
{
    /// <summary>
    /// v6 画面共通のスコア満点とグレード帯 (Issue #758 / ADR-0034、
    /// 帯の改訂は Issue #788)。
    ///
    /// scoringVersion ごとの満点:
    /// - v1–v3: 1,000,000 (プレイ全体を 1 つの正規化スコアに畳み込む。ADR-0028/0029)
    /// - v4:   2,000,000 (判定由来の基礎スコア ≤1M + コンボ等のボーナス ≤1M。
    ///   ADR-0033)
    ///
    /// v4 のグレード帯 (Issue #788 で改訂):
    /// SS = 満点 2,000,000 のみ / S ≥ 95% / A ≥ 70% / B ≥ 50% / 残り C。
    ///
    /// 初版 (ADR-0034、Issue #758) は v1–v3 の帯の単純な 2 倍
    /// (S ≥ 95% / A ≥ 90% / B ≥ 80%) で、「miss = 実質 C」を維持する
    /// 設計だった。しかし v4 のスコア構造では miss 1 回でノーミス加点と
    /// 終了加点が消え、コンボ継続ボーナスもそこで打ち切られるため、
    /// ボーナス側が大きく縮み、数回の miss を含む通常プレイが軒並み
    /// C に沈むことが実機受入で指摘された (Issue #788)。
    ///
    /// 改訂後の帯は成績分布シミュレーション (ノーツ数 139〜1000、
    /// Perfect/Great/Bad/Miss の構成と miss 位置を振った全プレイ) に
    /// 基づく:
    /// - miss を含むプレイの理論上限は約 1.53M (終盤 1 miss + ほぼ全
    ///   Perfect) なので、S (≥1.9M) と SS (=2,000,000) は引き続き
    ///   ノーミスの高評価プレイ専用。
    /// - A (≥1.4M) には「終盤ミス 1 回 + 高判定質」の上位ミスプレイと、
    ///   弱めのノーミス FC (全 Great FC = 1,600,000 等) の双方が届く。
    /// - B (≥1.0M) は判定質の良いプレイの ~1% 以下の miss や、
    ///   カジュアルな判定質での終盤ミス程度までをカバーし、
    ///   ~2% 以上の miss 率や判定質の低いミスプレイを C に分ける。
    ///
    /// 新しい帯は旧帯を緩める方向にのみ動く (どのスコアでもグレードが
    /// 下がることはなく、旧 C/B の一部が B/A に繰り上がるだけ) ので、
    /// 保存済み v4 記録を新しい帯で再解釈しても既存プレイヤーの
    /// グレードが悪化することはない。採点式 (scoringVersion 4) 自体は
    /// 変更していない — グレードは表示側の導出値であり、記録形式や
    /// サーバ再計算には影響しない。
    /// </summary>
    public static class V6GradeLogic
    {
        /// <summary>scoring v1–v3 のスコア満点。</summary>
        public const int ScoreMaxLegacy = 1000000;

        /// <summary>scoring v4 のスコア満点 (<see cref="Scoring.ScoreMaxV4"/> と同じ 2,000,000)。</summary>
        public const int ScoreMaxV4 = Scoring.ScoreMaxV4;

        /// <summary>v4 グレード S の下限 (満点の 95%)。</summary>
        public const int GradeV4S = 1900000;

        /// <summary>
        /// v4 グレード A の下限 (満点の 70%、Issue #788)。
        /// miss を含むプレイの理論上限 (約 1.53M) をわずかに下回る位置に
        /// 置くことで、終盤ミス 1 回 + 高判定質のような上位ミスプレイが
        /// A に届き、弱めのノーミス FC (全 Great FC = 1,600,000 等) と
        /// 同じ帯を共有する。
        /// </summary>
        public const int GradeV4A = 1400000;

        /// <summary>
        /// v4 グレード B の下限 (満点の 50%、Issue #788)。
        /// 判定質の良いプレイで ~1% 以下の miss、またはカジュアルな
        /// 判定質での終盤ミス程度までを B に含め、~2% 以上の miss 率の
        /// プレイを C に分ける。旧 B 境界 1,600,000 では miss を含む
        /// 通常プレイが全て C に沈んでいた (Issue #788)。
        /// </summary>
        public const int GradeV4B = 1000000;

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

            return FlashBeat.UI.V6.Result.V6ResultRules.RankFor(score);
        }

        /// <summary>
        /// scoring v4 のグレード (SS/S/A/B/C、Issue #788 の帯)。
        /// SS は満点 2,000,000 のみ。
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
