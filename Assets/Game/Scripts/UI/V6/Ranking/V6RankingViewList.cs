using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using FlashBeat.UI.V5;
using FlashBeat.UI.V6;
using FlashBeat.UI.V6.Common;
using FlashBeat.UI.V6.Select;
using FlashBeat.UI.V6.Select.Data;
using FlashBeat.UI.V6.Select.View;

namespace FlashBeat.UI.V6.Ranking
{
    /// <summary>
    /// V6RankingView の左列 (曲リスト + ドットインジケータ + スクロール) 構築
    /// (Issue #385 Phase 2.7 で分割)。partial class のためフィールド・メソッドは
    /// V6RankingView 本体と共有する。
    /// </summary>
    public sealed partial class V6RankingView
    {
        // --- 左列: 曲リスト (x0 w560 #131313CC r16) ---

        /// <summary>
        /// Issue #415: リストは骨格 (パネル・Rows コンテナ) を保持し、行はスロット単位で
        /// 既存オブジェクトを更新する。空状態 ↔ 曲表示のモード変化のときだけ Rows を作り直す。
        /// </summary>
        private void EnsureSongList(GameObject mainZone, IReadOnlyList<V6SongEntry> songs, int selectIndex)
        {
            GameObject list = listGo != null ? listGo : mainZone.transform.Find("Song List")?.gameObject;
            if (list == null)
            {
                list = CreateListScaffold(mainZone);
            }

            UpdateListRows(list, songs, selectIndex);
            UpdateDots(list);
        }

        private GameObject CreateListScaffold(GameObject mainZone)
        {
            var list = V6Layout.Place(V5Ui.Panel(mainZone, "Song List", new Vector2(ListW, ListH),
                V5Tokens.FromHex("#131313CC"), 16f), 0f, 0f);
            AddScrollCatcher(list, 0);

            var rows = V5Ui.Node(list, "Rows");
            V6Layout.Place(rows.GetComponent<RectTransform>(), 0f, 0f, ListW, ListH);
            listGo = list;
            rowsGo = rows;

            BuildScrollButton(list, "Scroll Up", 0f, "chevron-up", -1);
            BuildScrollButton(list, "Scroll Down", 732f, "chevron-down", 1);
            return list;
        }

        private void UpdateListRows(GameObject list, IReadOnlyList<V6SongEntry> songs, int selectIndex)
        {
            GameObject rows = rowsGo != null ? rowsGo : list.transform.Find("Rows")?.gameObject;
            if (rows == null)
            {
                rows = V5Ui.Node(list, "Rows");
                V6Layout.Place(rows.GetComponent<RectTransform>(), 0f, 0f, ListW, ListH);
                rowsGo = rows;
            }

            string mode = songs.Count == 0 ? "empty" : "rows";
            if (mode != listMode)
            {
                // モード変化 (曲表示 ↔ 空状態) は骨格変更扱い。Rows の中だけを作り直す。
                V6Hierarchy.ClearChildren(rows);
                listRowObjects.Clear();
                listMode = mode;
                if (mode == "empty")
                {
                    var empty = V5GameUi.Text(rows.gameObject, "No Songs", "曲がありません", 24f, V5Tokens.TextDim,
                        TextAlignmentOptions.Center);
                    V6Layout.StretchCenter(empty.GetComponent<RectTransform>(), ListW, ListH);
                    return;
                }
            }

            int first = listScroll;
            for (int slot = 0; slot < ListVisibleRows; slot++)
            {
                int i = first + slot;
                bool valid = i >= 0 && i < songs.Count;
                float y = RowY0 + RowStep * slot;
                GameObject row = slot < listRowObjects.Count ? listRowObjects[slot] : null;
                if (row == null)
                {
                    if (!valid)
                    {
                        if (slot < listRowObjects.Count) listRowObjects[slot] = null;
                        continue;
                    }

                    row = CreateSongRow(rows, songs[i], i == selectIndex, y);
                    if (slot < listRowObjects.Count) listRowObjects[slot] = row;
                    else listRowObjects.Add(row);
                    continue;
                }

                if (!valid)
                {
                    V6Hierarchy.DetachAndDestroy(row);
                    listRowObjects[slot] = null;
                    continue;
                }

                UpdateSongRow(row, songs[i], i == selectIndex, y);
            }

            while (listRowObjects.Count > ListVisibleRows)
            {
                int tail = listRowObjects.Count - 1;
                V6Hierarchy.DetachAndDestroy(listRowObjects[tail]);
                listRowObjects.RemoveAt(tail);
            }
        }

        private GameObject CreateSongRow(GameObject parent, V6SongEntry song, bool selected, float y)
        {
            // 行の内部座標は共通部品 V6Lists.SongRow に一本化 (design mcCOI 実測値)。
            var row = V6Lists.SongRow(parent, song.Title, song.Artist,
                song.Level > 0 ? song.Level.ToString() : "–", song.BestGrade,
                new Vector2(RowX, y), new Vector2(RowW, RowH), selected);
            var button = V6Button.Make(row, null).GetComponent<Button>();
            int songId = song.SongId;
            button.onClick.AddListener(() => data.Select(songId));

            SetJacket(V6Lists.Thumb(row), song, V6Tokens.Key2);
            return row;
        }

        /// <summary>既存の曲行を新しい曲データで書き換える (行オブジェクトは再利用)。</summary>
        private void UpdateSongRow(GameObject row, V6SongEntry song, bool selected, float y)
        {
            var rt = row.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition = new Vector2(RowX, -y);
            }

            var bg = row.GetComponent<Image>();
            if (bg != null)
            {
                bg.color = selected ? V6Tokens.Key6 : V6Tokens.Key1;
            }

            V6Hierarchy.SetChildText(row, "Level/Level", song.Level > 0 ? song.Level.ToString() : "–");
            V6Hierarchy.SetChildText(row, "Title", song.Title);
            V6Hierarchy.SetChildText(row, "Composer", song.Artist);
            V6Hierarchy.SetChildText(row, "Grade/Grade", string.IsNullOrEmpty(song.BestGrade) ? "–" : song.BestGrade);
            row.name = "SongRow " + song.Title;

            var button = row.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                int songId = song.SongId;
                button.onClick.AddListener(() => data.Select(songId));
            }

            SetJacket(V6Lists.Thumb(row), song, V6Tokens.Key2);
        }

        /// <summary>
        /// ドットインジケータの増減分更新。件数が変わったときだけ作り直し、
        /// 選択の移動では既存ドットの色とサイズだけを差分適用する。
        /// </summary>
        private void UpdateDots(GameObject list)
        {
            IReadOnlyList<V6SongEntry> songs = data.AllSongs;
            int count = songs == null ? 0 : songs.Count;
            if (dotsGo == null || dotsCount != count)
            {
                // 件数変化 = 骨格変更。ドットのみ作り直す。
                GameObject old = list.transform.Find("Dot indicator")?.gameObject;
                V6Hierarchy.DetachAndDestroy(old);

                dotImages.Clear();
                dotRects.Clear();
                dotsGo = CreateDots(list, songs, count);
                dotsCount = count;
            }

            int selectIndex = -1;
            if (data.Selected != null && songs != null)
            {
                for (int i = 0; i < songs.Count; i++)
                {
                    if (songs[i] != null && songs[i].SongId == data.Selected.SongId) { selectIndex = i; break; }
                }
            }
            int active = V6SelectViewLogic.DotActiveIndex(selectIndex, count);
            float[] offsets = V6SelectViewLogic.DotOffsets(count, 0f);
            for (int i = 0; i < count; i++)
            {
                bool isActive = i == active;
                float d = isActive ? V6SelectViewLogic.DotActiveSize : V6SelectViewLogic.DotSize;
                if (i < dotImages.Count && dotImages[i] != null)
                {
                    // Issue #427: 選択中は v6 の選択色 (Key6)。
                    dotImages[i].color = isActive ? V6Tokens.Key6 : V5Tokens.FromHex("#FFFFFF26");
                }

                if (i < dotRects.Count && dotRects[i] != null)
                {
                    dotRects[i].sizeDelta = new Vector2(d, d);
                    dotRects[i].anchoredPosition = new Vector2(V6SelectViewLogic.DotRectX(d), -offsets[i]);
                }
            }
        }

        private GameObject CreateDots(GameObject list, IReadOnlyList<V6SongEntry> songs, int count)
        {
            var dots = V5Ui.Node(list, "Dot indicator");
            float total = V6SelectViewLogic.DotBottom(count, 0f);
            // Issue #427: Select と同じくレールをリストパネルの上下中央に置く。
            V6Layout.Place(dots.GetComponent<RectTransform>(), V6SelectViewLogic.DotX, V6SelectViewLogic.DotRailTopY(count),
                V6SelectViewLogic.DotActiveSize, total);

            float[] offsets = V6SelectViewLogic.DotOffsets(count, 0f);
            for (int i = 0; i < count; i++)
            {
                float d = V6SelectViewLogic.DotSize;
                var dot = V5Ui.Panel(dots, "Dot " + (i + 1), new Vector2(d, d),
                    V5Tokens.FromHex("#FFFFFF26"), V6SelectViewLogic.DotRadius);
                V6Layout.Place(dot, V6SelectViewLogic.DotRectX(d), offsets[i]);
                dotImages.Add(dot.GetComponent<Image>());
                dotRects.Add(dot.GetComponent<RectTransform>());
            }

            return dots;
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
