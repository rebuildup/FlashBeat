using System.Text;

namespace FlashBeat.UI.V6.Result
{
    /// <summary>
    /// Result 画面のスコア → グレード判定と登録名の正規化 (Issue #740:
    /// 旧 V5 ResultView の静的ヘルパーを v6 Result 層へ移設)。
    ///
    /// <see cref="V6ResultLogic.GradeOf"/> / <see cref="V6ResultLogic.NormalizeLocalName"/>
    /// と ResultSceneManager の Assessment 表示が共有する。PlayerPrefs キーや
    /// 書き込み経路は <see cref="V6ResultLogic"/> / <c>V6LocalNameStore</c> が持つ。
    /// </summary>
    public static class V6ResultRules
    {
        /// <summary>登録名の最大文字数 (v5 と同じ 8)。</summary>
        public const int PlayerNameMax = 8;

        // v1–v3 のグレード閾値 (ScoreMaxLegacy = 1,000,000 に対するスコア)。
        // <see cref="V6GradeLogic.GradeBandsOf"/> のゲージ帯もこの値から導出する。
        public const int RankLegacyB = 700000;
        public const int RankLegacyA = 900000;
        public const int RankLegacyS = 950000;
        public const int RankLegacyMax = 1000000;

        /// <summary>既存の Assessment 判定と同一の閾値 (1:1 維持)。</summary>
        public static string RankFor(int score)
        {
            if (score == RankLegacyMax)
            {
                return "SS";
            }

            if (score >= RankLegacyS)
            {
                return "S";
            }

            if (score >= RankLegacyA)
            {
                return "A";
            }

            if (score >= RankLegacyB)
            {
                return "B";
            }

            return "C";
        }

        /// <summary>登録名の正規化: 大文字化 + A-Z0-9 のみ + 最大8文字。</summary>
        public static string NormalizeName(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return string.Empty;
            }

            var upper = raw.ToUpperInvariant();
            var sb = new StringBuilder();
            foreach (char c in upper)
            {
                if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))
                {
                    sb.Append(c);
                    if (sb.Length >= PlayerNameMax)
                    {
                        break;
                    }
                }
            }

            return sb.ToString();
        }
    }
}
