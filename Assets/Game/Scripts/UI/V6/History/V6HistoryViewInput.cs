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
    /// V6HistoryView の入力ヘルパ部分 (Issue #385 Phase 2.6 で分割)。
    /// マウスのみ (キー入力は統合担当が結線する): ScrollList / OnScroll
    /// (ホイール・スクロールボタンからのリスト/ランキング送り) と
    /// SongById / IndexOfSong の検索ヘルパを持つ。
    /// partial class のためフィールド・メソッドは V6HistoryView 本体と共有する。
    /// </summary>
    public sealed partial class V6HistoryView
    {
        // --- 入力ヘルパ (マウスのみ。キーは統合担当が結線する) ---

        private void ScrollList(int delta)
        {
            int count = V6HistoryLogic.HistoryList(data.AllSongs, sort).Count;
            listScroll = V6SelectViewLogic.ClampScroll(listScroll + delta, count, ListVisibleRows);
            ApplyVolatile(false);
        }

        internal void OnScroll(int target, float delta)
        {
            int step = delta > 0f ? -1 : 1;
            if (target == 0)
            {
                ScrollList(step);
                return;
            }
            V6SongEntry song = SongById(selectedSongId);
            int count = SongRankingRows(song).Count;
            songRankingScroll = V6SelectViewLogic.ClampScroll(songRankingScroll + step, count, SongRankingVisibleRows);
            ApplyVolatile(false);
        }

        private V6SongEntry SongById(int songId)
        {
            IReadOnlyList<V6SongEntry> all = data.AllSongs;
            if (all == null)
            {
                return null;
            }
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] != null && all[i].SongId == songId)
                {
                    return all[i];
                }
            }
            return null;
        }

        private static int IndexOfSong(List<V6SongEntry> songs, int songId)
        {
            if (songs == null)
            {
                return -1;
            }
            for (int i = 0; i < songs.Count; i++)
            {
                if (songs[i] != null && songs[i].SongId == songId)
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
