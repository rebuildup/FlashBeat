using System;
using System.Collections.Generic;
using FlashBeat.Update;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FlashBeat.UI.V5;
using FlashBeat.UI.V6;
using FlashBeat.UI.V6.Common;

namespace FlashBeat.UI.V6.Title
{
    /// <summary>
    /// V6TitleView のフッターゾーン構築部分 (Issue #385 Phase 2.9 で分割)。
    /// Footer 帯 (キー凡例 / 操作ボタン) の生成と、凡例項目・
    /// Button 配線などの uGUI ユーティリティを持つ。
    /// partial class のためフィールド・メソッドは V6TitleView 本体と共有する。
    /// </summary>
    public sealed partial class V6TitleView
    {
        // --- フッター (24,876) 1680x96 (V6Frame の Footer ゾーン内) ---

        private void BuildFooter()
        {
            var footer = footerZone.gameObject;

            // 左下: キー凡例 (design QQ4jM、gap16、3 個)。
            // V6UiMode.TitleLegend が false の間は描かない (2026-11-08 オーナー指示:
            // 実機で左下のヒント行を消す。フッター右下の START / 設定ボタンにも
            // キー表記があるため二重表示だった)。
            if (V6UiMode.TitleLegend)
            {
                // design QQ4jM: KeyCap h36 / pad[0,10] / 項内 gap10 / 項目間 gap16
                // (他画面の h32 / pad[0,8] / gap8 / gap24 とは別設計)。幅は TMP 実測。
                float x = 0f;
                string startKey = ShortcutKeyCap.PrimaryText(KeyActions.KeyAction.Start);
                x = AddLegendItem(footer, x, startKey, "決定");
                x = AddLegendItem(footer, x, "↑↓", "選択");
                string exitKey = ShortcutKeyCap.PrimaryText(KeyActions.KeyAction.Exit);
                AddLegendItem(footer, x, exitKey, "終了");
            }

            // 右下の操作ボタン (design T8HuQS) は正本で `enabled: false`。
            // タブ行 (GameStart / Option) とキー操作が同じ導線を持つので描かない。
            if (!V6UiMode.TitleFooterActions)
            {
                return;
            }

            string optionKey = ShortcutKeyCap.PrimaryText(KeyActions.KeyAction.Option);
            var aux = V6Controls.ButtonSecondary(footer, "設定", V6TitleLogic.FooterAuxPos,
                V6TitleLogic.FooterAuxSize, "settings", string.IsNullOrEmpty(optionKey) ? "O" : optionKey);
            WireClick(aux, onOption);
            string primaryKey = ShortcutKeyCap.PrimaryText(KeyActions.KeyAction.Start);
            var primary = V6Controls.ButtonPrimary(footer, "START", V6TitleLogic.FooterPrimaryPos,
                V6TitleLogic.FooterPrimarySize, "play", string.IsNullOrEmpty(primaryKey) ? "Space" : primaryKey);
            WireClick(primary, onStart);
        }

        /// <summary>
        /// キー凡例 1 個 (design QQ4jM): KeyCap (fs22 / h36 / pad[0,10]) + Label fs22 $text-muted
        /// を gap10 で並べ、項目間 gap16。戻り値は次の項目の x。
        /// </summary>
        private static float AddLegendItem(GameObject legend, float x, string key, string label)
        {
            return V6Legend.Item(legend, x, V6TitleLogic.LegendPos.y, key, label,
                V6Tokens.KeyCapHeight, V6Controls.KeyCapPaddingX, V6TitleLogic.LegendInnerGap,
                V6TitleLogic.LegendGap, V6TitleLogic.LegendFontSize, V6Tokens.Key4, V6Tokens.TextMuted);
        }

        // --- uGUI ユーティリティ (配置は V6Layout、部品は V6Controls に集約) ---

        private static GameObject MakeButton(GameObject go, Action onClick)
        {
            var button = go.GetComponent<Button>();
            if (button == null)
            {
                button = go.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                var nav = button.navigation;
                nav.mode = Navigation.Mode.None;
                button.navigation = nav;
            }

            V6ButtonPressFeedback.Attach(go);
            if (onClick != null)
            {
                // Title のボタンはすべて画面遷移 (Select / Option / 終了) を
                // 起こすので、押下リングが見えるだけ遅らせる。
                button.onClick.AddListener(() => V6ButtonPressFeedback.RunAfterFeedback(go, onClick));
            }

            return go;
        }

        /// <summary>V6Controls が作った Button にクリック時の処理を結線する。</summary>
        private static void WireClick(GameObject go, Action onClick)
        {
            MakeButton(go, onClick);
        }

    }
}
