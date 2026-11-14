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
    /// V6RankingView の右列 (PLAYERS パネル) 構築 (Issue #385 Phase 2.7 で分割)。
    /// partial class のためフィールド・メソッドは V6RankingView 本体と共有する。
    /// </summary>
    public sealed partial class V6RankingView
    {
        // --- 右列: PLAYERS (512x732 $k2) ---

        /// <summary>
        /// PLAYERS パネル。design (smhrU 実測): $k2 r8 / ヘッド g2307 (8,8) 496×40 =
        /// Title "PLAYERS" (12,7) 74×26 fs18 $text-dim + 右寄せの Metric「累計スコア」
        /// (354,7) 90×26 fs18 $text。プレイヤー別 (曲横断) の集計 API はこのリポジトリに
        /// 無いため本文は Select と同じ正直な空状態「データがありません」。
        /// design の左右矢印 (qPD75/BUt9A) は操作先が無い死んだボタンなので描かない
        /// (Issue #239 の判断を維持)。
        /// </summary>
        /// <summary>
        /// Issue #415: PLAYERS パネルは状態で変わらないので、中身が無いときだけ作る。
        /// </summary>
        private void EnsurePlayers(GameObject mainZone)
        {
            GameObject panel = mainZone.transform.Find("Players")?.gameObject;
            if (panel != null)
            {
                return;
            }

            BuildPlayers(mainZone);
        }

        private void BuildPlayers(GameObject mainZone)
        {
            var panel = V6Layout.Place(V5Ui.Panel(mainZone, "Players", new Vector2(PlayersW, RankingH),
                V5Tokens.Key2, V6Tokens.RadiusMedium), PlayersX, 24f);

            var head = V5Ui.Node(panel.gameObject, "Head");
            V6Layout.Place(head.GetComponent<RectTransform>(), 8f, 8f, 496f, 40f);

            // design の見出しは 1 行固定 (折り返すと "PLAYER" / "S" に割れる — r21 実機)。
            var headTitle = V5GameUi.Text(head, "Title", "PLAYERS", 18f, V5Tokens.TextDim);
            headTitle.textWrappingMode = TextWrappingModes.NoWrap;
            V6Layout.Place(headTitle, 12f, 7f, 74f, 26f);

            var metric = V5GameUi.Text(head, "Metric", "累計スコア", 18f, V5Tokens.Text,
                TextAlignmentOptions.Right, FontStyles.Normal);
            metric.textWrappingMode = TextWrappingModes.NoWrap;
            V6Layout.Place(metric, 354f, 7f, 90f, 26f);

            var empty = V5GameUi.Text(panel.gameObject, "NoData", "データがありません",
                24f, V5Tokens.TextDim, TextAlignmentOptions.Center);
            V6Layout.Place(empty, 8f, 300f, 496f, 44f, new Vector2(0f, 0.5f));
        }

    }
}
