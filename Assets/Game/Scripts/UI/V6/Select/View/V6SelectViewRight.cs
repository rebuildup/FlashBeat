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
    /// V6SelectView の右列部分 (Issue #385 Phase 2.4 で分割)。
    /// Ranking Column ゾーン (BEST・SCORE RANKING・PLAYERS) の構築と差分更新、
    /// ランキング行スロットの再利用を持つ。
    /// partial class のためフィールド・メソッドは V6SelectView 本体と共有する。
    /// </summary>
    public sealed partial class V6SelectView
    {
        // --- 右列: BEST / SCORE RANKING / PLAYERS ---

        /// <summary>
        /// 右カラム (Ranking): scaffold が無ければ作り、続けて
        /// <see cref="UpdateRight"/> で可変値 (BEST・source・ランキング行) を適用する。
        /// </summary>
        private void EnsureRight()
        {
            var column = FindZone("Ranking Column");
            if (column == null)
            {
                BuildRightScaffold();
                column = FindZone("Ranking Column");
            }

            if (column == null)
            {
                return;
            }

            UpdateRight(column);
        }

        /// <summary>右カラムの骨格。可変値 (Score/Grade/Source/行) は UpdateRight が適用する。</summary>
        private void BuildRightScaffold()
        {
            var column = V5Ui.Node(frame.gameObject, "Ranking Column");
            V6Layout.Place(column.GetComponent<RectTransform>(), MainX + RightX, MainY, RightW, ListH);

            var best = V6Layout.Place(V5Ui.Panel(column, "Best", new Vector2(528f, 112f), Color.clear, 0f), 16f, 24f);
            var bestLabel = V5GameUi.Text(best.gameObject, "Label", "BEST", 22f, V5Tokens.TextDim);
            V6Layout.Place(bestLabel, 0f, 0f, 528f, 22f);

            var scoreRow = V5GameUi.Row(best.gameObject, "Score Row", 12f, TextAnchor.MiddleLeft);
            V6Layout.Place(scoreRow.GetComponent<RectTransform>(), 0f, 30f, 528f, 72f);
            var scoreText = V5GameUi.Text(scoreRow.gameObject, "Score", "–", 56f, V5Tokens.Text,
                TextAlignmentOptions.Left, FontStyles.Bold);
            // BEST の大きな数値も折り返さない (#379)。childControlWidth=false のため
            // 幅は明示する: Score 400 + Spacer 0 + Grade 72 + gap 12x2 = 496 ≤ 528。
            // 幅を超えた分は自動縮小で 1 行に収める。
            scoreText.textWrappingMode = TextWrappingModes.NoWrap;
            scoreText.enableAutoSizing = true;
            scoreText.fontSizeMin = 40f;
            scoreText.fontSizeMax = 56f;
            scoreText.rectTransform.sizeDelta = new Vector2(400f, 72f);
            var spacer = V5Ui.Node(scoreRow.gameObject, "Spacer");
            spacer.GetComponent<RectTransform>().sizeDelta = Vector2.zero;
            var spacerLe = spacer.AddComponent<LayoutElement>();
            spacerLe.flexibleWidth = 1f;
            var gradeChip = V6Layout.Place(V5Ui.Panel(scoreRow.gameObject, "Grade", new Vector2(72f, 64f), V5Tokens.Key6, 8f),
                0f, 0f);
            var gradeText = V5GameUi.Text(gradeChip.gameObject, "Text", "–", 34f, V5Tokens.Text,
                TextAlignmentOptions.Center, FontStyles.Normal);
            V6Layout.StretchCenter(gradeText.GetComponent<RectTransform>(), 72f, 64f);

            // SCORE RANKING (528x300, $k2 r8)
            var ranking = V6Layout.Place(V5Ui.Panel(column, "Score Ranking", new Vector2(528f, 300f), V5Tokens.Key2, 8f),
                16f, 152f);
            AddScrollCatcher(ranking, 1);
            var head = V5GameUi.Row(ranking.gameObject, "Head", 8f, TextAnchor.MiddleLeft);
            V6Layout.Place(head.GetComponent<RectTransform>(), 8f, 8f, 496f, 36f);
            V5GameUi.Text(head.gameObject, "Label", "SCORE RANKING", 18f, V5Tokens.TextDim);
            var headSpacer = V5Ui.Node(head.gameObject, "Spacer");
            var headSpacerLe = headSpacer.AddComponent<LayoutElement>();
            headSpacerLe.flexibleWidth = 1f;
            // ランキングの source タグは実データから決める (#221)。
            // ローカル曲に「オンライン」と表示するのは嘘なので、IsCloud をそのまま出す。
            var source = V5Ui.Panel(head.gameObject, "Source", new Vector2(96f, 28f), V5Tokens.Key4, 4f);
            var sourceText = V5GameUi.Text(source, "Text", "–", 18f, V5Tokens.TextMuted,
                TextAlignmentOptions.Center);
            V6Layout.StretchCenter(sourceText.GetComponent<RectTransform>(), 96f, 28f);

            // PLAYERS (528x288, $k2 r8): IV6SelectDataSource にプレイヤー集計の
            // データ源が無い。実データを出せないので「データ無し」と正直に言う
            // (#221: 固定文字列を実データに見せかけない)。
            var players = V6Layout.Place(V5Ui.Panel(column, "Players", new Vector2(528f, 288f), V5Tokens.Key2, 8f), 16f, 468f);
            var playersHead = V5GameUi.Row(players.gameObject, "Head", 8f, TextAnchor.MiddleLeft);
            V6Layout.Place(playersHead.GetComponent<RectTransform>(), 8f, 8f, 496f, 36f);
            V5GameUi.Text(playersHead.gameObject, "Label", "PLAYERS", 18f, V5Tokens.TextDim);
            var playersSpacer = V5Ui.Node(playersHead.gameObject, "Spacer");
            var playersSpacerLe = playersSpacer.AddComponent<LayoutElement>();
            playersSpacerLe.flexibleWidth = 1f;
            V5GameUi.Text(playersHead.gameObject, "Mode", "累計スコア", 18f, V5Tokens.Text, TextAlignmentOptions.Right,
                FontStyles.Normal);
            // design IL4N7: 中央の "NO DATA" ((8,149) 496×35)。
            var noPlayers = V5GameUi.Text(players.gameObject, "No Data", "NO DATA", 24f,
                V5Tokens.TextDim, TextAlignmentOptions.Center);
            V6Layout.Place(noPlayers, 8f, 149f, 496f, 35f);
        }

        /// <summary>右カラムの可変値 (BEST・source タグ・ランキング行/空表示) を適用する。</summary>
        private void UpdateRight(GameObject column)
        {
            var song = data.Selected;
            V6Hierarchy.SetChildText(column, "Best/Score Row/Score",
                song != null ? V6SelectViewLogic.FormatScore(song.BestScore) : "–");
            V6Hierarchy.SetChildText(column, "Best/Score Row/Grade/Text",
                song != null && !string.IsNullOrEmpty(song.BestGrade) ? song.BestGrade : "–");
            V6Hierarchy.SetChildText(column, "Score Ranking/Head/Source/Text", RankingSourceLabel(song));
            UpdateRankingRows(column, song);
        }

        /// <summary>
        /// SCORE RANKING の行スロットと空表示を現在のランキングへ合わせる。
        /// rows ↔ empty のモード変化のときだけ表示を入れ替え、行はスロット再利用する。
        /// </summary>
        private void UpdateRankingRows(GameObject column, V6SongEntry song)
        {
            var rankingT = column.transform.Find("Score Ranking");
            if (rankingT == null)
            {
                return;
            }

            GameObject ranking = rankingT.gameObject;
            var rows = song != null ? data.RankingFor(song) : null;
            bool hasRows = rows != null && rows.Count > 0;
            string mode = hasRows ? "rows" : "empty";
            if (mode != rankMode)
            {
                // モード変化: 旧モードの表示 (行スロット or 空テキスト) を片付ける。
                for (int i = 0; i < rankRowObjects.Count; i++)
                {
                    if (rankRowObjects[i] != null)
                    {
                        FlashBeat.UI.V5.V5GameUi.DestroyNow(rankRowObjects[i]);
                    }
                }

                rankRowObjects.Clear();
                Transform oldEmpty = ranking.transform.Find("No Data");
                if (oldEmpty != null)
                {
                    FlashBeat.UI.V5.V5GameUi.DestroyNow(oldEmpty.gameObject);
                }

                rankEmptyGo = null;
                rankMode = mode;
            }

            if (mode == "empty")
            {
                // Issue #427: 取得中はテキストではなく控えめな「…」をパネル内に出す。
                // 失敗時はヘッダーの StatusMessage (エラー) が理由を伝える。
                if (rankEmptyGo == null)
                {
                    bool loading = data.IsLoading;
                    var empty = V5GameUi.Text(ranking, "No Data",
                        loading ? "…" : "NO DATA", loading ? 40f : 24f,
                        V5Tokens.TextDim, TextAlignmentOptions.Center);
                    V6Layout.Place(empty, 8f, 155f, 496f, 35f);
                    rankEmptyGo = empty.gameObject;
                }
                else
                {
                    // 取得中 ↔ 完了で文言とサイズが変わるので既存テキストへ適用する。
                    var emptyText = rankEmptyGo.GetComponent<TextMeshProUGUI>();
                    if (emptyText != null)
                    {
                        bool loading = data.IsLoading;
                        emptyText.text = loading ? "…" : "NO DATA";
                        emptyText.fontSize = loading ? 40f : 24f;
                    }
                }

                return;
            }

            // 行スロット配列は常に RankingVisibleRows 個に揃える
            // (曲リストと同じく、空スロットで index がずれないようにする)。
            while (rankRowObjects.Count < RankingVisibleRows)
            {
                rankRowObjects.Add(null);
            }

            int first = rankingScroll;
            for (int slot = 0; slot < RankingVisibleRows; slot++)
            {
                int i = first + slot;
                bool valid = i >= 0 && i < rows.Count;
                GameObject row = rankRowObjects[slot];
                if (row == null)
                {
                    if (!valid)
                    {
                        continue;
                    }

                    row = BuildRankingRow(ranking, rows[i], 92f + 52f * slot);
                    rankRowObjects[slot] = row;
                    continue;
                }

                if (!valid)
                {
                    FlashBeat.UI.V5.V5GameUi.DestroyNow(row);
                    rankRowObjects[slot] = null;
                    continue;
                }

                UpdateRankingRow(row, rows[i], 92f + 52f * slot);
            }

            while (rankRowObjects.Count > RankingVisibleRows)
            {
                int last = rankRowObjects.Count - 1;
                GameObject tail = rankRowObjects[last];
                if (tail != null) FlashBeat.UI.V5.V5GameUi.DestroyNow(tail);
                rankRowObjects.RemoveAt(last);
            }
        }

        /// <summary>既存のランキング行を別の行データで書き換える (行オブジェクトは再利用)。</summary>
        private void UpdateRankingRow(GameObject row, V6RankingRow dataRow, float y)
        {
            var rt = row.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition = new Vector2(8f, -y);
            }

            // CompactRankRow (design jnIY9) と同じ配色: YOU 行は $k6、他は $k1。
            var bg = row.GetComponent<Image>();
            if (bg != null)
            {
                bg.color = dataRow.IsYou ? V6Tokens.Key6 : V6Tokens.Key1;
            }

            V6Hierarchy.SetChildText(row, "Pos/PosText", dataRow.Position.ToString());
            var posText = row.transform.Find("Pos/PosText");
            var posTmp = posText != null ? posText.GetComponent<TextMeshProUGUI>() : null;
            if (posTmp != null)
            {
                posTmp.color = dataRow.IsYou ? V6Tokens.Text : V6Tokens.TextMuted;
            }

            V6Hierarchy.SetChildText(row, "Name", dataRow.IsYou ? "YOU" : dataRow.PlayerName);
            V6Hierarchy.SetChildText(row, "Value", V6SelectViewLogic.FormatScore(dataRow.Score));
            V6Hierarchy.SetChildText(row, "Grade/GradeText",
                string.IsNullOrEmpty(dataRow.Grade) ? "–" : dataRow.Grade);
            row.name = "Score " + dataRow.Position;
        }

        /// <summary>ランキングの source タグ表示。実データ (クラウド配信か) から決める。</summary>
        private static string RankingSourceLabel(V6SongEntry song)
        {
            if (song == null) return "–";
            return song.IsCloud ? "オンライン" : "ローカル";
        }

        private GameObject BuildRankingRow(GameObject ranking, V6RankingRow row, float y)
        {
            // 行内部は共通部品 (design jnIY9 496×44) が持つ。座標計算をここへ書き戻さない。
            return V6Lists.CompactRankRow(ranking, row.Position.ToString(),
                row.IsYou ? "YOU" : row.PlayerName, V6SelectViewLogic.FormatScore(row.Score),
                row.Grade, new Vector2(8f, y), row.IsYou);
        }
    }
}
