using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using FlashBeat.UI.V6;
using FlashBeat.UI.V6.Common;
using FlashBeat.UI.V6.Title;

namespace FlashBeat.V6CanvasTests
{
    /// <summary>
    /// 01 Title ヘッダー左の Exit (design QJtTv / CtA3V) を実際に組み立てて検証する。
    ///
    /// 正本は CtA3V (Header ローカル (0,20) 162×56、**fill なし**) の中に
    /// - Icon Button (Z6C0w): (0,0) 56×56 $k4 r8 + log-out 32 @(12,12)
    /// - KeyCap (D7mVkL): (68,12) 高さ 32 (#3A3A3A r4)
    /// - Label (qttPo): (109,6.5) 53×43 fs30 $text-muted "Exit"
    /// を置く構成。旧実装は Exit 全体を 162×56 の $k4 パネルで塗っていたため、
    /// 実機 (r12) では 3 要素が 1 本の横長ピルに乗って見えていた。
    /// </summary>
    public sealed class V6TitleExitTests
    {
        private GameObject headerGo;
        private GameObject exitGo;

        [SetUp]
        public void SetUp()
        {
            headerGo = new GameObject("Header", typeof(RectTransform));
            ((RectTransform)headerGo.transform).sizeDelta = new Vector2(1680f, 96f);
        }

        [TearDown]
        public void TearDown()
        {
            if (headerGo != null)
            {
                Object.DestroyImmediate(headerGo);
            }
            headerGo = null;
            exitGo = null;
        }

        /// <summary>pivot に依らない design 左上座標。</summary>
        private static Vector2 TopLeft(RectTransform rt)
        {
            return new Vector2(
                rt.anchoredPosition.x - rt.pivot.x * rt.sizeDelta.x,
                -rt.anchoredPosition.y - (1f - rt.pivot.y) * rt.sizeDelta.y);
        }

        private static RectTransform Child(GameObject parent, string name)
        {
            var rt = (RectTransform)parent.transform.Find(name);
            Assert.IsNotNull(rt, name + " not found under " + parent.name);
            return rt;
        }

        private GameObject BuildExit()
        {
            exitGo = V6TitleExit.Build(headerGo, () => exitClicks++);
            Assert.IsNotNull(exitGo, "Exit group");
            return exitGo;
        }

        private int exitClicks;

        [Test]
        public void ExitGroup_IsATransparentContainerAtDesignPosition()
        {
            var exit = BuildExit();
            var rt = (RectTransform)exit.transform;
            Assert.AreEqual(new Vector2(V6TitleLogic.BackButtonPos.x, V6TitleLogic.BackButtonPos.y),
                TopLeft(rt), "Exit top-left (0,20)");
            Assert.AreEqual(new Vector2(162f, 56f), rt.sizeDelta, "Exit 162×56");

            // design の Exit フレームは fill を持たない (塗りは Icon Button だけ)。
            var image = exit.GetComponent<Image>();
            Assert.IsNotNull(image, "Exit carries the click target Image");
            Assert.AreEqual(0f, image.color.a, 0.001f,
                "Exit container must not paint a 162×56 panel");
        }

        [Test]
        public void IconButton_IsTheOnly56PxFill_AtDesignPosition()
        {
            var exit = BuildExit();
            var iconButton = Child(exit, "Icon Button");
            Assert.AreEqual(new Vector2(0f, 0f), TopLeft(iconButton), "Icon Button (0,0)");
            Assert.AreEqual(new Vector2(56f, 56f), iconButton.sizeDelta, "Icon Button 56×56");
            Assert.AreEqual(V6Tokens.Key4, iconButton.GetComponent<Image>().color, "Icon Button $k4");

            // Icon Button/Icon (log-out 32) は (12,12)。
            var icon = (RectTransform)iconButton.Find("Icon");
            Assert.IsNotNull(icon, "log-out icon");
            Assert.AreEqual(new Vector2(V6TitleLogic.BackIconPos.x, V6TitleLogic.BackIconPos.y),
                TopLeft(icon), "icon (12,12)");
            Assert.AreEqual(new Vector2(32f, 32f), icon.sizeDelta, "icon 32×32");
            Assert.AreEqual(V6Tokens.TextMuted, icon.GetComponent<Image>().color, "icon $text-muted");

            // 56 より広い塗りが Exit 内に残っていないこと (旧 162×56 パネルの再発防止)。
            var images = exit.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i].gameObject == exit)
                {
                    continue;
                }
                Assert.LessOrEqual(images[i].rectTransform.sizeDelta.x,
                    V6TitleLogic.BackIconButtonSize + 0.5f,
                    images[i].name + " must not be wider than the 56px Icon Button");
            }
        }

        [Test]
        public void KeyCap_AndLabel_MatchDesign()
        {
            var exit = BuildExit();

            RectTransform keyCap = null;
            foreach (Transform child in exit.transform)
            {
                if (child.name.StartsWith("KeyCap"))
                {
                    keyCap = (RectTransform)child;
                }
            }
            Assert.IsNotNull(keyCap, "KeyCap child");
            Assert.AreEqual(new Vector2(V6TitleLogic.BackKeyCapPos.x, V6TitleLogic.BackKeyCapPos.y),
                TopLeft(keyCap), "KeyCap (68,12)");
            Assert.AreEqual(32f, keyCap.sizeDelta.y, "KeyCap height 32");
            // design の hug 幅は 29 (Key fs22 + padding 8×2)。TMP 実測の差は ±2px。
            Assert.That(keyCap.sizeDelta.x, Is.InRange(26f, 32f), "KeyCap width ≈ design 29");
            Assert.AreEqual(V6Tokens.Key4, keyCap.GetComponent<Image>().color, "KeyCap #3A3A3A");

            var keyText = keyCap.Find("Key");
            Assert.IsNotNull(keyText, "KeyCap/Key");
            var keyTmp = keyText.GetComponent<TextMeshProUGUI>();
            Assert.AreEqual(22f, keyTmp.fontSize, 0.01f, "Key fs22");
            Assert.IsFalse(string.IsNullOrEmpty(keyTmp.text), "Key shows the real exit key");

            var label = Child(exit, "Label");
            Assert.AreEqual(new Vector2(V6TitleLogic.BackLabelPos.x, V6TitleLogic.BackLabelPos.y),
                TopLeft(label), "Label (109,6.5)");
            Assert.AreEqual(new Vector2(53f, 43f), label.sizeDelta, "Label 53×43");
            var labelTmp = label.GetComponent<TextMeshProUGUI>();
            Assert.AreEqual("Exit", labelTmp.text, "Label text");
            Assert.AreEqual(30f, labelTmp.fontSize, 0.01f, "Label fs30");
            Assert.AreEqual(V6Tokens.TextMuted, labelTmp.color, "Label $text-muted");
            Assert.AreEqual(TextAlignmentOptions.MidlineLeft, labelTmp.alignment, "Label left/middle");
        }

        [Test]
        public void ExitGroup_IsClickableAndInvokesTheExitAction()
        {
            BuildExit();
            var button = exitGo.GetComponent<Button>();
            Assert.IsNotNull(button, "Exit Button");
            button.onClick.Invoke();
            Assert.AreEqual(1, exitClicks, "click invokes onExit");
        }
    }
}
