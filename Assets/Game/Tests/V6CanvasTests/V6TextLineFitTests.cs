using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FlashBeat.UI.V6.Common;
using FlashBeat.UI.V5;

namespace FlashBeat.V6CanvasTests
{
    /// <summary>
    /// 共通部品 Text の Ellipsis 0 行回帰の実測検証 (2026-11-11 r3 で Result の曲名が消えた)。
    ///
    /// 実測 (Unity 6000.5.6f1 / NotoSansJP-Medium SDF, faceInfo lineHeight 69.504 / pointSize 48):
    /// TMP が Ellipsis で 1 行を描く条件は「矩形の実効高 (rect − margin 上下) ≥ faceInfo 行送り
    /// = fs × 1.448」。lineSpacing は 2 行目以降の行間専用で、この判定には<b>効かない</b>
    /// (fs34/h36 は lineSpacing を −44.8 に詰めても 0 文字)。
    /// 旧テストは lineSpacing を織り込んだ「行送り ≤ 矩形高」だけを見ており、meshline を
    /// 生成した結果 (characterCount) を検証していなかったため、この退行を検出できなかった。
    /// ここでは <see cref="V5Ui.FitEllipsisToLineHeight"/> 適用後の実描画を固定する。
    /// </summary>
    public sealed class V6TextLineFitTests
    {
        private GameObject parent;

        [TearDown]
        public void TearDown()
        {
            if (parent != null)
            {
                Object.DestroyImmediate(parent);
            }
            parent = null;
        }

        private static float LineHeight(float fs, TextMeshProUGUI tmp)
        {
            var face = tmp.font.faceInfo;
            Assert.Greater(face.pointSize, 0f, "既定フォント (NotoSansJP-Medium SDF) が解決されていること");
            return face.lineHeight * (fs / face.pointSize);
        }

        private TextMeshProUGUI CreateV6Text(float fs, float rectH)
        {
            parent = new GameObject("parent", typeof(RectTransform), typeof(Canvas));
            return V6Controls.Text(parent, "Text", "ロストアンブレラ", new Vector2(0f, 0f),
                new Vector2(200f, rectH), fs, Color.white);
        }

        [Test]
        public void V6Text_EllipsisBelowLineHeight_DrawsNothing_WithoutFit()
        {
            // 症状の固定: fs26 / h36 は行送り 37.65 を下回るため Ellipsis で 1 文字も出ない。
            // (lineSpacing の詰めでは救えないことも同時に固定する)
            float fs = 26f;
            var tmp = CreateV6Text(fs, 36f);
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.ForceMeshUpdate(true, true);

            Assert.Less(tmp.lineSpacing, 0f, "行送り詰めは負の lineSpacing で行うこと");
            Assert.Less(36f, LineHeight(fs, tmp), "前提: 矩形高 < 行送り");
            Assert.AreEqual(0, tmp.textInfo.characterCount, "行送り未満の Ellipsis は 0 行 (TMP の仕様)");
        }

        [Test]
        public void V6Text_EllipsisWithFitHelper_DrawsOneLine()
        {
            float fs = 26f;
            var tmp = CreateV6Text(fs, 36f);
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            V5Ui.FitEllipsisToLineHeight(tmp);
            tmp.ForceMeshUpdate(true, true);

            Assert.Greater(tmp.textInfo.characterCount, 0, "補正後は 1 行描画される");
            Assert.AreEqual(1, tmp.textInfo.lineCount);
            Assert.AreEqual(36f, tmp.rectTransform.rect.height, 0.01f, "design 矩形は変えない");
        }

        [Test]
        public void V5Text_EllipsisWithFitHelper_DrawsOneLine()
        {
            // V5GameUi / V5Ui.Text (旧 v5 画面と v6 の一部) も同じ扱いにする。
            float fs = 34f;
            parent = new GameObject("parent", typeof(RectTransform), typeof(Canvas));
            var tmp = V5Ui.Text(parent, "Text", "ロストアンブレラ", fs, Color.white);
            tmp.rectTransform.sizeDelta = new Vector2(204f, 36f);
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Ellipsis;

            tmp.ForceMeshUpdate(true, true);
            Assert.AreEqual(0, tmp.textInfo.characterCount, "前提: 補正なしでは 0 行");

            V5Ui.FitEllipsisToLineHeight(tmp);
            tmp.ForceMeshUpdate(true, true);
            Assert.Greater(tmp.textInfo.characterCount, 0, "補正後は描画される");
            Assert.AreEqual(1, tmp.textInfo.lineCount);
        }
    }
}
