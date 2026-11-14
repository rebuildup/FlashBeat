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
    /// V6SelectView の中央列部分 (Issue #385 Phase 2.4 で分割)。
    /// Selected Song Panel ゾーン (ヒーロージャケット・Meta・Stats・Controls) の
    /// 構築と差分更新を持つ。
    /// partial class のためフィールド・メソッドは V6SelectView 本体と共有する。
    /// </summary>
    public sealed partial class V6SelectView
    {
        // --- 中央列: 選択中の曲 (dRztp, $k2 r16) ---

        private void BuildCenter()
        {
            var panel = V6Layout.Place(V5Ui.Panel(frame.gameObject, "Selected Song Panel", new Vector2(CenterW, ListH),
                V5Tokens.Key2, 16f), MainX + CenterX, MainY);
            var song = data.Selected;
            bool hasSong = song != null;

            var hero = V6Layout.Place(V5Ui.Panel(panel, "Hero", new Vector2(512f, 288f),
                hasSong ? V5Tokens.Key3 : V5Tokens.Key3, 8f), 12f, 24f);
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
                // design dRztp: Meta は縦 gap4・中央揃え。Title (0,8.5) 512×50、
                // Artist Row (0,62.5) 512×29 — 50 + 4 + 29 = 83 が 100 の中で中央。
                // 長い曲名は折り返さず自動縮小で 1 行に収める (Artist 行との重なり防止)。
                title.textWrappingMode = TextWrappingModes.NoWrap;
                title.enableAutoSizing = true;
                title.fontSizeMin = 30f;
                title.fontSizeMax = 48f;
                V6Layout.Place(title, 0f, 8.5f, 512f, 50f);
                // Issue #427: Title と同じ 512 幅・中央揃えにして、曲名と作曲者名の
                // 中心軸を構造的に一致させる。タグ (No.N) は左端に絶対配置し、
                // アーティスト名はパネル全幅で中央寄せする (行グループの中央揃えだと
                // タグ分だけ文字が右へずれて見えた)。
                var tag = V6Layout.Place(V5Ui.Panel(meta, "Tag No", new Vector2(63f, 29f), V5Tokens.Key4, 4f),
                    0f, 62.5f);
                var tagText = V5GameUi.Text(tag, "Text", "No." + song.SongId, 20f, V5Tokens.TextMuted,
                    TextAlignmentOptions.Center);
                V6Layout.StretchCenter(tagText.GetComponent<RectTransform>(), 63f, 29f);
                var artistText = V5GameUi.Text(meta, "Artist", song.Artist, 26f, V5Tokens.TextMuted,
                    TextAlignmentOptions.Center);
                artistText.textWrappingMode = TextWrappingModes.NoWrap;
                artistText.overflowMode = TextOverflowModes.Ellipsis;
                artistText.enableAutoSizing = true;
                artistText.fontSizeMin = 18f;
                artistText.fontSizeMax = 26f;
                V6Layout.Place(artistText, 0f, 62.5f, 512f, 29f);
            }
            else
            {
                V6Layout.Place(V5Ui.Panel(meta, "Skeleton Title", new Vector2(280f, 44f), V5Tokens.Key3, 4f), 116f, 10f);
                V6Layout.Place(V5Ui.Panel(meta, "Skeleton Artist", new Vector2(160f, 24f), V5Tokens.Key3, 4f), 176f, 62f);
            }

            var stats = V6Layout.Place(V5Ui.Panel(panel, "Stats", new Vector2(512f, 196f), V5Tokens.Key1, 8f), 12f, 452f);
            BuildStat(stats, 0f, 0f, "BPM", hasSong && song.Bpm > 0
                ? Mathf.RoundToInt((float)song.Bpm).ToString() : "–");
            BuildStat(stats, 256f, 0f, "NOTES", hasSong && song.NoteCount > 0
                ? song.NoteCount.ToString() : "–");
            BuildStat(stats, 0f, 98f, "LEVEL", hasSong && song.Level > 0
                ? song.Level.ToString() : "–");
            BuildStat(stats, 256f, 98f, "BEST", hasSong
                ? V6SelectViewLogic.FormatScore(song.BestScore) : "–");
            V6Layout.Place(V5Ui.Panel(stats, "Divider V", new Vector2(1f, 196f), V5Tokens.FromHex("#FFFFFF1F"), 0f), 256f, 0f);
            V6Layout.Place(V5Ui.Panel(stats, "Divider H", new Vector2(512f, 1f), V5Tokens.FromHex("#FFFFFF1F"), 0f), 0f, 98f);

            // Controls (skip-back / play / skip-forward): 実在する操作だけに結線する
            // (#221 Task 3: 押せない疑似ボタンは禁止)。曲送りは data.MoveSelection、
            // play は Ready (フッターの Ready ボタンと同じ経路)。プレビュー再生の
            // ような新しい操作は作らない (Issue #222 スコープ訂正 2026-11-08)。
            var controls = V5Ui.Node(panel, "Controls");
            V6Layout.Place(controls.GetComponent<RectTransform>(), 12f, 668f, 512f, 88f);
            var controlDefs = V6SelectViewLogic.CenterControls();
            for (int i = 0; i < controlDefs.Length; i++)
            {
                BuildControlButton(controls, controlDefs[i]);
            }

            centerHasSong = hasSong;
        }

        /// <summary>
        /// 選択曲パネル: scaffold が無いか選択曲の有無 (Meta 構造) が変わったときだけ
        /// 作り直し、それ以外は <see cref="UpdateCenter"/> で可変値を適用する。
        /// </summary>
        private void EnsureCenter()
        {
            bool hasSong = data.Selected != null;
            var panel = FindZone("Selected Song Panel");
            if (panel == null || hasSong != centerHasSong)
            {
                DestroyZone("Selected Song Panel");
                BuildCenter();
                return;
            }

            UpdateCenter(panel);
        }

        /// <summary>中央パネルの可変値 (ジャケット・曲名/作曲者・統計・Ready 結線) を適用する。</summary>
        private void UpdateCenter(GameObject panel)
        {
            var song = data.Selected;
            if (song == null)
            {
                // スケルトン (選択曲なし) は静的。null↔非 null の切替はゾーン再構築で処理する。
                return;
            }

            var hero = panel.transform.Find("Hero");
            if (hero != null)
            {
                SetJacket(hero.gameObject, song);
            }

            V6Hierarchy.SetChildText(panel, "Meta/Title", song.Title);
            V6Hierarchy.SetChildText(panel, "Meta/Tag No/Text", "No." + song.SongId);
            V6Hierarchy.SetChildText(panel, "Meta/Artist", song.Artist);
            V6Hierarchy.SetChildText(panel, "Stats/Stat BPM/Value",
                song.Bpm > 0 ? Mathf.RoundToInt((float)song.Bpm).ToString() : "–");
            V6Hierarchy.SetChildText(panel, "Stats/Stat NOTES/Value",
                song.NoteCount > 0 ? song.NoteCount.ToString() : "–");
            V6Hierarchy.SetChildText(panel, "Stats/Stat LEVEL/Value",
                song.Level > 0 ? song.Level.ToString() : "–");
            V6Hierarchy.SetChildText(panel, "Stats/Stat BEST/Value",
                V6SelectViewLogic.FormatScore(song.BestScore));

            // Controls の play (Ready) は絞り込みで選択が隠れると押せなくなる。
            // 結線の有無を毎回張り直して表示と実動作を一致させる。
            var play = panel.transform.Find("Controls/Btn play");
            var playButton = play != null ? play.GetComponent<Button>() : null;
            if (playButton != null)
            {
                playButton.onClick.RemoveAllListeners();
                if (ReadyAvailable())
                {
                    playButton.onClick.AddListener(() =>
                    {
                        if (onReady != null) onReady();
                    });
                }
            }
        }

        /// <summary>
        /// 統計セル (BPM / NOTES / LEVEL / BEST)。design は「ラベル左 / 値右」の
        /// 2 要素だけなので、LayoutGroup ではなく絶対位置で置く
        /// (LayoutGroup だと値の幅が計算されず、BPM の値だけ描画されない)。
        /// </summary>
        private void BuildStat(GameObject stats, float x, float y, string label, string value)
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
                TextAlignmentOptions.Right, FontStyles.Normal);
            // スコアは桁数が増えても折り返さない (FHD で「992,30 / 7」と 2 行化した実機報告 #379)。
            // 幅を超えた分は自動縮小で 1 行に収める。
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
        }

        /// <summary>中央パネルの Controls 行のボタン 1 個分。実操作へ結線する (onClick null は作らない)。</summary>
        private void BuildControlButton(GameObject controls, V6SelectViewLogic.V6CenterControlDef def)
        {
            float h = def.Size;
            Color bg = def.Primary ? V5Tokens.ButtonPrimaryBg : V5Tokens.Key1;
            Action onClick;
            switch (def.Action)
            {
                case V6SelectViewLogic.V6SelectControlAction.MovePrev:
                    onClick = () => data.MoveSelection(-1);
                    break;
                case V6SelectViewLogic.V6SelectControlAction.MoveNext:
                    onClick = () => data.MoveSelection(1);
                    break;
                case V6SelectViewLogic.V6SelectControlAction.Ready:
                    // Ready は絞り込みで隠れた選択には効かない (フッターと同じ規約)。
                    onClick = ReadyAvailable() ? (Action)(() =>
                    {
                        if (onReady != null) onReady();
                    }) : null;
                    break;
                default:
                    // 実在しない操作のボタンは作らない (疑似ボタン禁止)。
                    return;
            }

            var btn = V6Button.Panel(controls, "Btn " + def.Icon, new Vector2(def.Size, def.Size), bg,
                def.Size * 0.5f, onClick);
            V6Layout.Place(btn, def.X, (88f - def.Size) * 0.5f);
            V6Icons.Create(btn, def.Icon, def.Primary ? 36f : 32f,
                def.Primary ? V5Tokens.ButtonPrimaryLabel : V5Tokens.TextMuted, def.Icon);
        }
    }
}
