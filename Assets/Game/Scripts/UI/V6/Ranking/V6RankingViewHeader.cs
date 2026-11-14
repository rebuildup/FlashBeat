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
    /// V6RankingView のヘッダーゾーン (対象曲バッジ + 統計値) 構築 (Issue #385 Phase 2.7 で分割)。
    /// partial class のためフィールド・メソッドは V6RankingView 本体と共有する。
    /// </summary>
    public sealed partial class V6RankingView
    {
        // --- ヘッダー (共通フレームの Header ゾーン: design (24,0) 1680×96) ---

        /// <summary>
        /// Issue #415: ヘッダーは骨格を保持し、ジャケット・曲名・アーティスト・
        /// 統計値 (LEVEL/BPM/NOTES) だけを既存要素へ適用する。
        /// </summary>
        private void EnsureHeader(GameObject headerZone, V6SongEntry song)
        {
            if (headerZone.transform.childCount == 0)
            {
                CreateHeaderScaffold(headerZone, song);
            }

            ApplyHeader(song);
        }

        private void ApplyHeader(V6SongEntry song)
        {
            bool hasSong = song != null;
            if (headerThumbGo != null)
            {
                SetJacket(headerThumbGo, song);
            }

            if (headerTitleText != null) headerTitleText.text = hasSong ? song.Title : "–";
            if (headerArtistText != null) headerArtistText.text = hasSong ? song.Artist : "";

            if (headerStatValues.Count == 3)
            {
                headerStatValues[0].text = hasSong && song.Level > 0 ? song.Level.ToString() : "–";
                headerStatValues[1].text = hasSong && song.Bpm > 0
                    ? Mathf.RoundToInt((float)song.Bpm).ToString() : "–";
                headerStatValues[2].text = hasSong && song.NoteCount > 0 ? song.NoteCount.ToString() : "–";
            }
        }

        private void CreateHeaderScaffold(GameObject headerZone, V6SongEntry song)
        {
            // 戻るボタン + タイトルは共通部品 (Issue #385)。design S1TZD:
            // header (0,20) 56×56 / Title (72,20) 400×56 fs38。
            // 確定キーはキー設定の正本から取る。
            V6Header.BackButton(headerZone, "Back Button",
                ActionKeyHint.Primary(KeyActions.KeyAction.Back), () => TriggerBack());
            V6Header.Title(headerZone, "RANKING");

            // 右上: 曲情報 (ジャケット + 曲名/作家 + LEVEL / BPM / NOTES)。
            // design (S1TZD 情報ゾーン w4siL): header 内 x840 / w840 / padding [0,24] /
            // gap16 の横並び。Thumb 64×36 → Song 320×76 → Divider 1×44 →
            // Stat 100×62 ×3 がこの順で並ぶ (x は gap16 を挟んだ実測値)。
            // 曲バッジ部 (Thumb + Song 列) は共通部品 (Issue #385 Phase 1.2)。
            bool hasSong = song != null;
            var badge = V6SongBadge.Create(headerZone, new V6SongBadge.Spec
            {
                ThumbPos = new Vector2(864f, 30f),
                ThumbSize = new Vector2(64f, 36f),
                ThumbColor = V5Tokens.Key3,
                ColumnName = "Song",
                ColumnPos = new Vector2(944f, 10f),
                ColumnSize = new Vector2(320f, 76f),
                // design w4siL/Song: Title (0,1) 320×43 fs30 / Artist (0,46) 320×29 fs20。
                // 旧実装は Title に Rect を与えておらず、既定の 100×100 中央寄せで
                // design より右下へずれていた (1 行固定 + ellipsis で列内に収める)。
                Title = new V6SongBadge.Line
                {
                    Pos = new Vector2(0f, 1f), Size = new Vector2(320f, 43f), FontSize = 30f,
                    Color = V5Tokens.Text, Text = hasSong ? song.Title : "–",
                    // design h43 < 行送り (fs30 × 1.448 = 43.44) のため、Ellipsis が 0 行に
                    // なって曲名が消える (r3 実機で発生)。margin 補正で実効高を確保する。
                    FitLineHeight = true,
                },
                Sub = new V6SongBadge.Line
                {
                    Name = "Artist", Pos = new Vector2(0f, 46f), Size = new Vector2(320f, 29f),
                    FontSize = 20f, Color = V5Tokens.TextMuted, Text = hasSong ? song.Artist : "",
                    FitLineHeight = true,
                },
            });
            headerThumbGo = badge.Thumb;
            headerTitleText = badge.Title;
            headerArtistText = badge.Sub;
            if (hasSong)
            {
                SetJacket(headerThumbGo, song);
            }

            V6Layout.Place(V5Ui.Panel(headerZone, "Divider", new Vector2(1f, 44f), V5Tokens.FromHex("#FFFFFF26"), 0f), 1280f, 26f);

            headerStatValues.Add(BuildHeaderStat(headerZone, 1297f, "LEVEL", ""));
            headerStatValues.Add(BuildHeaderStat(headerZone, 1413f, "BPM", ""));
            headerStatValues.Add(BuildHeaderStat(headerZone, 1529f, "NOTES", ""));
        }

        /// <summary>ヘッダー右の統計 (100x62: label 上 / value 下)。値テキストを返す (差分更新で書き換えるため)。</summary>
        private TextMeshProUGUI BuildHeaderStat(GameObject header, float x, string label, string value)
        {
            var cell = V5Ui.Node(header, "Stat " + label);
            V6Layout.Place(cell.GetComponent<RectTransform>(), x, 17f, 100f, 62f);
            var col = V5GameUi.Column(cell, "Inner", 2f, TextAnchor.MiddleLeft);
            // 子の幅は TMP の preferredWidth に従わせる。childControlWidth=false のままだと
            // 既定の矩形幅 (200) のまま 100 幅のセルから右へはみ出す。
            col.childControlWidth = true;
            V6Layout.StretchCenter(col.GetComponent<RectTransform>(), 100f, 62f);
            V5GameUi.Text(col.gameObject, "Label", label, 16f, V5Tokens.TextDim, TextAlignmentOptions.Left);
            return V5GameUi.Text(col.gameObject, "Value", value, 24f, V5Tokens.Text, TextAlignmentOptions.Left,
                FontStyles.Bold);
        }

    }
}
