using FlashBeat.UI;
using FlashBeat.UI.V5;
using FlashBeat.UI.V6.Game.Pause;
using FlashBeat.UI.V6;
using NUnit.Framework;
using UnityEngine;

namespace FlashBeat.Tests
{
    /// <summary>
    /// Issue #400: 状態検出 (v6 も使う) と、v5 UI の生成 (v6 では不要) を分ける。
    ///
    /// 一次監査では v6 有効でも `V5ReadyCanvas` / `V5OverlayCanvas` / `V5PauseCanvas`
    /// を生成し、`V6GameRuntime` が毎フレーム隠していた。ここで固定するのは:
    ///   - 状態判定は UI を生成しなくても動く (v6 のローディングが読める)
    ///   - v6 有効時は v5 の View を生成しない
    ///   - v6 ポーズが無効なときは v5 ポーズだけは残す (未移植分の役割)
    ///   - v5 ポーズ探索を毎フレーム続けない
    /// </summary>
    public class GamePlayPhaseServiceTests
    {
        [SetUp]
        public void SetUp()
        {
            GamePlayPhaseService.ResetForAudit();
        }

        [TearDown]
        public void TearDown()
        {
            GamePlayPhaseService.ResetForAudit();
            GameSceneManager.ResetPauseLookupForAudit();
        }

        [Test]
        public void LastPhase_IsReadableWithoutAnyV5View()
        {
            // v6 のローディング (V6GameRuntime.UpdateLoading) はこの値だけを読む。
            // UI を 1 つも作らずに状態を公開できることが、責務分離の要件。
            GamePlayPhaseService.SetPhaseForAudit(HudPlayPhase.Preparing);
            Assert.AreEqual(HudPlayPhase.Preparing, GamePlayPhaseService.LastPhase);
        }

        [Test]
        public void LastPhase_IsForwardedByTheLegacyAccessor()
        {
            // 既存の読み手 (V6GameRuntime) との互換。実体を 1 つにする。
            GamePlayPhaseService.SetPhaseForAudit(HudPlayPhase.Failed);
            Assert.AreEqual(HudPlayPhase.Failed, GameHudController.LastPhase);
        }

        [Test]
        public void ResetForAudit_ClearsTheObservedState()
        {
            GamePlayPhaseService.SetPhaseForAudit(HudPlayPhase.Failed);
            GamePlayPhaseService.ResetForAudit();

            Assert.AreEqual(HudPlayPhase.Hidden, GamePlayPhaseService.LastPhase);
            Assert.IsFalse(GamePlayPhaseService.LegacyMediaLoading);
        }

        [Test]
        public void PhaseResolution_StillSeparatesLoadingReadyAndPlaying()
        {
            // 分離してもフェーズ判定の意味は変えない (回帰防止)。
            Assert.AreEqual(GameUiPhase.Loading,
                GameUiExclusivity.ResolvePhase(HudPlayPhase.Preparing, false, false));
            Assert.AreEqual(GameUiPhase.Failed,
                GameUiExclusivity.ResolvePhase(HudPlayPhase.Failed, false, false));
            Assert.AreEqual(GameUiPhase.Ready,
                GameUiExclusivity.ResolvePhase(HudPlayPhase.Hidden, false, false));
            Assert.AreEqual(GameUiPhase.Playing,
                GameUiExclusivity.ResolvePhase(HudPlayPhase.Hidden, true, false));
            Assert.AreEqual(GameUiPhase.Paused,
                GameUiExclusivity.ResolvePhase(HudPlayPhase.Hidden, true, true));
        }

        /// <summary>
        /// v6 有効時は v5 の View を作らないことを、生成側の構造で固定する。
        /// (作ると V6GameRuntime が毎フレーム隠す二重構造に戻る。)
        /// </summary>
        [Test]
        public void GameHudController_DoesNotCreateV5ViewsWhenV6OwnsTheScreen()
        {
            string path = System.IO.Path.Combine(Application.dataPath,
                "Game/Scripts/UI/V5/GameHudRuntime.cs");
            string src = System.IO.File.ReadAllText(path);

            // Ready / Loading / Error の生成が v6Hud 判定で囲われていること。
            // 生成呼び出しそのもの (NewCanvas) を目印にする。コメント中の名前と
            // 取り違えないため。
            int readyGuard = src.IndexOf("if (!v6Hud)", System.StringComparison.Ordinal);
            Assert.Greater(readyGuard, 0, "v5 の View 生成が v6Hud で分岐していない");

            int readyCanvas = src.IndexOf("NewCanvas(\"V5ReadyCanvas\"", System.StringComparison.Ordinal);
            Assert.Greater(readyCanvas, readyGuard,
                "V5ReadyCanvas を v6 有効時にも作っている");

            int overlayCanvas = src.IndexOf("NewCanvas(\"V5OverlayCanvas\"", System.StringComparison.Ordinal);
            Assert.Greater(overlayCanvas, readyGuard,
                "V5OverlayCanvas を v6 有効時にも作っている");
        }

        /// <summary>
        /// v6 ポーズが無効なときは v5 ポーズの役割を残す (未移植分を壊さない)。
        /// </summary>
        [Test]
        public void V5PauseIsStillCreatedWhenV6PauseIsDisabled()
        {
            string path = System.IO.Path.Combine(Application.dataPath,
                "Game/Scripts/UI/V5/GameHudRuntime.cs");
            string src = System.IO.File.ReadAllText(path);

            Assert.IsTrue(src.Contains("if (!v6Hud || !v6Pause)"),
                "v6 ポーズ無効時に v5 ポーズを作る条件が消えている");
        }

        /// <summary>
        /// v5 ポーズ探索を毎フレーム続けない (v6 構成では存在しないため)。
        /// </summary>
        [Test]
        public void PauseLookupDoesNotScanEveryFrameInV6Mode()
        {
            string path = System.IO.Path.Combine(Application.dataPath,
                "Game/Scripts/UI/GameSceneManager.cs");
            string src = System.IO.File.ReadAllText(path);

            Assert.IsTrue(src.Contains("v5PauseLookupExhausted"),
                "探索の打ち切りが無い (毎フレーム FindFirstObjectByType になる)");
            Assert.IsTrue(src.Contains("V5PauseLookupFrames"),
                "探索の上限フレーム数が無い");
        }

        [Test]
        public void LegacyBackOpensMenu_KeepsItsContract()
        {
            Assert.IsTrue(GameSceneManager.LegacyBackOpensMenu(false, false),
                "どちらのポーズも無ければレガシーが Esc を担う");
            Assert.IsFalse(GameSceneManager.LegacyBackOpensMenu(true, false),
                "v5 ポーズがあるのにレガシーも開くと二重");
            Assert.IsFalse(GameSceneManager.LegacyBackOpensMenu(false, true),
                "v6 ポーズがあるのにレガシーも開くと二重");
        }
    }
}
