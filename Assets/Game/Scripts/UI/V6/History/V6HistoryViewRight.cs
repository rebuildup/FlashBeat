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
    /// V6HistoryView の右列部分 (Issue #385 Phase 2.6 で分割)。
    /// 「Record Column」(design 記録情報列) の scaffold 構築、
    /// プレイヤー情報 / プレイ情報 / 曲ランキング情報の構築と差分適用、
    /// ランキング行スロットの再利用を持つ。
    /// partial class のためフィールド・メソッドは V6HistoryView 本体と共有する。
    /// </summary>
    public sealed partial class V6HistoryView
    {
        // --- 右列: プレイヤー情報 / プレイ情報 / 曲ランキング情報 ---

        /// <summary>
        /// Issue #415: 右列 (プレイヤー情報 / プレイ情報 / 曲ランキング) は骨格を保持し、
        /// 変動する値テキストとランキング行を差分更新する。
        /// 曲ランキングの Song チップは曲の有無で構造が変わるため、その切替時だけパネルを作り直す。
        /// </summary>
        private void EnsureRight(GameObject mainZone, V6SongEntry song)
        {
            GameObject column = rightGo != null ? rightGo : mainZone.transform.Find("Record Column")?.gameObject;
            if (column == null)
            {
                column = CreateRightScaffold(mainZone, song);
            }

            ApplyRight(column, song);
        }

        private GameObject CreateRightScaffold(GameObject mainZone, V6SongEntry song)
        {
            // Main ゾーン内 (design: 記録情報 (列5-6) = Main 内 (1120,0) 560×780)
            GameObject column = V5Ui.Node(mainZone, "Record Column");
            V6Layout.Place(column, RightX, 0f, ListW, ListH);
            rightGo = column;

            BuildPlayerInfo(column, song);
            BuildPlayInfo(column, song);
            BuildSongRanking(column, song);
            return column;
        }

        private void ApplyRight(GameObject column, V6SongEntry song)
        {
            ApplyPlayerInfo(column, song);
            ApplyPlayInfo(column, song);
            ApplySongRanking(column, song);
        }

        private void ApplyPlayerInfo(GameObject column, V6SongEntry song)
        {
            List<V6SongEntry> history = V6HistoryLogic.HistoryList(data.AllSongs, sort);
            if (infoNicknameText != null)
            {
                string nickname = RankingClient.LoadNickname();
                infoNicknameText.text = string.IsNullOrEmpty(nickname) ? "YOU" : nickname;
            }

            if (infoValueTexts.Count == 4)
            {
                infoValueTexts[0].text = V6SelectViewLogic.FormatScore(V6HistoryLogic.TotalScore(history));
                infoValueTexts[1].text = history.Count + " / " + data.AllSongs.Count;
                infoValueTexts[2].text = V6HistoryLogic.CountGradeAtLeastS(history).ToString();
                // 順位セルは API 未実装のまま "–位 / –人" (design どおり・静的なので書き換え不要)
            }
        }

        private void ApplyPlayInfo(GameObject column, V6SongEntry song)
        {
            bool valid = LastPlayValid(song);
            if (playScoreValueText != null)
            {
                playScoreValueText.text = valid ? V6SelectViewLogic.FormatScore(GManager.score) : "–";
            }

            if (judgeValueTexts.Count == 6)
            {
                judgeValueTexts[0].text = valid ? GManager.perfect.ToString() : "–";
                judgeValueTexts[1].text = valid ? GManager.great.ToString() : "–";
                judgeValueTexts[2].text = valid ? GManager.bad.ToString() : "–";
                judgeValueTexts[3].text = valid ? GManager.miss.ToString() : "–";
                judgeValueTexts[4].text = valid ? "0" : "–";
                judgeValueTexts[5].text = valid
                    ? V6HistoryLogic.ComboLabel(GManager.maxCombo, song != null ? song.NoteCount : 0) : "–";
            }
        }

        private void ApplySongRanking(GameObject column, V6SongEntry song)
        {
            bool hasSong = song != null;
            GameObject panel = songRankPanelGo != null ? songRankPanelGo
                : column.transform.Find("Song Ranking")?.gameObject;
            if (panel == null)
            {
                BuildSongRanking(column, song);
                return;
            }

            if (songRankHasSong != hasSong)
            {
                // 曲チップの有無で Head の構造が変わるので、曲ランキングパネルだけ作り直す。
                V6Hierarchy.DetachAndDestroy(panel);
                songRankPanelGo = null;
                songRankThumbGo = null;
                songRankChipText = null;
                songRankNoDataText = null;
                songRankRowObjects.Clear();
                BuildSongRanking(column, song);
                return;
            }

            if (songRankChipText != null)
            {
                songRankChipText.text = song != null ? song.Title : "";
            }

            if (songRankThumbGo != null && song != null)
            {
                SetJacket(songRankThumbGo, song);
            }

            UpdateSongRankingRows(panel, song);
        }

        private void UpdateSongRankingRows(GameObject panel, V6SongEntry song)
        {
            List<V6RankingRow> rows = SongRankingRows(song);
            bool hasRows = rows.Count > 0;
            if (songRankNoDataText != null)
            {
                if (hasRows)
                {
                    songRankNoDataText.gameObject.SetActive(false);
                }
                else
                {
                    songRankNoDataText.text = V6RankingLogic.SongRankingEmptyMessage(
                        song, data.IsLoading, data.HasError, data.StatusMessage);
                    songRankNoDataText.gameObject.SetActive(true);
                }
            }

            int first = songRankingScroll;
            for (int slot = 0; slot < SongRankingVisibleRows; slot++)
            {
                int i = first + slot;
                bool valid = hasRows && i < rows.Count;
                GameObject row = slot < songRankRowObjects.Count ? songRankRowObjects[slot] : null;
                float y = V6HistoryLogic.SongRankRowTop(slot);
                if (!valid)
                {
                    if (row != null)
                    {
                        V6Hierarchy.DetachAndDestroy(row);
                        songRankRowObjects[slot] = null;
                    }

                    continue;
                }

                if (row == null)
                {
                    row = CreateSongRankRow(panel, rows[i], slot);
                    if (slot < songRankRowObjects.Count) songRankRowObjects[slot] = row;
                    else songRankRowObjects.Add(row);
                }
                else
                {
                    UpdateSongRankRow(row, rows[i], y);
                }
            }

            while (songRankRowObjects.Count > SongRankingVisibleRows)
            {
                int last = songRankRowObjects.Count - 1;
                V6Hierarchy.DetachAndDestroy(songRankRowObjects[last]);
                songRankRowObjects.RemoveAt(last);
            }
        }

        private GameObject CreateSongRankRow(GameObject panel, V6RankingRow row, int visibleIndex)
        {
            return V6Lists.RankRow(
                panel,
                row.Position.ToString(),
                row.IsYou ? "YOU" : row.PlayerName,
                V6SelectViewLogic.FormatScore(row.Score),
                string.IsNullOrEmpty(row.Grade) ? "–" : row.Grade,
                new Vector2(V6HistoryLogic.SongRankRowX, V6HistoryLogic.SongRankRowTop(visibleIndex)),
                new Vector2(V6HistoryLogic.SongRankRowW, V6HistoryLogic.SongRankRowH),
                row.IsYou);
        }

        /// <summary>既存の曲ランキング行を新しい行データで書き換える (行オブジェクトは再利用)。</summary>
        private static void UpdateSongRankRow(GameObject row, V6RankingRow dataRow, float y)
        {
            var rt = row.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition = new Vector2(V6HistoryLogic.SongRankRowX, -y);
            }

            var bg = row.GetComponent<Image>();
            if (bg != null)
            {
                bg.color = dataRow.IsYou ? V5Tokens.Key6 : V5Tokens.Key1;
            }

            V6Hierarchy.SetChildText(row, "Pos/Pos", dataRow.Position.ToString());
            V6Hierarchy.SetChildText(row, "Name", dataRow.IsYou ? "YOU" : dataRow.PlayerName);
            V6Hierarchy.SetChildText(row, "Value", V6SelectViewLogic.FormatScore(dataRow.Score));
            V6Hierarchy.SetChildText(row, "Grade/Grade", string.IsNullOrEmpty(dataRow.Grade) ? "–" : dataRow.Grade);

            Transform posText = row.transform.Find("Pos/Pos");
            var posTmp = posText != null ? posText.GetComponent<TextMeshProUGUI>() : null;
            if (posTmp != null)
            {
                posTmp.color = dataRow.IsYou ? V5Tokens.Text : V5Tokens.TextMuted;
            }
        }

        /// <summary>プレイヤー情報 (528x176 $k2): 累計 / プレイ済み / S以上 (Issue #239 で順位セルは削除)。</summary>
        private void BuildPlayerInfo(GameObject column, V6SongEntry song)
        {
            infoValueTexts.Clear();
            List<V6SongEntry> history = V6HistoryLogic.HistoryList(data.AllSongs, sort);
            var panel = V6Layout.Place(V5Ui.Panel(column, "Player Info", new Vector2(528f, 176f), V5Tokens.Key2, 8f), 16f, 24f);

            var head = V5GameUi.Row(panel.gameObject, "Head", 8f, TextAnchor.MiddleLeft);
            V6Layout.Place(head.GetComponent<RectTransform>(), 8f, 8f, 496f, 36f);
            // 子の幅は TMP の preferredWidth に従わせる。childControlWidth=false だと
            // 既定の矩形幅 (200) のままヘッダーの右外へはみ出す。
            head.childControlWidth = true;
            V5GameUi.Text(head.gameObject, "Title", "プレイヤー情報", 18f, V5Tokens.TextDim);
            var spacer = V5Ui.Node(head.gameObject, "Spacer");
            var spacerLe = spacer.AddComponent<LayoutElement>();
            spacerLe.flexibleWidth = 1f;

            string nickname = RankingClient.LoadNickname();
            infoNicknameText = V5GameUi.Text(head.gameObject, "Name",
                string.IsNullOrEmpty(nickname) ? "YOU" : nickname, 22f, V5Tokens.Text,
                TextAlignmentOptions.Right);

            infoValueTexts.Add(BuildInfoCell(panel, 8f, 52f, "累計", V6SelectViewLogic.FormatScore(V6HistoryLogic.TotalScore(history))));
            // design (YuQgv value "4 / 26") は「記録のある曲数 / 全曲数」。
            infoValueTexts.Add(BuildInfoCell(panel, 272f, 52f, "プレイ曲", history.Count + " / " + data.AllSongs.Count));
            infoValueTexts.Add(BuildInfoCell(panel, 8f, 116f, "S以上", V6HistoryLogic.CountGradeAtLeastS(history).ToString()));
            // design (xnuYO) は 4 番目のセル「順位」("–位 / –人") を持つ。
            // 総合順位を返す API はまだ無いので、design と同じく未確定表示 "–位 / –人" を出す。
            // (以前はセルごと削除していたが、それは design 差分だったので戻した。)
            infoValueTexts.Add(BuildInfoCell(panel, 272f, 116f, "順位", "–位 / –人"));
        }

        /// <summary>プレイ情報 (528x220 $k2): 直前のプレイの SCORE と判定内訳。</summary>
        private void BuildPlayInfo(GameObject column, V6SongEntry song)
        {
            judgeValueTexts.Clear();
            var panel = V6Layout.Place(V5Ui.Panel(column, "Play Info", new Vector2(528f, 220f), V5Tokens.Key2, 8f), 16f, 216f);

            var head = V5GameUi.Row(panel.gameObject, "Head", 8f, TextAnchor.MiddleLeft);
            V6Layout.Place(head.GetComponent<RectTransform>(), 8f, 8f, 496f, 36f);
            // 子の幅は TMP の preferredWidth に従わせる (SCORE ラベル + 値が右へはみ出さない)。
            head.childControlWidth = true;
            V5GameUi.Text(head.gameObject, "Title", "プレイ情報", 18f, V5Tokens.TextDim);
            var spacer = V5Ui.Node(head.gameObject, "Spacer");
            var spacerLe = spacer.AddComponent<LayoutElement>();
            spacerLe.flexibleWidth = 1f;
            V5GameUi.Text(head.gameObject, "Score Label", "SCORE", 18f, V5Tokens.TextDim, TextAlignmentOptions.Right);
            playScoreValueText = V5GameUi.Text(head.gameObject, "Score Value", LastPlayValid(song)
                ? V6SelectViewLogic.FormatScore(GManager.score) : "–", 24f, V5Tokens.Text,
                TextAlignmentOptions.Right, FontStyles.Bold);
            playScoreValueText.textWrappingMode = TextWrappingModes.NoWrap;

            bool valid = LastPlayValid(song);
            judgeValueTexts.Add(BuildJudgeCell(panel, 20f, 52f, "PERFECT", valid ? GManager.perfect.ToString() : "–"));
            judgeValueTexts.Add(BuildJudgeCell(panel, 189f, 52f, "GREAT", valid ? GManager.great.ToString() : "–"));
            judgeValueTexts.Add(BuildJudgeCell(panel, 358f, 52f, "BAD", valid ? GManager.bad.ToString() : "–"));
            judgeValueTexts.Add(BuildJudgeCell(panel, 20f, 140f, "MISS", valid ? GManager.miss.ToString() : "–"));
            // design (d0bZbJ) は 5 番目のセル「HOLD」("0") を持つ。現行エンジンの譜面は
            // すべて通常ノーツ (chart の type は 1 のみ) で HOLD の集計は存在しないため、
            // プレイが有効なときの真の値は常に 0。design と同じ "0" を出す。
            judgeValueTexts.Add(BuildJudgeCell(panel, 189f, 140f, "HOLD", valid ? "0" : "–"));
            // design: MAX COMBO の value のみ fs24
            judgeValueTexts.Add(BuildJudgeCell(panel, 358f, 140f, "MAX COMBO",
                valid ? V6HistoryLogic.ComboLabel(GManager.maxCombo, song != null ? song.NoteCount : 0) : "–",
                24f));
        }

        /// <summary>
        /// 直前のプレイを実数で出せるか。GManager の play 静的フィールドは
        /// 「最後にプレイした曲」のものなので、選択中の曲がその曲
        /// (GManager.songID) のときだけ出す。次の遷移 (ResetSession) で消える。
        /// </summary>
        private static bool LastPlayValid(V6SongEntry song)
        {
            return song != null && song.SongId == GManager.songID
                && V6HistoryLogic.HasLastPlay(GManager.score, GManager.perfect, GManager.great, GManager.bad, GManager.miss);
        }

        /// <summary>曲ランキング情報 (528x304 $k2): 選択中の曲のランキング 4 行。</summary>
        private void BuildSongRanking(GameObject column, V6SongEntry song)
        {
            songRankRowObjects.Clear();
            songRankNoDataText = null;
            songRankThumbGo = null;
            songRankChipText = null;
            var panel = V6Layout.Place(V5Ui.Panel(column, "Song Ranking", new Vector2(528f, 304f), V5Tokens.Key2, 8f), 16f, 452f);
            AddScrollCatcher(panel, 1);
            songRankPanelGo = panel;
            songRankHasSong = song != null;

            var head = V5GameUi.Row(panel.gameObject, "Head", 8f, TextAnchor.MiddleLeft);
            V6Layout.Place(head.GetComponent<RectTransform>(), 8f, 8f, 496f, 36f);
            // 子の幅は TMP の preferredWidth に従わせる (曲名チップが右端を越えない)。
            head.childControlWidth = true;
            V5GameUi.Text(head.gameObject, "Title", "曲ランキング", 18f, V5Tokens.TextDim);
            var headSpacer = V5Ui.Node(head.gameObject, "Spacer");
            var headSpacerLe = headSpacer.AddComponent<LayoutElement>();
            headSpacerLe.flexibleWidth = 1f;
            if (song != null)
            {
                var songChip = V5GameUi.Row(head.gameObject, "Song", 6f, TextAnchor.MiddleCenter);
                // 親 Head は childControlWidth=true。Image 単体は preferredWidth を持たないため、
                // 40x24 を LayoutElement で明示しないとサムネイルが幅 0 に潰れる。
                songChip.childControlWidth = true;
                songChip.childControlHeight = false;
                var thumb = V6Layout.Place(V5Ui.Panel(songChip.gameObject, "Thumb", new Vector2(40f, 24f), V5Tokens.Key3, 4f), 0f, 0f);
                var thumbLe = thumb.AddComponent<LayoutElement>();
                thumbLe.preferredWidth = 40f;
                thumbLe.preferredHeight = 24f;
                songRankThumbGo = thumb;
                SetJacket(thumb, song);
                songRankChipText = V5GameUi.Text(songChip.gameObject, "Text", song.Title, 18f, V5Tokens.Text);
            }

            // 空状態テキストは常に作っておき、有無だけ setActive で切り替える
            // (作成・破棄で骨格を揺らさない。取得状態に応じて文言は更新される)。
            songRankNoDataText = V5GameUi.Text(panel.gameObject, "No Data", "",
                24f, V5Tokens.TextDim, TextAlignmentOptions.Left);
            V6Layout.Place(songRankNoDataText, 24f, 60f, 496f, 44f, new Vector2(0f, 0.5f));

            List<V6RankingRow> rows = SongRankingRows(song);
            if (rows.Count == 0)
            {
                // 空 / 取得中 / 失敗 / 対象外を区別して出す (Issue #239)。
                // cloud 曲はデータソースの取得状態、ローカル曲は記録の有無のみ。
                songRankNoDataText.text = V6RankingLogic.SongRankingEmptyMessage(
                    song, data.IsLoading, data.HasError, data.StatusMessage);
                songRankNoDataText.gameObject.SetActive(true);
                return;
            }

            songRankNoDataText.gameObject.SetActive(false);

            int first = songRankingScroll;
            int last = Mathf.Min(rows.Count, first + SongRankingVisibleRows);
            for (int i = first; i < last; i++)
            {
                songRankRowObjects.Add(CreateSongRankRow(panel, rows[i], i - first));
            }
        }

        /// <summary>
        /// 選択中の曲のランキング行。cloud 曲はオンライン取得結果
        /// (data.RankingFor)、ローカル曲 / 未取得はローカルの自分のベスト 1 行。
        /// </summary>
        private List<V6RankingRow> SongRankingRows(V6SongEntry song)
        {
            IReadOnlyList<V6RankingRow> fetched = data.RankingFor(song);
            if (fetched != null && fetched.Count > 0)
            {
                return new List<V6RankingRow>(fetched);
            }
            return V6RankingLogic.LocalRows(song);
        }

        /// <summary>プレイヤー情報 / プレイ情報の小セル (label 左 / value 右)。値テキストを返す (差分更新で書き換えるため)。</summary>
        private TextMeshProUGUI BuildInfoCell(GameObject panel, float x, float y, string label, string value)
        {
            var cell = V6Layout.Place(V5Ui.Panel(panel, "Stat " + label, new Vector2(248f, 60f), V5Tokens.Key1, 8f), x, y);

            var labelText = V5GameUi.Text(cell.gameObject, "Label", label, 18f, V5Tokens.TextDim);
            V6Layout.Place(labelText, 12f, 0f, 100f, 60f, new Vector2(0f, 0.5f));

            var valueText = V5GameUi.Text(cell.gameObject, "Value", value, 24f, V5Tokens.Text,
                TextAlignmentOptions.Right);
            // 累計スコアは全曲合計で 8 桁を超えうる。120px の値セルで折り返さないよう
            // 1 行固定 + 自動縮小にする (#379 と同じ規約)。
            valueText.textWrappingMode = TextWrappingModes.NoWrap;
            valueText.enableAutoSizing = true;
            valueText.fontSizeMin = 16f;
            valueText.fontSizeMax = 24f;
            V6Layout.Place(valueText, 116f, 0f, 120f, 60f, new Vector2(0f, 0.5f));
            return valueText;
        }

        /// <summary>
        /// プレイ情報の判定セル (design: 150×80 $k1、label fs18 上 / value fs32 下。
        /// MAX COMBO の value のみ fs24)。値テキストを返す (差分更新で書き換えるため)。
        /// </summary>
        private TextMeshProUGUI BuildJudgeCell(GameObject panel, float x, float y, string label, string value,
            float valueFs = 32f)
        {
            var cell = V6Layout.Place(V5Ui.Panel(panel, "Judge " + label, new Vector2(150f, 80f), V5Tokens.Key1, 8f), x, y);
            var col = V5GameUi.Column(cell, "Inner", 2f, TextAnchor.MiddleCenter);
            V6Layout.StretchCenter(col.GetComponent<RectTransform>(), 150f, 80f);
            V5GameUi.Text(col.gameObject, "Label", label, 18f, V5Tokens.TextDim, TextAlignmentOptions.Center);
            return V5GameUi.Text(col.gameObject, "Value", value, valueFs, V5Tokens.Text, TextAlignmentOptions.Center);
        }
    }
}
