using FlashBeat.UI;
using FlashBeat.UI.V5;
using NUnit.Framework;
using UnityEngine;

namespace FlashBeat.Tests
{
    /// <summary>
    /// Issue #400 / #740: GameScene の再生パイプライン状態サービス
    /// (<see cref="GamePlayPhaseService"/>) と、v6 表示への状態公開のテスト。
    ///
    /// 固定するのは:
    ///   - 状態判定は UI を生成しなくても動く (v6 のローディングが読める)
    ///   - v5 の View はもう存在しない (Issue #740 で削除) ため、
    ///     GameHudController は旧 UI の非表示と Raycaster 無効化だけを担う
    ///   - 旧歌詞 / ロード表示の名前判定は GameHudController が正本
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

        /// <summary>
        /// v5 の View は存在しない (Issue #740)。GameHudController が
        /// 旧 View を生成するコードを持たないことをソースで固定する
        /// (EditMode では Start が走らず SkipsV5ViewsForAudit は false のままなので、
        /// 実行時フラグではなく生成呼び出しの不在を見る)。
        /// </summary>
        [Test]
        public void GameHudController_DoesNotCreateV5Views()
        {
            string path = System.IO.Path.Combine(Application.dataPath,
                "Game/Scripts/UI/V5/GameHudRuntime.cs");
            string src = System.IO.File.ReadAllText(path);

            string[] deletedViews =
            {
                "GameReadyView", "GameLoadingView", "GameErrorView",
                "GameHudView", "PauseMenuView",
            };
            for (int i = 0; i < deletedViews.Length; i++)
            {
                Assert.IsFalse(src.Contains("AddComponent<" + deletedViews[i] + ">"),
                    deletedViews[i] + " を生成するコードが残っている");
            }
        }

        // --- 旧歌詞オブジェクト名 (旧 GameUiExclusivity から GameHudController へ移設) ---

        [Test]
        public void LegacyLyricObjectNames_CoverTheThreeLegacyLyricTexts()
        {
            // v6 の HUD と二重表示になる旧の歌詞オブジェクト 3 つ。
            Assert.IsTrue(GameHudController.IsLegacyLyricObjectName("QuestionText"));
            Assert.IsTrue(GameHudController.IsLegacyLyricObjectName("FuriganaText"));
            Assert.IsTrue(GameHudController.IsLegacyLyricObjectName("RomajiText"));
        }

        [Test]
        public void IsLegacyLyricObjectName_RejectsUnrelatedAndEmptyNames()
        {
            Assert.IsFalse(GameHudController.IsLegacyLyricObjectName("TextPanel"));
            Assert.IsFalse(GameHudController.IsLegacyLyricObjectName("V6GameHudCanvas"));
            Assert.IsFalse(GameHudController.IsLegacyLyricObjectName(""));
            Assert.IsFalse(GameHudController.IsLegacyLyricObjectName(null));
        }

        [Test]
        public void LegacyLyricObjectNames_AreUnique()
        {
            var names = GameHudController.LegacyLyricObjectNames;
            for (int i = 0; i < names.Length; i++)
            {
                for (int j = i + 1; j < names.Length; j++)
                {
                    Assert.AreNotEqual(names[i], names[j]);
                }
            }
        }
    }
}
