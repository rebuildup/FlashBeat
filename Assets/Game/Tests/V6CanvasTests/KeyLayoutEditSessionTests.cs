using System.Collections.Generic;
using System.Linq;
using FlashBeat.UI.V6.Option;
using NUnit.Framework;
using UnityEngine;

namespace FlashBeat.V6CanvasTests
{
    /// <summary>
    /// Key Layout (キー割当) の一連の操作を固定する:
    /// 表示 → 編集 → 確定 / キャンセル → 保存 (永続化) → シーン遷移 / 再起動後の復元。
    ///
    /// 正本:
    /// - KeyBindings (8 レーンの割当の正本。保存先 IStorage を注入できる)
    /// - KeyConfigFileStorage (設定ファイル + レガシー PlayerPrefs ミラー。
    ///   テストではインメモリへ差し替え、実ファイル / PlayerPrefs に触らない)
    /// - V6OptionView.CancelKeyConfigEdit (Option の F1 キャンセル:
    ///   保存済みの状態へ戻してからオーバーレイを閉じる)
    ///
    /// 確定は既存経路 (KeyConfigUI で Esc / F2 と閉じる → KeyBindings.Save())。
    /// 未保存の編集は static 状態に残る (シーン遷移では再読込しない)が、
    /// アプリ再起動 (起動時の読み込み) では保存値へ戻る — その仕様をここで固定する。
    ///
    /// キーを押して編集する入力経路 (listen モード) は EditMode から Input を
    /// 擬似的に押せないため、-autotest keyconfig (AutoTestKeyConfig) が実機で担保する。
    /// </summary>
    public class KeyLayoutEditSessionTests
    {
        /// <summary>インメモリ保存先 (KeyBindings / KeyActions で共有する型、インスタンスは分ける)。</summary>
        private sealed class FakeStorage : KeyBindings.IStorage, KeyActions.IStorage
        {
            public string Raw;

            public string LoadRaw()
            {
                return Raw;
            }

            public void SaveRaw(string json)
            {
                Raw = json;
            }
        }

        private FakeStorage lanes;
        private FakeStorage actions;

        [SetUp]
        public void SetUp()
        {
            lanes = new FakeStorage();
            actions = new FakeStorage();
            KeyBindings.SetStorage(lanes);
            KeyActions.SetStorage(actions);
            KeyBindings.Load(); // 空の保存先 → 既定値
            KeyActions.Load();
        }

        [TearDown]
        public void TearDown()
        {
            // 既定へ戻してから本物の保存先へ戻す (他テストへカスタム状態を残さない)。
            KeyBindings.ResetToDefaults(); // 保存先はまだインメモリ (実ファイルには書かない)
            KeyActions.ResetToDefaults();
            KeyBindings.SetStorage(null);
            KeyActions.SetStorage(null);
        }

        // --- 1. 変更 → 保存 → 読み直しで復元 (永続化と再起動後復元) ---

        [Test]
        public void ChangedLane_SavesAndComesBackAfterReload()
        {
            // 既定のキーバインドから 1 レーンを変更 (レーン 1 へ I を割り当て)。
            Assert.IsTrue(KeyBindings.SetLane(0, new[] { KeyCode.I }), "レーンの割り当てを変更できる");
            CollectionAssert.AreEqual(new[] { KeyCode.I }, KeyBindings.GetKeys(0), "変更が反映される");
            Assert.IsFalse(KeyBindings.GetKeys(5).Contains(KeyCode.I),
                "I は元のレーンから移動する (キーは最大 1 レーンのみ)");

            // 確定 = 保存。
            Assert.IsTrue(KeyBindings.Save(), "保存できる");
            string saved = lanes.Raw;
            Assert.IsNotNull(saved, "保存先 (設定ファイル相当) に書かれた");

            // 読み直し (アプリ再起動相当: 起動時に保存先から読み込む)。
            KeyBindings.Load();
            CollectionAssert.AreEqual(new[] { KeyCode.I }, KeyBindings.GetKeys(0), "読み直しで変更が復元される");
            Assert.IsFalse(KeyBindings.GetKeys(5).Contains(KeyCode.I), "移動元からは消えている");

            for (int lane = 0; lane < KeyBindings.LaneCount; lane++)
            {
                Assert.Greater(KeyBindings.GetKeys(lane).Length, 0,
                    "読み直し後もレーン " + lane + " に 1 個以上のキーがある");
            }
        }

        // --- 2. キャンセルで元に戻る / 保存値は変わらない ---

        [Test]
        public void Cancel_RestoresTheBindingsAndKeepsTheSavedValueUnchanged()
        {
            // 確定済みの既定を保存しておく。
            Assert.IsTrue(KeyBindings.Save(), "既定を保存できる");
            Assert.IsTrue(KeyActions.Save(), "メニュー操作も保存できる");
            string savedLanes = lanes.Raw;
            string savedActions = actions.Raw;

            // 編集 (未保存)。P をレーン 8 からレーン 1 へ移す。
            Assert.IsTrue(KeyBindings.ToggleKey(0, KeyCode.P), "編集できる");
            Assert.IsFalse(V6OptionView.IsDefaultKeyLayout(), "前提: カスタム配列になっている");

            // キャンセル (Option の F1 → V6OptionView.CancelKeyConfigEdit)。
            V6OptionView.CancelKeyConfigEdit();

            Assert.AreEqual(savedLanes, lanes.Raw, "キャンセル時に保存値が変わらない");
            Assert.AreEqual(savedActions, actions.Raw, "キャンセル時に保存値が変わらない");
            Assert.IsTrue(V6OptionView.IsDefaultKeyLayout(), "元の割当に戻る");
            for (int lane = 0; lane < KeyBindings.LaneCount; lane++)
            {
                CollectionAssert.AreEqual(KeyBindings.DefaultKeys[lane], KeyBindings.GetKeys(lane),
                    "レーン " + lane + " が元の既定へ戻る");
            }
        }

        // --- 3. 未保存の編集の扱い (シーン遷移で残る / 再起動で失われる仕様の固定) ---

        [Test]
        public void UnsavedEdits_SurviveASceneTransition_ButAreLostOnRestart()
        {
            Assert.IsTrue(KeyBindings.Save(), "既定を保存できる");
            string saved = lanes.Raw;

            Assert.IsTrue(KeyBindings.ToggleKey(0, KeyCode.P), "編集 (未保存)");

            // シーン遷移: KeyBindings は static 状態で、遷移時に再読込する経路が無い
            // (遷移相当として何も呼ばれない。保存も Load もしない)。
            Assert.IsTrue(KeyBindings.GetKeys(0).Contains(KeyCode.P),
                "未保存の編集はシーン遷移でも失われない (static 状態のまま)");
            Assert.AreEqual(saved, lanes.Raw, "未保存なので保存値は変わらない");

            // アプリ再起動: 起動時に保存先から読み直す (Load 相当)。
            KeyBindings.Load();
            Assert.IsFalse(KeyBindings.GetKeys(0).Contains(KeyCode.P),
                "再起動後は保存値へ戻り、未保存の編集は失われる");
            Assert.AreEqual(saved, lanes.Raw, "再起動でも保存値は変わらない");
            Assert.IsTrue(V6OptionView.IsDefaultKeyLayout(), "再起動後は既定配列");
        }

        // --- 4. 8 レーンすべてにキーが 1 個以上 (design のレーン 0..7) ---

        [Test]
        public void AllEightLanes_HaveAtLeastOneKey_AndShowUpInTheDesignKeyGrid()
        {
            Assert.AreEqual(8, KeyBindings.LaneCount, "design のレーン 0..7 (8 レーン)");

            for (int lane = 0; lane < KeyBindings.LaneCount; lane++)
            {
                Assert.Greater(KeyBindings.GetKeys(lane).Length, 0,
                    "レーン " + lane + " に 1 個以上のキーが割り当てられている");
            }

            // Option のプレビュー (design のキーグリッド) は V6OptionLogic.JisGridKeys に
            // 所属レーン番号を刻印する。全レーンの刻印が 1 個以上出る = 8 レーンすべてが
            // グリッド上で表示されること。
            var gridKeys = new HashSet<KeyCode>();
            for (int row = 0; row < V6OptionLogic.JisGridKeys.Length; row++)
            {
                KeyCode[] keys = V6OptionLogic.JisGridKeys[row];
                for (int i = 0; i < keys.Length; i++)
                {
                    gridKeys.Add(keys[i]);
                }
            }

            for (int lane = 0; lane < KeyBindings.LaneCount; lane++)
            {
                bool shown = false;
                KeyCode[] keys = KeyBindings.GetKeys(lane);
                for (int i = 0; i < keys.Length; i++)
                {
                    if (gridKeys.Contains(keys[i]))
                    {
                        shown = true;
                        break;
                    }
                }

                Assert.IsTrue(shown, "レーン " + lane + " の刻印が design のキーグリッドに 1 個以上ある");
            }

            // 変更 → 保存 → 読み直しの後も 8 レーンの制約は保たれる。
            Assert.IsTrue(KeyBindings.SetLane(0, new[] { KeyCode.I }), "編集できる");
            Assert.IsTrue(KeyBindings.Save(), "保存できる");
            KeyBindings.Load();
            for (int lane = 0; lane < KeyBindings.LaneCount; lane++)
            {
                Assert.Greater(KeyBindings.GetKeys(lane).Length, 0,
                    "保存・読み直し後もレーン " + lane + " が空にならない");
            }
        }
    }
}
