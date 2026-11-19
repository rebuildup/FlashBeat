using NUnit.Framework;
using FlashBeat.UI.V6.Game.Hud;

namespace FlashBeat.Tests.Editor
{
    /// <summary>
    /// Issue #740: 旧 V5 GameHudLogic から v6 HUD 層へ移設した
    /// <see cref="GameHudLogic"/> の純ロジック (ランク表記 / スコア整形) のテスト。
    /// v5 HUD 専用だったスコアレール幅 / コンボボーナス近似式は v6 HUD が
    /// <c>V6HudScore</c> / <c>V6HudLogic</c> で担うため対象外。
    /// </summary>
    public class GameHudLogicTests
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
