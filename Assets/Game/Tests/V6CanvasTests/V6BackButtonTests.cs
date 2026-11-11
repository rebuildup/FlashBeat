using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using FlashBeat.UI.V6.Common;

namespace FlashBeat.V6CanvasTests
{
    /// <summary>
    /// Issue #370: ヘッダー左上の戻るボタンは 1 つの共通部品
    /// (<see cref="V6Controls.BackButton"/>) から作り、画面ごとに
    /// アイコンと Key の位置がずれないようにする。
    /// </summary>
    public sealed class V6BackButtonTests
    {
        private GameObject canvasGo;
        private int clicks;

        [SetUp]
        public void SetUp()
        {
            canvasGo = new GameObject("V6BackButtonTests Canvas", typeof(Canvas));
            clicks = 0;
        }

        [TearDown]
        public void TearDown()
        {
            if (canvasGo != null)
            {
                Object.DestroyImmediate(canvasGo);
            }

            canvasGo = null;
        }

        [Test]
        public void BackButton_IsA56SquareSecondaryPanel()
        {
            GameObject back = V6Controls.BackButton(canvasGo, "Back", new Vector2(24f, 20f), "Esc", null);

            var rt = back.GetComponent<RectTransform>();
            Assert.AreEqual(V6Controls.BackButtonSize, rt.rect.width, 0.001f);
            Assert.AreEqual(V6Controls.BackButtonSize, rt.rect.height, 0.001f);

            var image = back.GetComponent<Image>();
            Assert.IsNotNull(image);
            Assert.AreEqual(Image.Type.Sliced, image.type, "角丸は 9-slice で描く");
            Assert.IsNotNull(image.sprite, "角丸スプライトが無い (四角に見える)");
        }

        [Test]
        public void BackButton_PlacesTheIconAndKeyAtTheCanonicalCoordinates()
        {
            GameObject back = V6Controls.BackButton(canvasGo, "Back", new Vector2(0f, 20f), "Esc", null);

            RectTransform icon = FindChild(back.transform, "Icon");
            Assert.IsNotNull(icon, "arrow-left アイコンが無い");
            Assert.AreEqual(V6Controls.BackButtonIconPos.x, icon.anchoredPosition.x, 0.001f);
            Assert.AreEqual(-V6Controls.BackButtonIconPos.y, icon.anchoredPosition.y, 0.001f);
            Assert.AreEqual(V6Controls.BackButtonIconSize, icon.rect.width, 0.001f);

            RectTransform key = FindChild(back.transform, "Key");
            Assert.IsNotNull(key, "Key 表記が無い");
            Assert.AreEqual(V6Controls.BackButtonKeyPos.x, key.anchoredPosition.x, 0.001f);
            Assert.AreEqual(-V6Controls.BackButtonKeyPos.y, key.anchoredPosition.y, 0.001f);

            // design zCQBY: アイコン 24 → gap 2 → Key の並びが 56 の中で中央に来る。
            // アイコン下端と Key 枠上端の間は 2、上下の余白は等しい。
            Assert.AreEqual(2f, -key.anchoredPosition.y - (V6Controls.BackButtonIconPos.y
                + V6Controls.BackButtonIconSize), 0.001f);
            float top = V6Controls.BackButtonIconPos.y;
            float bottom = V6Controls.BackButtonSize
                - (-key.anchoredPosition.y + V6Controls.BackButtonKeySize.y);
            Assert.AreEqual(top, bottom, 0.001f, "上下の余白が等しくない (アイコンが上へ寄る)");
        }

        [Test]
        public void BackButton_InvokesTheCallback()
        {
            GameObject back = V6Controls.BackButton(canvasGo, "Back", Vector2.zero, "Esc", () => clicks++);

            back.GetComponent<Button>().onClick.Invoke();

            Assert.AreEqual(1, clicks);
        }

        [Test]
        public void BackButton_HasNoKeyboardNavigation()
        {
            GameObject back = V6Controls.BackButton(canvasGo, "Back", Vector2.zero, "Esc", null);
            var button = back.GetComponent<Button>();

            Assert.AreEqual(Selectable.Transition.None, button.transition);
            Assert.AreEqual(Navigation.Mode.None, button.navigation.mode,
                "矢印キーでフォーカスが当たらない (キー操作は画面側が受ける)");
        }

        [Test]
        public void BackButton_WithoutKeyLabel_OmitsTheKeyText()
        {
            GameObject back = V6Controls.BackButton(canvasGo, "Back", Vector2.zero, null, null);

            Assert.IsNull(FindChild(back.transform, "Key"));
        }

        private static RectTransform FindChild(Transform root, string name)
        {
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name == name)
                {
                    return all[i] as RectTransform;
                }
            }

            return null;
        }
    }
}
