using FlashBeat.UI.V6.Common;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FlashBeat.Tests.Editor
{
    /// <summary>
    /// Issue #385 (Phase 1.1): V6Header が全画面共通のヘッダー帯
    /// (frame ローカル (24,0) 1680×96)・戻るボタン ((0,20) 56×56)・
    /// タイトル ((72,20) 400×56 fs38 w700) を design 座標どおりに組み立てることを固定する。
    /// 画面側の呼び出しはこの座標が変わらないことを前提にしている。
    /// </summary>
    public class V6HeaderTests
    {
        private GameObject frame;

        [SetUp]
        public void SetUp()
        {
            frame = new GameObject("Frame", typeof(RectTransform));
        }

        [TearDown]
        public void TearDown()
        {
            if (frame != null)
            {
                Object.DestroyImmediate(frame);
            }
        }

        private static RectTransform Rect(Transform parent, string path)
        {
            Transform t = parent.Find(path);
            return t != null ? t.GetComponent<RectTransform>() : null;
        }

        [Test]
        public void Create_Node_ProducesHeaderStripAtContractRect()
        {
            var header = V6Header.Create(frame, false);
            Assert.AreEqual("Header", header.name);
            Assert.AreEqual(frame.transform, header.transform.parent);

            var rt = header.GetComponent<RectTransform>();
            Assert.AreEqual(new Vector2(0f, 1f), rt.anchorMin);
            Assert.AreEqual(new Vector2(0f, 1f), rt.anchorMax);
            Assert.AreEqual(new Vector2(0f, 1f), rt.pivot);
            Assert.AreEqual(new Vector2(24f, 0f), rt.anchoredPosition);
            Assert.AreEqual(new Vector2(1680f, 96f), rt.sizeDelta);
            Assert.IsNull(header.GetComponent<Image>(), "panel=false は Image を付けない");
        }

        [Test]
        public void Create_Panel_AddsRaycastBlockingImage()
        {
            // Select / Result の従来実装: Image 付きの透明パネル (raycastTarget=true)。
            var header = V6Header.Create(frame, true);
            var image = header.GetComponent<Image>();
            Assert.IsNotNull(image, "panel=true は Image を付ける");
            Assert.IsTrue(image.raycastTarget);
            Assert.AreEqual(Image.Type.Sliced, image.type);
        }

        [Test]
        public void BackButton_Is56x56AtDesignPosWithKeyLabel()
        {
            var header = V6Header.Create(frame, false);
            var back = V6Header.BackButton(header, "Back Button", "Esc", null);

            var rt = back.GetComponent<RectTransform>();
            Assert.AreEqual(new Vector2(0f, 1f), rt.anchorMin);
            Assert.AreEqual(new Vector2(0f, -20f), rt.anchoredPosition);
            Assert.AreEqual(new Vector2(56f, 56f), rt.sizeDelta);
            Assert.IsNotNull(back.GetComponent<Button>());

            var key = back.transform.Find("Key");
            Assert.IsNotNull(key, "戻るボタン内に Key ラベルが無い");
            var keyTmp = key.GetComponent<TextMeshProUGUI>();
            Assert.AreEqual("Esc", keyTmp.text);
        }

        [Test]
        public void Title_UsesContractRectAndStyle()
        {
            var header = V6Header.Create(frame, false);
            var tmp = V6Header.Title(header, "RESULT");

            Assert.AreEqual("Title", tmp.gameObject.name);
            Assert.AreEqual("RESULT", tmp.text);
            var rt = tmp.rectTransform;
            Assert.AreEqual(new Vector2(72f, -20f), rt.anchoredPosition);
            Assert.AreEqual(new Vector2(400f, 56f), rt.sizeDelta);
            Assert.AreEqual(38f, tmp.fontSize);
            Assert.AreEqual(FontStyles.Bold, tmp.fontStyle & FontStyles.Bold);
            Assert.AreEqual(TextWrappingModes.NoWrap, tmp.textWrappingMode);
        }

        [Test]
        public void Title_AllowsPerScreenRect()
        {
            // Pause は正本の矩形 (72,20) 185×56 — 既定 400×56 ではない。
            var header = V6Header.Create(frame, false);
            var tmp = V6Header.Title(header, "PAUSE",
                new Vector2(72f, 20f), new Vector2(185f, 56f));

            var rt = tmp.rectTransform;
            Assert.AreEqual(new Vector2(185f, 56f), rt.sizeDelta);
        }
    }
}
