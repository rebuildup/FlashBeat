using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using FlashBeat.UI.V6.Result;

namespace FlashBeat.Tests.Editor
{
    /// <summary>
    /// Result 画面の背景: v6 Result View は画面全体を不透明パネルで覆わない
    /// (フレームの外側はゲーム自身のアニメーション背景 bg-anim3 が敷く。
    /// V6Frame.cs の規約。Title / Select と同じ)。全面を広げる Image は
    /// 薄いスクリーン ($scrim #00000099) のみで、可読性はそれで確保する。
    /// </summary>
    public class V6ResultBackgroundTests
    {
        [Test]
        public void Root_HasNoFullScreenOpaqueImage()
        {
            var parent = new GameObject("Parent", typeof(RectTransform));
            V6ResultView view = null;
            try
            {
                view = V6ResultView.Create(parent.GetComponent<RectTransform>(), () => { }, () => { }, () => { });
                var root = view.Root.gameObject;

                // 旧 "V6 Result Background" (全面 $k1 不透明) は作らない。
                Assert.IsNull(root.transform.Find("V6 Result Background"));

                // 全面に広がる Image は不透明にしない (scrim #00000099 のみ許容)。
                foreach (var image in root.GetComponentsInChildren<Image>(true))
                {
                    var rt = image.rectTransform;
                    bool fillsParent = rt.anchorMin == Vector2.zero && rt.anchorMax == Vector2.one;
                    if (fillsParent)
                    {
                        Assert.Less(image.color.a, 1f,
                            image.name + ": 全面に広がる Image は不透明にしない (ゲームのアニメーション背景を潰す)");
                    }
                }
            }
            finally
            {
                if (view != null)
                {
                    view.Dispose();
                }

                UnityEngine.Object.DestroyImmediate(parent);
            }
        }
    }
}
