using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FlashBeat.UI.V5;
using FlashBeat.UI.V6;
using FlashBeat.UI.V6.Common;

namespace FlashBeat.UI.V6.Option
{
    /// <summary>
    /// V6OptionView の毎フレーム入力処理 (Issue #385 Phase 2.5 で分割)。
    /// Tick (キー → V6OptionAction → 行選択/タブ/モーダル) と、選択行の値変更・
    /// 決定・タブ切替の内部操作を持つ。partial class のため
    /// フィールド・メソッドは V6OptionView 本体と共有する。
    /// </summary>
    public sealed partial class V6OptionView
    {
        // --- 毎フレーム (V6OptionViewDriver から呼ばれる) ---

        internal void Tick()
        {
            if (rootGo == null || !rootGo.activeInHierarchy)
            {
                return;
            }

            // キー設定モーダルが前面のときは背後の操作を止める (Issue #376)。
            // モーダル自身がキーを消費し、閉じたフレームも BlocksInput が塞ぐ。
            if (V6KeyConfigModal.BlocksInput)
            {
                return;
            }

            if (!FlashBeat.UI.SceneInputGate.InputAllowed)
            {
                return;
            }

            PlaceFrame();
            FlushPendingVolumeSave();

            bool up = Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W);
            bool down = Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S);
            bool left = Input.GetKeyDown(KeyCode.LeftArrow);
            bool right = Input.GetKeyDown(KeyCode.RightArrow);
            bool confirm = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)
                || Input.GetKeyDown(KeyCode.Space);
            bool cancel = Input.GetKeyDown(KeyCode.Escape);
            bool tab = Input.GetKeyDown(KeyCode.Tab);
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            var action = V6OptionLogic.MapKey(new V6OptionInput
            {
                ModalOpen = confirmClearOpen,
                Up = up,
                Down = down,
                Left = left,
                Right = right,
                Confirm = confirm,
                Cancel = cancel,
                TabNext = tab && !shift,
                TabPrev = tab && shift,
            });

            switch (action)
            {
                case V6OptionAction.Cancel:
                    if (confirmClearOpen)
                    {
                        CancelClearConfirm();
                    }
                    else if (onBack != null)
                    {
                        // 画面を離れる: 猶予中の音量保存を先に確定させる。
                        SavePendingVolumeNow();
                        onBack();
                    }
                    return;
                case V6OptionAction.Confirm:
                    if (confirmClearOpen)
                    {
                        ConfirmCurrent();
                        return;
                    }
                    ActivateRow();
                    return;
                case V6OptionAction.None:
                    return;
            }

            if (currentRows.Count == 0)
            {
                return;
            }

            switch (action)
            {
                case V6OptionAction.MoveUp:
                    selectedRow = V6OptionLogic.WrapIndex(selectedRow, currentRows.Count, -1);
                    Rebuild();
                    return;
                case V6OptionAction.MoveDown:
                    selectedRow = V6OptionLogic.WrapIndex(selectedRow, currentRows.Count, 1);
                    Rebuild();
                    return;
                case V6OptionAction.AdjustPrev:
                    AdjustSelected(-1, shift);
                    return;
                case V6OptionAction.AdjustNext:
                    AdjustSelected(1, shift);
                    return;
                case V6OptionAction.NextTab:
                    SelectTab(selectedTab + 1);
                    return;
                case V6OptionAction.PrevTab:
                    SelectTab(selectedTab - 1);
                    return;
            }
        }

        private void AdjustSelected(int delta, bool shift = false)
        {
            if (currentRows.Count == 0)
            {
                return;
            }

            var row = currentRows[selectedRow];

            // 音量行はスライダーと同じ値モデルを編集する (Issue #375 §3.2)。
            // Rebuild せずスライダーと値テキストだけを更新し、保存は 0.5 秒後にまとめる。
            if (row.VolumeIndex >= 0)
            {
                SetVolume(row.VolumeIndex,
                    V6OptionLogic.NudgeVolume(GetVolume(row.VolumeIndex), delta, shift), false);
                UpdateVolumeVisuals();
                return;
            }

            if (row.Adjust != null)
            {
                row.Adjust(delta);
            }

            Rebuild();
        }

        private void ActivateRow()
        {
            if (currentRows.Count == 0)
            {
                return;
            }

            var row = currentRows[selectedRow];
            if (row.Activate != null)
            {
                row.Activate();
                Rebuild();
            }
        }

        private void SelectTab(int index)
        {
            selectedTab = V6OptionLogic.ClampTabIndex(index);
            selectedRow = 0;
            Rebuild();
        }

        /// <summary>
        /// 自動撮影 (-autotest v6screens) が各タブの実画面を撮るための操作 API。
        /// 実機では Tab / クリックで切り替わる動作そのものを呼ぶ。
        /// </summary>
        public void SelectTabForAudit(int index)
        {
            SelectTab(index);
        }

        /// <summary>
        /// churn 計測 (Issue #415) が行選択を往復させるための操作 API。
        /// ↑↓ キーと同じ WrapIndex + Rebuild 経路を呼ぶ。
        /// </summary>
        public void MoveSelectionForAudit(int delta)
        {
            if (currentRows.Count == 0)
            {
                return;
            }

            selectedRow = V6OptionLogic.WrapIndex(selectedRow, currentRows.Count, delta);
            Rebuild();
        }

        // --- 検証ハーネス用の読み取り API (製品動作は変えない) ---

        /// <summary>選択中タブの行数。</summary>
        public int RowCountForAudit
        {
            get { return currentRows.Count; }
        }

        /// <summary>行のラベル (例: "キー設定")。</summary>
        public string RowLabelForAudit(int index)
        {
            return index >= 0 && index < currentRows.Count ? currentRows[index].Label : null;
        }

        /// <summary>行の現在値 (Value をその場で評価)。</summary>
        public string RowValueForAudit(int index)
        {
            return index >= 0 && index < currentRows.Count ? currentRows[index].Value?.Invoke() : null;
        }

        /// <summary>行を選択する (↑↓ と同じ WrapIndex + Rebuild 経路)。</summary>
        public void SelectRowForAudit(int index)
        {
            if (currentRows.Count == 0)
            {
                return;
            }

            selectedRow = V6OptionLogic.WrapIndex(index, currentRows.Count, 0);
            Rebuild();
        }

        /// <summary>選択中の行を値変更する (←→ と同じ AdjustSelected 経路)。</summary>
        public void AdjustSelectedRowForAudit(int delta, bool shift = false)
        {
            AdjustSelected(delta, shift);
        }

        /// <summary>選択中の行を決定する (Enter と同じ ActivateRow 経路)。</summary>
        public void ActivateSelectedRowForAudit()
        {
            ActivateRow();
        }

        /// <summary>選択中タブの番号 (Play=0)。</summary>
        public int SelectedTabForAudit
        {
            get { return selectedTab; }
        }

        /// <summary>キー設定モーダル (開いていなければ IsOpen=false)。</summary>
        public V6KeyConfigModal KeyConfigModalForAudit
        {
            get { return keyConfigModal; }
        }
    }
}
