using NUnit.Framework;
using FlashBeat.UI.V6.Game.Loading;

namespace FlashBeat.V6HudTests
{
    /// <summary>
    /// v6 曲ローディング画面 (Issue #224) の純ロジックの EditMode テスト。
    /// design に進捗表現が無いので、不定インジケータの往復計算だけを検証する。
    /// </summary>
    public class V6LoadingViewLogicTests
    {
        [Test]
        public void IndeterminateX_PingPongsBetweenEnds()
        {
            float track = 464f;
            float fill = 160f;
            float range = track - fill;

            Assert.AreEqual(0f, V6LoadingViewLogic.IndeterminateX(0f, track, fill), 1e-4f);
            // 半周期 (phase 0.5) で反対端、1 周期で戻る。
            Assert.AreEqual(range, V6LoadingViewLogic.IndeterminateX(
                V6LoadingViewLogic.CycleSeconds * 0.5f, track, fill), 1e-3f);
            Assert.AreEqual(0f, V6LoadingViewLogic.IndeterminateX(
                V6LoadingViewLogic.CycleSeconds, track, fill), 1e-4f);
        }

        [Test]
        public void IndeterminateX_DegenerateSizes_StaysAtZero()
        {
            Assert.AreEqual(0f, V6LoadingViewLogic.IndeterminateX(1f, 0f, 160f));
            Assert.AreEqual(0f, V6LoadingViewLogic.IndeterminateX(1f, 160f, 160f));
            Assert.AreEqual(0f, V6LoadingViewLogic.IndeterminateX(1f, 100f, 160f));
            Assert.AreEqual(0f, V6LoadingViewLogic.IndeterminateX(1f, 464f, 0f));
        }
    }
}
