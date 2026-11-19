using NUnit.Framework;
using FlashBeat.UI.V6.Result;

namespace FlashBeat.Tests.Editor
{
    /// <summary>
    /// Issue #740: 旧 V5 ResultView の純ロジックを v6 Result 層へ移設した
    /// <see cref="V6ResultRules"/> / <see cref="V6ResultLogic"/> のテスト
    /// (ランク判定 / スコア表記 / 差分ラベル / 登録名正規化)。
    /// RankFor は旧 ResultSceneManager の Assessment 閾値と 1:1 であることを検証する。
    /// </summary>
    public class V6ResultRulesTests
    {
        // --- RankFor (旧 Assessment と同一閾値) ---

        [Test]
        public void RankFor_1000000_IsSS()
        {
            Assert.AreEqual("SS", V6ResultRules.RankFor(1000000));
        }

        [Test]
        public void RankFor_Boundaries_MatchLegacyThresholds()
        {
            Assert.AreEqual("S", V6ResultRules.RankFor(999999));
            Assert.AreEqual("S", V6ResultRules.RankFor(950000));
            Assert.AreEqual("A", V6ResultRules.RankFor(949999));
            Assert.AreEqual("A", V6ResultRules.RankFor(900000));
            Assert.AreEqual("B", V6ResultRules.RankFor(899999));
            Assert.AreEqual("B", V6ResultRules.RankFor(700000));
            Assert.AreEqual("C", V6ResultRules.RankFor(699999));
            Assert.AreEqual("C", V6ResultRules.RankFor(0));
        }

        // --- FormatScore (3桁カンマ区切り) ---

        [Test]
        public void FormatScore_UsesThousandsSeparator()
        {
            Assert.AreEqual("972,310", V6ResultLogic.FormatScore(972310));
            Assert.AreEqual("0", V6ResultLogic.FormatScore(0));
            Assert.AreEqual("1,000,000", V6ResultLogic.FormatScore(1000000));
        }

        // --- DeltaLabel ---

        [Test]
        public void DeltaLabel_NewRecord_ReturnsPlusDelta()
        {
            Assert.AreEqual("+50,430", V6ResultLogic.DeltaLabel(921880, 972310));
        }

        [Test]
        public void DeltaLabel_EqualOrLower_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, V6ResultLogic.DeltaLabel(972310, 972310));
            Assert.AreEqual(string.Empty, V6ResultLogic.DeltaLabel(972310, 100000));
        }

        // --- NormalizeName ---

        [Test]
        public void NormalizeName_UppercasesAndStripsInvalid()
        {
            Assert.AreEqual("AB12", V6ResultRules.NormalizeName("ab12"));
            Assert.AreEqual("ABCDE", V6ResultRules.NormalizeName(" abc-de "));
        }

        [Test]
        public void NormalizeName_LimitsTo8Chars()
        {
            Assert.AreEqual("ABCDEFGH", V6ResultRules.NormalizeName("abcdefghij"));
        }

        [Test]
        public void NormalizeName_NullOrEmpty_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, V6ResultRules.NormalizeName(null));
            Assert.AreEqual(string.Empty, V6ResultRules.NormalizeName(""));
            Assert.AreEqual(string.Empty, V6ResultRules.NormalizeName("%%%"));
        }
    }
}
