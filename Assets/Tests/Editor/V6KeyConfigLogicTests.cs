using System;
using FlashBeat.UI.V6.Common;
using NUnit.Framework;
using UnityEngine;

namespace FlashBeat.Tests.Editor
{
    /// <summary>
    /// Issue #376: キー設定モーダル (design vo4kB) の編集モデルと、その保存経路の検証。
    ///
    /// - 代表キーの差し替え・残りキーの保持
    /// - 他レーンが使用中のキーは確定しない (衝突先を表示する)
    /// - 予約キー/修飾キーは受け付けない
    /// - 「既定に戻す」は保存せず、「適用」だけが KeyBindings と保存先へ書く
    ///
    /// モーダルは KeyCode を直接読むため、入力検出 (Update) ではなく
    /// 押されたキーを渡すモデル (<see cref="V6KeyConfigLogic"/>) を固定する。
    /// </summary>
    public class V6KeyConfigLogicTests
    {
        private KeyBindings.IStorage previousStorage;
        private MemoryStorage storage;

        private sealed class MemoryStorage : KeyBindings.IStorage
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

        [SetUp]
        public void SetUp()
        {
            previousStorage = null;
            storage = new MemoryStorage();
            KeyBindings.SetStorage(storage);
        }

        [TearDown]
        public void TearDown()
        {
            KeyBindings.SetStorage(previousStorage);
        }

        private static KeyCode[][] Defaults()
        {
            var copy = new KeyCode[KeyBindings.DefaultKeys.Length][];
            for (int lane = 0; lane < copy.Length; lane++)
            {
                copy[lane] = (KeyCode[])KeyBindings.DefaultKeys[lane].Clone();
            }

            return copy;
        }

        // --- 代表キーの差し替え ---

        [Test]
        public void Assign_ReplacesTheRepresentative_AndKeepsTheOtherKeys()
        {
            var logic = new V6KeyConfigLogic(Defaults());
            logic.Select(0); // Lane 1 = 1 / Q / A / Z
            logic.BeginListen();

            V6KeyAssignResult result = logic.Assign(KeyCode.Home);

            Assert.AreEqual(V6KeyAssignResult.Assigned, result);
            Assert.IsFalse(logic.Listening, "確定すると待受は終わる");
            Assert.AreEqual(KeyCode.Home, logic.Representative(0), "押したキーが代表キーになる");
            Assert.That(logic.Alternates(0), Has.Member(KeyCode.Q), "残りのキーは保持する");
            Assert.That(logic.Alternates(0), Has.Member(KeyCode.A));
            Assert.That(logic.Alternates(0), Has.Member(KeyCode.Z));
            Assert.That(logic.Alternates(0), Has.Member(KeyCode.Alpha1),
                "旧代表キーは残りキーへ降りる (割り当てを失わない)");
            Assert.That(logic.Alternates(0), Has.No.Member(KeyCode.Home));
        }

        [Test]
        public void Assign_PromotesAnExistingAlternate_WithoutDuplicatingIt()
        {
            var logic = new V6KeyConfigLogic(Defaults());
            logic.Select(0);
            logic.BeginListen();

            Assert.AreEqual(V6KeyAssignResult.Assigned, logic.Assign(KeyCode.A));

            Assert.AreEqual(KeyCode.A, logic.Representative(0));
            Assert.That(logic.Alternates(0), Has.No.Member(KeyCode.A), "同じキーを 2 つ持たない");
        }

        // --- 衝突と予約キー ---

        [Test]
        public void Assign_RefusesAKeyUsedByAnotherLane_AndKeepsListening()
        {
            var logic = new V6KeyConfigLogic(Defaults());
            logic.Select(0); // Lane 1
            logic.BeginListen();

            // Q は Lane 1 の残りキー、W は Lane 2 の代表キー。
            V6KeyAssignResult result = logic.Assign(KeyCode.W);

            Assert.AreEqual(V6KeyAssignResult.Conflict, result);
            Assert.AreEqual(1, logic.ConflictLane, "衝突先レーン (0 始まり) を返す");
            Assert.IsTrue(logic.Listening, "衝突では待受を続ける");
            Assert.AreEqual(KeyCode.Alpha1, logic.Representative(0), "衝突では割り当てを変えない");
            Assert.AreEqual("Lane 2 で使用中", logic.StateText(0), "衝突先をレーン番号で表示する");
        }

        [Test]
        public void Assign_RefusesReservedAndModifierKeys()
        {
            var logic = new V6KeyConfigLogic(Defaults());
            logic.Select(2);
            logic.BeginListen();

            Assert.AreEqual(V6KeyAssignResult.Reserved, logic.Assign(KeyCode.Escape));
            Assert.AreEqual(V6KeyAssignResult.Reserved, logic.Assign(KeyCode.Return));
            Assert.AreEqual(V6KeyAssignResult.Reserved, logic.Assign(KeyCode.F2), "F キーは使えない");
            Assert.AreEqual(V6KeyAssignResult.Reserved, logic.Assign(KeyCode.LeftShift),
                "修飾キー単体は代表キーにしない");
            Assert.AreEqual(V6KeyAssignResult.Reserved, logic.Assign(KeyCode.None));
            Assert.IsTrue(logic.Listening, "拒否しても待受は続く");
            Assert.AreEqual(V6KeyConfigLogic.ReservedText, logic.StateText(2));
        }

        [Test]
        public void Esc_CancelsListening_WithoutChangingTheTable()
        {
            var logic = new V6KeyConfigLogic(Defaults());
            logic.Select(0);
            logic.BeginListen();

            logic.CancelListen();

            Assert.IsFalse(logic.Listening);
            Assert.IsFalse(logic.HasChanges(), "待受の中止では編集内容を作らない");
        }

        // --- 既定復帰と適用 ---

        [Test]
        public void ResetToDefaults_RestoresTheTable_WithoutSaving()
        {
            var logic = new V6KeyConfigLogic(Defaults());
            logic.Select(4);
            logic.BeginListen();
            Assert.AreEqual(V6KeyAssignResult.Assigned, logic.Assign(KeyCode.End));
            Assert.IsTrue(logic.HasChanges());

            logic.ResetToDefaults();

            Assert.IsFalse(logic.HasChanges(), "既定表へ戻る");
            Assert.AreEqual(KeyCode.Alpha6, logic.Representative(4));
            Assert.IsNull(storage.Raw, "「既定に戻す」では保存しない (適用まで書かない)");
        }

        [Test]
        public void Commit_WritesEveryLane_AndSaves()
        {
            var logic = new V6KeyConfigLogic(Defaults());
            logic.Select(0);
            logic.BeginListen();
            logic.Assign(KeyCode.Home);
            logic.Select(1);
            logic.BeginListen();
            logic.Assign(KeyCode.End);

            Assert.IsTrue(logic.Commit());

            Assert.AreEqual(KeyCode.Home, KeyBindings.GetKeys(0)[0], "レーン 1 の代表キー");
            Assert.AreEqual(KeyCode.End, KeyBindings.GetKeys(1)[0], "レーン 2 の代表キー");
            Assert.IsNotNull(storage.Raw, "適用で保存先へ書き込む");
            StringAssert.Contains("Home", storage.Raw);
        }

        [Test]
        public void Commit_DoesNothingWhenThereAreNoChanges()
        {
            var logic = new V6KeyConfigLogic(Defaults());

            Assert.IsFalse(logic.HasChanges());
            Assert.IsTrue(logic.Commit(), "変更なしの適用も成功扱い (保存だけ行う)");
            Assert.AreEqual(KeyCode.Alpha1, KeyBindings.GetKeys(0)[0]);
        }

        // --- 表示テキスト ---

        [Test]
        public void RowTexts_FollowTheDesignStates()
        {
            var logic = new V6KeyConfigLogic(Defaults());

            Assert.AreEqual("変更", logic.StateText(1));
            logic.Select(1);
            Assert.AreEqual("Enter で変更", logic.StateText(1));
            logic.BeginListen();
            Assert.AreEqual(V6KeyConfigLogic.ListeningText, logic.StateText(1));
            Assert.AreEqual("変更", logic.StateText(3), "他の行は通常表示のまま");

            Assert.AreEqual("他: Q / A / Z", logic.AlternatesText(0));
        }

        [Test]
        public void Move_SelectsAcrossLanes_AndWraps()
        {
            var logic = new V6KeyConfigLogic(Defaults());
            Assert.AreEqual(0, logic.SelectedLane);

            logic.Move(-1);
            Assert.AreEqual(V6KeyConfigLogic.LaneCount - 1, logic.SelectedLane, "上端から下端へ折り返す");
            logic.Move(1);
            Assert.AreEqual(0, logic.SelectedLane);
        }

        // --- KeyBindings.SetAll (原子的な一括適用) ---

        [Test]
        public void SetAll_RejectsEmptyLanes_AndCleansDuplicates()
        {
            var table = Defaults();
            table[3] = new KeyCode[0];
            Assert.IsFalse(KeyBindings.SetAll(table), "空レーンは拒否する");
            Assert.AreEqual(KeyCode.Alpha1, KeyBindings.GetKeys(0)[0], "拒否時は現状維持");

            table = Defaults();
            table[0] = new[] { KeyCode.I, KeyCode.I, KeyCode.F2, KeyCode.None };
            Assert.IsTrue(KeyBindings.SetAll(table));
            Assert.AreEqual(new[] { KeyCode.I }, KeyBindings.GetKeys(0),
                "重複・予約キー・None は取り除く");

            Assert.IsFalse(KeyBindings.SetAll(null), "null は拒否する");
            Assert.IsFalse(KeyBindings.SetAll(new KeyCode[3][]), "8 レーン以外は拒否する");
        }

        [Test]
        public void SetAll_SwapsKeysBetweenLanes_Atomically()
        {
            var table = Defaults();
            KeyCode[] lane0 = table[0];
            KeyCode[] lane1 = table[1];
            table[0] = new[] { KeyCode.Alpha2, KeyCode.Q, KeyCode.A, KeyCode.Z };
            table[1] = new[] { KeyCode.Alpha1, KeyCode.W, KeyCode.S, KeyCode.X };

            Assert.IsTrue(KeyBindings.SetAll(table), "入れ替えの途中状態で空レーンを作らない");
            Assert.AreEqual(KeyCode.Alpha2, KeyBindings.GetKeys(0)[0]);
            Assert.AreEqual(KeyCode.Alpha1, KeyBindings.GetKeys(1)[0]);
        }
    }
}
