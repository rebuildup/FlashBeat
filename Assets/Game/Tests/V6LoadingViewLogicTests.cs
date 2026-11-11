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

        // --- 実進捗 (Issue #224) ---

        [Test]
        public void HasProgress_NullIsUnknown_ZeroIsKnown()
        {
            // 進捗不明 (null) だけが不定インジケータの条件。0 は既知。
            Assert.IsFalse(V6LoadingViewLogic.HasProgress(null));
            Assert.IsTrue(V6LoadingViewLogic.HasProgress(0f));
            Assert.IsTrue(V6LoadingViewLogic.HasProgress(1f));
        }

        [Test]
        public void DeterminateFillWidth_ProportionalAndClamped()
        {
            float track = 464f;
            Assert.AreEqual(0f, V6LoadingViewLogic.DeterminateFillWidth(0f, track), 1e-4f);
            Assert.AreEqual(232f, V6LoadingViewLogic.DeterminateFillWidth(0.5f, track), 1e-4f);
            Assert.AreEqual(track, V6LoadingViewLogic.DeterminateFillWidth(1f, track), 1e-4f);
            // 範囲外はクランプ (負値 / 1 超え)。
            Assert.AreEqual(0f, V6LoadingViewLogic.DeterminateFillWidth(-0.2f, track), 1e-4f);
            Assert.AreEqual(track, V6LoadingViewLogic.DeterminateFillWidth(1.5f, track), 1e-4f);
        }

        [Test]
        public void ProgressPercentText_RoundsToWholePercent()
        {
            Assert.AreEqual("0%", V6LoadingViewLogic.ProgressPercentText(0f));
            Assert.AreEqual("42%", V6LoadingViewLogic.ProgressPercentText(0.42f));
            Assert.AreEqual("100%", V6LoadingViewLogic.ProgressPercentText(1f));
            Assert.AreEqual("100%", V6LoadingViewLogic.ProgressPercentText(0.999f));
            Assert.AreEqual("0%", V6LoadingViewLogic.ProgressPercentText(-0.5f));
        }

        [Test]
        public void ProgressDetail_IncludesStageAndPercent()
        {
            string text = V6LoadingViewLogic.ProgressDetail(0.42f);
            Assert.IsTrue(text.Contains("譜面"));
            Assert.IsTrue(text.Contains("42%"));
        }

        // --- ジャケット (Issue #224) ---

        [Test]
        public void JacketResourcePathOf_MapsCatalogIndexToRealFile()
        {
            // Resources.Load は拡張子付きパスを受け付けないため、パスは拡張子なし。
            Assert.AreEqual("V6/select/jacket/01-rosen", V6LoadingViewLogic.JacketResourcePathOf(1));
            Assert.AreEqual("V6/select/jacket/10-tutorial", V6LoadingViewLogic.JacketResourcePathOf(10));
            Assert.AreEqual("V6/select/jacket/26-ikken", V6LoadingViewLogic.JacketResourcePathOf(26));
        }

        [Test]
        public void JacketResourcePathOf_SongsWithoutJacket_ReturnEmpty()
        {
            // 22 = 灰Φ倶楽部 も実画像 (22-haihai.jpg) に対応する。
            Assert.AreEqual("V6/select/jacket/22-haihai", V6LoadingViewLogic.JacketResourcePathOf(22));
            // 表に無い index (0 / 27) は空文字。
            Assert.AreEqual("", V6LoadingViewLogic.JacketResourcePathOf(0));
            Assert.AreEqual("", V6LoadingViewLogic.JacketResourcePathOf(27));
        }
    }
}
