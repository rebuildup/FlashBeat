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
    /// V6SelectView の曲リスト部分 (Issue #385 Phase 2.4 で分割)。
    /// Song List ゾーン (skeleton / empty / rows の表示モード、可視行スロットの再利用、
    /// スクロールボタン、ドットインジケータ) の構築と差分更新を持つ。
    /// partial class のためフィールド・メソッドは V6SelectView 本体と共有する。
    /// </summary>
    public sealed partial class V6SelectView
    {
        // --- 左列: 曲リスト (x0 w560 #131313CC r16) ---

        /// <summary>
        /// 曲リストゾーンが無いとき scaffold (パネル・Rows・スクロールボタン・
        /// ドット容器) を作り、続けて <see cref="UpdateListContent"/> で中身を載せる。
        /// </summary>
        private void EnsureList(IReadOnlyList<V6SongEntry> songs, int selectIndex)
        {
            var list = FindZone("Song List");
            if (list == null)
            {
                BuildListScaffold();
                list = FindZone("Song List");
            }

            if (list == null)
            {
                return;
            }

            UpdateListContent(list, songs, selectIndex);
        }

        private void BuildListScaffold()
        {
            // 位置も design 定数 (frame ローカル (24,96) 560x780 = ListPanelRect) から取る。
            var list = V6Layout.Place(V5Ui.Panel(frame.gameObject, "Song List", new Vector2(ListW, ListH),
                V5Tokens.FromHex("#131313CC"), 16f), V6SelectViewLogic.ListX, V6SelectViewLogic.ListY);
            AddScrollCatcher(list, 0);

            var rows = V5Ui.Node(list, "Rows");
            V6Layout.Place(rows.GetComponent<RectTransform>(), 0f, 0f, ListW, ListH);

            BuildScrollButton(list, "Scroll Up", false);
            BuildScrollButton(list, "Scroll Down", true);
            // ドット容器だけ先に作る。中身 (ドット個) は UpdateDots が曲数に合わせる。
            V5Ui.Node(list, "Dot indicator");
        }

        /// <summary>
        /// 曲リストの中身を現在の状態へ合わせる。skeleton / empty / rows の
        /// モードが変わったときだけ Rows の中身を作り直し、rows では可視行を
        /// スロット単位で再利用する。ドットは曲数が変わったときだけ再生成する。
        /// </summary>
        private void UpdateListContent(GameObject list, IReadOnlyList<V6SongEntry> songs, int selectIndex)
        {
            var rowsT = list.transform.Find("Rows");
            if (rowsT == null)
            {
                return;
            }

            GameObject rows = rowsT.gameObject;
            string mode = V6SelectViewLogic.ShouldShowSkeleton(data.IsLoading, songs.Count) ? "skeleton"
                : songs.Count == 0 ? "empty" : "rows";
            if (mode != listMode)
            {
                Common.V6Hierarchy.ClearChildren(rows);
                listRowObjects.Clear();
                listRowSongIds.Clear();
                listMode = mode;
                if (mode == "skeleton")
                {
                    // AqFtx のスケルトン行 (初回の読み込み中だけ)。再取得では出さない。
                    for (int i = 0; i < ListVisibleRows; i++)
                    {
                        // スケルトンも design 行 (mcCOI) と同じ x 配置 (Level 12 / Thumb 68 /
                        // Texts 176 / Grade 436)。バーの寸法・y は AqFtx のプレースホルダのまま。
                        var skeleton = V6Layout.Place(V5Ui.Panel(rows, "Song Skeleton " + i, new Vector2(RowW, RowH),
                            V5Tokens.Key1, 8f), RowX, RowY0 + RowStep * i);
                        V6Layout.Place(V5Ui.Panel(skeleton, "Level", new Vector2(44f, 44f), V5Tokens.Key3, 22f), 12f, 14f);
                        V6Layout.Place(V5Ui.Panel(skeleton, "Thumb", new Vector2(96f, 54f), V5Tokens.Key3, 4f), 68f, 9f);
                        V6Layout.Place(V5Ui.Panel(skeleton, "Texts", new Vector2(220f, 26f), V5Tokens.Key3, 4f), 176f, 14f);
                        V6Layout.Place(V5Ui.Panel(skeleton, "Artist", new Vector2(120f, 18f), V5Tokens.Key3, 4f), 176f, 44f);
                        V6Layout.Place(V5Ui.Panel(skeleton, "Grade", new Vector2(56f, 36f), V5Tokens.Key3, 4f), 436f, 18f);
                    }
                }
                else if (mode == "empty")
                {
                    // AqFtx: 該当なしの空状態。3 要素は design の座標 (Icon 32 @y14 /
                    // Title 24 @(0,54) h35 / Hint 20 @(0,97) h29) に置く。
                    var empty = V6Layout.Place(V5Ui.Panel(rows, "Empty Note", new Vector2(448f, 140f), V5Tokens.Key0, 8f), 56f, 300f);
                    // design UIN4a は「検索して該当なし」の虫めがね + x。音楽アイコンではない。
                    V6Icons.Create(empty, "search-x", 32f, V5Tokens.TextDim);
                    var emptyIcon = empty.transform.Find("Icon")?.gameObject;
                    if (emptyIcon != null)
                    {
                        var iconRt = emptyIcon.GetComponent<RectTransform>();
                        iconRt.anchorMin = new Vector2(0.5f, 1f);
                        iconRt.anchorMax = new Vector2(0.5f, 1f);
                        iconRt.pivot = new Vector2(0.5f, 1f);
                        iconRt.anchoredPosition = new Vector2(0f, -14f);
                        iconRt.sizeDelta = new Vector2(32f, 32f);
                    }
                    var emptyTitle = V5GameUi.Text(empty.gameObject, "Title", "曲が見つかりません", 24f, V5Tokens.Text,
                        TextAlignmentOptions.Center, FontStyles.Normal);
                    V6Layout.Place(emptyTitle, 0f, 54f, 448f, 35f);
                    var emptyHint = V5GameUi.Text(empty.gameObject, "Hint", "検索語を変えるか フィルターを解除してください", 20f,
                        V5Tokens.TextMuted, TextAlignmentOptions.Center);
                    SingleLine(emptyHint);
                    V6Layout.Place(emptyHint, 0f, 97f, 448f, 29f);
                }
            }

            if (mode == "rows")
            {
                // 先頭スロットから連続で描く (ClampScroll 済み)。選択行はその中で
                // ハイライトされるだけで、スロット位置は曲順どおり。
                // 範囲外の index は描かない (空行にする)。
                UpdateListRows(rows, songs, selectIndex);
            }

            // 正本 OENRv のリスト下端はスクロールボタンとドットのみ。
            // 「1 / 28」のような選択位置テキストは設計に無いので描かない。
            UpdateDots(list);
        }

        /// <summary>
        /// 可視行スロットを既存オブジェクトの再利用で更新する。
        /// スロット s は常に i = listScroll + s の曲を表示し、y もスロット固定
        /// (RowY0 + RowStep * s) なので、選択移動では中身の差し替えだけで済む。
        /// </summary>
        private void UpdateListRows(GameObject rows, IReadOnlyList<V6SongEntry> songs, int selectIndex)
        {
            // スロット配列は常に ListVisibleRows 個に揃える
            // (途中の空スロットで index がずれないようにする)。
            while (listRowObjects.Count < ListVisibleRows)
            {
                listRowObjects.Add(null);
                listRowSongIds.Add(-1);
            }

            int first = listScroll;
            for (int slot = 0; slot < ListVisibleRows; slot++)
            {
                int i = first + slot;
                bool valid = i >= 0 && i < songs.Count;
                GameObject row = listRowObjects[slot];
                if (row == null)
                {
                    if (!valid)
                    {
                        continue;
                    }

                    row = BuildSongRow(rows, songs[i], slot, i == selectIndex, RowY0 + RowStep * slot);
                    listRowObjects[slot] = row;
                    listRowSongIds[slot] = songs[i].SongId;
                    continue;
                }

                if (!valid)
                {
                    FlashBeat.UI.V5.V5GameUi.DestroyNow(row);
                    listRowObjects[slot] = null;
                    listRowSongIds[slot] = -1;
                    continue;
                }

                // SongId が変わったスロットだけ行名 ("SongRow <title>") を
                // 付け直す。毎回連結すると Refresh ごとに文字列を確保する。
                bool identityChanged = listRowSongIds[slot] != songs[i].SongId;
                UpdateSongRow(row, songs[i], i == selectIndex, RowY0 + RowStep * slot,
                    identityChanged);
                listRowSongIds[slot] = songs[i].SongId;
            }

            while (listRowObjects.Count > ListVisibleRows)
            {
                int last = listRowObjects.Count - 1;
                GameObject tail = listRowObjects[last];
                if (tail != null) FlashBeat.UI.V5.V5GameUi.DestroyNow(tail);
                listRowObjects.RemoveAt(last);
                listRowSongIds.RemoveAt(listRowSongIds.Count - 1);
            }
        }

        /// <summary>既存の曲行を別の曲データで書き換える (行オブジェクトは再利用)。</summary>
        private void UpdateSongRow(GameObject row, V6SongEntry song, bool selected, float y,
            bool rename)
        {
            var rt = row.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition = new Vector2(RowX, -y);
            }

            // 行の内部座標は共通部品 V6Lists.SongRow (design mcCOI) と同じ:
            // 背景は非選択 $k1 / 選択 $k6。
            var bg = row.GetComponent<Image>();
            if (bg != null)
            {
                bg.color = selected ? V6Tokens.Key6 : V6Tokens.Key1;
            }

            V6Hierarchy.SetChildText(row, "Level/Level", song.Level > 0 ? song.Level.ToString() : "–");
            V6Hierarchy.SetChildText(row, "Title", song.Title);
            V6Hierarchy.SetChildText(row, "Composer", song.Artist);
            V6Hierarchy.SetChildText(row, "Grade/Grade",
                string.IsNullOrEmpty(song.BestGrade) ? "–" : song.BestGrade);
            if (rename)
            {
                row.name = "SongRow " + song.Title;
            }

            SetJacket(V6Lists.Thumb(row), song, V6Tokens.Key2);
        }

        /// <summary>
        /// 行スロットのクリック結線。スロットが今表示している曲を選ぶ
        /// (行オブジェクトを再利用するので、結線はスロット index に対して 1 回だけ張る)。
        /// </summary>
        private void SelectListSlot(int slot)
        {
            if (slot < 0 || slot >= listRowSongIds.Count)
            {
                return;
            }

            int songId = listRowSongIds[slot];
            if (songId >= 0)
            {
                data.Select(songId);
            }
        }

        private GameObject BuildSongRow(GameObject parent, V6SongEntry song, int slot, bool selected, float y)
        {
            // 行の内部座標は共通部品 V6Lists.SongRow に一本化 (design mcCOI/Odv71 実測値)。
            // この画面はクリック→選択・ジャケット・グレード文字の結線だけを行う。
            var row = V6Lists.SongRow(parent, song.Title, song.Artist,
                song.Level > 0 ? song.Level.ToString() : "–", song.BestGrade,
                new Vector2(RowX, y), new Vector2(RowW, RowH), selected);
            var button = row.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            var nav = button.navigation;
            nav.mode = Navigation.Mode.None;
            button.navigation = nav;
            V6ButtonPressFeedback.Attach(row);
            button.onClick.AddListener(() => SelectListSlot(slot));

            SetJacket(V6Lists.Thumb(row), song, V6Tokens.Key2);
            return row;
        }

        private void BuildScrollButton(GameObject list, string name, bool down)
        {
            var rect = V6SelectViewLogic.ListScrollButton(down);
            var btn = V6Button.Panel(list, name, new Vector2(rect.W, rect.H), V5Tokens.Key4, 8f,
                () => ScrollList(down ? 1 : -1));
            V6Layout.Place(btn, rect.X, rect.Y);
            V6Icons.Create(btn, down ? "chevron-down" : "chevron-up", 24f, V5Tokens.TextMuted);
        }

        /// <summary>
        /// ドットインジケクタ。design (AqFtx / OENRv) は **全曲数** の分だけ
        /// (x=536, y=136 から縦に gap 4, size 8、選択中だけ 10px の $accent)。
        /// 絞り込みで曲数が減ってもドット数は変えない (ページ位置の案内)。
        /// 各ドットの y は <see cref="V6SelectViewLogic.DotOffsets"/> (純ロジック) の
        /// とおり size+gap 間隔で積む (親ローカル座標系を統一しないと全ドットが重なる)。
        ///
        /// Issue #415: ドット個は曲数が変わったときだけ作り直し、選択の移動では
        /// 色とサイズだけを既存ドットへ適用する。
        /// </summary>
        private void UpdateDots(GameObject list)
        {
            var dotsT = list.transform.Find("Dot indicator");
            if (dotsT == null)
            {
                return;
            }

            GameObject dots = dotsT.gameObject;
            IReadOnlyList<V6SongEntry> all = data.AllSongs;
            int count = all == null ? 0 : all.Count;

            int active = -1;
            if (data.Selected != null && all != null)
            {
                for (int i = 0; i < all.Count; i++)
                {
                    if (all[i] != null && all[i].SongId == data.Selected.SongId) { active = i; break; }
                }
            }
            active = V6SelectViewLogic.DotActiveIndex(active, count);

            if (count != dotCount)
            {
                // 曲数 (カタログ構造) が変わったときだけドットを作り直す。
                Common.V6Hierarchy.ClearChildren(dots);
                dotImages.Clear();
                dotRects.Clear();
                dotOffsetsCache = V6SelectViewLogic.DotOffsets(count, 0f);
                float total = V6SelectViewLogic.DotBottom(count, 0f);
                // レール幅は design どおり最大ドット幅 (10)。ドットはレール中央に置く。
                // Issue #427: レールはリストパネルの上下中央 (design の DotY=136 は上寄りだった)。
                V6Layout.Place(dots, V6SelectViewLogic.DotX, V6SelectViewLogic.DotRailTopY(count),
                    V6SelectViewLogic.DotActiveSize, total);
                for (int i = 0; i < count; i++)
                {
                    var dot = V5Ui.Panel(dots, "Dot " + (i + 1),
                        new Vector2(V6SelectViewLogic.DotSize, V6SelectViewLogic.DotSize),
                        V5Tokens.FromHex("#FFFFFF26"), V6SelectViewLogic.DotRadius);
                    dotImages.Add(dot.GetComponent<Image>());
                    dotRects.Add(dot.GetComponent<RectTransform>());
                    // anchor/pivot を (0,1) に揃える (サイズ・色・位置は下のループが確定する)。
                    V6Layout.Place(dot, V6SelectViewLogic.DotRectX(V6SelectViewLogic.DotSize),
                        dotOffsetsCache[i], V6SelectViewLogic.DotSize, V6SelectViewLogic.DotSize);
                }

                dotCount = count;
            }

            float[] offsets = dotOffsetsCache ?? (dotOffsetsCache = V6SelectViewLogic.DotOffsets(count, 0f));
            for (int i = 0; i < count && i < dotImages.Count; i++)
            {
                bool isActive = i == active;
                float d = isActive ? V6SelectViewLogic.DotActiveSize : V6SelectViewLogic.DotSize;
                // Issue #427: 選択中は v6 の選択色 (Key6)。design の水色 Accent は
                // v6 の選択表現と一致しないため使わない。
                var img = dotImages[i];
                if (img != null)
                {
                    img.color = isActive ? V6Tokens.Key6 : V5Tokens.FromHex("#FFFFFF26");
                }

                var rt = dotRects[i];
                if (rt != null)
                {
                    rt.sizeDelta = new Vector2(d, d);
                    rt.anchoredPosition = new Vector2(V6SelectViewLogic.DotRectX(d), -offsets[i]);
                }
            }
        }
    }
}
