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
    /// V6RankingView のマウススクロール / スクロール受け皿 / ジャケット適用ヘルパ
    /// (Issue #385 Phase 2.7 で分割)。partial class のためフィールド・メソッドは
    /// V6RankingView 本体と共有する。キー入力は統合担当が結線する (Input は直接読まない)。
    /// </summary>
    public sealed partial class V6RankingView
    {
        // --- 入力ヘルパ (マウスのみ。キーは統合担当が結線する) ---

        private void ScrollList(int delta)
        {
            listScroll = V6SelectViewLogic.ClampScroll(listScroll + delta, data.AllSongs.Count, ListVisibleRows);
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
            rankingScroll = V6SelectViewLogic.ClampScroll(rankingScroll + step, RankingRows(EffectiveSource()).Count, RankingVisibleRows);
            ApplyVolatile(false);
        }

        // --- ジャケット ---

        private void SetJacket(GameObject thumbPanel, V6SongEntry song, Color? fallback = null)
        {
            // Issue #398 / #385: ジャケットの Sprite は V6JacketProvider が所有し、
            // 適用手順は V6SongBadge.ApplyJacket に一本化 (History と同じ画像を
            // ここで別実体にしない)。
            V6SongBadge.ApplyJacket(thumbPanel,
                song != null ? song.SongId : 0, song != null ? song.Title : null, fallback);
        }

        private void AddScrollCatcher(GameObject target, int targetId)
        {
            var catcher = target.AddComponent<V6RankingScrollCatcher>();
            catcher.View = this;
            catcher.Target = targetId;
        }
    }
}
