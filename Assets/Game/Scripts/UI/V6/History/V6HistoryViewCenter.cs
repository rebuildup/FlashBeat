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
    /// V6HistoryView の中央列部分 (Issue #385 Phase 2.6 で分割)。
    /// 「Selected Song Panel」(design dRztp と同じ構造) の scaffold 構築、
    /// ジャケット・曲情報・統計セル (BPM/NOTES/LEVEL/BEST)・
    /// Controls (skip-back / play / skip-forward) の構築と差分適用を持つ。
    /// partial class のためフィールド・メソッドは V6HistoryView 本体と共有する。
    /// </summary>
    public sealed partial class V6HistoryView
    {
        // --- 中央列: 選択中の曲 (dRztp と同じ構造) ---

        /// <summary>
        /// Issue #415: 中央パネルは骨格を保持する。曲の有無で Meta の構造が変わる
        /// (タイトル+アーティスト行 ↔ スケルトンバー) ため、その切替時だけ作り直す。
        /// </summary>
        private void EnsureCenter(GameObject mainZone, V6SongEntry song)
        {
            bool hasSong = song != null;
            GameObject panel = centerGo != null ? centerGo : mainZone.transform.Find("Selected Song Panel")?.gameObject;
            if (panel == null)
            {
                CreateCenterScaffold(mainZone, song);
                ApplyCenter(song);
                return;
            }

            if (centerHasSong != hasSong)
            {
                V6Hierarchy.DetachAndDestroy(panel);
                centerGo = null;
                centerHeroGo = null;
                centerTitleText = null;
                centerArtistText = null;
                centerTagText = null;
                centerStatValues.Clear();
                CreateCenterScaffold(mainZone, song);
            }

            ApplyCenter(song);
        }

        private GameObject CreateCenterScaffold(GameObject mainZone, V6SongEntry song)
        {
            bool hasSong = song != null;
            centerHasSong = hasSong;
            var panel = V6Layout.Place(V5Ui.Panel(mainZone, "Selected Song Panel", new Vector2(CenterW, ListH),
                V5Tokens.Key2, 16f), CenterX, 0f);
            centerGo = panel;

            var hero = V6Layout.Place(V5Ui.Panel(panel, "Hero", new Vector2(512f, 288f), V5Tokens.Key3, 8f), 12f, 24f);
            centerHeroGo = hero;
            if (hasSong)
            {
                SetJacket(hero, song);
            }

            var meta = V5Ui.Node(panel, "Meta");
            V6Layout.Place(meta.GetComponent<RectTransform>(), 12f, 332f, 512f, 100f);
            if (hasSong)
            {
                var title = V5GameUi.Text(meta, "Title", song.Title, 48f, V5Tokens.Text,
                    TextAlignmentOptions.Center, FontStyles.Bold);
                // design q9JZcH 中央 選択中の曲/f54HAH/DcLR3: Title は Meta 内 (0,9) 512×50。
                // sizeDelta だけ変えると既定アンカー (中央) のままで 13px 下がり、
                // Artist Row (y60) に食い込む (r4 実機で重なって見えた)。
                V6Layout.Place(title, 0f, 9f, 512f, 50f);
                centerTitleText = title;
                var artistRow = V5GameUi.Row(meta, "Artist Row", 10f, TextAnchor.MiddleCenter);
                V6Layout.Place(artistRow.GetComponent<RectTransform>(), 0f, 60f, 512f, 36f);
                var tag = V5Ui.Panel(artistRow.gameObject, "Tag No", new Vector2(64f, 30f), V5Tokens.Key4, 4f);
                var tagText = V5GameUi.Text(tag, "Text", "No." + song.SongId, 20f, V5Tokens.TextMuted,
                    TextAlignmentOptions.Center);
                centerTagText = tagText;
                V6Layout.StretchCenter(tagText.GetComponent<RectTransform>(), 64f, 30f);
                centerArtistText = V5GameUi.Text(artistRow.gameObject, "Artist", song.Artist, 26f, V5Tokens.TextMuted);
            }
            else
            {
                V6Layout.Place(V5Ui.Panel(meta, "Skeleton Title", new Vector2(280f, 44f), V5Tokens.Key3, 4f), 116f, 10f);
                V6Layout.Place(V5Ui.Panel(meta, "Skeleton Artist", new Vector2(160f, 24f), V5Tokens.Key3, 4f), 176f, 62f);
            }

            var stats = V6Layout.Place(V5Ui.Panel(panel, "Stats", new Vector2(512f, 196f), V5Tokens.Key1, 8f), 12f, 452f);
            centerStatValues.Add(BuildStat(stats, 0f, 0f, "BPM", ""));
            centerStatValues.Add(BuildStat(stats, 256f, 0f, "NOTES", ""));
            centerStatValues.Add(BuildStat(stats, 0f, 98f, "LEVEL", ""));
            centerStatValues.Add(BuildStat(stats, 256f, 98f, "BEST", ""));
            V6Layout.Place(V5Ui.Panel(stats, "Divider V", new Vector2(1f, 196f), V5Tokens.FromHex("#FFFFFF1F"), 0f), 256f, 0f);
            V6Layout.Place(V5Ui.Panel(stats, "Divider H", new Vector2(512f, 1f), V5Tokens.FromHex("#FFFFFF1F"), 0f), 0f, 98f);

            // Controls (skip-back / play / skip-forward): 実在する操作だけに結線する
            // (V6SelectView と同じ規約。プレビュー再生のような新しい操作は作らない)。
            // skip は履歴リストの曲送り (MoveSelection)、play はフッターの
            // 「この曲へ」と同じ経路 (TriggerGotoSong)。
            var controls = V5Ui.Node(panel, "Controls");
            V6Layout.Place(controls.GetComponent<RectTransform>(), 12f, 668f, 512f, 88f);
            // design (dRztp Controls): gap20 で中央寄せ → 56+20+72+20+56 = 224、開始 x = (512−224)/2 = 144
            BuildControlButton(controls, "skip-back", 144f, 56f, 28f, 32f, () => MoveSelection(-1));
            BuildControlButton(controls, "play", 210f, 72f, 36f, 36f, TriggerGotoSong);
            BuildControlButton(controls, "skip-forward", 292f, 56f, 28f, 32f, () => MoveSelection(1));
            return panel;
        }

        /// <summary>中央パネルの変動値を既存要素へ適用する。</summary>
        private void ApplyCenter(V6SongEntry song)
        {
            bool hasSong = song != null;
            if (hasSong)
            {
                if (centerHeroGo != null) SetJacket(centerHeroGo, song);
                if (centerTitleText != null) centerTitleText.text = song.Title;
                if (centerTagText != null) centerTagText.text = "No." + song.SongId;
                if (centerArtistText != null) centerArtistText.text = song.Artist;

                if (centerStatValues.Count == 4)
                {
                    centerStatValues[0].text = song.Bpm > 0 ? Mathf.RoundToInt((float)song.Bpm).ToString() : "–";
                    centerStatValues[1].text = song.NoteCount > 0 ? song.NoteCount.ToString() : "–";
                    centerStatValues[2].text = song.Level > 0 ? song.Level.ToString() : "–";
                    centerStatValues[3].text = V6SelectViewLogic.FormatScore(song.BestScore);
                }
            }
            else if (centerStatValues.Count == 4)
            {
                centerStatValues[0].text = "–";
                centerStatValues[1].text = "–";
                centerStatValues[2].text = "–";
                centerStatValues[3].text = "–";
            }
        }

        /// <summary>
        /// 統計セル (BPM / NOTES / LEVEL / BEST)。LayoutGroup ではなく絶対位置で置く
        /// (V6SelectView.BuildStat と同じ実装)。値テキストを返す (差分更新で書き換えるため)。
        /// </summary>
        private TextMeshProUGUI BuildStat(GameObject stats, float x, float y, string label, string value)
        {
            var cell = V5Ui.Node(stats, "Stat " + label);
            V6Layout.Place(cell.GetComponent<RectTransform>(), x, y, 256f, 98f);

            var labelText = V5GameUi.Text(cell.gameObject, "Label", label, 22f, V5Tokens.TextDim);
            var labelRt = labelText.GetComponent<RectTransform>();
            labelRt.anchorMin = new Vector2(0f, 0.5f);
            labelRt.anchorMax = new Vector2(0f, 0.5f);
            labelRt.pivot = new Vector2(0f, 0.5f);
            labelRt.anchoredPosition = new Vector2(16f, 0f);
            labelRt.sizeDelta = new Vector2(160f, 30f);

            var valueText = V5GameUi.Text(cell.gameObject, "Value", value, 32f, V5Tokens.Text,
                TextAlignmentOptions.Right);
            // スコアは桁数が増えても折り返さない (V6SelectView.BuildStat と同じ規約 #379)。
            valueText.textWrappingMode = TextWrappingModes.NoWrap;
            valueText.enableAutoSizing = true;
            valueText.fontSizeMin = 24f;
            valueText.fontSizeMax = 32f;
            var valueRt = valueText.GetComponent<RectTransform>();
            valueRt.anchorMin = new Vector2(1f, 0.5f);
            valueRt.anchorMax = new Vector2(1f, 0.5f);
            valueRt.pivot = new Vector2(1f, 0.5f);
            valueRt.anchoredPosition = new Vector2(-16f, 0f);
            valueRt.sizeDelta = new Vector2(200f, 44f);
            return valueText;
        }

        /// <summary>中央パネルの Controls 行のボタン 1 個分。実操作へ結線する (onClick null は作らない)。</summary>
        private void BuildControlButton(GameObject controls, string name, float x, float size, float radius,
            float iconSize, Action onClick)
        {
            // design (dRztp Controls): skip 56×56 r28 $k4 / play 72×72 r36 #EDEDED、
            // icon は skip 系 32 $text-muted、play 36 #1B1B1B。
            bool isPlay = name == "play";
            GameObject btn = V6Controls.IconButton(controls, name,
                new Vector2(x, (88f - size) * 0.5f), new Vector2(size, size),
                isPlay ? V5Tokens.ButtonPrimaryBg : V5Tokens.Key4, radius,
                iconSize, isPlay ? V5Tokens.ButtonPrimaryLabel : V5Tokens.TextMuted);
            V6Button.Configure(btn, onClick);
        }
    }
}
