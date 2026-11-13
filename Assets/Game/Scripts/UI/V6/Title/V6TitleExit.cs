using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FlashBeat.Update;
using FlashBeat.UI.V5;
using FlashBeat.UI.V6;
using FlashBeat.UI.V6.Common;

namespace FlashBeat.UI.V6.Title
{
    /// <summary>
    /// 01 Title ヘッダー左の Exit (design QJtTv / CtA3V)。
    ///
    /// 正本の Exit フレーム (CtA3V, Header ローカル (0,20) 162×56) は
    /// **fill を持たない**入れ物で、中に 3 つの子を置く:
    /// - Icon Button (Z6C0w): (0,0) 56×56、$k4 r8、log-out 32 @(12,12) $text-muted
    /// - KeyCap (D7mVkL): (68,12) 29×32、#3A3A3A r4、Key fs22
    /// - Label (qttPo): (109,6.5) 53×43、fs30 $text-muted "Exit"
    ///
    /// 旧実装は Exit 全体を 162×56 の 1 枚 ($k4 r8) で塗っていたため、実機では
    /// アイコン・キー・ラベルが 1 本の横長ピルに乗って見えていた (design では
    /// 背景が透明で、塗りは Icon Button の 56×56 だけ)。r13 実機で確認。
    /// </summary>
    public static class V6TitleExit
    {
        /// <summary>Exit 一式を組み立てる (Header ゾーン直下)。クリックは Exit 全体が受ける。</summary>
        public static GameObject Build(GameObject header, Action onExit)
        {
            // CtA3V: 塗りなしの入れ物。透明でもクリックは受ける (終了という実操作)。
            var group = V6Controls.Panel(header, "Exit", V6TitleLogic.BackButtonPos,
                V6TitleLogic.BackButtonSize, Clear(), 0f);
            var button = group.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            var nav = button.navigation;
            nav.mode = Navigation.Mode.None;
            button.navigation = nav;
            if (onExit != null)
            {
                button.onClick.AddListener(() => onExit());
            }

            // Z6C0w: 56×56 $k4 r8。design で塗られているのはここだけ。
            var iconButton = V6Controls.Panel(group, "Icon Button", V6TitleLogic.BackIconButtonPos,
                new Vector2(V6TitleLogic.BackIconButtonSize, V6TitleLogic.BackIconButtonSize),
                V6Tokens.Key4, V6Tokens.RadiusMedium);
            // 押下リングは見た目のボタン (アイコン) 側に出す。group 全体は
            // 透明な当たり判定なので、そこに付けるとヘッダ全体に輪郭が出る。
            V6ButtonPressFeedback.Attach(iconButton);

            var icon = V6Icons.Create(iconButton, "log-out", V6TitleLogic.BackIconSize,
                V6Tokens.TextMuted, "Icon");
            if (icon != null)
            {
                var iconRect = icon.rectTransform;
                iconRect.anchorMin = new Vector2(0f, 1f);
                iconRect.anchorMax = new Vector2(0f, 1f);
                iconRect.pivot = new Vector2(0f, 1f);
                iconRect.anchoredPosition = new Vector2(V6TitleLogic.BackIconPos.x,
                    -V6TitleLogic.BackIconPos.y);
                iconRect.sizeDelta = new Vector2(V6TitleLogic.BackIconSize, V6TitleLogic.BackIconSize);
            }

            // D7mVkL: 実キーを表示する KeyCap。幅は design の hug (E で 29) を
            // TMP 実測で求める (キー割り当てが変わっても文字がはみ出さない)。
            string exitKey = ShortcutKeyCap.PrimaryText(KeyActions.KeyAction.Exit);
            if (string.IsNullOrEmpty(exitKey))
            {
                exitKey = "E";
            }

            float capW = Mathf.Ceil(V6Ui.LabelAdvanceEm(exitKey) * V6Controls.ButtonKeyFontSize)
                + V6TitleLogic.BackKeyCapPaddingX * 2f;
            V6Controls.KeyCap(group, exitKey, V6TitleLogic.BackKeyCapPos,
                new Vector2(capW, V6TitleLogic.BackKeyCapHeight), V6TitleLogic.BackKeyCapPaddingX);

            // qttPo: ラベル (レイアウト上は縦中央寄せの 43 高、左揃え)。
            V6Controls.Text(group, "Label", "Exit", V6TitleLogic.BackLabelPos,
                V6TitleLogic.BackLabelSize, V6Controls.ButtonFontSize, V6Tokens.TextMuted,
                TextAlignmentOptions.MidlineLeft);

            return group;
        }

        private static Color Clear()
        {
            return new Color(0f, 0f, 0f, 0f);
        }
    }
}
