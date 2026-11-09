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
            var root = new GameObject("root", typeof(RectTransform));
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
            var root = new GameObject("root", typeof(RectTransform), typeof(CanvasGroup));
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

        [Test]
        public static void IsDrawn_Null_IsFalse()
        {
            Assert.IsFalse(GraphicVisibility.IsDrawn(null));
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
