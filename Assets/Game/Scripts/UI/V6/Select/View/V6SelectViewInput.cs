using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FlashBeat.UI.V5;
using FlashBeat.UI.V6.Common;

namespace FlashBeat.UI.V6.Select.View
{
    /// <summary>
    /// V6SelectView のキー入力・スクロール処理部分 (Issue #385 Phase 2.4 で分割)。
    /// Tick (毎フレームのキー判定と検索フォーカス処理) と、
    /// ScrollList / OnScroll (ホイール・スクロールボタンからの曲/ランキング送り) を持つ。
    /// partial class のためフィールド・メソッドは V6SelectView 本体と共有する。
    /// </summary>
    public sealed partial class V6SelectView
    {
        // --- 毎フレーム (V6SelectViewDriver から呼ばれる) ---

        internal void Tick()
        {
            if (rootGo == null || !rootGo.activeInHierarchy)
            {
                return;
            }

            PlaceFrame();

            // メニュー操作は KeyActions (キー設定の正本, #142) 経由で拾う。
            // v5 SelectView と同じ短縮キーを維持する: ↑↓ / ←→ (NavPrev/NavNext) で
            // 曲送り、Confirm (既定 Space) と Enter/KeypadEnter で Ready、
            // Back (既定 Esc) で戻る。Tab (未プレイのみの絞り込み) は KeyActions に
            // 無い v6 固定ショートカットなので raw KeyCode のまま。
            // 検索は正本 OENRv の凡例どおり「A-Z を押すと開始」。
            bool up = Input.GetKeyDown(KeyCode.UpArrow) || KeyActions.IsDown(KeyActions.KeyAction.NavPrev);
            bool down = Input.GetKeyDown(KeyCode.DownArrow) || KeyActions.IsDown(KeyActions.KeyAction.NavNext);
            bool confirm = KeyActions.IsDown(KeyActions.KeyAction.Confirm)
                || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
            bool cancel = KeyActions.IsDown(KeyActions.KeyAction.Back);
            bool tab = Input.GetKeyDown(KeyCode.Tab);
            var action = V6SelectViewLogic.MapKey(searchFocused, up, down, confirm, cancel, tab);

            switch (action)
            {
                case V6SelectAction.UnfocusSearch:
                    searchFocused = false;
                    RefreshVolatile();
                    return;
                case V6SelectAction.MoveUp:
                    data.MoveSelection(-1);
                    return;
                case V6SelectAction.MoveDown:
                    data.MoveSelection(1);
                    return;
                case V6SelectAction.Confirm:
                    // Enter/Space = Ready。遷移は統合担当が onReady で決める
                    // (data.CommitReady の後にゲーム画面へ移す)。
                    // 絞り込みで隠れた選択は Ready できない (#221/#222)。
                    if (ReadyAvailable() && onReady != null)
                    {
                        onReady();
                    }
                    return;
                case V6SelectAction.Cancel:
                    if (settingsOpen)
                    {
                        CloseSettings();
                    }
                    else if (onBack != null)
                    {
                        onBack();
                    }
                    return;
                case V6SelectAction.ToggleFilter:
                    ToggleUnplayedFilter();
                    return;
            }

            if (searchFocused)
            {
                string input = Input.inputString;
                if (!string.IsNullOrEmpty(input))
                {
                    data.SetQuery(V6SelectViewLogic.ApplySearchInput(data.Query, input));
                }

                return;
            }

            // 正本 OENRv: 英字 (A-Z) を押すと検索欄に入る。制御文字は対象外。
            // ただし操作キー (既定 O = Option) が優先。検索シードを先に判定すると、
            // O を押すたびに検索語が入り、フッターの「設定 O」が動かなくなる。
            if (KeyActions.IsDown(KeyActions.KeyAction.Option)) // 既定 O (#142)
            {
                OpenSettings();
                return;
            }

            string typed = Input.inputString;
            char seed = V6SelectViewLogic.SearchSeed(typed);
            if (seed != '\0')
            {
                searchFocused = true;
                string seeded = V6SelectViewLogic.ApplySearchInput("", seed.ToString());
                if (data.Query == seeded)
                {
                    // 検索語が変わらないときは Changed が発火しないため、フォーカス表示を
                    // 反映する更新をここで 1 回だけ行う。変わるときは SetQuery →
                    // Changed → OnDataChanged が更新するので、二重に呼ばない。
                    RefreshVolatile();
                }
                else
                {
                    data.SetQuery(seeded);
                }

                return;
            }
        }

        private void ScrollList(int delta)
        {
            // スクロール (ホイール / ボタン) は選択曲を delta だけ動かす操作。
            // スクロール位置は差分更新で選択曲から常に再計算される (中央固定)。
            data.MoveSelection(delta);
            RefreshVolatile();
        }

        internal void OnScroll(int target, float delta)
        {
            int step = delta > 0f ? -1 : 1;
            if (target == 0)
            {
                ScrollList(step);
            }
            else
            {
                rankingScroll = V6SelectViewLogic.ClampScroll(rankingScroll + step,
                    data.Selected != null ? data.RankingFor(data.Selected).Count : 0, RankingVisibleRows);
                RefreshVolatile();
            }
        }
    }
}
