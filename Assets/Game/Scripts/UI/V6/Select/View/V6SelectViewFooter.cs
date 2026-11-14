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
    /// V6SelectView のフッター部分 (Issue #385 Phase 2.4 で分割)。
    /// Footer ゾーン (キー凡例・設定ボタン・Ready ボタン) の構築と、
    /// Ready の有効/無効に応じた再構築判定を持つ。
    /// partial class のためフィールド・メソッドは V6SelectView 本体と共有する。
    /// </summary>
    public sealed partial class V6SelectView
    {
        // --- フッター (x24 y876 1680x96) ---

        private void BuildFooter()
        {
            var footer = V6Layout.Place(V5Ui.Panel(frame.gameObject, "Footer", new Vector2(1680f, 96f), Color.clear, 0f), 24f, 876f);

            // 凡例 (design OENRv Zone · キー操作: y30 / 項目間 gap24 / 項内 gap8 /
            // KeyCap h32 pad[0,8] / Key・Label fs22)。幅は共通部品 V6Legend が TMP 実測で
            // 詰める (design 実測 x = 0 / 155 / 315 / 442、zone 幅 593)。
            // 確定キーはキー設定 (KeyActions) の正本から取る (#142 / #221)。
            string confirmKey = FlashBeat.UI.V5.ActionKeyHint.Primary(KeyActions.KeyAction.Confirm);
            var legendItems = V6SelectViewLogic.FooterLegend(confirmKey);
            float legendX = 0f;
            for (int i = 0; i < legendItems.Length; i++)
            {
                legendX = V6Legend.Item(footer, legendX, V6Legend.Y,
                    legendItems[i].Key, legendItems[i].Label);
            }

            // 右下: 設定 (secondary) + Ready (primary)。design 座標のまま右詰め。
            // 設定パネル展開中 (wSjAm) は「閉じる (Esc)」Secondary に差し替える。
            // 中身は正本 Prefab/Button Secondary と同じ中央揃え (共通部品に任せる)。
            var settingsRect = V6SelectViewLogic.FooterSettings(settingsOpen);
            string settingsKey = FlashBeat.UI.V5.ActionKeyHint.Primary(settingsOpen
                ? KeyActions.KeyAction.Back
                : KeyActions.KeyAction.Option);
            var settingsBtn = V6Controls.ButtonSecondary(footer,
                settingsOpen
                    ? V6SelectViewLogic.FooterSettingsOpenLabelText
                    : V6SelectViewLogic.FooterSettingsLabelText,
                new Vector2(settingsRect.X, settingsRect.Y),
                new Vector2(settingsRect.W, settingsRect.H),
                settingsOpen
                    ? V6SelectViewLogic.FooterSettingsOpenIconName
                    : V6SelectViewLogic.FooterSettingsIconName,
                settingsKey);
            settingsBtn.name = settingsOpen ? "Op Close" : "Button Settings";
            var settingsButton = settingsBtn.GetComponent<Button>();
            if (settingsButton != null)
            {
                settingsButton.transition = Selectable.Transition.None;
                var nav = settingsButton.navigation;
                nav.mode = Navigation.Mode.None;
                settingsButton.navigation = nav;
                if (settingsOpen)
                {
                    settingsButton.onClick.AddListener(CloseSettings);
                }
                else
                {
                    settingsButton.onClick.AddListener(OpenSettings);
                }
            }

            var readyRect = V6SelectViewLogic.FooterReady();
            // 絞り込みで隠れた選択は Ready できない (#221/#222)。無効のときは
            // 押しても反応しないよう、クリックを張らず弱い色で描画する。
            bool readyEnabled = ReadyAvailable();
            var ready = V6Controls.ButtonPrimary(footer, "Ready",
                new Vector2(readyRect.X, readyRect.Y),
                new Vector2(readyRect.W, readyRect.H), "play", confirmKey);
            ready.name = "Op Ready";
            var readyButton = ready.GetComponent<Button>();
            if (readyButton != null)
            {
                readyButton.transition = Selectable.Transition.None;
                var nav = readyButton.navigation;
                nav.mode = Navigation.Mode.None;
                readyButton.navigation = nav;
                if (readyEnabled)
                {
                    // Select→Game は画面遷移なので、押下リングが見えるだけ遅らせる。
                    // 遅延中に絞り込みで選択が隠れた場合に備え、実行直前に
                    // ReadyAvailable を再確認する (CodeRabbit #438)。
                    readyButton.onClick.AddListener(() =>
                    {
                        V6ButtonPressFeedback.RunAfterFeedback(ready, () =>
                        {
                            if (ReadyAvailable() && onReady != null)
                            {
                                onReady();
                            }
                        });
                    });
                }
                else
                {
                    // design に無効状態の見本は無いが、押せないことが分かるよう
                    // 背景を $k1、文字・アイコンを $text-dim に落とす (旧実装と同じ扱い)。
                    readyButton.interactable = false;
                    var bg = ready.GetComponent<Image>();
                    if (bg != null) bg.color = V5Tokens.Key1;
                    foreach (var graphic in ready.GetComponentsInChildren<Graphic>(true))
                    {
                        var tmp = graphic as TextMeshProUGUI;
                        if (tmp != null) tmp.color = V5Tokens.TextDim;
                        else if (graphic is Image && graphic.gameObject.name.StartsWith("Icon"))
                            graphic.color = V5Tokens.TextDim;
                    }
                }
            }
        }

        /// <summary>
        /// フッターは骨格として保持するが、Ready の有効/無効 (選択曲が絞り込みで
        /// 隠れるか) は表示に出るので、状態が変わったときだけフッターを作り直す。
        /// </summary>
        private void EnsureFooter()
        {
            bool ready = ReadyAvailable();
            if (FindZone("Footer") == null || ready != footerReadyEnabled)
            {
                DestroyZone("Footer");
                BuildFooter();
                footerReadyEnabled = ready;
            }
        }
    }
}
