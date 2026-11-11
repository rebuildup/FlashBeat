using NUnit.Framework;
using FlashBeat.UI.V6.Game.Pause;

namespace FlashBeat.Tests
{
    /// <summary>
    /// Issue #382: ポーズ再開時の 3 → 2 → 1 カウントダウンの時間割り。
    /// 実機での入力無効・ノーツ位置の連続性は Mac 受入で確認する。
    /// </summary>
    public class ResumeCountdownTests
    {
        [Test]
        public void NumberAt_ShowsThreeThenTwoThenOne()
        {
            Assert.AreEqual(3, V6ResumeCountdownLogic.NumberAt(0f));
            Assert.AreEqual(3, V6ResumeCountdownLogic.NumberAt(0.99f));
            Assert.AreEqual(2, V6ResumeCountdownLogic.NumberAt(1f));
            Assert.AreEqual(2, V6ResumeCountdownLogic.NumberAt(1.99f));
            Assert.AreEqual(1, V6ResumeCountdownLogic.NumberAt(2f));
            Assert.AreEqual(1, V6ResumeCountdownLogic.NumberAt(2.99f));
        }

        [Test]
        public void NumberAt_IsClampedOutsideTheWindow()
        {
            Assert.AreEqual(3, V6ResumeCountdownLogic.NumberAt(-5f));
            Assert.AreEqual(1, V6ResumeCountdownLogic.NumberAt(30f));
        }

        [Test]
        public void IsFinished_OnlyAfterThreeSeconds()
        {
            Assert.IsFalse(V6ResumeCountdownLogic.IsFinished(0f));
            Assert.IsFalse(V6ResumeCountdownLogic.IsFinished(2.999f));
            Assert.IsTrue(V6ResumeCountdownLogic.IsFinished(3f));
            Assert.IsTrue(V6ResumeCountdownLogic.IsFinished(3.001f));
        }
    }
}
