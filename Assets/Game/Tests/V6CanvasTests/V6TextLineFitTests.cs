using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FlashBeat.UI.V6.Common;
using FlashBeat.UI.V5;

namespace FlashBeat.V6CanvasTests
{
    /// <summary>
    /// 共通部品 Text の行送り詰め検証。NotoSansJP-Medium SDF の行送りは
    /// fs の約 1.45 倍 (lineHeight 69.504 / pointSize 48) で、矩形の縦高が
    /// 行送り未満だと Ellipsis では 1 行も描画されない (v6-select の
    /// fs26×矩形28 消滅事故)。V6Controls.Text / V5Ui.Text は行送りを ≈ fs に
    /// 詰めるため、「矩形高 = fs」でも 1 行は必ず描画される。その設定を固定する。
    /// TMP の行送り: lineHeight×(fs/pointSize) + lineSpacing×fs/100
    /// (com.unity.ugui TMP_Text.cs の m_lineOffset 更新式)。
    /// </summary>
    public sealed class V6TextLineFitTests
    {
        private GameObject _parent;

        [TearDown]
        public void TearDown()
        {
            if (_parent != null)
            {
                Object.DestroyImmediate(_parent);
            }
        }

        private TextMeshProUGUI CreateV6Text(float fs, float rectH)
        {
            _parent = new GameObject("parent", typeof(RectTransform), typeof(Canvas));
            return V6Controls.Text(_parent, "Text", "あ", new Vector2(0f, 0f),
                new Vector2(200f, rectH), fs, Color.white);
        }

        private static float LineAdvance(TextMeshProUGUI tmp, float fs)
        {
            var face = tmp.font.faceInfo;
            Assert.Greater(face.pointSize, 0f, "既定フォント (NotoSansJP-Medium SDF) が解決されていること");
            return face.lineHeight * (fs / face.pointSize) + tmp.lineSpacing * fs / 100f;
        }

        [Test]
        public void V6Text_LineAdvanceFitsRectEqualToFontSize()
        {
            float fs = 26f;
            var tmp = CreateV6Text(fs, fs); // 矩形高 = fs (従来は行送り 1.45fs で消えていた条件)
            Assert.Less(tmp.lineSpacing, 0f, "行送り詰めは負の lineSpacing で行うこと");
            Assert.LessOrEqual(LineAdvance(tmp, fs), fs + 0.01f, "行送りが fs に詰められていること");
        }

        [Test]
        public void V6Text_OneLineFitsEvenWithEllipsis()
        {
            // 呼び出し側が Ellipsis を指定しても、矩形高 = fs なら 1 行収まること。
            float fs = 26f;
            var tmp = CreateV6Text(fs, fs);
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            Assert.LessOrEqual(LineAdvance(tmp, fs), tmp.rectTransform.rect.height + 0.01f);
        }

        [Test]
        public void V5Text_LineAdvanceFitsRectEqualToFontSize()
        {
            float fs = 26f;
            _parent = new GameObject("parent", typeof(RectTransform), typeof(Canvas));
            var tmp = V5Ui.Text(_parent, "Text", "あ", fs, Color.white);
            tmp.rectTransform.sizeDelta = new Vector2(200f, fs);
            Assert.Less(tmp.lineSpacing, 0f, "行送り詰めは負の lineSpacing で行うこと");
            Assert.LessOrEqual(LineAdvance(tmp, fs), fs + 0.01f, "行送りが fs に詰められていること");
        }
    }
}
