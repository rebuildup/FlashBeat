using NUnit.Framework;
using FlashBeat.UI.V5;

namespace FlashBeat.Tests.Editor
{
    /// <summary>
    /// Issue #123: ResultView の純ロジック (ランク判定 / スコア表記 / 差分ラベル / 登録名正規化)。
    /// RankFor は旧 ResultSceneManager の Assessment 閾値と 1:1 であることを検証する。
    /// </summary>
    public class ResultV5ViewTests
    {
        // --- RankFor (旧 Assessment と同一閾値) ---

        [Test]
        public void RankFor_1000000_IsSS()
        {
            Assert.AreEqual("SS", ResultView.RankFor(1000000));
        }

        [Test]
        public void RankFor_Boundaries_MatchLegacyThresholds()
        {
            Assert.AreEqual("S", ResultView.RankFor(999999));
            Assert.AreEqual("S", ResultView.RankFor(950000));
            Assert.AreEqual("A", ResultView.RankFor(949999));
            Assert.AreEqual("A", ResultView.RankFor(900000));
            Assert.AreEqual("B", ResultView.RankFor(899999));
            Assert.AreEqual("B", ResultView.RankFor(700000));
            Assert.AreEqual("C", ResultView.RankFor(699999));
            Assert.AreEqual("C", ResultView.RankFor(0));
        }

        // --- FormatScore (3桁カンマ区切り) ---

        [Test]
        public void FormatScore_UsesThousandsSeparator()
        {
            Assert.AreEqual("972,310", ResultView.FormatScore(972310));
            Assert.AreEqual("0", ResultView.FormatScore(0));
            Assert.AreEqual("1,000,000", ResultView.FormatScore(1000000));
        }

        // --- DeltaLabel ---

        [Test]
        public void DeltaLabel_NewRecord_ReturnsPlusDelta()
        {
            Assert.AreEqual("+50,430", ResultView.DeltaLabel(921880, 972310));
        }

        [Test]
        public void DeltaLabel_EqualOrLower_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, ResultView.DeltaLabel(972310, 972310));
            Assert.AreEqual(string.Empty, ResultView.DeltaLabel(972310, 100000));
        }

        // --- NormalizeName ---

        [Test]
        public void NormalizeName_UppercasesAndStripsInvalid()
        {
            Assert.AreEqual("AB12", ResultView.NormalizeName("ab12"));
            Assert.AreEqual("ABCDE", ResultView.NormalizeName(" abc-de "));
        }

        [Test]
        public void NormalizeName_LimitsTo8Chars()
        {
            Assert.AreEqual("ABCDEFGH", ResultView.NormalizeName("abcdefghij"));
        }

        [Test]
        public void NormalizeName_NullOrEmpty_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, ResultView.NormalizeName(null));
            Assert.AreEqual(string.Empty, ResultView.NormalizeName(""));
            Assert.AreEqual(string.Empty, ResultView.NormalizeName("%%%"));
        }
    }
}
