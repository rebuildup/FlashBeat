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
    /// V6SelectView のヘッダー部分 (Issue #385 Phase 2.4 で分割)。
    /// Header ゾーン (戻るボタン・タイトル・検索欄・件数・状態メッセージ・フィルターボタン) の
    /// 構築と差分更新、検索欄の表示文字列、キーキャップ部品を持つ。
    /// partial class のためフィールド・メソッドは V6SelectView 本体と共有する。
    /// </summary>
    public sealed partial class V6SelectView
    {
        // --- ヘッダー (x24 y0 1680x96) ---

        // 外周 stroke は描かない (Issue #374)。design OENRv は共有フレーム rw7zx の
        // 外周線を全ゾーンで #00000000 に置換しており、枠線は画面ごとに出さない
        // (docs/ui/v6-screen-contract.md §5)。四隅のガイド線として残さないこと。

        private void BuildHeader(int filteredCount)
        {
            // 帯 + 戻るボタン + タイトルは共通部品 (Issue #385)。
            // 帯は Image 付きの透明パネル (従来実装どおり panel=true)。
            var header = V6Header.Create(frame.gameObject, true);

            // 戻るボタン。設計 OENRv: ヘッダー左端 (フレーム (24,20))。
            // 位置の正本は V6SelectViewLogic.HeaderBackButton() (design (0,20) 56×56)。
            var backRect = V6SelectViewLogic.HeaderBackButton();
            V6Header.BackButton(header, "Back Button",
                FlashBeat.UI.V5.ActionKeyHint.Primary(KeyActions.KeyAction.Back),
                () =>
                {
                    if (onBack != null) onBack();
                }, new Vector2(backRect.X, backRect.Y));

            var titleRect = V6SelectViewLogic.HeaderTitle();
            V6Header.Title(header, "選曲",
                new Vector2(titleRect.X, titleRect.Y), new Vector2(titleRect.W, titleRect.H));

            // 右上: 検索欄 / 件数 / 状態メッセージ / フィルターボタン
            // 右上情報ゾーンは header-local x=840..1680 (padding 24 / gap 12)。
            // Search(幅 580) → Count(140) → Filter(48)。
            var searchRect = V6SelectViewLogic.HeaderSearch();
            var search = V6Button.Panel(header, "Search", new Vector2(searchRect.W, searchRect.H),
                V5Tokens.Overlay, 8f, FocusSearch);
            V6Layout.Place(search, searchRect.X, searchRect.Y);
            // design OENRv `/W1K1R/sIkNa/FiU1X`: lucide "search" 20x20 @(12,14)。
            // これが無いと design の「虫めがね + タイプして検索」にならない。
            var searchIcon = V6Icons.Create(search, "search", 20f, V5Tokens.TextDim);
            if (searchIcon != null)
            {
                var iconRect = V6SelectViewLogic.HeaderSearchIcon();
                V6Layout.Place(searchIcon, iconRect.X, iconRect.Y);
            }

            var queryRect = V6SelectViewLogic.HeaderSearchQuery();
            var queryText = V5GameUi.Text(search, "Query", SearchDisplayText(), 22f,
                searchFocused ? V5Tokens.Text : V5Tokens.TextDim, TextAlignmentOptions.Left);
            SingleLine(queryText);
            V6Layout.Place(queryText, queryRect.X, queryRect.Y, queryRect.W, queryRect.H);
            var capRect = V6SelectViewLogic.HeaderSearchCap();
            // design `/W1K1R/sIkNa/mMt7c/T3nJjq`: Key fs22 / h32。高さ比 (22/36) に
            // 任せると fs19 まで縮むので、design の fs22 を明示する。
            var cap = KeyCap(search, "A-Z", V6SelectViewLogic.CapFontSize,
                capRect.W, capRect.H);
            V6Layout.Place(cap, capRect.X, capRect.Y);

            var count = V5GameUi.Text(header.gameObject, "Count",
                $"{filteredCount} / {data.AllSongs.Count}", 22f, V5Tokens.TextMuted,
                TextAlignmentOptions.Right);
            var countRect = V6SelectViewLogic.HeaderCount();
            V6Layout.Place(count, countRect.X, countRect.Y, countRect.W, countRect.H);

            // Issue #427: ヘッダーにはエラーだけを出す。取得中の「ランキングを
            // 取得しています」は SCORE RANKING パネル内に出す (空白地への浮き表示をやめる)。
            if (data.HasError && !string.IsNullOrEmpty(data.StatusMessage))
            {
                var status = V5GameUi.Text(header.gameObject, "Status", data.StatusMessage, 18f,
                    V5Tokens.DangerLight, TextAlignmentOptions.Left);
                V6Layout.Place(status, 864f, 74f, 792f, 20f);
            }

            var filter = V6Button.Panel(header, "Filter Button", new Vector2(48f, 48f),
                unplayedOnly ? V5Tokens.Key6 : V5Tokens.Key4, 8f, ToggleUnplayedFilter);
            var filterRect = V6SelectViewLogic.HeaderFilter();
            V6Layout.Place(filter, filterRect.X, filterRect.Y);
            V6Icons.Create(filter, "list-filter", 24f, V5Tokens.TextMuted);
        }

        /// <summary>
        /// ヘッダー: scaffold が無ければ作り、可変値 (検索語・件数・状態メッセージ・
        /// フィルターボタン色) を既存要素へ適用する。
        /// </summary>
        private void EnsureHeader(int filteredCount)
        {
            var header = FindZone("Header");
            if (header == null)
            {
                BuildHeader(filteredCount);
                return;
            }

            UpdateHeader(header, filteredCount);
        }

        private void UpdateHeader(GameObject header, int filteredCount)
        {
            var queryT = header.transform.Find("Search/Query");
            var queryText = queryT != null ? queryT.GetComponent<TextMeshProUGUI>() : null;
            if (queryText != null)
            {
                queryText.text = SearchDisplayText();
                queryText.color = searchFocused ? V5Tokens.Text : V5Tokens.TextDim;
            }

            var countT = header.transform.Find("Count");
            var countText = countT != null ? countT.GetComponent<TextMeshProUGUI>() : null;
            if (countText != null)
            {
                countText.text = $"{filteredCount} / {data.AllSongs.Count}";
            }

            // Issue #427: ヘッダーの Status はエラー時だけ存在する可変要素。
            // 必要になったら作り、不要になったら外す (毎回の再生成はしない)。
            Transform statusT = header.transform.Find("Status");
            bool needStatus = data.HasError && !string.IsNullOrEmpty(data.StatusMessage);
            if (needStatus)
            {
                TextMeshProUGUI status;
                if (statusT == null)
                {
                    status = V5GameUi.Text(header, "Status", data.StatusMessage, 18f,
                        V5Tokens.DangerLight, TextAlignmentOptions.Left);
                    V6Layout.Place(status, 864f, 74f, 792f, 20f);
                }
                else
                {
                    status = statusT.GetComponent<TextMeshProUGUI>();
                    if (status != null)
                    {
                        status.text = data.StatusMessage;
                    }
                }
            }
            else if (statusT != null)
            {
                FlashBeat.UI.V5.V5GameUi.DestroyNow(statusT.gameObject);
            }

            var filterT = header.transform.Find("Filter Button");
            var filterImage = filterT != null ? filterT.GetComponent<Image>() : null;
            if (filterImage != null)
            {
                filterImage.color = unplayedOnly ? V5Tokens.Key6 : V5Tokens.Key4;
            }
        }

        private string SearchDisplayText()
        {
            string query = data.Query ?? "";
            if (searchFocused)
            {
                return query + "|";
            }

            return query.Length > 0 ? query : "タイプして検索";
        }

        /// <summary>
        /// キーキャップ (design 空間のフォントサイズと幅で自前で組む)。
        /// V5Ui.KeyCap は UISizeProfile.Current.FsS (解像度依存) をラベルに使うため、
        /// 実解像度でキャップの中で折り返していた。v6 は design px で決める。
        /// </summary>
        private static GameObject KeyCap(GameObject parent, string label, float fontSize, float width, float height)
        {
            var cap = V5Ui.Panel(parent, "KeyCap " + label, new Vector2(width, height), V5Tokens.ControlBg,
                V5Tokens.RadiusSmall);
            var tmp = V5GameUi.Text(cap, "Key", label, fontSize, V5Tokens.TextMuted, TextAlignmentOptions.Center);
            SingleLine(tmp);
            var rt = tmp.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return cap;
        }
    }
}
