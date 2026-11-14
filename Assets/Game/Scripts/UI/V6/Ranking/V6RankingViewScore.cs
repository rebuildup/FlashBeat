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
    /// V6RankingView の中列 (SCORE RANKING パネル: セグメント / ランキング行 / 空状態 /
    /// 再試行) 構築 (Issue #385 Phase 2.7 で分割)。partial class のためフィールド・メソッドは
    /// V6RankingView 本体と共有する。
    /// </summary>
    public sealed partial class V6RankingView
    {
        // --- 中列: SCORE RANKING (512x732 $k2) ---

        /// <summary>
        /// Issue #415: SCORE RANKING は骨格を保持する。曲の有無 / Online セグメントの
        /// 出し分けが変わったときだけパネルを作り直す (骨格変更扱い)。
        /// それ以外はジャケット・セグメント色・ランキング行 (既存 RankRow) を差分更新する。
        /// </summary>
        private void EnsureScoreRanking(GameObject mainZone, V6SongEntry song)
        {
            bool hasSong = song != null;
            bool onlineAvailable = ShowOnlineSegmentForSelected();
            GameObject panel = scorePanelGo != null ? scorePanelGo
                : mainZone.transform.Find("Score Ranking")?.gameObject;
            if (panel == null)
            {
                CreateScoreRankingScaffold(mainZone, song);
                ApplyScoreRanking(mainZone, song);
                return;
            }

            if (scoreHasSong != hasSong || scoreOnlineShown != onlineAvailable)
            {
                V6Hierarchy.DetachAndDestroy(panel);
                scorePanelGo = null;
                songChipThumbGo = null;
                songChipText = null;
                segmentObjects.Clear();
                noDataText = null;
                retryGo = null;
                rankRowObjects.Clear();
                CreateScoreRankingScaffold(mainZone, song);
            }

            ApplyScoreRanking(mainZone, song);
        }

        private GameObject CreateScoreRankingScaffold(GameObject mainZone, V6SongEntry song)
        {
            rankRowObjects.Clear();
            segmentObjects.Clear();
            songChipThumbGo = null;
            songChipText = null;
            bool hasSong = song != null;
            scoreHasSong = hasSong;
            bool onlineAvailable = ShowOnlineSegmentForSelected();
            scoreOnlineShown = onlineAvailable;

            // design (qICtu 実測): SCORE RANKING は Main 内 (584,24) 512×732 $k2 r8。
            var panel = V6Layout.Place(V5Ui.Panel(mainZone, "Score Ranking", new Vector2(RankingW, RankingH),
                V5Tokens.Key2, V6Tokens.RadiusMedium), RankingX, RankingY);
            AddScrollCatcher(panel, 1);
            scorePanelGo = panel;

            // design (H8yC4 実測): Head (8,8) 496×40。子は自動レイアウトに任せず
            // design の絶対座標で置く (自動幅だと曲名チップが Segment に重なり、
            // Online が画面外へ切れていた)。
            var head = V5Ui.Node(panel.gameObject, "Head");
            V6Layout.Place(head.GetComponent<RectTransform>(), 8f, 8f, 496f, 40f);

            // Title "SCORE RANKING" (12,7) 139×26 fs18 $text-dim。
            // design は 1 行。V5GameUi.Text の既定は折り返し有効で、この書体だと
            // "SCORE RANKING" が 139px に収まらず "SCORE" / "RANKING" の 2 行に
            // 割れていた (r21 実機) ため NoWrap を明示する。
            var headTitle = V5GameUi.Text(head.gameObject, "Label", "SCORE RANKING", 18f, V5Tokens.TextDim);
            headTitle.textWrappingMode = TextWrappingModes.NoWrap;
            V6Layout.Place(headTitle, 12f, 7f, 139f, 26f);

            if (song != null)
            {
                // design (wIE4t): Song チップ (159,4) 159×32 =
                // Thumb (0,4) 40×24 r4 + Text (48,0) 111×32 fs22 1 行固定 ellipsis。
                // 曲バッジ (タイトルのみ) は共通部品 (Issue #385 Phase 1.2)。
                var songChip = V5Ui.Node(head.gameObject, "Song");
                V6Layout.Place(songChip.GetComponent<RectTransform>(), 159f, 4f, 159f, 32f);
                var chipBadge = V6SongBadge.Create(songChip, new V6SongBadge.Spec
                {
                    ThumbPos = new Vector2(0f, 4f),
                    ThumbSize = new Vector2(40f, 24f),
                    ThumbColor = V5Tokens.Key3,
                    Title = new V6SongBadge.Line
                    {
                        Name = "Text", Pos = new Vector2(48f, 0f), Size = new Vector2(111f, 32f),
                        FontSize = 22f, Color = V5Tokens.Text, Text = song.Title,
                        FitLineHeight = true,
                    },
                });
                songChipThumbGo = chipBadge.Thumb;
                songChipText = chipBadge.Title;
            }

            // Online セグメントは cloud 曲だけ (spec 167: ローカル曲では非表示)。
            // 非表示の間は表示を Local に寄せる (見えないセグメントへの切替で
            // 本体が空になることを防ぐ)。

            // design (NEbbi): Segment (335,0) 156×40 $k0 r8。子は Local (4,4) /
            // Online (80,4) を 72×32 r4 で絶対配置 (Song が無くても位置は固定)。
            var segment = V6Layout.Place(V5Ui.Panel(head.gameObject, "Segment", new Vector2(156f, 40f), V5Tokens.Key0, 8f),
                335f, 0f);

            segmentObjects.Add(BuildSegment(segment, "Local", V6RankingLogic.RankingSource.Local, 4f));
            if (onlineAvailable)
            {
                segmentObjects.Add(BuildSegment(segment, "Online", V6RankingLogic.RankingSource.Online, 80f));
            }

            // 空状態テキストと再試行ボタンは常に作っておき、有無だけ setActive で切り替える。
            noDataText = V5GameUi.Text(panel.gameObject, "No Data", "", 24f, V5Tokens.TextDim,
                TextAlignmentOptions.Left);
            V6Layout.Place(noDataText, 24f, 60f, 496f, 44f, new Vector2(0f, 0.5f));

            var retry = V6Button.Make(V5Ui.Panel(panel, "Retry", new Vector2(160f, 48f), V5Tokens.Key4, 8f),
                RetryFetch);
            V6Layout.Place(retry, 176f, 140f);
            var retryRow = V5GameUi.Row(retry, "Inner", 8f, TextAnchor.MiddleCenter);
            V6Layout.StretchCenter(retryRow.GetComponent<RectTransform>(), 160f, 48f);
            V6Icons.Create(retryRow.gameObject, "skip-back", 22f, V5Tokens.TextMuted, "Icon");
            V5GameUi.Text(retryRow.gameObject, "Label", "再試行", 24f, V5Tokens.Text, TextAlignmentOptions.Center);
            retryGo = retry;
            return panel;
        }

        /// <summary>SCORE RANKING の変動値 (ジャケット/セグメント色/行/空状態/再試行) を適用する。</summary>
        private void ApplyScoreRanking(GameObject mainZone, V6SongEntry song)
        {
            GameObject panel = scorePanelGo != null ? scorePanelGo
                : mainZone.transform.Find("Score Ranking")?.gameObject;
            if (panel == null)
            {
                return;
            }

            if (songChipText != null) songChipText.text = song != null ? song.Title : "";
            if (songChipThumbGo != null && song != null) SetJacket(songChipThumbGo, song);

            // セグメントの有効色を実効ソースに合わせて塗り直す。
            V6RankingLogic.RankingSource effectiveSource = EffectiveSource();
            UpdateSegmentChip(segmentObjects.Count > 0 ? segmentObjects[0] : null,
                effectiveSource == V6RankingLogic.RankingSource.Local);
            UpdateSegmentChip(segmentObjects.Count > 1 ? segmentObjects[1] : null,
                effectiveSource == V6RankingLogic.RankingSource.Online);

            IReadOnlyList<V6RankingRow> rows = RankingRows(effectiveSource);
            bool hasRows = rows.Count > 0;
            if (noDataText != null)
            {
                if (hasRows)
                {
                    noDataText.gameObject.SetActive(false);
                }
                else
                {
                    // 未選択のときは「記録がありません」ではなく未選択であることを出す
                    // (Issue #239)。空データと未選択を同じ文言にしない。
                    noDataText.text = song == null
                        ? V6RankingLogic.NoSongSelectedMessage()
                        : V6RankingLogic.EmptyMessage(effectiveSource, data.IsLoading, data.HasError, data.StatusMessage);
                    noDataText.gameObject.SetActive(true);
                }
            }

            // 取得失敗時は再試行ボタン (Online のみ。Local は通信しない)。
            if (retryGo != null)
            {
                retryGo.SetActive(!hasRows
                    && effectiveSource == V6RankingLogic.RankingSource.Online
                    && data.HasError && !data.IsLoading);
            }

            int first = rankingScroll;
            for (int slot = 0; slot < RankingVisibleRows; slot++)
            {
                int i = first + slot;
                bool valid = hasRows && i < rows.Count;
                GameObject row = slot < rankRowObjects.Count ? rankRowObjects[slot] : null;
                float y = V6RankingLogic.RankRowTop(slot);
                if (!valid)
                {
                    if (row != null)
                    {
                        V6Hierarchy.DetachAndDestroy(row);
                        rankRowObjects[slot] = null;
                    }

                    continue;
                }

                if (row == null)
                {
                    row = CreateRankingRow(panel, rows[i], slot);
                    if (slot < rankRowObjects.Count) rankRowObjects[slot] = row;
                    else rankRowObjects.Add(row);
                }
                else
                {
                    UpdateRankingRow(row, rows[i], y);
                }
            }

            while (rankRowObjects.Count > RankingVisibleRows)
            {
                int last = rankRowObjects.Count - 1;
                V6Hierarchy.DetachAndDestroy(rankRowObjects[last]);
                rankRowObjects.RemoveAt(last);
            }
        }

        private static void UpdateSegmentChip(GameObject chip, bool active)
        {
            if (chip == null)
            {
                return;
            }

            var bg = chip.GetComponent<Image>();
            if (bg != null)
            {
                bg.color = active ? V5Tokens.Key4 : V5Tokens.Key1;
            }

            Transform text = chip.transform.Find("Text");
            var tmp = text != null ? text.GetComponent<TextMeshProUGUI>() : null;
            if (tmp != null)
            {
                tmp.color = active ? V5Tokens.Text : V5Tokens.TextMuted;
            }
        }

        private GameObject CreateRankingRow(GameObject panel, V6RankingRow row, int visibleIndex)
        {
            return V6Lists.RankRow(
                panel,
                row.Position.ToString(),
                row.IsYou ? "YOU" : row.PlayerName,
                V6SelectViewLogic.FormatScore(row.Score),
                string.IsNullOrEmpty(row.Grade) ? "–" : row.Grade,
                new Vector2(V6RankingLogic.RankRowX, V6RankingLogic.RankRowTop(visibleIndex)),
                new Vector2(V6RankingLogic.RankRowW, V6RankingLogic.RankRowH),
                row.IsYou);
        }

        /// <summary>既存のランキング行を新しい行データで書き換える (行オブジェクトは再利用)。</summary>
        private static void UpdateRankingRow(GameObject row, V6RankingRow dataRow, float y)
        {
            var rt = row.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition = new Vector2(V6RankingLogic.RankRowX, -y);
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

        private GameObject BuildSegment(GameObject segment, string label, V6RankingLogic.RankingSource target, float x)
        {
            // 有効判定は実効ソース基準 (Online 非対象の曲は Local 表示のため)。
            bool active = EffectiveSource() == target;
            // design (NEbbi): Local (4,4) / Online (80,4) を 72×32 r4 で絶対配置。
            // Online が無い曲でも Local の位置は (4,4) のまま (自動レイアウトで詰めない)。
            var chip = V6Button.Make(V5Ui.Panel(segment, "Segment " + label, new Vector2(72f, 32f),
                active ? V5Tokens.Key4 : V5Tokens.Key1, 4f), ToggleSource);
            V6Layout.Place(chip, x, 4f);
            // design (S1TZD Segment): text fs20
            var text = V5GameUi.Text(chip, "Text", V6RankingLogic.SourceLabel(target), 20f,
                active ? V5Tokens.Text : V5Tokens.TextMuted, TextAlignmentOptions.Center);
            V6Layout.StretchCenter(text.GetComponent<RectTransform>(), 72f, 32f);
            return chip;
        }

        /// <summary>選択中の曲での実効ソース (Online セグメント非表示の曲は Local)。</summary>
        private V6RankingLogic.RankingSource EffectiveSource()
        {
            return ShowOnlineSegmentForSelected()
                ? source
                : V6RankingLogic.RankingSource.Local;
        }

        /// <summary>選択中の曲で Online セグメントを出すか。</summary>
        private bool ShowOnlineSegmentForSelected()
        {
            return V6RankingLogic.ShowOnlineSegment(data.Selected);
        }

        /// <summary>表示するランキング行 (Local = 実保存の自分のベスト / Online = 実 API)。</summary>
        private IReadOnlyList<V6RankingRow> RankingRows(V6RankingLogic.RankingSource rankingSource)
        {
            V6SongEntry song = data.Selected;
            if (rankingSource == V6RankingLogic.RankingSource.Local)
            {
                return V6RankingLogic.LocalRows(song);
            }
            return data.RankingFor(song);
        }

    }
}
