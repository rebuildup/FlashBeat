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
    /// V6HistoryView の履歴リスト部分 (Issue #385 Phase 2.6 で分割)。
    /// 左列「History List」(x0 w560 #131313CC r16) の scaffold 構築、
    /// skeleton/empty/rows の表示モード、可視行スロットの差分更新、
    /// スクロールボタンを持つ。
    /// partial class のためフィールド・メソッドは V6HistoryView 本体と共有する。
    /// </summary>
    public sealed partial class V6HistoryView
    {
        // --- 左列: 履歴リスト (x0 w560 #131313CC r16) ---

        /// <summary>
        /// Issue #415: リストは骨格 (パネル・Rows コンテナ) を保持し、行はスロット単位で
        /// 既存オブジェクトを更新する。空状態 ↔ 行表示のモード変化のときだけ Rows を作り直す。
        /// </summary>
        private void EnsureList(GameObject mainZone, List<V6SongEntry> history, int selectIndex)
        {
            GameObject list = historyListGo != null ? historyListGo : mainZone.transform.Find("History List")?.gameObject;
            if (list == null)
            {
                list = CreateListScaffold(mainZone);
            }

            UpdateListRows(list, history, selectIndex);
        }

        private GameObject CreateListScaffold(GameObject mainZone)
        {
            var list = V6Layout.Place(V5Ui.Panel(mainZone, "History List", new Vector2(ListW, ListH),
                V5Tokens.FromHex("#131313CC"), 16f), 0f, 0f);
            AddScrollCatcher(list, 0);

            var rows = V5Ui.Node(list, "Rows");
            V6Layout.Place(rows.GetComponent<RectTransform>(), 0f, 0f, ListW, ListH);
            historyListGo = list;
            historyRowsGo = rows;

            BuildScrollButton(list, "Scroll Up", 0f, "chevron-up", -1);
            BuildScrollButton(list, "Scroll Down", 732f, "chevron-down", 1);
            return list;
        }

        private void UpdateListRows(GameObject list, List<V6SongEntry> history, int selectIndex)
        {
            GameObject rows = historyRowsGo != null ? historyRowsGo : list.transform.Find("Rows")?.gameObject;
            if (rows == null)
            {
                rows = V5Ui.Node(list, "Rows");
                V6Layout.Place(rows.GetComponent<RectTransform>(), 0f, 0f, ListW, ListH);
                historyRowsGo = rows;
            }

            string mode = history.Count == 0 ? "empty" : "rows";
            if (mode != listMode)
            {
                // モード変化 (行表示 ↔ 空状態) は骨格変更扱い。Rows の中だけを作り直す。
                V6Hierarchy.ClearChildren(rows);
                historyRowObjects.Clear();
                listMode = mode;
                if (mode == "empty")
                {
                    // 空状態: プレイ記録がまだ無い (design に History 空状態は無いので
                    // Select の空状態 (AqFtx) と同じ表現に合わせる)。
                    var empty = V6Layout.Place(V5Ui.Panel(rows, "Empty Note", new Vector2(448f, 140f), V5Tokens.Key0, 8f), 56f, 300f);
                    var col = V5GameUi.Column(empty.gameObject, "Inner", 8f, TextAnchor.MiddleCenter);
                    V6Layout.StretchCenter(col.GetComponent<RectTransform>(), 448f, 140f);
                    var emptyIcon = V6Icons.Create(col.gameObject, "music", 32f, V5Tokens.TextDim, "Icon");
                    V5GameUi.Text(col.gameObject, "Title", "プレイ履歴がありません", 24f, V5Tokens.Text,
                        TextAlignmentOptions.Center);
                    V5GameUi.Text(col.gameObject, "Hint", "曲をプレイすると記録がここに残ります", 20f,
                        V5Tokens.TextMuted, TextAlignmentOptions.Center);
                    return;
                }
            }

            // mode == "rows": スロット単位の差分更新。
            int first = listScroll;
            // design q9JZcH: 全行が収まるときはスタックを縦中央に置く (4 行 → 254)。
            // 収まらない / スクロール中のときは上端 RowY0 基準を保つ。
            float y0 = first == 0 ? V6HistoryLogic.HistoryFirstRowY(history.Count) : RowY0;
            for (int slot = 0; slot < ListVisibleRows; slot++)
            {
                int i = first + slot;
                bool valid = i >= 0 && i < history.Count;
                float y = y0 + RowStep * slot;
                GameObject row = slot < historyRowObjects.Count ? historyRowObjects[slot] : null;
                if (row == null)
                {
                    if (!valid)
                    {
                        if (slot < historyRowObjects.Count) historyRowObjects[slot] = null;
                        continue;
                    }

                    row = CreateHistoryRow(rows, history[i], i + 1, i == selectIndex, y);
                    if (slot < historyRowObjects.Count) historyRowObjects[slot] = row;
                    else historyRowObjects.Add(row);
                    continue;
                }

                if (!valid)
                {
                    FlashBeat.UI.V5.V5GameUi.DestroyNow(row);
                    historyRowObjects[slot] = null;
                    continue;
                }

                UpdateHistoryRow(row, history[i], i + 1, i == selectIndex, y);
            }

            while (historyRowObjects.Count > ListVisibleRows)
            {
                int last = historyRowObjects.Count - 1;
                GameObject tail = historyRowObjects[last];
                if (tail != null) FlashBeat.UI.V5.V5GameUi.DestroyNow(tail);
                historyRowObjects.RemoveAt(last);
            }
        }

        /// <summary>既存の履歴行を新しい曲データで書き換える (行オブジェクトは再利用)。</summary>
        private void UpdateHistoryRow(GameObject row, V6SongEntry song, int pos, bool selected, float y)
        {
            var rt = row.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition = new Vector2(RowX, -y);
            }

            var bg = row.GetComponent<Image>();
            if (bg != null)
            {
                bg.color = selected ? V5Tokens.Key6 : V5Tokens.Recessed2;
            }

            V6Hierarchy.SetChildText(row, "Pos/PosText", pos.ToString());
            Transform posText = row.transform.Find("Pos/PosText");
            var posTmp = posText != null ? posText.GetComponent<TextMeshProUGUI>() : null;
            if (posTmp != null)
            {
                posTmp.color = pos == 1 ? V5Tokens.Text : V5Tokens.TextMuted;
            }

            V6Hierarchy.SetChildText(row, "Title", song.Title);
            V6Hierarchy.SetChildText(row, "Score", song.BestScore.ToString());
            V6Hierarchy.SetChildText(row, "Grade/GradeText", string.IsNullOrEmpty(song.BestGrade) ? "–" : song.BestGrade);
            row.name = "History " + song.Title;

            var button = row.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                int songId = song.SongId;
                button.onClick.AddListener(() => SelectSong(songId));
            }
        }

        private GameObject CreateHistoryRow(GameObject parent, V6SongEntry song, int pos, bool selected, float y)
        {
            return BuildHistoryRow(parent, song, pos, selected, y);
        }

        private GameObject BuildHistoryRow(GameObject parent, V6SongEntry song, int pos, bool selected, float y)
        {
            var row = V6Layout.Place(V5Ui.Panel(parent, "History " + song.Title, new Vector2(496f, RowH),
                selected ? V5Tokens.Key6 : V5Tokens.Recessed2, 8f), RowX, y);
            var button = V6Button.Make(row.gameObject, null).GetComponent<Button>();
            int songId = song.SongId;
            button.onClick.AddListener(() => SelectSong(songId));

            // design q9JZcH/F676m/BuztA: Pos (12,14) 36×36 r18
            var posBadge = V6Layout.Place(V5Ui.Panel(row, "Pos", new Vector2(36f, 36f), V5Tokens.Key4, 18f),
                V6HistoryLogic.HistoryPosX, V6HistoryLogic.HistoryPosY);
            var posText = V5GameUi.Text(posBadge.gameObject, "PosText", pos.ToString(), 22f,
                pos == 1 ? V5Tokens.Text : V5Tokens.TextMuted, TextAlignmentOptions.Center);
            V6Layout.StretchCenter(posText.GetComponent<RectTransform>(), 36f, 36f);

            // design: Title (60,15) 220×35 fs24 左揃え 1 行
            var title = V5GameUi.Text(row.gameObject, "Title", song.Title, 24f, V5Tokens.Text);
            V6Layout.Place(title, V6HistoryLogic.HistoryTitleX, V6HistoryLogic.HistoryTitleY,
                V6HistoryLogic.HistoryTitleW, V6HistoryLogic.HistoryInnerH);
            title.textWrappingMode = TextWrappingModes.NoWrap;
            title.overflowMode = TextOverflowModes.Ellipsis;
            // fs24 の行送りは 34.75 で design h35 との余裕が 0.25px しかないため、
            // フォント差で 0 行にならないよう実効高を margin で確保する。
            V5Ui.FitEllipsisToLineHeight(title, V6HistoryLogic.HistoryInnerH);

            // design (BuztA 等) の Score は区切りなしの生の数値 ("987410")。
            // design: Score (292,15) 120×35 fs24 右揃え
            var score = V5GameUi.Text(row.gameObject, "Score", song.BestScore.ToString(), 24f,
                V5Tokens.Text, TextAlignmentOptions.Right);
            V6Layout.Place(score, V6HistoryLogic.HistoryScoreX, V6HistoryLogic.HistoryScoreY,
                V6HistoryLogic.HistoryScoreW, V6HistoryLogic.HistoryInnerH);
            score.textWrappingMode = TextWrappingModes.NoWrap;
            score.overflowMode = TextOverflowModes.Ellipsis;
            V5Ui.FitEllipsisToLineHeight(score, V6HistoryLogic.HistoryInnerH);

            // design: Grade (424,14) 56×36 r4
            var grade = V6Layout.Place(V5Ui.Panel(row, "Grade", new Vector2(56f, 36f), V5Tokens.Key4, 4f),
                V6HistoryLogic.HistoryGradeX, V6HistoryLogic.HistoryGradeY);
            var gradeText = V5GameUi.Text(grade.gameObject, "GradeText",
                string.IsNullOrEmpty(song.BestGrade) ? "–" : song.BestGrade, 22f, V5Tokens.Text,
                TextAlignmentOptions.Center);
            V6Layout.StretchCenter(gradeText.GetComponent<RectTransform>(), 56f, 36f);
            return row;
        }

        private void BuildScrollButton(GameObject list, string name, float y, string iconSprite, int delta)
        {
            // design: Scroll Up/Down は 140×44 $k4 r8 + icon 24×24 ($text-muted)
            GameObject btn = V6Controls.IconButton(list, iconSprite, new Vector2(210f, y),
                new Vector2(140f, 44f), V5Tokens.Key4, V6Tokens.RadiusMedium, 24f, V5Tokens.TextMuted);
            V6Button.Configure(btn, () => ScrollList(delta));
        }
    }
}
