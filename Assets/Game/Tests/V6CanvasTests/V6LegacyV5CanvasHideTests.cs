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

    /// <summary>
    /// v6 Result で旧 (v5) Result の残骸が重なって描画される不具合の修正テスト。
    /// ResultScene.unity 実測: v5 由来のものに "V5" 以外の名前がある
    /// (シーン配置 "Canvas" / 3D の曲サムネイル Panel "スクリーン")。
    /// V6UiMode.HideLegacyResultUi がこれらを隠し、v6 自身 ("V6 ..." / v6 Canvas 配下) は
    /// 隠さないこと。
    /// </summary>
    public static class V6ResultLegacyHideTests
    {
        [Test]
        public static void HideLegacyResultUi_HidesNonV5LegacyObjects()
        {
            var legacyCanvas = new GameObject("Canvas");
            var legacyScreen = new GameObject("スクリーン");
            var v5Canvas = new GameObject("V5 Result Canvas");

            V6UiMode.HideLegacyResultUi();

            Assert.IsFalse(legacyCanvas.activeSelf, "シーン配置の旧 Canvas が残ると v6 Result に重なる");
            Assert.IsFalse(legacyScreen.activeSelf, "3D のサムネイル Panel が残ると v6 Result に重なる");
            Assert.IsFalse(v5Canvas.activeSelf, "V5 名の残骸も一緒に隠す");
        }

        [Test]
        public static void HideLegacyResultUi_KeepsV6Objects()
        {
            var v6Canvas = new GameObject("V6 Result Canvas");
            // IsUnderV6Canvas は Canvas コンポーネント経由で親をたどるので実物に合わせ付ける。
            v6Canvas.AddComponent<Canvas>();
            var v6View = new GameObject("V6 Result View");
            v6View.transform.SetParent(v6Canvas.transform);
            // v6 Canvas 配下に "Canvas" という名の子がいても v6 自身なので隠さない。
            var v6Child = new GameObject("Canvas");
            v6Child.transform.SetParent(v6Canvas.transform);
            var background = new GameObject("BackGround");

            V6UiMode.HideLegacyResultUi();

            Assert.IsTrue(v6Canvas.activeSelf, "v6 自身の Canvas は隠さない");
            Assert.IsTrue(v6View.activeSelf);
            Assert.IsTrue(v6Child.activeSelf, "v6 Canvas 配下のオブジェクトは隠さない");
            Assert.IsTrue(background.activeSelf, "BackGround は v6 Result も使うので隠さない");
        }

        [Test]
        public static void HideLegacyResultUi_IsIdempotent()
        {
            var legacyCanvas = new GameObject("Canvas");

            Assert.IsTrue(V6UiMode.HideLegacyResultUi());
            Assert.IsTrue(V6UiMode.HideLegacyResultUi(), "毎フレーム呼んでよい (冪等)");
            Assert.IsFalse(legacyCanvas.activeSelf);
        }
    }
}
