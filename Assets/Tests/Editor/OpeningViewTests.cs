using FlashBeat.UI.V5;
using NUnit.Framework;
using UnityEngine;

namespace FlashBeat.Tests.Editor
{
    /// <summary>
    /// Issue #371: Opening はオープニング映像だけを表示する。
    /// 映像がない / デコードできない場合だけ背景 + ロゴへフォールバックする。
    /// </summary>
    public class OpeningViewTests
    {
        [TearDown]
        public void TearDown()
        {
            Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            for (int i = 0; i < canvases.Length; i++)
            {
                if (canvases[i].name == "V5 Opening Canvas")
                {
                    Object.DestroyImmediate(canvases[i].gameObject);
                }
            }
        }

        [Test]
        public void VideoOnlyBuild_DrawsNoLogoOrEdgeDecoration()
        {
            OpeningView view = OpeningView.Build(withBackdrop: false);

            Assert.IsFalse(view.HasDecorations, "映像のみのときは装飾を生成しない");
            Assert.IsNull(FindChild(view.transform, "Decorations"), "エッジ装飾・ロゴのまとまりが無い");
            Assert.IsNull(FindChild(view.transform, "EdgeDeco"), "エッジ装飾を描かない");
        }

        [Test]
        public void Fallback_AddsTheBackdropAndTheDecorations()
        {
            OpeningView view = OpeningView.Build(withBackdrop: false);

            view.ShowBackdropFallback();

            Assert.IsTrue(view.HasDecorations, "フォールバックで装飾を足す (黒画面にしない)");
            GameObject decorations = FindChild(view.transform, "Decorations");
            Assert.IsNotNull(decorations);
            Assert.IsTrue(decorations.activeSelf);
        }

        [Test]
        public void BuildWithBackdrop_KeepsTheLegacyLook()
        {
            OpeningView view = OpeningView.Build();

            Assert.IsTrue(view.HasDecorations);
        }

        [Test]
        public void AutoAdvanceDelay_IsUnchanged()
        {
            Assert.AreEqual(OpeningView.BaseAdvanceSeconds,
                OpeningView.ResolveAutoAdvanceDelay(false, 0f), 0.001f);
            Assert.AreEqual(OpeningView.MaxAdvanceSeconds, OpeningView.ResolveAutoAdvanceDelay(true, 30f), 0.001f,
                "長い映像でも上限 (12 秒) で止まる");
            Assert.AreEqual(OpeningView.BaseAdvanceSeconds,
                OpeningView.ResolveAutoAdvanceDelay(true, 1f), 0.001f);
        }

        private static GameObject FindChild(Transform root, string name)
        {
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name == name)
                {
                    return all[i].gameObject;
                }
            }

            return null;
        }
    }
}
