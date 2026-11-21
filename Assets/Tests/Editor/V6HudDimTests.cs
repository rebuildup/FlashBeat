using System.Reflection;
using FlashBeat.UI.V6.Game.Hud;
using NUnit.Framework;
using UnityEngine;

namespace FlashBeat.Tests
{
    /// <summary>
    /// Issue #735 (再発): HUD 内蔵の "Video Dim" (黒 α≈0.5 の全画面 Image) が
    /// 解像度・プロファイル変更による <see cref="V6GameHudView"/> の Rebuild で
    /// 復活し、ノーツを灰色に沈めないことを固定する回帰テスト。
    ///
    /// 出荷経路では減光レイヤーの正本は V6GameRuntime.BuildDimLayer の
    /// "V6GameDimCanvas" (V6UiMode.VideoDim が有効なときだけ) で、HUD 内蔵の
    /// 同名ノードは V6GameRuntime.DisableInternalDim が起動時に一度だけ切る。
    /// 以前は Rebuild() が子を全て作り直したあと再無効化されず、全画面の
    /// 黒 α≈0.5 が残り続けて「ノーツが白く見えない」状態になっていた。
    /// </summary>
    public class V6HudDimTests
    {
        [Test]
        public void InternalVideoDim_StaysDisabled_AcrossRebuild()
        {
            var canvasGo = new GameObject("V6HudDimTests Canvas", typeof(Canvas));
            try
            {
                var canvasRect = (RectTransform)canvasGo.transform;
                canvasRect.sizeDelta = new Vector2(1920f, 1080f);

                var view = V6GameHudView.Create(canvasRect);
                Assert.NotNull(view, "V6GameHudView.Create がビューを返さない");

                // 生成直後から無効であること (DisableInternalDim の
                // 一度きりの無効化に依存しない)。
                AssertInternalDimInactive(view, "initial build");

                // 解像度プロファイル変更で Update() から呼ばれるのと同じ
                // Rebuild() を直接起こし、再生成後も無効のままであること。
                MethodInfo rebuild = typeof(V6GameHudView).GetMethod(
                    "Rebuild", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.NotNull(rebuild, "V6GameHudView.Rebuild が見つからない");
                rebuild.Invoke(view, null);

                AssertInternalDimInactive(view, "after Rebuild()");
            }
            finally
            {
                Object.DestroyImmediate(canvasGo);
            }
        }

        private static void AssertInternalDimInactive(V6GameHudView view, string label)
        {
            Transform dim = view.Root.Find("Video Dim");
            Assert.NotNull(dim,
                label + ": HUD 内蔵の \"Video Dim\" ノードが無い " +
                "(監査が参照するノードは残す必要がある)");
            Assert.IsFalse(dim.gameObject.activeSelf,
                label + ": \"Video Dim\" (黒 α≈0.5 全画面) が有効 — " +
                "ノーツと映像を半分に暗くする。出荷の減光は " +
                "V6GameDimCanvas (V6UiMode.VideoDim) が担う。");
        }
    }
}
