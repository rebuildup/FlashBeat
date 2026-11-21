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
    ///
    /// Issue #733 フォローアップ: 行選択・タブ切替・値変更・決定の規則は
    /// 共有部品 <see cref="V6SettingsScreen"/> へ移した (Pause と同一実装)。
    /// ここに残るのは Option 固有のモーダル処理と監査 API だけ。
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

            // Game Preview の疑似メディア時計を進める (Issue #579)。
            // モーダル表示中・入力ゲート中でもアニメーションは止めない
            // (設定の見え方を確認する画面なので、背後の流れは生かす)。
            //
            // Issue #677: 既知のキー競合 — 既定のレーンキー S (lane 1) は
            // メニューの「下へ移動」(KeyCode.S → MoveDown) と同じ物理キー。
            // プレビューはゲーム操作を再現するため KeyBindings の実キーを
            // そのまま読むので、S を押すと「行選択が下がる」+「レーン 1 が
            // 点灯・判定される」が同時に起きる。これは本物のゲームと同じ
            // キーが別の意味を持つ仕様上の競合であり、メニュー側の
            // ナビゲーション割当 (↑↓/WASD) を変えると本編の操作感と
            // ずれるため、ここでは意図的に両方を許容する (文書化のみ)。
            if (settingsScreen != null && settingsScreen.GamePreview != null)
            {
                settingsScreen.GamePreview.Tick();
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
            volumeSettings.Tick();

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
                        volumeSettings.FlushNow();
                        onBack();
                    }
                    return;
                case V6OptionAction.Confirm:
                    if (confirmClearOpen)
                    {
                        ConfirmCurrent();
                        return;
                    }
                    ActivateSelected();
                    return;
                case V6OptionAction.None:
                    return;
            }

            if (settingsScreen == null || settingsScreen.CurrentRows.Count == 0)
            {
                return;
            }

            // 行選択・タブ切替・値変更は共有部品 (Pause と同一規則)。
            // 音量行はスライダーと同じ値モデルを編集する (Issue #375 §3.2)。
            // Rebuild せずスライダーと値テキストだけを更新し、保存は 0.5 秒後にまとめる。
            // Issue #733: 値モデルは Pause と同じ <see cref="V6VolumeSettings"/>。
            switch (action)
            {
                case V6OptionAction.MoveUp:
                    settingsScreen.MoveSelection(-1);
                    return;
                case V6OptionAction.MoveDown:
                    settingsScreen.MoveSelection(1);
                    return;
                case V6OptionAction.AdjustPrev:
                    settingsScreen.AdjustSelected(-1, shift);
                    return;
                case V6OptionAction.AdjustNext:
                    settingsScreen.AdjustSelected(1, shift);
                    return;
                case V6OptionAction.NextTab:
                    settingsScreen.SelectTab(settingsScreen.SelectedTab + 1);
                    return;
                case V6OptionAction.PrevTab:
                    settingsScreen.SelectTab(settingsScreen.SelectedTab - 1);
                    return;
            }
        }

        /// <summary>選択中の行を決定する (Enter / Space。行の Activate を呼ぶ)。</summary>
        private void ActivateSelected()
        {
            if (settingsScreen != null)
            {
                settingsScreen.ActivateSelected();
            }
        }

        // --- 検証ハーネス用の読み取り API (製品動作は変えない) ---
        // Issue #733 フォローアップ: 状態は共有部品 V6SettingsScreen が持つ。

        /// <summary>選択中タブの行数。</summary>
        public int RowCountForAudit
        {
            get { return settingsScreen != null ? settingsScreen.CurrentRows.Count : 0; }
        }

        /// <summary>行のラベル (例: "キー設定")。</summary>
        public string RowLabelForAudit(int index)
        {
            var rows = settingsScreen != null ? settingsScreen.CurrentRows : null;
            return rows != null && index >= 0 && index < rows.Count ? rows[index].Label : null;
        }

        /// <summary>行の現在値 (Value をその場で評価)。</summary>
        public string RowValueForAudit(int index)
        {
            var rows = settingsScreen != null ? settingsScreen.CurrentRows : null;
            return rows != null && index >= 0 && index < rows.Count ? rows[index].Value?.Invoke() : null;
        }

        /// <summary>行に実際に描画されている値テキスト (描画が止まっても検出できる)。</summary>
        public string RenderedRowValueForAudit(int index)
        {
            return settingsScreen != null ? settingsScreen.RenderedRowValue(index) : null;
        }

        /// <summary>行を選択する (↑↓ と同じ WrapIndex + Refresh 経路)。</summary>
        public void SelectRowForAudit(int index)
        {
            if (settingsScreen != null)
            {
                settingsScreen.SelectRow(index);
            }
        }

        /// <summary>選択中の行を値変更する (←→ と同じ AdjustSelected 経路)。</summary>
        public void AdjustSelectedRowForAudit(int delta, bool shift = false)
        {
            if (settingsScreen != null)
            {
                settingsScreen.AdjustSelected(delta, shift);
            }
        }

        /// <summary>選択中の行を決定する (Enter と同じ ActivateSelected 経路)。</summary>
        public void ActivateSelectedRowForAudit()
        {
            ActivateSelected();
        }

        /// <summary>選択中タブの番号 (Play=0)。</summary>
        public int SelectedTabForAudit
        {
            get { return settingsScreen != null ? settingsScreen.SelectedTab : 0; }
        }

        /// <summary>
        /// 自動撮影 (-autotest v6screens) が各タブの実画面を撮るための操作 API。
        /// 実機では Tab / クリックで切り替わる動作そのものを呼ぶ。
        /// </summary>
        public void SelectTabForAudit(int index)
        {
            if (settingsScreen != null)
            {
                settingsScreen.SelectTab(index);
            }
        }

        /// <summary>
        /// churn 計測 (Issue #415) が行選択を往復させるための操作 API。
        /// ↑↓ キーと同じ WrapIndex + Refresh 経路を呼ぶ。
        /// </summary>
        public void MoveSelectionForAudit(int delta)
        {
            if (settingsScreen != null)
            {
                settingsScreen.MoveSelection(delta);
            }
        }

        /// <summary>キー設定モーダル (開いていなければ IsOpen=false)。</summary>
        public V6KeyConfigModal KeyConfigModalForAudit
        {
            get { return keyConfigModal; }
        }
    }
}
