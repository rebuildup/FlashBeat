using NUnit.Framework;
using FlashBeat.UI.V5;

namespace FlashBeat.Tests.Editor
{
    /// <summary>
    /// Issue #122: Game HUD / Pause の純ロジックのテスト。
    /// ランク判定、スコアレールの幅、コンボボーナス表示、ポーズの行/タブ操作。
    /// </summary>
    public class V5GameHudTests
    {
        // --- ランク ---

        [Test]
        public void RankText_1000000_IsSS()
        {
            Assert.AreEqual("SS", GameHudLogic.RankText(1_000_000));
        }

        [Test]
        public void RankText_950000_IsS()
        {
            Assert.AreEqual("S", GameHudLogic.RankText(950_000));
        }

        [Test]
        public void RankText_900000_IsA()
        {
            Assert.AreEqual("A", GameHudLogic.RankText(900_000));
        }

        [Test]
        public void RankText_700000_IsB()
        {
            Assert.AreEqual("B", GameHudLogic.RankText(700_000));
        }

        [Test]
        public void RankText_Below700000_IsC()
        {
            Assert.AreEqual("C", GameHudLogic.RankText(699_999));
        }

        // --- スコアレール ---

        [Test]
        public void RailFillWidth_Zero_IsZero()
        {
            Assert.AreEqual(0f, GameHudLogic.RailFillWidth(0, 600f));
        }

        [Test]
        public void RailFillWidth_FullScore_FillsSegmentSpan()
        {
            Assert.AreEqual(530f, GameHudLogic.RailFillWidth(1_000_000, 600f));
        }

        [Test]
        public void RailFillWidth_Overshoot_ClampsToSegmentSpan()
        {
            Assert.AreEqual(530f, GameHudLogic.RailFillWidth(1_050_000, 600f));
        }

        [Test]
        public void RailPlusWidth_Below1M_IsZero()
        {
            Assert.AreEqual(0f, GameHudLogic.RailPlusWidth(999_999, 600f));
        }

        [Test]
        public void RailPlusWidth_At1Point1M_IsFull()
        {
            Assert.AreEqual(70f, GameHudLogic.RailPlusWidth(1_100_000, 600f));
        }

        [Test]
        public void RailBestX_HalfHiscore_IsHalfSpan()
        {
            Assert.AreEqual(265f, GameHudLogic.RailBestX(500_000, 600f));
        }

        // --- コンボボーナス ---

        [Test]
        public void BonusTotal_ZeroCombo_IsZero()
        {
            Assert.AreEqual(0, GameHudLogic.BonusTotal(0));
        }

        [Test]
        public void BonusTotal_GrowsWithCombo()
        {
            Assert.AreEqual(12_400, GameHudLogic.BonusTotal(248));
        }

        [Test]
        public void NextBonusMilestone_248_Is300()
        {
            Assert.AreEqual(300, GameHudLogic.NextBonusMilestone(248));
        }

        [Test]
        public void NextBonusMilestone_At300_Is600()
        {
            Assert.AreEqual(600, GameHudLogic.NextBonusMilestone(300));
        }

        [Test]
        public void NextBonusReward_300_Is3000()
        {
            Assert.AreEqual(3_000, GameHudLogic.NextBonusReward(300));
        }

        // --- スコア整形 ---

        [Test]
        public void FormatScore_UsesThousandsSeparator()
        {
            Assert.AreEqual("1,012,340", GameHudLogic.FormatScore(1_012_340));
        }

        [Test]
        public void FormatScore_Zero_IsZero()
        {
            Assert.AreEqual("0", GameHudLogic.FormatScore(0));
        }
    }
}
