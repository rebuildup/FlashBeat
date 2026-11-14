using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FlashBeat.Gameplay;
using FlashBeat.UI.V5;
using FlashBeat.UI.V6;
using FlashBeat.UI.V6.Common;

namespace FlashBeat.UI.V6.Game.Pause
{
    /// <summary>
    /// V6PauseView の確認ダイアログ部分 (Issue #385 Phase 2.3 で分割)。
    /// Key Layout リセットの確認ダイアログ (Scrim + Dialog) の生成を持つ。
    /// partial class のためフィールド・メソッドは V6PauseView 本体と共有する。
    /// </summary>
    public sealed partial class V6PauseView
    {
        /// <summary>
        /// Key Layout を標準へ戻す確認ダイアログ (共通フレームのモーダルゾーン:
        /// Scrim 全面 + Dialog (444,231) 840×510 $k1 r16)。Option の同じ操作
        /// (issue-162 C-6: 破壊的なリセットは確認してから) と形を揃える。
        /// </summary>
        private void BuildResetConfirm()
        {
            confirmRoot = V5Ui.Node(frame.gameObject, "V6 Pause Confirm");
            var rootRt = V6Layout.RectOf(confirmRoot);
            V6Layout.StretchFill(rootRt);
            rootRt.SetAsLastSibling();

            var scrim = V5Ui.Node(confirmRoot, "Scrim");
            V5Ui.Fill(V6Layout.RectOf(scrim), 0f);
            var scrimImg = scrim.AddComponent<Image>();
            scrimImg.color = new Color(0f, 0f, 0f, 0xCC / 255f);
            scrimImg.raycastTarget = true;

            var dialog = V5Ui.Panel(confirmRoot, "Dialog", new Vector2(840f, 510f), V5Tokens.Key1, 16f);
            V6Layout.PlaceAt(dialog, 444f, 231f, new Vector2(840f, 510f));

            var head = V5Ui.Text(dialog, "Head", "Key Layout", 30f, V5Tokens.Text,
                TextAlignmentOptions.Left, FontStyles.Bold);
            V6Layout.PlaceAt(head.gameObject, 28f, 28f, new Vector2(784f, 40f));

            var desc = V5Ui.Text(dialog, "Desc",
                "すべてのレーンのキー割り当てを標準に戻します。\n現在のカスタム割り当ては消去されます (元に戻せません)。",
                24f, V5Tokens.Text, TextAlignmentOptions.TopLeft);
            V6Layout.PlaceAt(desc.gameObject, 28f, 100f, new Vector2(784f, 120f));
            desc.lineSpacing = 40f;

            var cancel = V5Ui.Panel(dialog, "Op Cancel", new Vector2(198f, 50f), V5Tokens.Key4, 8f);
            V6Layout.PlaceAt(cancel, 28f, 432f, new Vector2(198f, 50f));
            var cancelLabel = V5Ui.Text(cancel, "Label", "キャンセル", 26f, V5Tokens.Text,
                TextAlignmentOptions.Center);
            V6Layout.StretchFill(cancelLabel.rectTransform);
            var cancelButton = cancel.AddComponent<Button>();
            cancelButton.targetGraphic = cancel.GetComponent<Image>();
            V6ButtonPressFeedback.Attach(cancel);
            cancelButton.onClick.AddListener(CancelResetKeyLayout);

            var confirm = V5Ui.Panel(dialog, "Op Reset", new Vector2(252f, 50f),
                new Color(0xE5 / 255f, 0x3E / 255f, 0x3E / 255f), 8f);
            V6Layout.PlaceAt(confirm, 560f, 432f, new Vector2(252f, 50f));
            var confirmLabel = V5Ui.Text(confirm, "Label", "戻す", 26f, V5Tokens.ButtonPrimaryLabel,
                TextAlignmentOptions.Center);
            V6Layout.StretchFill(confirmLabel.rectTransform);
            var confirmButton = confirm.AddComponent<Button>();
            confirmButton.targetGraphic = confirm.GetComponent<Image>();
            V6ButtonPressFeedback.Attach(confirm);
            confirmButton.onClick.AddListener(ApplyResetKeyLayout);
        }
    }
}
