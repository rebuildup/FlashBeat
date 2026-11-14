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
    /// V6OptionView の確認モーダル部分 (Issue #385 Phase 2.5 で分割)。
    /// Clear save data / Media cache / Key Layout リセットの確認ダイアログ
    /// (ConfirmTexts/BuildConfirmClearModal) と、開く・閉じる・確定の振り分け、
    /// キー設定モーダル (V6KeyConfigModal) の開閉を持つ。partial class のため
    /// フィールド・メソッドは V6OptionView 本体と共有する。
    /// </summary>
    public sealed partial class V6OptionView
    {
        // --- モーダル (確認。契約 §4 モーダルゾーン: Scrim 全面 +
        //     Dialog (444,231) 840x510 $k1 r16 — V6Frame.Create が層ごと所有) ---

        /// <summary>確認モーダルの文言 (種類ごと)。</summary>
        private void ConfirmTexts(out string title, out string desc, out string confirmLabel)
        {
            if (confirmKind == ConfirmKind.ResetKeyLayout)
            {
                title = "Key Layout";
                desc = "すべてのレーンのキー割り当てを標準に戻します。\n現在のカスタム割り当ては消去されます (元に戻せません)。";
                confirmLabel = "戻す";
                return;
            }

            if (confirmKind == ConfirmKind.MediaCache)
            {
                // Issue #352: 消去対象とサイズを明示する。破壊的操作は
                // 「Clear save data」と同じく確認してから実行する。
                title = "Media cache";
                desc = "ダウンロードした楽曲映像のキャッシュ (" + MediaCacheSizeLabel() + ") を消去します。\n"
                    + "次回プレイ時に再ダウンロードが必要になります。";
                confirmLabel = "消去する";
                return;
            }

            title = "Clear save data";
            desc = "ハイスコアとキー設定 (レーン・メニュー操作) を消去します。\n実行後は元に戻せません。";
            confirmLabel = "消去する";
        }

        private void BuildConfirmClearModal(GameObject modal)
        {
            if (modal == null)
            {
                return;
            }

            var dialog = modal.transform.Find("Dialog")?.gameObject;
            if (dialog == null)
            {
                return;
            }

            V6Frame.ShowModal(frame.gameObject);

            string titleText, descText, confirmLabelText;
            ConfirmTexts(out titleText, out descText, out confirmLabelText);

            var title = V6Controls.Text(dialog, "Head", titleText,
                new Vector2(28f, 28f), new Vector2(784f, 40f), 30f, V6Tokens.Text,
                TextAlignmentOptions.Left, FontStyles.Bold);
            title.textWrappingMode = TextWrappingModes.NoWrap;

            var desc = V6Controls.Text(dialog, "Desc", descText,
                new Vector2(28f, 100f), new Vector2(784f, 120f), 24f, V6Tokens.Text,
                TextAlignmentOptions.TopLeft);
            desc.lineSpacing = 40f; // design lineHeight 1.4

            var cancel = V6Controls.Panel(dialog, "Op Cancel", new Vector2(28f, 432f),
                new Vector2(198f, 50f), V6Tokens.Key4, V6Tokens.RadiusMedium);
            var cancelButton = cancel.AddComponent<Button>();
            cancelButton.transition = Selectable.Transition.None;
            var cancelNav = cancelButton.navigation;
            cancelNav.mode = Navigation.Mode.None;
            cancelButton.navigation = cancelNav;
            V6ButtonPressFeedback.Attach(cancel);
            cancelButton.onClick.AddListener(CancelClearConfirm);
            V6Controls.Text(cancel, "Label", "キャンセル", Vector2.zero, new Vector2(198f, 50f),
                26f, V6Tokens.Text, TextAlignmentOptions.Center);

            var clear = V6Controls.Panel(dialog, "Op Clear", new Vector2(784f - 252f + 28f, 432f),
                new Vector2(252f, 50f), DangerLabel, V6Tokens.RadiusMedium);
            var clearButton = clear.AddComponent<Button>();
            clearButton.transition = Selectable.Transition.None;
            var clearNav = clearButton.navigation;
            clearNav.mode = Navigation.Mode.None;
            clearButton.navigation = clearNav;
            V6ButtonPressFeedback.Attach(clear);
            clearButton.onClick.AddListener(ConfirmCurrent);
            V6Controls.Text(clear, "Label", confirmLabelText, Vector2.zero, new Vector2(252f, 50f),
                26f, V6Tokens.ButtonPrimaryLabel, TextAlignmentOptions.Center);
        }

        // --- Clear save data の確認モーダル (マウスとキーボードで同じ結果にする) ---

        /// <summary>確認モーダルを開く (まだ何も消去しない)。</summary>
        private void OpenClearConfirm()
        {
            confirmKind = ConfirmKind.ClearSaveData;
            confirmClearOpen = true;
        }

        /// <summary>
        /// Media cache の消去確認を開く (Issue #352)。行のクリック / Enter では
        /// 消去せず、モーダルの「消去する」で初めて実行する。
        /// </summary>
        private void OpenMediaCacheConfirm()
        {
            confirmKind = ConfirmKind.MediaCache;
            confirmClearOpen = true;
        }

        /// <summary>Key Layout のリセット確認を開く (まだ何も戻さない)。</summary>
        private void OpenResetKeyLayoutConfirm()
        {
            confirmKind = ConfirmKind.ResetKeyLayout;
            confirmClearOpen = true;
            Rebuild();
        }

        /// <summary>確認モーダルを閉じるだけ (何も変えない)。</summary>
        private void CancelClearConfirm()
        {
            confirmClearOpen = false;
            Rebuild();
        }

        /// <summary>消去を実行し、結果をモーダルを閉じて表示する (マウス・キーボードで共通)。</summary>
        private void ConfirmClearSaveData()
        {
            ClearSaveData();
            clearResult = "消去しました";
            confirmClearOpen = false;
            Rebuild();
        }

        /// <summary>Media cache を消去してモーダルを閉じる (#352)。表示値は実測サイズ。</summary>
        private void ConfirmClearMediaCache()
        {
            ClearMediaCache();
            confirmClearOpen = false;
            Rebuild();
        }

        /// <summary>モーダルの「消去する / 戻す」を種類ごとの処理へ振り分ける。</summary>
        private void ConfirmCurrent()
        {
            switch (confirmKind)
            {
                case ConfirmKind.ResetKeyLayout:
                    ConfirmResetKeyLayout();
                    return;
                case ConfirmKind.MediaCache:
                    ConfirmClearMediaCache();
                    return;
                default:
                    ConfirmClearSaveData();
                    return;
            }
        }

        /// <summary>レーン割当を標準へ戻し、モーダルを閉じる (マウス・キーボードで共通)。</summary>
        private void ConfirmResetKeyLayout()
        {
            ResetKeyLayout();
            confirmClearOpen = false;
            Rebuild();
        }

        private void OpenKeyConfig()
        {
            if (keyConfigModal != null)
            {
                keyConfigModal.Show();
            }
        }

        /// <summary>モーダルが閉じたら、プレビューのキー割当表示を更新する。</summary>
        private void OnKeyConfigClosed()
        {
            Rebuild();
        }
    }
}
