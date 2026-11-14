using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using FlashBeat.UI.V5;
using FlashBeat.UI.V6.Common;
using FlashBeat.UI.V6.Ranking;
using FlashBeat.UI.V6.Select;
using FlashBeat.UI.V6.Select.Data;
using FlashBeat.UI.V6.Select.View;

namespace FlashBeat.UI.V6.History
{
    /// <summary>
    /// V6HistoryView のヘッダーゾーン部分 (Issue #385 Phase 2.6 で分割)。
    /// 共通フレーム Header ゾーン (design (24,0) 1680×96) の並び替えチップ・
    /// 件数表示と、Footer ゾーン (design (24,876) 1680×96) のキー凡例・
    /// 「この曲へ」ボタンの構築・差分適用を持つ。
    /// partial class のためフィールド・メソッドは V6HistoryView 本体と共有する。
    /// </summary>
    public sealed partial class V6HistoryView
    {
        // --- ヘッダー (共通フレームの Header ゾーン: design (24,0) 1680×96) ---

        /// <summary>
        /// Issue #415: ヘッダーは骨格を保持し、並び順ラベルと件数だけを既存 TMP に適用する。
        /// 骨格が無いときだけ作る (Header ゾーンは V6Frame が所有、中身はここが作る)。
        /// </summary>
        private void EnsureHeader(GameObject headerZone, int playedCount)
        {
            if (headerZone.transform.childCount == 0)
            {
                CreateHeaderScaffold(headerZone);
            }

            if (headerSortText != null)
            {
                headerSortText.text = V6HistoryLogic.SortLabel(sort);
            }

            if (headerCountText != null)
            {
                headerCountText.text = V6HistoryLogic.RecordedCountLabel(playedCount);
            }
        }

        private void CreateHeaderScaffold(GameObject headerZone)
        {
            // 戻るボタン + タイトルは共通部品 (Issue #385)。design q9JZcH:
            // header (0,20) 56×56 / Title (72,20) 400×56 fs38。
            // 確定キーはキー設定の正本から取る。
            V6Header.BackButton(headerZone, "Back Button",
                ActionKeyHint.Primary(KeyActions.KeyAction.Back), () => TriggerBack());
            V6Header.Title(headerZone, "HISTORY");

            // 右上: 並び替えチップ (design q9JZcH/Y8lcc: ヘッダー内 (864,33.5) 104×29 r8) + 件数
            var sortChip = V6Button.Make(V5Ui.Panel(headerZone, "Sort",
                new Vector2(V6HistoryLogic.HeaderSortW, V6HistoryLogic.HeaderSortH), V5Tokens.Key4, 8f),
                ToggleSort);
            V6Layout.Place(sortChip, V6HistoryLogic.HeaderSortX, V6HistoryLogic.HeaderSortY);
            var sortText = V5GameUi.Text(sortChip, "Text", V6HistoryLogic.SortLabel(sort), 20f, V5Tokens.TextMuted,
                TextAlignmentOptions.Left);
            V6Layout.Place(sortText, V6HistoryLogic.HeaderSortTextX, 0f, V6HistoryLogic.HeaderSortTextW,
                V6HistoryLogic.HeaderSortH);
            // design の文言 "スコア順" (4 文字 × fs20 = 80) がテキスト枠ちょうど。
            // 実装のもう一方のラベル "カタログ順" (5 文字 = 100) は枠を超えるため、
            // design の枠は変えずに最小 16px まで自動縮小してチップ内 (右 padding 12) に収める。
            sortText.textWrappingMode = TextWrappingModes.NoWrap;
            sortText.enableAutoSizing = true;
            sortText.fontSizeMax = 20f;
            sortText.fontSizeMin = 16f;
            headerSortText = sortText;

            // design: Count fs24、右端はヘッダー内 1656 (= ページ 1680、情報ゾーン右 padding 24 の内側)
            var count = V5GameUi.Text(headerZone, "Count", V6HistoryLogic.RecordedCountLabel(0), 24f,
                V5Tokens.TextMuted, TextAlignmentOptions.Right);
            V6Layout.Place(count, V6HistoryLogic.HeaderCountX, V6HistoryLogic.HeaderCountY, V6HistoryLogic.HeaderCountW,
                V6HistoryLogic.HeaderCountH, new Vector2(0f, 0.5f));
            headerCountText = count;
        }

        // --- フッター (共通フレームの Footer ゾーン: design (24,876) 1680×96) ---

        /// <summary>
        /// Issue #415: フッターは状態で変わらないので、中身が無いときだけ作る
        /// (既存なら何もしない)。
        /// </summary>
        private void EnsureFooter(GameObject footerZone)
        {
            if (footerZone.transform.childCount > 0)
            {
                return;
            }

            BuildFooter(footerZone);
        }

        private void BuildFooter(GameObject footerZone)
        {
            // design (rw7zx): キー操作ゾーン (0,30)、Legend 間 gap16、Legend 内は
            // KeyCap (高36 / pad[0,10] / fs22) + gap10 + Label fs22 $text-muted。
            // design の凡例はこの 3 つだけ (q9JZcH フッター: ↑↓ 選択 / Space 決定 / Esc 戻る)。
            // S (並べ替え) は実装済みのキー操作だが design に凡例が無いため出さない。
            // (design の「凡例 (左下)」ゾーン (445,30) 240×36 は中身が空。)
            V6Legend.KeyLegend(footerZone, new[]
            {
                new[] { "↑↓", "選択" },
                new[] { "Space", "決定" },
                new[] { "Esc", "戻る" }
            });

            // design (q9JZcH): 右下は「この曲へ」(567,20) 273×56 のみ。
            // 「設定」ゾーン (358,20) 197×56 は design で空のプレースホルダ (子ノード無し / fill 無し)
            // なのでボタンは描かない。O キーの設定遷移 (TriggerOpenSettings) はキーボード操作として残す。
            // icon は design の corner-down-left (旧: music で別アイコンだった)。
            GameObject gotoBtn = V6Controls.ButtonPrimary(footerZone, "この曲へ",
                new Vector2(V6HistoryLogic.FooterGotoButtonX, V6HistoryLogic.FooterButtonY),
                new Vector2(273f, V6HistoryLogic.FooterButtonH), "corner-down-left", "Space");
            V6Button.Configure(gotoBtn, TriggerGotoSong);
        }
    }
}
