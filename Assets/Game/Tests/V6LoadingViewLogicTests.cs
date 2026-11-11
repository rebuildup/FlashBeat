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

        // --- フッター凡例 (design g4Q3F) ---

        [Test]
        public void FooterLegend_MatchesDesignCoordinates()
        {
            // design g4Q3F: 決定 / 曲送り を 0×0 の項目として据え置き、項目間 gap16 で
            // 戻る まで進める → 戻る はゾーンローカル x32 = フレーム x56
            // (2x 書き出しの Cap 実測 x112..221 = フレーム 56..110.5)。
            Assert.AreEqual(56f, V6LoadingViewLogic.FooterLegendX);
            Assert.AreEqual(30f, V6LoadingViewLogic.FooterLegendY);
            // KeyCap h36 / pad10 / 項内 gap10 / fs22 (Title の凡例と同じ別設計)。
            Assert.AreEqual(36f, V6LoadingViewLogic.FooterLegendCapHeight);
            Assert.AreEqual(10f, V6LoadingViewLogic.FooterLegendInnerGap);
            Assert.AreEqual(16f, V6LoadingViewLogic.FooterLegendItemGap);
            Assert.AreEqual(22f, V6LoadingViewLogic.FooterLegendFontSize);
        }

        [Test]
        public void FooterLegend_StaysInsideDesignFooter()
        {
            // design e6RJ2: Cap 55 + 項内 gap10 + Label 45 = 110 幅。
            // フレーム x56 から 166 まで — フッター (24..1704) 内に収まる。
            float width = 55f + V6LoadingViewLogic.FooterLegendInnerGap + 45f;
            Assert.LessOrEqual(24f + V6LoadingViewLogic.FooterLegendX + width, 24f + 1680f);
        }
    }
}
