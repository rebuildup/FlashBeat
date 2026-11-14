using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FlashBeat.UI.V5;
using FlashBeat.UI.V6;
using FlashBeat.UI.V6.Common;

namespace FlashBeat.UI.V6.Result
{

    /// <summary>
    /// V6ResultView のトロフィーバッジ部分 (Issue #385 Phase 2.1 で分割)。
    /// ヘッダー左上スロット (56×56) のトロフィー構築。partial class のため
    /// フィールド・メソッドは V6ResultView 本体と共有する。
    /// </summary>
    public sealed partial class V6ResultView
    {
        // トロフィーは結果ブロックの中ではなく、ヘッダー左上の空きスロット
        // (design zcSHh/biox4、他画面の戻るボタンと同じ 56×56) に置く。
        // メイン領域に置くとスコアの左に浮いて読み順を乱し、他画面では
        // 戻るボタンが入る枠が Result だけ空白になっていた。
        private const float TrophyBadgeX = 0f, TrophyBadgeY = 20f, TrophyBadgeSize = 56f;
        private const float TrophyIconSize = 28f;

        /// <summary>
        /// ヘッダー左上のトロフィーバッジを構築する (design zcSHh/biox4)。
        /// 戻るボタンが入る枠を他画面と同じ位置・サイズで使う。
        /// </summary>
        private void BuildTrophyBadge(GameObject header)
        {
            // design zcSHh/biox4 の左上スロット (56×56) にトロフィーを置く。
            // 他画面ではここが戻るボタン (Esc) で、Result だけ空いていた。
            // 戻るはフッターの OK / Esc キーが担うので、ここは結果の記号に使う。
            var badge = V5Ui.Panel(header, "Trophy Badge",
                new Vector2(TrophyBadgeSize, TrophyBadgeSize), V5Tokens.Key4, 8f);
            V6Layout.Place(badge, TrophyBadgeX, TrophyBadgeY);
            var trophyNode = V5Ui.Node(badge, "Icon");
            var nodeRt = trophyNode.GetComponent<RectTransform>();
            nodeRt.anchorMin = Vector2.zero;
            nodeRt.anchorMax = Vector2.one;
            nodeRt.offsetMin = Vector2.zero;
            nodeRt.offsetMax = Vector2.zero;
            trophyIcon = V6Icons.Create(trophyNode, "trophy", TrophyIconSize, V5Tokens.Text, "Glyph");
            if (trophyIcon != null)
            {
                var iconRt = trophyIcon.rectTransform;
                iconRt.anchorMin = new Vector2(0.5f, 0.5f);
                iconRt.anchorMax = new Vector2(0.5f, 0.5f);
                iconRt.pivot = new Vector2(0.5f, 0.5f);
                iconRt.sizeDelta = new Vector2(TrophyIconSize, TrophyIconSize);
                iconRt.anchoredPosition = Vector2.zero;
            }

            trophyGroup = FadeGroup(badge);
            trophyRect = badge.GetComponent<RectTransform>();
        }
    }
}
