using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using FlashBeat.UI.V6.Common;

namespace FlashBeat.V6CanvasTests
{
    /// <summary>
    /// v6 の主要ボタン (Button Primary / Secondary) に押下・ホバーの視覚フィードバックが
    /// あることの回帰。Button.targetGraphic を設定しないと ColorTint 遷移が走らず、
    /// クリックしても見た目が変わらない (IconButton だけ設定されていた 2026-11-11 の指摘)。
    /// </summary>
    public sealed class V6ButtonFeedbackTests
    {
        private GameObject canvasGo;

        [SetUp]
        public void SetUp()
        {
            canvasGo = new GameObject("V6ButtonFeedbackTests Canvas", typeof(Canvas));
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
        public void ButtonPrimary_TintsItsBackgroundOnPress()
        {
            var go = V6Controls.ButtonPrimary(canvasGo, "OK", Vector2.zero, new Vector2(240f, 56f));
            var button = go.GetComponent<Button>();
            var background = go.GetComponent<Image>();

            Assert.IsNotNull(button, "Button Primary には Button が付く");
            Assert.IsNotNull(background, "Button Primary には背景 Image が付く");
            Assert.AreSame(background, button.targetGraphic,
                "背景 Image を targetGraphic にして ColorTint を効かせる");
            Assert.AreEqual(Selectable.Transition.ColorTint, button.transition);
        }

        [Test]
        public void ButtonSecondary_TintsItsBackgroundOnPress()
        {
            var go = V6Controls.ButtonSecondary(canvasGo, "キャンセル", Vector2.zero, new Vector2(240f, 56f));
            var button = go.GetComponent<Button>();
            var background = go.GetComponent<Image>();

            Assert.IsNotNull(button, "Button Secondary には Button が付く");
            Assert.AreSame(background, button.targetGraphic,
                "背景 Image を targetGraphic にして ColorTint を効かせる");
        }
    }
}
