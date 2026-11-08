using NUnit.Framework;
using UnityEngine;

namespace FlashBeat.UI.V6.Tests
{
    /// <summary>
    /// v6 モードで旧 (v5) の Canvas / ルートが重なって描画される不具合の修正テスト。
    /// "V5" で始まる名前の GameObject が非表示にされ、"V6" で始まるものは残ること。
    /// </summary>
    public static class V6LegacyV5CanvasHideTests
    {
        [Test]
        public static void HideLegacyV5Canvases_DeactivatesV5Roots()
        {
            var v5Canvas = new GameObject("V5 Title Canvas");
            var v5Root = new GameObject("V5 Select View");
            var inactiveV5 = new GameObject("V5 Result Canvas");
            inactiveV5.SetActive(false); // すでに非表示のものも例外を出さずそのまま

            V6UiMode.HideLegacyV5Canvases();

            Assert.IsFalse(v5Canvas.activeSelf, "v6 モードで V5 Canvas が残ると v6 UI に重なる");
            Assert.IsFalse(v5Root.activeSelf);
            Assert.IsFalse(inactiveV5.activeSelf);
        }

        [Test]
        public static void HideLegacyV5Canvases_KeepsV6Objects()
        {
            var v6Canvas = new GameObject("V6 Select Canvas");
            var v6Root = new GameObject("V6SelectView");
            var nonV5 = new GameObject("Canvas");

            V6UiMode.HideLegacyV5Canvases();

            Assert.IsTrue(v6Canvas.activeSelf, "v6 自身のオブジェクトは隠さない");
            Assert.IsTrue(v6Root.activeSelf);
            Assert.IsTrue(nonV5.activeSelf, "V5 で始まらない名前には触らない");
        }

        [Test]
        public static void HideLegacyV5Canvases_IsIdempotent()
        {
            var v5 = new GameObject("V5 Option Canvas");

            Assert.IsTrue(V6UiMode.HideLegacyV5Canvases());
            Assert.IsTrue(V6UiMode.HideLegacyV5Canvases(), "毎フレーム呼んでよい (冪等)");
            Assert.IsFalse(v5.activeSelf);
        }
    }
}
