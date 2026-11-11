using NUnit.Framework;
using FlashBeat.Gameplay;

namespace FlashBeat.Tests.Editor
{
    /// <summary>
    /// Issue #351: HUD は準備前から「Space to Start」を出すため、準備中の押下を
    /// 捨てると「無反応」に見える。押下を保留し、準備完了時に 1 回だけ開始する契約。
    /// </summary>
    public class PlaybackStartGateTests
    {
        [Test]
        public void NoPress_DoesNotStart()
        {
            bool requested = false;

            Assert.IsFalse(PlaybackStartGate.ShouldStart(true, false, ref requested),
                "押していない Space で開始しない");
        }

        [Test]
        public void PressWhenReady_StartsImmediately()
        {
            bool requested = false;

            Assert.IsTrue(PlaybackStartGate.ShouldStart(true, true, ref requested),
                "準備完了後の押下はそのフレームで開始する");
            Assert.IsFalse(PlaybackStartGate.ShouldStart(true, false, ref requested),
                "同じ押下で 2 回開始しない");
        }

        [Test]
        public void PressBeforeReady_IsKeptUntilTheSongCanStart()
        {
            bool requested = false;

            Assert.IsFalse(PlaybackStartGate.ShouldStart(false, true, ref requested),
                "準備中はまだ開始しない");
            Assert.IsFalse(PlaybackStartGate.ShouldStart(false, false, ref requested),
                "準備中のフレームでは開始しない");
            Assert.IsTrue(PlaybackStartGate.ShouldStart(true, false, ref requested),
                "準備完了で保留した押下が効く (押し直し不要)");
            Assert.IsFalse(PlaybackStartGate.ShouldStart(true, false, ref requested),
                "開始は 1 回だけ");
        }

        [Test]
        public void RepeatedPressesWhilePreparing_StillStartOnce()
        {
            bool requested = false;

            for (int i = 0; i < 5; i++)
            {
                Assert.IsFalse(PlaybackStartGate.ShouldStart(false, true, ref requested),
                    "準備中の押下は開始しない");
            }

            Assert.IsTrue(PlaybackStartGate.ShouldStart(true, false, ref requested),
                "準備完了で 1 回だけ開始する");
            Assert.IsFalse(PlaybackStartGate.ShouldStart(true, false, ref requested),
                "連打しても開始は 1 回だけ");
        }
    }
}
