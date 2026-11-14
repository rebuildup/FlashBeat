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
    /// V6TitleView のメインゾーン構築部分 (Issue #385 Phase 2.9 で分割)。
    /// Logo とタブ行 (GameStart / History / Ranking / Option) の生成、
    /// タブ一覧の構築と 1 枚ごとの描画を持つ。
    /// partial class のためフィールド・メソッドは V6TitleView 本体と共有する。
    /// </summary>
    public sealed partial class V6TitleView
    {
        // --- メイン (x24 y96 1680x780) ---

        private void BuildMain(UpdateFlowModel model)
        {
            var main = mainZone.gameObject;

            // Logo (design CnRJj): Main ローカル (0,180) 1680×332、assets/13.png contain。
            // タイトル動画 (ゲーム自身の背景) の上に重ねて描く。
            var logo = new GameObject("Logo", typeof(RectTransform), typeof(Image));
            logo.transform.SetParent(main.transform, false);
            // 13.png は importer が textureType: Default (spriteMode: 0) のため
            // Resources.Load<Sprite> は常に null を返す (実機で白クアッドに
            // なっていた原因)。Texture2D で読んでスプライトを作る。
            var logoTex = Resources.Load<Texture2D>("V5/Common/13");
            var logoImage = logo.GetComponent<Image>();
            if (logoTex != null)
            {
                // Issue #401: タイトルへ戻るたび Sprite.Create していた。
                // 原 Texture から 1 枚だけ実体化して共有する。
                logoImage.sprite = V6Sprites.FromTexture(logoTex, "V6TitleLogo");
            }
            logoImage.preserveAspect = true;
            logoImage.raycastTarget = false;
            if (logoImage.sprite == null)
            {
                // スプライトが読めないとき (LFS 未取得のビルド等) は白クアッドを
                // 描かない: Image はスプライト無しで白く塗られるため、
                // タイトル動画 (ゲーム背景) が隠れてしまう。動画を見せる。
                logoImage.enabled = false;
            }
            V6Layout.Place(logo.GetComponent<RectTransform>(), V6TitleLogic.LogoPos, V6TitleLogic.LogoSize);

            // Controls · タブ (FHcxk): (280,552) 1120×48, $overlay r8, タブ各 280×48。
            // V6UiMode.TitleTabs が false の間は描かない (2026-11-08 オーナー指示:
            // 実機で中段のバーを消す。フッターの START / 設定ボタンが同じ操作を
            // 受け持つため二重表示だった)。tabList 自体は BuildTabList が作り続けるので
            // キー操作 (↑↓/Space/O) は無効化しない。
            if (V6UiMode.TitleTabs)
            {
                var tabs = V6Controls.Panel(main, "Tabs", V6TitleLogic.TabsPos, V6TitleLogic.TabsSize,
                    V6Tokens.Overlay, V6Tokens.RadiusMedium);
                for (int i = 0; i < tabList.Count; i++)
                {
                    BuildTab(tabs, tabList[i], i);
                }
            }

        }

        /// <summary>
        /// タブの一覧を作る。操作が設定されているものだけを並べる
        /// (押しても何も起きないボタンを作らない)。
        /// アイコン / ラベル / キーは design QJtTv のタブ 4 枚 (play/history/trophy/settings)。
        /// Start / Option のキーは実際の割り当て (KeyActions) から取る
        /// (design 値 Space / O は既定割り当てと同じ)。
        /// History / Ranking は KeyActions に割り当てが無いため固定キー
        /// (V6TitleLogic の単一ソース。TitleSceneManager が poll するキーと同じ)。
        /// </summary>
        private void BuildTabList()
        {
            tabList.Clear();
            tabList.Add(new Tab
            {
                Name = "Tab GameStart",
                IconSprite = "play",
                Label = "GameStart",
                Key = ShortcutKeyCap.PrimaryText(KeyActions.KeyAction.Start),
                Action = onStart,
            });

            if (onHistory != null)
            {
                tabList.Add(new Tab
                {
                    Name = "Tab History",
                    IconSprite = "history",
                    Label = "History",
                    Key = V6TitleLogic.HistoryTabKey,
                    Action = onHistory,
                });
            }

            if (onRanking != null)
            {
                tabList.Add(new Tab
                {
                    Name = "Tab Ranking",
                    IconSprite = "trophy",
                    Label = "Ranking",
                    Key = V6TitleLogic.RankingTabKey,
                    Action = onRanking,
                });
            }

            tabList.Add(new Tab
            {
                Name = "Tab Option",
                IconSprite = "settings",
                Label = "Option",
                Key = ShortcutKeyCap.PrimaryText(KeyActions.KeyAction.Option),
                Action = onOption,
            });

            if (selectedTab >= tabList.Count)
            {
                selectedTab = 0;
            }
        }

        /// <summary>
        /// タブ 1 枚 (design i6ziD 等): 280×48、gap8 padding[0,10]、
        /// アイコン 24×24 / Label fs26 w500 / 右端に KeyCap (fs22、高さ 36)。
        /// 選択中のタブだけ $k6 (#525252) 背景。
        /// </summary>
        private void BuildTab(GameObject tabs, Tab tab, int index)
        {
            bool selected = index == selectedTab;
            var tabGo = V6Controls.Panel(tabs, tab.Name,
                new Vector2(V6TitleLogic.TabSize.x * index, 0f), V6TitleLogic.TabSize,
                selected ? V6Tokens.Key6 : Color.clear, 0f);
            MakeButton(tabGo, tab.Action);

            // アイコン (design 24×24、縦中央)。
            if (!string.IsNullOrEmpty(tab.IconSprite))
            {
                var iconColor = selected ? V6Tokens.Text : V6Tokens.TextMuted;
                var icon = V6Icons.Create(tabGo, tab.IconSprite, V6TitleLogic.TabIconSize, iconColor, "Icon");
                if (icon != null)
                {
                    var iconRt = icon.GetComponent<RectTransform>();
                    iconRt.anchorMin = new Vector2(0f, 0.5f);
                    iconRt.anchorMax = new Vector2(0f, 0.5f);
                    iconRt.pivot = new Vector2(0.5f, 0.5f);
                    iconRt.anchoredPosition = new Vector2(
                        V6TitleLogic.TabPaddingX + V6TitleLogic.TabIconSize * 0.5f, 0f);
                }
            }

            // ラベル (design fs26 w500、ノードは y5 / 高さ 38)。アイコンの右から。
            float labelX = V6TitleLogic.TabPaddingX + V6TitleLogic.TabIconSize + V6TitleLogic.TabGap;
            var labelColor = selected ? V6Tokens.Text : V6Tokens.TextMuted;
            V6Controls.Text(tabGo, "Label", tab.Label, new Vector2(labelX, 5f), new Vector2(160f, 38f),
                V6TitleLogic.TabLabelFontSize, labelColor, TextAlignmentOptions.Left);

            // 右端の KeyCap (design: 高さ 32・padding [0,8]、y8)。
            if (!string.IsNullOrEmpty(tab.Key))
            {
                float keyW = Mathf.Ceil(V6Ui.LabelAdvanceEm(tab.Key) * V6Controls.ButtonKeyFontSize)
                    + V6Controls.ButtonKeyCapPaddingX * 2f;
                V6Controls.KeyCap(tabGo, tab.Key,
                    new Vector2(V6TitleLogic.TabSize.x - V6TitleLogic.TabPaddingX - keyW,
                        (V6TitleLogic.TabSize.y - V6Controls.ButtonKeyCapHeight) * 0.5f),
                    new Vector2(keyW, V6Controls.ButtonKeyCapHeight), V6Controls.ButtonKeyCapPaddingX);
            }
        }

    }
}
