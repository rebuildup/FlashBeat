using FlashBeat.Gameplay;
using NUnit.Framework;

namespace FlashBeat.Tests
{
    /// <summary>
    /// Issue #383 の <c>startTiming</c> 待ちの判定 (PR #387 レビュー指摘)。
    ///
    /// 待ちが済んだ後に開始処理を呼び直したとき、待ちが再開してしまうと
    /// <c>playbackStarted</c> に到達せず、その譜面は永久に始まらない。
    /// ここではその再入がないことと、待ち時間 0 が従来どおり即開始で
    /// あることを固定する。
    /// </summary>
    public class StartTimingGateTests
    {
        [Test]
        public void NoStartTimingStartsImmediately()
        {
            Assert.IsFalse(StartTimingGate.ShouldWait(0f, pending: false, satisfied: false),
                "startTiming 未指定の譜面は待たない (既存譜面の挙動)");
            Assert.IsFalse(StartTimingGate.ShouldWait(-1f, pending: false, satisfied: false));
        }

        [Test]
        public void AStartTimingWaitsOnce()
        {
            Assert.IsTrue(StartTimingGate.ShouldWait(2f, pending: false, satisfied: false));
        }

        [Test]
        public void ItDoesNotWaitTwiceWhileTheWaitIsRunning()
        {
            Assert.IsFalse(StartTimingGate.ShouldWait(2f, pending: true, satisfied: false),
                "待ちコルーチンが走っている間に 2 本目を立てない");
        }

        [Test]
        public void ItDoesNotWaitAgainAfterTheWaitFinished()
        {
            // これが本題: 待ちが済んだ後にもう一度 ShouldWait が true になると、
            // 待ち終わりの開始処理が待ちをやり直して playbackStarted に届かない。
            Assert.IsFalse(StartTimingGate.ShouldWait(2f, pending: false, satisfied: true),
                "待ち済みの譜面は再び待たない (無限ループ回帰)");
        }

        [Test]
        public void ABrokenStartTimingDoesNotBlockTheSong()
        {
            Assert.IsFalse(StartTimingGate.ShouldWait(float.NaN, false, false));
            Assert.IsFalse(StartTimingGate.ShouldWait(float.PositiveInfinity, false, false));
        }
    }
}
