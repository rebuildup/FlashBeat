using NUnit.Framework;
using FlashBeat.UI.V5;
using UnityEngine;
using UnityEngine.UI;

namespace FlashBeat.Tests.Editor
{
    /// <summary>
    /// Issue #233: 旧 UI の残存検査が「実際に描かれているか」を見るようにする。
    ///
    /// Graphic.isActiveAndEnabled だけでは、祖先 CanvasGroup の alpha が 0 の
    /// オブジェクト (実行時生成の旧ロード表示など) を「見えている」と誤検出する。
    /// </summary>
    public class GraphicVisibilityTests
    {
        [Test]
        public static void IsDrawn_VisibleImage_IsTrue()
        {
            var root = new GameObject("root", typeof(RectTransform), typeof(Canvas));
            try
            {
                var go = new GameObject("img", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(root.transform, false);
                Assert.IsTrue(GraphicVisibility.IsDrawn(go.GetComponent<Image>()));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public static void IsDrawn_ImageUnderTransparentCanvasGroup_IsFalse()
        {
            // これが今回の誤検出の核心: alpha 0 の CanvasGroup の下にある
            // Graphic は「有効」だが画面には何も描かれていない。
            var root = new GameObject("root", typeof(RectTransform), typeof(CanvasGroup));
            try
            {
                var group = root.GetComponent<CanvasGroup>();
                group.alpha = 0f;
                var go = new GameObject("img", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(root.transform, false);
                Assert.IsFalse(GraphicVisibility.IsDrawn(go.GetComponent<Image>()));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public static void IsDrawn_ImageUnderVisibleCanvasGroup_IsTrue()
        {
            var root = new GameObject("root", typeof(RectTransform), typeof(CanvasGroup), typeof(Canvas));
            try
            {
                root.GetComponent<CanvasGroup>().alpha = 1f;
                var go = new GameObject("img", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(root.transform, false);
                Assert.IsTrue(GraphicVisibility.IsDrawn(go.GetComponent<Image>()));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public static void IsDrawn_InactiveObject_IsFalse()
        {
            var root = new GameObject("root", typeof(RectTransform));
            try
            {
                var go = new GameObject("img", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(root.transform, false);
                go.SetActive(false);
                Assert.IsFalse(GraphicVisibility.IsDrawn(go.GetComponent<Image>()));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// Canvas が 1 つも無い Graphic は uGUI の描画対象にならない
        /// (PR #263 レビュー)。祖先チェーンを辿って Canvas が見つからない
        /// 場合は「描かれていない」と判定する。
        /// </summary>
        [Test]
        public static void IsDrawn_ImageWithoutAnyCanvas_IsFalse()
        {
            var root = new GameObject("root", typeof(RectTransform));
            try
            {
                var go = new GameObject("img", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(root.transform, false);
                Assert.IsFalse(GraphicVisibility.IsDrawn(go.GetComponent<Image>()),
                    "A Graphic that is not under any Canvas is never rendered.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public static void IsDrawn_Null_IsFalse()
        {
            Assert.IsFalse(GraphicVisibility.IsDrawn(null));
        }

        /// <summary>
        /// 祖先 CanvasGroup の alpha は **掛け算** で合成する
        /// (PR #263 レビュー S4)。
        ///
        /// 親の alpha 0.5 と子の alpha 0.5 = 0.25 なので「片方だけ 0」
        /// 見る実装だと 0.5 x 0.5 でも見えたことになってしまう。
        /// </summary>
        [Test]
        public static void IsDrawn_HalfTransparentParentAndChild_IsTrue()
        {
            var root = new GameObject("root", typeof(RectTransform), typeof(CanvasGroup), typeof(Canvas));
            try
            {
                root.GetComponent<CanvasGroup>().alpha = 0.5f;

                var go = new GameObject("img", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
                go.transform.SetParent(root.transform, false);
                go.GetComponent<CanvasGroup>().alpha = 0.5f;

                Assert.IsTrue(GraphicVisibility.IsDrawn(go.GetComponent<Image>()),
                    "Two half transparent groups are still visible.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// 合成結果が透明なときは「描かれていない」。
        /// </summary>
        [Test]
        public static void IsDrawn_AlmostTransparentProduct_IsFalse()
        {
            var root = new GameObject("root", typeof(RectTransform), typeof(CanvasGroup));
            try
            {
                root.GetComponent<CanvasGroup>().alpha = 0.01f;

                var go = new GameObject("img", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
                go.transform.SetParent(root.transform, false);
                go.GetComponent<CanvasGroup>().alpha = 0.01f;

                Assert.IsFalse(GraphicVisibility.IsDrawn(go.GetComponent<Image>()),
                    "0.01 x 0.01 is invisible.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// 祖先の Canvas が無効なら、その下の UI は描画されない。
        /// </summary>
        [Test]
        public static void IsDrawn_ImageUnderDisabledCanvas_IsFalse()
        {
            var root = new GameObject("root", typeof(RectTransform), typeof(Canvas));
            try
            {
                var go = new GameObject("img", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(root.transform, false);

                var image = go.GetComponent<Image>();
                Assert.IsTrue(GraphicVisibility.IsDrawn(image));

                root.GetComponent<Canvas>().enabled = false;
                Assert.IsFalse(GraphicVisibility.IsDrawn(image),
                    "A graphic under a disabled canvas is not drawn.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// Graphic 自身の色が透明なら「描かれていない」とみなす。
        /// </summary>
        [Test]
        public static void IsDrawn_TransparentGraphic_IsFalse()
        {
            var root = new GameObject("root", typeof(RectTransform), typeof(Canvas));
            try
            {
                var go = new GameObject("img", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(root.transform, false);
                var image = go.GetComponent<Image>();

                image.color = new Color(1f, 1f, 1f, 0f);
                Assert.IsFalse(GraphicVisibility.IsDrawn(image),
                    "A fully transparent graphic is not visible.");

                image.color = new Color(1f, 1f, 1f, 1f);
                Assert.IsTrue(GraphicVisibility.IsDrawn(image));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public static void IsUnderNamedObject_FindsSelfAndAncestors()
        {
            var root = new GameObject("LoadingOverlay", typeof(RectTransform));
            try
            {
                var go = new GameObject("img", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(root.transform, false);
                var image = go.GetComponent<Image>();
                Assert.IsTrue(GraphicVisibility.IsUnderNamedObject(image, "LoadingOverlay"));
                Assert.IsTrue(GraphicVisibility.IsUnderNamedObject(image, "img"));
                Assert.IsFalse(GraphicVisibility.IsUnderNamedObject(image, "SomethingElse"));
                Assert.IsFalse(GraphicVisibility.IsUnderNamedObject(image, ""));
                Assert.IsFalse(GraphicVisibility.IsUnderNamedObject(null, "LoadingOverlay"));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
