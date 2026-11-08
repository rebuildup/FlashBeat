using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using FlashBeat.UI.V5;
using FlashBeat.UI.V6.Select;
using FlashBeat.UI.V6.Select.View;

namespace FlashBeat.UI.V6.Select.View.Tests
{
    /// <summary>V6SelectView の純ロジック (#221)。Unity API に依存しない部分のみ。</summary>
    public sealed class V6SelectViewLogicTests
    {
        // --- DesignScale ---

        [Test]
        public void DesignScale_Fhd_ReturnsUniformScale()
        {
            Assert.AreEqual(1920f / 1728f, V6SelectViewLogic.DesignScale(1920, 1080), 0.0001f);
        }

        [Test]
        public void DesignScale_2k_And_Mac_HeightIsBinding()
        {
            // 2560x1440 と 2560x1664 はどちらも高さ基準 (972 * 1.481 < 1728 * 1.481)。
            float expected = 1440f / 972f;
            Assert.AreEqual(expected, V6SelectViewLogic.DesignScale(2560, 1440), 0.0001f);
            Assert.AreEqual(expected, V6SelectViewLogic.DesignScale(2560, 1664), 0.0001f);
        }

        [Test]
        public void DesignScale_InvalidSize_FallsBackToOne()
        {
            Assert.AreEqual(1f, V6SelectViewLogic.DesignScale(0, 1080));
            Assert.AreEqual(1f, V6SelectViewLogic.DesignScale(1920, -1));
        }

        // --- スクロール位置計算 ---

        [Test]
        public void ClampScroll_KeepsWindowInsideRows()
        {
            Assert.AreEqual(0, V6SelectViewLogic.ClampScroll(-3, 26, 9));
            Assert.AreEqual(17, V6SelectViewLogic.ClampScroll(30, 26, 9));
            Assert.AreEqual(5, V6SelectViewLogic.ClampScroll(5, 26, 9));
        }

        [Test]
        public void ClampScroll_FewerRowsThanViewport_IsZero()
        {
            Assert.AreEqual(0, V6SelectViewLogic.ClampScroll(4, 5, 9));
            Assert.AreEqual(0, V6SelectViewLogic.ClampScroll(0, 0, 9));
        }

        [Test]
        public void ScrollToReveal_RevealsSelectedRow()
        {
            // 可視範囲 [scroll, scroll+visibleRows) 内なら動かさない。
            Assert.AreEqual(2, V6SelectViewLogic.ScrollToReveal(2, 5, 9));
            Assert.AreEqual(6, V6SelectViewLogic.ScrollToReveal(14, 5, 9));
            Assert.AreEqual(5, V6SelectViewLogic.ScrollToReveal(12, 5, 9));
        }

        // --- ドットインジケータ ---

        [Test]
        public void DotActiveIndex_ClampsToDots()
        {
            Assert.AreEqual(-1, V6SelectViewLogic.DotActiveIndex(0, 0));
            Assert.AreEqual(0, V6SelectViewLogic.DotActiveIndex(-1, 26));
            Assert.AreEqual(25, V6SelectViewLogic.DotActiveIndex(99, 26));
            Assert.AreEqual(4, V6SelectViewLogic.DotActiveIndex(4, 26));
        }

        // --- スコア表示 ---

        [Test]
        public void FormatScore_Unplayed_IsDash()
        {
            Assert.AreEqual("–", V6SelectViewLogic.FormatScore(-1));
        }

        [Test]
        public void FormatScore_GroupsThousands()
        {
            Assert.AreEqual("988,513", V6SelectViewLogic.FormatScore(988513));
            Assert.AreEqual("0", V6SelectViewLogic.FormatScore(0));
        }

        // --- 検索入力 ---

        [Test]
        public void ApplySearchInput_AppendsPrintableChars()
        {
            Assert.AreEqual("abc", V6SelectViewLogic.ApplySearchInput("", "abc"));
            // 入力文字は末尾に追記される (Backspace は 1 文字削る)。
            Assert.AreEqual("まにに", V6SelectViewLogic.ApplySearchInput("まに", "に"));
        }

        [Test]
        public void ApplySearchInput_BackspaceRemovesOneChar()
        {
            Assert.AreEqual("ま", V6SelectViewLogic.ApplySearchInput("まに", "\b"));
            Assert.AreEqual("", V6SelectViewLogic.ApplySearchInput("", "\b"));
        }

        [Test]
        public void ApplySearchInput_IgnoresControlChars()
        {
            Assert.AreEqual("ab", V6SelectViewLogic.ApplySearchInput("ab", "\n\u007f"));
        }

        [Test]
        public void ApplySearchInput_NullQuery_IsTreatedAsEmpty()
        {
            Assert.AreEqual("x", V6SelectViewLogic.ApplySearchInput(null, "x"));
        }

        // --- キーボード状態遷移 ---

        [Test]
        public void MapKey_ListFocus_MapsNavigation()
        {
            Assert.AreEqual(V6SelectAction.MoveUp, V6SelectViewLogic.MapKey(false, true, false, false, false, false));
            Assert.AreEqual(V6SelectAction.MoveDown, V6SelectViewLogic.MapKey(false, false, true, false, false, false));
            Assert.AreEqual(V6SelectAction.Confirm, V6SelectViewLogic.MapKey(false, false, false, true, false, false));
            Assert.AreEqual(V6SelectAction.Cancel, V6SelectViewLogic.MapKey(false, false, false, false, true, false));
            Assert.AreEqual(V6SelectAction.FocusSearch, V6SelectViewLogic.MapKey(false, false, false, false, false, true));
            Assert.AreEqual(V6SelectAction.None, V6SelectViewLogic.MapKey(false, false, false, false, false, false));
        }

        [Test]
        public void MapKey_SearchFocus_ConsumesConfirmAndCancel()
        {
            // 検索中の Enter/Esc/Tab はフォーカス解除のみ。確定 (Ready) や戻るを同時に発火させない。
            Assert.AreEqual(V6SelectAction.UnfocusSearch, V6SelectViewLogic.MapKey(true, false, false, true, false, false));
            Assert.AreEqual(V6SelectAction.UnfocusSearch, V6SelectViewLogic.MapKey(true, false, false, false, true, false));
            Assert.AreEqual(V6SelectAction.UnfocusSearch, V6SelectViewLogic.MapKey(true, false, false, false, false, true));
            Assert.AreEqual(V6SelectAction.None, V6SelectViewLogic.MapKey(true, true, true, false, false, false));
        }

        // --- 凡例と実装の一致 (#221 / #222 Task 2) ---

        [Test]
        public void FooterLegend_ConfirmKey_FollowsKeyActionsDefault()
        {
            // 凡例の決定キーは KeyActions (キー設定の正本) から取る。ハードコードしない。
            Assert.AreEqual(ActionKeyHint.Primary(KeyActions.KeyAction.Confirm),
                V6SelectViewLogic.FooterLegend(ActionKeyHint.Primary(KeyActions.KeyAction.Confirm))[0].Key);
        }

        [Test]
        public void FooterLegend_FilterToggleKey_MatchesImplementation()
        {
            // 絞り込みは F (Tab は検索フォーカス)。凡例が実装と矛盾しない。
            V6LegendItem[] items = V6SelectViewLogic.FooterLegend("Space");
            Assert.AreEqual("Tab", items[2].Key);
            Assert.AreEqual("検索", items[2].Label);
            Assert.AreEqual("F", items[3].Key);
            Assert.AreEqual("絞り込み", items[3].Label);
        }

        [Test]
        public void FooterLegend_ConfirmKey_FollowsUserRebinding()
        {
            // キー設定を変えると凡例のキャップも変わる (ActionKeyHint 方式の一元化)。
            var storage = new MemoryKeyStorage();
            KeyActions.SetStorage(storage);
            try
            {
                Assert.IsTrue(KeyActions.Set(KeyActions.KeyAction.Confirm, new[] { KeyCode.F8 }));
                string rebound = ActionKeyHint.Primary(KeyActions.KeyAction.Confirm);

                V6LegendItem[] items = V6SelectViewLogic.FooterLegend(rebound);
                Assert.AreEqual("F8", items[0].Key);
                Assert.AreEqual("決定", items[0].Label);
            }
            finally
            {
                KeyActions.SetStorage(null);
            }
        }

        // --- Ready の有効性 (#221 / #222: 絞り込みで隠れた曲を Ready させない) ---

        [Test]
        public void ReadyAvailable_SelectionVisibleInList_IsTrue()
        {
            var selected = new V6SongEntry { SongId = 1, Title = "終焉逃避行" };
            var display = new List<V6SongEntry> { new V6SongEntry { SongId = 1, Title = "終焉逃避行" } };

            Assert.IsTrue(V6SelectViewLogic.ReadyAvailable(selected, display));
        }

        [Test]
        public void ReadyAvailable_SelectionHiddenByFilter_IsFalse()
        {
            // 選択は生き残るが表示リストに無い → Ready できない。
            var selected = new V6SongEntry { SongId = 5, Title = "終焉逃避行" };
            var display = new List<V6SongEntry> { new V6SongEntry { SongId = 2, Title = "まにまに" } };

            Assert.IsFalse(V6SelectViewLogic.ReadyAvailable(selected, display));
        }

        [Test]
        public void ReadyAvailable_NoSelection_IsFalse()
        {
            Assert.IsFalse(V6SelectViewLogic.ReadyAvailable(null, new List<V6SongEntry>()));
            Assert.IsFalse(V6SelectViewLogic.ReadyAvailable(new V6SongEntry { SongId = 1 }, null));
        }

        // --- 中央カードの操作ボタン (#221 Task 3: 疑似ボタン禁止) ---

        [Test]
        public void CenterControls_EveryButtonMapsToAnExistingOperation()
        {
            // onClick=null の装飾ボタン (死んだ操作) を作らない。定義は
            // 曲送り (MovePrev/MoveNext) と Ready の 3 個だけ。
            V6SelectViewLogic.V6CenterControlDef[] defs = V6SelectViewLogic.CenterControls();

            Assert.AreEqual(3, defs.Length);
            Assert.AreEqual(V6SelectViewLogic.V6SelectControlAction.MovePrev, defs[0].Action);
            Assert.AreEqual(V6SelectViewLogic.V6SelectControlAction.Ready, defs[1].Action);
            Assert.AreEqual(V6SelectViewLogic.V6SelectControlAction.MoveNext, defs[2].Action);
        }
    }

    /// <summary>KeyActions のテスト用インメモリ保存先 (ActionKeyHintTests と同じ規約)。</summary>
    internal sealed class MemoryKeyStorage : KeyActions.IStorage
    {
        public string Raw = "";
        public string LoadRaw() { return Raw; }
        public void SaveRaw(string json) { Raw = json; }
    }
}
