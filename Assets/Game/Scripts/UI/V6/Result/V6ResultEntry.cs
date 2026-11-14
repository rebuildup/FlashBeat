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
    /// V6ResultView のランキング (曲別 / 登録名ごと) パネルと
    /// ランキング取得部分 (Issue #385 Phase 2.1 で分割)。partial class のため
    /// フィールド・メソッドは V6ResultView 本体と共有する。
    /// </summary>
    public sealed partial class V6ResultView
    {
        // ランキング (列4-5, Main 内 x=856 → V6ResultLogic.RankX)。
        private const float SongRankY = 24f, SongRankH = 412f;
        private const float PlayerRankY = 452f, PlayerRankH = 304f;
        private const int SongRankVisibleRows = 6;
        private const int PlayerRankVisibleRows = 4;
        private const float RankRowStep = 52f;

        private TextMeshProUGUI songRankEmpty;
        private TextMeshProUGUI songRankTitle;
        private RectTransform songRankRows;
        private TextMeshProUGUI playerRankEmpty;
        // Issue #415: 曲別ランキング行の差分更新用スロット (行オブジェクトを再利用する)。
        private readonly List<GameObject> songRankRowObjects = new List<GameObject>();
        /// <summary>登録成功時にサーバから返された順位 (0 = 未登録)。</summary>
        private int myRank;
        // オンラインランキング (実通信は既存 RankingClient を使う)。
        private readonly List<FlashBeat.UI.V6.Select.V6RankingRow> rankingRows
            = new List<FlashBeat.UI.V6.Select.V6RankingRow>();
        private bool rankingFetchInFlight;
        private bool hasRankingError;
        private string rankingStatusMessage = "";

        // 曲別ランキング (856,24) 528x412: Head + 曲名 + 6 行。

        private void BuildSongRanking(GameObject main)
        {
            var panel = V6Layout.Place(V5Ui.Panel(main.gameObject, "Song Ranking", new Vector2(528f, SongRankH),
                V5Tokens.Key2, 8f), V6ResultLogic.RankX, SongRankY);

            var head = V6Layout.Place(V5Ui.Node(panel.gameObject, "Head"), V6ResultLogic.RankHeadX, V6ResultLogic.RankHeadY,
                V6ResultLogic.RankHeadW, V6ResultLogic.RankHeadH);
            var headTitle = V5GameUi.Text(head.gameObject, "Title", "曲別ランキング", 18f, V5Tokens.TextDim);
            // design d6Noy (12,5) 126×26。Spacer (150,17.5) 350×1 は fill 無しのレイアウト用で描かない。
            V6Layout.Place(headTitle, 12f, 5f, V6ResultLogic.RankHeadTitleW, V6ResultLogic.RankHeadTitleH);

            // design Lo2pD「終焉逃避行」(20,52) 150×43 fs30。150 は 5 文字サンプルの hug 幅なので、
            // 実データ (最長 12 文字) は行幅いっぱいの 484 まで使い、はみ出す分だけ … で省略する。
            songRankTitle = V5GameUi.Text(panel.gameObject, "Song", "", 30f, V5Tokens.Text);
            V6Layout.Place(songRankTitle, V6ResultLogic.SongRankTitleX, V6ResultLogic.SongRankTitleY,
                V6ResultLogic.SongRankTitleW, V6ResultLogic.SongRankTitleH);
            songRankTitle.textWrappingMode = TextWrappingModes.NoWrap;
            songRankTitle.overflowMode = TextOverflowModes.Ellipsis;
            V5Ui.FitEllipsisToLineHeight(songRankTitle);

            // 空状態は design に無い実装固有の表示。Song 見出し (y52..95) と重ならないよう
            // 行領域 (y92..404) の縦中央に置く。
            songRankEmpty = V5GameUi.Text(panel.gameObject, "No Data", "", 24f, V5Tokens.TextDim);
            V6Layout.Place(songRankEmpty, 20f, 226f, 496f, 44f, new Vector2(0f, 0.5f));

            songRankRows = V5Ui.Node(panel.gameObject, "Rows").GetComponent<RectTransform>();
            V6Layout.Place(songRankRows, 0f, 0f, 528f, SongRankH);

            lateGroups.Add(FadeGroup(panel));
        }

        // 登録名ランキング (856,452) 528x304: Head (集計軸) + 4 行。
        // Head は design どおり 1 行 (見出し | 矢印 | 集計軸 | 矢印、padding [0,12] gap8)。
        // 登録名ごとの集計は既存 API に無いため空状態のみ (未実装は PR に記載)。

        private void BuildPlayerRanking(GameObject main)
        {
            var panel = V6Layout.Place(V5Ui.Panel(main.gameObject, "Player Ranking", new Vector2(528f, PlayerRankH),
                V5Tokens.Key2, 8f), V6ResultLogic.RankX, PlayerRankY);

            var head = V6Layout.Place(V5Ui.Node(panel.gameObject, "Head"), V6ResultLogic.RankHeadX, V6ResultLogic.RankHeadY,
                V6ResultLogic.RankHeadW, V6ResultLogic.RankHeadH);
            var headTitle = V5GameUi.Text(head.gameObject, "Title", "登録名ごとのランキング", 18f, V5Tokens.TextDim);
            V6Layout.Place(headTitle, 12f, 0f, V6ResultLogic.RankHeadTitleW, V6ResultLogic.RankHeadH, new Vector2(0f, 0.5f));
            BuildChevron(head, V6ResultLogic.RankHeadChevronL, "chevron-left");
            var metric = V5GameUi.Text(head.gameObject, "Metric", "累計スコア", 18f, V5Tokens.Text,
                TextAlignmentOptions.Center);
            // 累計スコア (5 文字 x 18 = 90) が幅 92 に収まるよう折り返さない
            // (旧実装は幅 68 で折り返し、2 行目が矢印と重なった)。
            metric.textWrappingMode = TextWrappingModes.NoWrap;
            V6Layout.Place(metric, V6ResultLogic.RankHeadMetricX, 0f, V6ResultLogic.RankHeadMetricW, V6ResultLogic.RankHeadH,
                new Vector2(0f, 0.5f));
            BuildChevron(head, V6ResultLogic.RankHeadChevronR, "chevron-right");

            playerRankEmpty = V5GameUi.Text(panel.gameObject, "No Data", "NO DATA", 24f, V5Tokens.TextDim);
            V6Layout.Place(playerRankEmpty, 20f, 60f, 496f, 44f, new Vector2(0f, 0.5f));

            lateGroups.Add(FadeGroup(panel));
        }

        private void BuildChevron(GameObject head, float x, string iconName)
        {
            var chevron = V6Layout.Place(V5Ui.Panel(head.gameObject, "Arrow", new Vector2(32f, 32f), V5Tokens.Key4, 8f), x, 2f);
            var icon = V6Icons.Create(chevron.gameObject, iconName, 24f, V5Tokens.TextMuted, "Icon");
            if (icon != null)
            {
                V6Layout.StretchCenter(icon.rectTransform, 32f, 32f);
            }
        }

        private void RefreshRanking()
        {
            // 曲別ランキング: 実取得結果 (未取得中は状態メッセージ)。
            RebuildSongRankingRows();

            if (rankingFetchInFlight)
            {
                songRankEmpty.text = string.IsNullOrEmpty(rankingStatusMessage)
                    ? "ランキングを取得しています…" : rankingStatusMessage;
                songRankEmpty.gameObject.SetActive(true);
            }
            else if (rankingRows.Count == 0)
            {
                songRankEmpty.text = hasRankingError && !string.IsNullOrEmpty(rankingStatusMessage)
                    ? rankingStatusMessage : "NO DATA";
                songRankEmpty.gameObject.SetActive(true);
            }
            else
            {
                songRankEmpty.gameObject.SetActive(false);
            }
        }

        private void RebuildSongRankingRows()
        {
            // Issue #415: 曲別ランキング行はスロット単位で既存オブジェクトを再利用し、
            // 行データの差分だけを既存 Image/TMP に適用する (全行破棄再生成はやめる)。
            int rows = Mathf.Min(rankingRows.Count, SongRankVisibleRows);
            for (int slot = 0; slot < SongRankVisibleRows; slot++)
            {
                bool valid = slot < rows;
                float y = 92f + RankRowStep * slot;
                GameObject row = slot < songRankRowObjects.Count ? songRankRowObjects[slot] : null;
                if (!valid)
                {
                    if (row != null)
                    {
                        row.transform.SetParent(null, false);
                        V5GameUi.DestroyNow(row);
                        songRankRowObjects[slot] = null;
                    }

                    continue;
                }

                var rowData = rankingRows[slot];
                if (row == null)
                {
                    row = BuildRankingRow(songRankRows.gameObject, rowData, y);
                    if (slot < songRankRowObjects.Count) songRankRowObjects[slot] = row;
                    else songRankRowObjects.Add(row);
                }
                else
                {
                    UpdateRankingRow(row, rowData, y);
                }
            }

            while (songRankRowObjects.Count > SongRankVisibleRows)
            {
                int last = songRankRowObjects.Count - 1;
                GameObject tail = songRankRowObjects[last];
                if (tail != null)
                {
                    tail.transform.SetParent(null, false);
                    V5GameUi.DestroyNow(tail);
                }

                songRankRowObjects.RemoveAt(last);
            }
        }

        /// <summary>既存のランキング行を新しい行データで書き換える (行オブジェクトは再利用)。</summary>
        private static void UpdateRankingRow(GameObject row, FlashBeat.UI.V6.Select.V6RankingRow rowData, float y)
        {
            var rt = row.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition = V6Layout.ToLocal(new Vector2(8f, y));
            }

            var bg = row.GetComponent<Image>();
            if (bg != null)
            {
                bg.color = rowData.IsYou ? V6Tokens.Key6 : V6Tokens.Key1;
            }

            V6Hierarchy.SetChildText(row, "Pos/PosText", rowData.Position.ToString());
            V6Hierarchy.SetChildText(row, "Name", rowData.IsYou ? "YOU" : rowData.PlayerName);
            V6Hierarchy.SetChildText(row, "Value", V6ResultLogic.FormatScore(rowData.Score));
            V6Hierarchy.SetChildText(row, "Grade/GradeText", string.IsNullOrEmpty(rowData.Grade) ? "–" : rowData.Grade);

            Transform posText = row.transform.Find("Pos/PosText");
            var posTmp = posText != null ? posText.GetComponent<TextMeshProUGUI>() : null;
            if (posTmp != null)
            {
                posTmp.color = rowData.IsYou ? V6Tokens.Text : V6Tokens.TextMuted;
            }
        }

        private GameObject BuildRankingRow(GameObject parent, FlashBeat.UI.V6.Select.V6RankingRow row, float y)
        {
            // 行内部は共通部品 (design Chlqd 496×44) が持つ。行位置だけがパネル固有点。
            return V6Lists.CompactRankRow(parent, row.Position.ToString(),
                row.IsYou ? "YOU" : row.PlayerName, V6ResultLogic.FormatScore(row.Score),
                row.Grade, new Vector2(8f, y), row.IsYou);
        }

        // --- ランキング取得 (実通信は既存 RankingClient) ---

        private void StartRankingFetch()
        {
            SongData song = SongOf();
            if (song == null || !song.isRemote)
            {
                return; // ローカル曲にオンラインランキングはない (V6SelectDataSource と同じ規律)
            }

            // 譜面版はプレイ開始時に固定した (pinned) 版を使う (Issue #240)。
            int chartVersion = V6ResultLogic.ResolveChartVersion(
                PinnedChartVersionOf(), song.chartVersion);
            if (string.IsNullOrEmpty(song.remoteId) || chartVersion <= 0
                || !RankingClient.IsValidSongId(song.remoteId))
            {
                return;
            }

            if (!Application.isPlaying)
            {
                return; // EditMode (テスト) では通信しない
            }

            if (rankingFetchInFlight)
            {
                return;
            }

            rankingFetchInFlight = true;
            hasRankingError = false;
            rankingStatusMessage = "ランキングを取得しています…";
            RefreshRanking();
            RunCoroutine(FetchRanking(song.remoteId, chartVersion));
        }

        private IEnumerator FetchRanking(string remoteId, int chartVersion)
        {
            RankingClient.Result result = null;
            yield return RankingClient.GetLeaderboard(remoteId, chartVersion, r => { result = r; });

            rankingFetchInFlight = false;
            rankingRows.Clear();
            if (result != null && result.ok && result.leaderboard != null && result.leaderboard.entries != null)
            {
                hasRankingError = false;
                rankingStatusMessage = "";
                string nickname = RankingClient.LoadNickname();
                RankingClient.LeaderboardEntry[] entries = result.leaderboard.entries;
                for (int i = 0; i < entries.Length; i++)
                {
                    RankingClient.LeaderboardEntry entry = entries[i];
                    if (entry == null)
                    {
                        continue;
                    }

                    rankingRows.Add(new FlashBeat.UI.V6.Select.V6RankingRow
                    {
                        Position = entry.rank,
                        PlayerName = entry.nickname ?? "",
                        Score = entry.score,
                        Grade = FlashBeat.UI.V6.Select.Data.V6SelectDataLogic.GradeOf(entry.score),
                        IsYou = (!string.IsNullOrEmpty(nickname) && entry.nickname == nickname)
                            || (myRank > 0 && entry.rank == myRank),
                    });
                }
            }
            else
            {
                hasRankingError = true;
                rankingStatusMessage = RankingFailureMessage(result);
            }

            RefreshRanking();
        }

        private static string RankingFailureMessage(RankingClient.Result result)
        {
            if (result == null)
            {
                return "ランキングを取得できませんでした";
            }

            if (result.error == RankingClient.UnavailableError)
            {
                return "ネットワークに接続できないためランキングを取得できませんでした";
            }

            if (result.httpCode > 0)
            {
                return string.Format(CultureInfo.InvariantCulture,
                    "ランキングの取得に失敗しました (HTTP {0})", result.httpCode);
            }

            return string.IsNullOrEmpty(result.error)
                ? "ランキングを取得できませんでした"
                : "ランキングの取得に失敗しました (" + result.error + ")";
        }
    }
}
