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

        [Test]
        public void AdvanceElapsed_PassesNormalDeltasThrough()
        {
            Assert.AreEqual(0.016f, V6ResumeCountdownLogic.AdvanceElapsed(0f, 0.016f), 1e-4f);
        }

        [Test]
        public void AdvanceElapsed_ClampsLongFrameSpikes()
        {
            // 数秒のフレーム飛びで 3 → 2 → 1 の数字を飛ばさないこと (Issue #382)。
            Assert.AreEqual(V6ResumeCountdownLogic.MaxFrameAdvanceSeconds,
                V6ResumeCountdownLogic.AdvanceElapsed(0f, 6f), 1e-4f);
            Assert.AreEqual(0.5f + V6ResumeCountdownLogic.MaxFrameAdvanceSeconds,
                V6ResumeCountdownLogic.AdvanceElapsed(0.5f, 6f), 1e-4f);
        }

        [Test]
        public void AdvanceElapsed_IgnoresNegativeDelta()
        {
            Assert.AreEqual(1f, V6ResumeCountdownLogic.AdvanceElapsed(1f, -0.5f), 1e-4f);
        }
    }
}
