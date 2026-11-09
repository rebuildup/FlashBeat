using NUnit.Framework;
using FlashBeat.UI.V5;

namespace FlashBeat.V6CanvasTests
{
    /// <summary>
    /// OpeningScene の自動遷移待ち時間の決定ロジック (純ロジック)。
    /// - 映像が無い / 長さ不明なら従来値 (6.031s)
    /// - 映像がある場合は映像の長さと従来値の遅い方
    /// - ただし上限 (12s) を超えない
    /// 実行は TestRunner (EditMode)。
    /// </summary>
    public class V6OpeningTransitionTests
    {
        [Test]
        public void NoVideo_UsesLegacyDelay()
        {
            Assert.AreEqual(OpeningView.BaseAdvanceSeconds,
                OpeningView.ResolveAutoAdvanceDelay(false, 0f));
        }

        [Test]
        public void UnknownLength_UsesLegacyDelay()
        {
            Assert.AreEqual(OpeningView.BaseAdvanceSeconds,
                OpeningView.ResolveAutoAdvanceDelay(true, 0f));
            Assert.AreEqual(OpeningView.BaseAdvanceSeconds,
                OpeningView.ResolveAutoAdvanceDelay(true, -1f));
        }

        [Test]
        public void ShorterThanLegacy_LegacyWins()
        {
            Assert.AreEqual(OpeningView.BaseAdvanceSeconds,
                OpeningView.ResolveAutoAdvanceDelay(true, 4f));
        }

        [Test]
        public void LongerThanLegacy_VideoLengthWins()
        {
            Assert.AreEqual(8f, OpeningView.ResolveAutoAdvanceDelay(true, 8f));
        }

        [Test]
        public void LongerThanCap_ClampedToCap()
        {
            Assert.AreEqual(OpeningView.MaxAdvanceSeconds,
                OpeningView.ResolveAutoAdvanceDelay(true, 30f));
        }

        [Test]
        public void LegacyDelayNeverExceedsCap()
        {
            Assert.LessOrEqual(OpeningView.BaseAdvanceSeconds, OpeningView.MaxAdvanceSeconds);
        }
    }
}
