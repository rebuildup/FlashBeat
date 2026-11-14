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
    /// V6RankingView のフッターゾーン (キー凡例 + 「この曲へ」) 構築 (Issue #385 Phase 2.7 で分割)。
    /// partial class のためフィールド・メソッドは V6RankingView 本体と共有する。
    /// </summary>
    public sealed partial class V6RankingView
    {
        // --- フッター (共通フレームの Footer ゾーン: design (24,876) 1680×96) ---

        /// <summary>
        /// Issue #415: フッターは状態で変わらないので、中身が無いときだけ作る。
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
            // design (S1TZD フッター): キー凡例は 4 項目 (↑↓ 曲送り / Space 決定 /
            // Tab 切替 / Esc 戻る)。項目間 gap24 / KeyCap padding [0,8] / Cap-Label gap8。
            // 切替は Ranking 画面では Tab (SelectSceneManager.HandleRankingInput が
            // Tab と S の両方を受ける)。F5 再試行は design に凡例が無いので出さない。
            V6Legend.KeyLegend(footerZone, new[]
            {
                new[] { "↑↓", "曲送り" },
                new[] { "Space", "決定" },
                new[] { "Tab", "切替" },
                new[] { "Esc", "戻る" }
            });

            // design (S1TZD 操作ボタンゾーン): 右下は「この曲へ」(567,20) 273×56 のみ。
            // 「補助操作」ゾーン (358,20) 197×56 は design で子ノードを持たない
            // 空プレースホルダなのでボタンは描かない (O キーの設定遷移は残す)。
            GameObject gotoBtn = V6Controls.ButtonPrimary(footerZone, "この曲へ",
                new Vector2(V6RankingLogic.FooterGotoButtonX, V6RankingLogic.FooterButtonY),
                new Vector2(273f, V6RankingLogic.FooterButtonH), "corner-down-left", "Space");
            V6Button.Configure(gotoBtn, TriggerGotoSong);
        }

    }
}
