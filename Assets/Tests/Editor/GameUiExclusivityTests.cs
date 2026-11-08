using NUnit.Framework;
using FlashBeat.UI;
using FlashBeat.UI.V5;
using UnityEngine;
using UnityEngine.UI;

namespace FlashBeat.Tests.Editor
{
    /// <summary>
    /// Issue #233: GameScene UI の排他制御 (GameUiExclusivity) のテスト。
    ///
    /// - フェーズ判定 (ResolvePhase) と表示決定 (Resolve) は純粋関数なので
    ///   v5 / v6 両モードの行列を直接検証する。
    /// - Apply は実際の v5 View を EditMode で生成して検証する
    ///   (名前ベースの 1 回 SetActive ではなく、状態から再適用できること)。
    /// </summary>
    public class GameUiExclusivityTests
    {
        // --- ResolvePhase: 再生パイプライン → フェーズ ---

        [Test]
        public static void ResolvePhase_FailedPipeline_IsFailed()
        {
            Assert.AreEqual(GameUiPhase.Failed,
                GameUiExclusivity.ResolvePhase(HudPlayPhase.Failed, false, false));
        }

        [Test]
        public static void ResolvePhase_PreparingPipeline_IsLoading()
        {
            Assert.AreEqual(GameUiPhase.Loading,
                GameUiExclusivity.ResolvePhase(HudPlayPhase.Preparing, false, false));
        }

        [Test]
        public static void ResolvePhase_BeforePlayback_IsReady()
        {
            Assert.AreEqual(GameUiPhase.Ready,
                GameUiExclusivity.ResolvePhase(HudPlayPhase.Hidden, false, false));
        }

        [Test]
        public static void ResolvePhase_PlaybackStarted_TogglesPlayingAndPaused()
        {
            Assert.AreEqual(GameUiPhase.Playing,
                GameUiExclusivity.ResolvePhase(HudPlayPhase.Hidden, true, false));
            Assert.AreEqual(GameUiPhase.Paused,
                GameUiExclusivity.ResolvePhase(HudPlayPhase.Hidden, true, true));
        }

        // --- Resolve (v5 モード): フェーズごとに 1 枚の主表示 ---

        [Test]
        public static void Resolve_V5_Ready_ShowsOnlyReady()
        {
            var vis = GameUiExclusivity.Resolve(GameUiPhase.Ready, DefaultV5(), false);
            Assert.IsTrue(vis.Ready);
            Assert.IsFalse(vis.Hud, "Ready 中に HUD を出してはならない (Issue #233 の二重表示)。");
            Assert.IsFalse(vis.Loading);
            Assert.IsFalse(vis.Error);
            Assert.IsFalse(vis.Pause);
        }

        [Test]
        public static void Resolve_V5_Playing_ShowsHud_AndPauseOverIt()
        {
            var vis = GameUiExclusivity.Resolve(GameUiPhase.Playing, DefaultV5(), true);
            Assert.IsTrue(vis.Hud);
            Assert.IsTrue(vis.Pause, "ポーズは HUD の上に重ねる (別 Canvasなので両方可視が正)。");
            Assert.IsFalse(vis.Ready);
        }

        [Test]
        public static void Resolve_V5_Paused_ShowsHudAndPause()
        {
            var vis = GameUiExclusivity.Resolve(GameUiPhase.Paused, DefaultV5(), true);
            Assert.IsTrue(vis.Hud);
            Assert.IsTrue(vis.Pause);
            Assert.IsFalse(vis.Ready);
            Assert.IsFalse(vis.Loading);
            Assert.IsFalse(vis.Error);
        }

        [Test]
        public static void Resolve_V5_Loading_And_Failed_AreExclusiveOverlays()
        {
            var loading = GameUiExclusivity.Resolve(GameUiPhase.Loading, DefaultV5(), true);
            Assert.IsTrue(loading.Loading);
            Assert.IsFalse(loading.Pause, "ロード中はポーズを開けない。");
            Assert.IsFalse(loading.Hud);
            Assert.IsFalse(loading.Ready);

            var failed = GameUiExclusivity.Resolve(GameUiPhase.Failed, DefaultV5(), true);
            Assert.IsTrue(failed.Error);
            Assert.IsFalse(failed.Pause, "エラー中はポーズを開けない (Esc は選曲への導線)。");
            Assert.IsFalse(failed.Loading);
            Assert.IsFalse(failed.Hud);
            Assert.IsFalse(failed.Ready);
        }

        /// <summary>主表示 (Ready / Hud / Loading / Error) は全フェーズで高々 1 枚。</summary>
        [Test]
        public static void Resolve_V5_AtMostOnePrimaryView_PerPhase()
        {
            var phases = new[]
            {
                GameUiPhase.Loading, GameUiPhase.Ready, GameUiPhase.Playing,
                GameUiPhase.Paused, GameUiPhase.Failed,
            };
            for (int i = 0; i < phases.Length; i++)
            {
                var vis = GameUiExclusivity.Resolve(phases[i], DefaultV5(), false);
                int primary = (vis.Ready ? 1 : 0) + (vis.Hud ? 1 : 0)
                    + (vis.Loading ? 1 : 0) + (vis.Error ? 1 : 0);
                Assert.IsTrue(primary <= 1,
                    phases[i] + " で主表示が " + primary + " 枚 (排他違反)。");
            }
        }

        // --- Resolve (v6 モード): v6 が表示を所有する ---

        [Test]
        public static void Resolve_V6_HidesAllV5Views_InEveryPhase()
        {
            var phases = new[]
            {
                GameUiPhase.Loading, GameUiPhase.Ready, GameUiPhase.Playing,
                GameUiPhase.Paused, GameUiPhase.Failed,
            };
            var v6 = new V6UiFlags { GameHud = true, PauseEnabled = true };
            for (int i = 0; i < phases.Length; i++)
            {
                var vis = GameUiExclusivity.Resolve(phases[i], v6, false);
                Assert.IsFalse(vis.Ready, phases[i] + ": v6 モードで V5ReadyCanvas が残っている。");
                Assert.IsFalse(vis.Hud, phases[i] + ": v6 モードで v5 HUD が残っている。");
                Assert.IsFalse(vis.Loading, phases[i] + ": v6 モードで v5 ローディングが残っている。");
                Assert.IsFalse(vis.Error, phases[i] + ": v6 モードで v5 エラーが残っている。");
                Assert.IsFalse(vis.Pause, phases[i] + ": v6 ポーズ有効時に v5 ポーズが残っている。");
            }
        }

        [Test]
        public static void Resolve_V6_PauseDisabled_FallsBackToV5Pause()
        {
            var v6 = new V6UiFlags { GameHud = true, PauseEnabled = false };
            var vis = GameUiExclusivity.Resolve(GameUiPhase.Playing, v6, true);
            Assert.IsTrue(vis.Pause, "v6 ポーズ無効時は v5 ポーズが役割を持つ。");

            var closed = GameUiExclusivity.Resolve(GameUiPhase.Playing, v6, false);
            Assert.IsFalse(closed.Pause);

            var failed = GameUiExclusivity.Resolve(GameUiPhase.Failed, v6, true);
            Assert.IsFalse(failed.Pause, "エラー中は v5 ポーズも開かない。");
        }

        // --- Apply: 実際の v5 View を生成して状態から再適用できること ---

        [Test]
        public static void Apply_SwapsReadyAndHud_AndReappliesIdempotently()
        {
            var readyCanvas = NewTestCanvas("V5ReadyCanvas Test", 550);
            var overlayCanvas = NewTestCanvas("V5OverlayCanvas Test", 500);
            var pauseCanvas = NewTestCanvas("V5PauseCanvas Test", 600);

            var ready = readyCanvas.gameObject.AddComponent<GameReadyView>();
            ready.Build(readyCanvas);
            var hudGo = new GameObject("V5GameHud Test");
            var hud = hudGo.AddComponent<GameHudView>();
            hud.Build();
            var loading = overlayCanvas.gameObject.AddComponent<GameLoadingView>();
            loading.Build(overlayCanvas);
            var error = overlayCanvas.gameObject.AddComponent<GameErrorView>();
            error.Build(overlayCanvas);
            var pause = pauseCanvas.gameObject.AddComponent<PauseMenuView>();
            pause.Build(pauseCanvas);

            try
            {
                // Ready 中: Ready だけ出る (HUD は隠れる)。
                GameUiExclusivity.Apply(
                    GameUiExclusivity.Resolve(GameUiPhase.Ready, DefaultV5(), false),
                    ready, hud, loading, error, pause);
                Assert.IsTrue(readyCanvas.gameObject.activeSelf, "Ready 中なのに Ready が見えていない。");
                Assert.IsFalse(hud.Canvas.gameObject.activeSelf, "Ready 中に HUD が出ている (Issue #233)。");
                Assert.IsFalse(error.IsShown);
                Assert.IsFalse(pause.IsOpen);

                // 同じ決定をもう 1 回適用しても同じ状態 (再適用可能 / 冪等)。
                GameUiExclusivity.Apply(
                    GameUiExclusivity.Resolve(GameUiPhase.Ready, DefaultV5(), false),
                    ready, hud, loading, error, pause);
                Assert.IsTrue(readyCanvas.gameObject.activeSelf);
                Assert.IsFalse(hud.Canvas.gameObject.activeSelf);

                // 再生中: Ready が隠れ HUD が出る。
                GameUiExclusivity.Apply(
                    GameUiExclusivity.Resolve(GameUiPhase.Playing, DefaultV5(), false),
                    ready, hud, loading, error, pause);
                Assert.IsFalse(readyCanvas.gameObject.activeSelf, "再生中なのに Ready が残っている。");
                Assert.IsTrue(hud.Canvas.gameObject.activeSelf, "再生中なのに HUD が出ていない。");
                Assert.IsFalse(pause.IsOpen);

                // ポーズ: HUD を出したままポーズを重ねる。
                GameUiExclusivity.Apply(
                    GameUiExclusivity.Resolve(GameUiPhase.Paused, DefaultV5(), true),
                    ready, hud, loading, error, pause);
                Assert.IsTrue(hud.Canvas.gameObject.activeSelf, "ポーズ中に HUD が隠れた。");
                Assert.IsTrue(pause.IsOpen, "ポーズ中なのにポーズメニューが出ていない。");

                // ポーズを閉じると HUD だけに戻る (状態からの再適用)。
                GameUiExclusivity.Apply(
                    GameUiExclusivity.Resolve(GameUiPhase.Playing, DefaultV5(), false),
                    ready, hud, loading, error, pause);
                Assert.IsTrue(hud.Canvas.gameObject.activeSelf);
                Assert.IsFalse(pause.IsOpen);
            }
            finally
            {
                Object.DestroyImmediate(readyCanvas.gameObject);
                Object.DestroyImmediate(overlayCanvas.gameObject);
                Object.DestroyImmediate(pauseCanvas.gameObject);
                Object.DestroyImmediate(hud.Canvas.gameObject);
                Object.DestroyImmediate(hudGo);
            }
        }

        [Test]
        public static void Apply_NullViews_AreSkipped()
        {
            // v6 モードでは GameHudView を作らない (null)。Apply が null を許すこと。
            Assert.DoesNotThrow(() =>
                GameUiExclusivity.Apply(
                    GameUiExclusivity.Resolve(GameUiPhase.Playing, new V6UiFlags { GameHud = true }, false),
                    null, null, null, null, null));
        }

        // --- ヘルパ ---

        private static V6UiFlags DefaultV5()
        {
            return default(V6UiFlags); // GameHud = false
        }

        private static Canvas NewTestCanvas(string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            return canvas;
        }
    }
}
