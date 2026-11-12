using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace FlashBeat.UI.V6.Tests
{
    /// <summary>
    /// v6 モードで旧 (v5) の Canvas が重なって描画される不具合の修正テスト。
    ///
    /// Issue #396 で方式を変えた: 以前は「"V5" で始まる名前の GameObject」を
    /// 毎フレームシーン全体から列挙して隠していた。いまは v5 が
    /// <see cref="V5GameUi.NewCanvas"/> で作った Canvas を登録し、その参照だけを
    /// 隠す。したがって判定基準は「名前が V5 で始まるか」ではなく
    /// **「v5 のファクトリが作ったか」** になる。
    /// </summary>
    public static class V6LegacyV5CanvasHideTests
    {
        private static Canvas NewV5Canvas(string name)
        {
            return V6UiMode.CreateLegacyCanvasForAudit(name);
        }

        [TearDown]
        public static void TearDown()
        {
            V6UiMode.ResetLegacyHidingForAudit();
        }

        [Test]
        public static void HidesTheCanvasesV5Created()
        {
            Canvas title = NewV5Canvas("V5 Title Canvas");
            Canvas select = NewV5Canvas("V5 Select View");
            Canvas result = NewV5Canvas("V5 Result Canvas");
            result.gameObject.SetActive(false); // すでに非表示のものも例外を出さない

            V6UiMode.HideLegacyV5Canvases();

            Assert.IsFalse(title.gameObject.activeSelf, "v6 モードで V5 Canvas が残ると v6 UI に重なる");
            Assert.IsFalse(select.gameObject.activeSelf);
            Assert.IsFalse(result.gameObject.activeSelf);
        }

        [Test]
        public static void KeepsV6AndNonRegisteredObjects()
        {
            var v6Canvas = new GameObject("V6 Select Canvas");
            var v6Root = new GameObject("V6SelectView");
            var plainV5Named = new GameObject("V5 NotFromTheFactory");
            var nonV5 = new GameObject("Canvas");

            V6UiMode.HideLegacyV5Canvases();

            Assert.IsTrue(v6Canvas.activeSelf, "v6 自身のオブジェクトは隠さない");
            Assert.IsTrue(v6Root.activeSelf);
            Assert.IsTrue(plainV5Named.activeSelf,
                "v5 のファクトリを通っていない名前だけのオブジェクトは触らない (#396)");
            Assert.IsTrue(nonV5.activeSelf, "V5 で始まらない名前には触らない");
        }

        [Test]
        public static void IsIdempotent()
        {
            Canvas v5 = NewV5Canvas("V5 Option Canvas");

            Assert.IsTrue(V6UiMode.HideLegacyV5Canvases());
            Assert.IsTrue(V6UiMode.HideLegacyV5Canvases(), "毎フレーム呼んでよい (冪等)");
            Assert.IsFalse(v5.gameObject.activeSelf);
        }

        [Test]
        public static void LateCreatedCanvasIsHiddenOnTheNextCall()
        {
            // v5 が後から Canvas を作る経路 (Start/OnEnable) を再現する。
            V6UiMode.HideLegacyV5Canvases();
            Canvas late = NewV5Canvas("V5 Late Canvas");

            V6UiMode.HideLegacyV5Canvases();

            Assert.IsFalse(late.gameObject.activeSelf,
                "後から生成された v5 Canvas も次の呼び出しで隠れる");
        }

        [Test]
        public static void TheOnceVariantStillSweepsByPrefix()
        {
            // 登録を通らない旧オブジェクトへの保険 (起動時の 1 回だけ)。
            var stray = new GameObject("V5 Legacy Leftover");
            Canvas registered = NewV5Canvas("V5 Title Canvas");

            V6UiMode.HideLegacyV5CanvasesOnce();

            Assert.IsFalse(stray.activeSelf, "接頭辞の走査は起動時の掃除として残す");
            Assert.IsFalse(registered.gameObject.activeSelf);
        }

        [Test]
        public static void DestroyedCanvasesDoNotThrow()
        {
            Canvas temp = NewV5Canvas("V5 Temp Canvas");
            Object.DestroyImmediate(temp.gameObject);

            Assert.DoesNotThrow(() => V6UiMode.HideLegacyV5Canvases(),
                "破棄済みの登録は偽 null として飛ばす");
        }
    }
}
