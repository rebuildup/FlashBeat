using NUnit.Framework;
using UnityEngine;
using FlashBeat.UI.V6;

namespace FlashBeat.V5CanvasTests
{
    /// <summary>
    /// v6 KeyCap のフォントサイズ整合 (純ロジック)。
    /// v6 ビューは design 空間 (1728×972 / 1920×1080) を frame で等比拡大するため、
    /// fs はキャップ高さと同一座標空間で決め、かつラベルが 1 行に収まる必要がある。
    /// </summary>
    public class V6UiKeyCapFontSizeTests
    {
        [Test]
        public void HeightRatio_MatchesContract_GeneralKeyCap()
        {
            // 契約: 汎用 KeyCap 高さ 36, Key fs22。幅が十分なら比 22/36 の床切り。
            Assert.AreEqual(22f, V6Ui.KeyCapFontSize(200f, 36f, "A"));
            Assert.AreEqual(36f * V6Ui.KeyCapFontSizeRatio, V6Ui.KeyCapFontSize(200f, 36f, "A"));
        }

        [Test]
        public void LongLabel_ShrinksToFitWidth_Space_76x32()
        {
            // 2560×1664 実機で "Space" が折り返していた実測ケース (design 76×32)。
            float fs = V6Ui.KeyCapFontSize(76f, 32f, "Space");
            float available = 76f - V6Ui.KeyCapPaddingX * 2f;
            Assert.LessOrEqual(fs * V6Ui.LabelAdvanceEm("Space"), available);
            Assert.Less(fs, 32f * V6Ui.KeyCapFontSizeRatio, "幅フィットで縮小されていること");
        }

        [Test]
        public void LongLabel_ShrinksToFitWidth_Tab_56x32()
        {
            float fs = V6Ui.KeyCapFontSize(56f, 32f, "Tab");
            float available = 56f - V6Ui.KeyCapPaddingX * 2f;
            Assert.GreaterOrEqual(fs, 1f);
            Assert.LessOrEqual(fs * V6Ui.LabelAdvanceEm("Tab"), available);
        }

        [TestCase("↑↓", 56f, 32f)]
        [TestCase("Esc", 56f, 32f)]
        [TestCase("Space", 86f, 36f)]
        [TestCase("Esc", 44f, 36f)]
        public void RealUsages_FitOnOneLine(string label, float w, float h)
        {
            float fs = V6Ui.KeyCapFontSize(w, h, label);
            float available = w - V6Ui.KeyCapPaddingX * 2f;
            Assert.GreaterOrEqual(fs, 1f);
            Assert.LessOrEqual(fs * V6Ui.LabelAdvanceEm(label), available,
                $"{label} ({w}x{h}) が 1 行に収まらない");
        }

        [Test]
        public void EmptyLabel_FallsBackToHeightRatio()
        {
            Assert.AreEqual(22f, V6Ui.KeyCapFontSize(76f, 36f, ""));
            Assert.AreEqual(0f, V6Ui.LabelAdvanceEm(""));
            Assert.AreEqual(0f, V6Ui.LabelAdvanceEm(null));
        }

        [Test]
        public void DegenerateCap_NeverReturnsZeroOrNegative()
        {
            Assert.GreaterOrEqual(V6Ui.KeyCapFontSize(8f, 36f, "Space"), 1f);
            Assert.GreaterOrEqual(V6Ui.KeyCapFontSize(76f, 0f, "Space"), 1f);
        }

        [Test]
        public void AsciiVsFullWidth_Estimate()
        {
            // ASCII は 0.62em、それ以外は 1.0em の上側推定。
            Assert.AreEqual(3f * V6Ui.AsciiAdvanceEm, V6Ui.LabelAdvanceEm("Tab"));
            Assert.AreEqual(V6Ui.FullWidthAdvanceEm * 2f, V6Ui.LabelAdvanceEm("↑↓"), 1e-6f);
        }
    }
}
