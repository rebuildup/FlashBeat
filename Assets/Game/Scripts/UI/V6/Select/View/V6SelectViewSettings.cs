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
    /// V6SelectView のクイック設定部分 (Issue #385 Phase 2.4 で分割)。
    /// wSjAm の右カラム差し替え (このプレイの設定: Key Guide / Lyrics / NoteSpeed) の
    /// 構築と差分更新を持つ。partial class のためフィールド・メソッドは V6SelectView 本体と共有する。
    /// </summary>
    public sealed partial class V6SelectView
    {
        // --- 設定 (wSjAm 右カラム差し替え。モーダル/Scrim は使わない) ---

        private void OpenSettings()
        {
            if (onOpenSettings != null)
            {
                onOpenSettings();
                return;
            }

            settingsOpen = true;
            Rebuild();
        }

        private void CloseSettings()
        {
            settingsOpen = false;
            Rebuild();
        }

        /// <summary>
        /// 右カラム (Ranking カラムの位置) を「このプレイの設定」パネルへ差し替える。
        /// design wSjAm: Head (24,8) fs26 w500、行 512×56 (背景 #2D2D2D r8) を
        /// 68 間隔で 3 行 (Key Guide / Lyrics / NoteSpeed)。
        /// </summary>
        private void BuildSettings()
        {
            var column = V6Layout.Place(V5Ui.Panel(frame.gameObject, "Settings Column", new Vector2(RightW, ListH),
                Color.clear, 0f), MainX + RightX, MainY);

            // 契約の w500 は既定フォント (NotoSansJP-Medium) 自身のウェイトなので Normal。
            var head = V5GameUi.Text(column.gameObject, "Head", "このプレイの設定", 26f, V5Tokens.Text,
                TextAlignmentOptions.Left, FontStyles.Normal);
            V6Layout.Place(head, V6SelectViewLogic.SettingsHeadX, V6SelectViewLogic.SettingsHeadY,
                240f, 36f);

            BuildSettingsRow(column, 0, "Key Guide", PlaySession.KeyGuideLabel(PlaySession.KeyGuide),
                d => PlaySession.SetKeyGuide(PlaySession.NextKeyGuide(PlaySession.KeyGuide, d)));
            BuildSettingsRow(column, 1, "Lyrics", PlaySession.LyricsLabel(PlaySession.LyricsEnabled),
                d => PlaySession.SetLyricsEnabled(!PlaySession.LyricsEnabled));
            BuildSettingsRow(column, 2, "NoteSpeed", Mathf.RoundToInt(GManager.noteSpeed).ToString(),
                d =>
                {
                    float value = Mathf.RoundToInt(GManager.noteSpeed) + d;
                    GManager.noteSpeed = Mathf.Clamp(value, OptionSettings.NoteSpeedMin,
                        OptionSettings.NoteSpeedMax);
                    OptionSettings.Save();
                });
        }

        /// <summary>設定カラム: scaffold が無ければ作り、値表示だけを更新する。</summary>
        private void EnsureSettings()
        {
            var column = FindZone("Settings Column");
            if (column == null)
            {
                BuildSettings();
                return;
            }

            UpdateSettings(column);
        }

        /// <summary>クイック設定の値表示を現在値へ合わせる (行オブジェクトは再利用)。</summary>
        private void UpdateSettings(GameObject column)
        {
            V6Hierarchy.SetChildText(column, "Set Key Guide/Value", PlaySession.KeyGuideLabel(PlaySession.KeyGuide));
            V6Hierarchy.SetChildText(column, "Set Lyrics/Value", PlaySession.LyricsLabel(PlaySession.LyricsEnabled));
            V6Hierarchy.SetChildText(column, "Set NoteSpeed/Value", Mathf.RoundToInt(GManager.noteSpeed).ToString());
        }

        /// <summary>
        /// 設定行 1 個分 (design wSjAm)。列は 3 行で完全に共通 (Issue #370):
        /// Icon 24 @12 / Label @48 / Arrow · chevron-left @276 / Value 120 @328 /
        /// Arrow · chevron-right @460。値は左右の矢印の幾何学的な中央 (x388) に置き、
        /// 値の文字数で列を動かさない。
        /// </summary>
        private void BuildSettingsRow(GameObject column, int index, string label, string value, Action<int> adjust)
        {
            var rect = V6SelectViewLogic.SettingsRowRect(index);
            var row = V6Layout.Place(V5Ui.Panel(column, "Set " + label, new Vector2(rect.W, rect.H),
                V5Tokens.Base, FlashBeat.UI.V6.Common.V6Tokens.RadiusMedium), rect.X, rect.Y);

            var iconRect = V6SelectViewLogic.SettingsRowIcon();
            var iconImage = V6Icons.Create(row, "list-filter", iconRect.W, V5Tokens.TextMuted);
            if (iconImage != null)
            {
                V6Layout.Place(iconImage.gameObject, iconRect.X, iconRect.Y, iconRect.W, iconRect.H);
            }

            var labelRect = V6SelectViewLogic.SettingsRowLabel();
            var labelText = V5GameUi.Text(row.gameObject, "Label", label, 24f, V5Tokens.Text);
            labelText.textWrappingMode = TextWrappingModes.NoWrap;
            V6Layout.Place(labelText, labelRect.X, labelRect.Y, labelRect.W, labelRect.H, new Vector2(0f, 0.5f));

            // 値は固定幅 120 の中央揃え (design の Value フレーム)。矢印の位置は値に依存しない。
            var valueRect = V6SelectViewLogic.SettingsRowValue();
            var valueText = V5GameUi.Text(row.gameObject, "Value", value, 24f, V5Tokens.Text,
                TextAlignmentOptions.Center);
            valueText.textWrappingMode = TextWrappingModes.NoWrap;
            V6Layout.Place(valueText, valueRect.X, valueRect.Y, valueRect.W, valueRect.H, new Vector2(0f, 0.5f));

            var prevRect = V6SelectViewLogic.SettingsRowPrev();
            var left = V6Button.Panel(row, "Prev", new Vector2(prevRect.W, prevRect.H), V5Tokens.Key4, 8f,
                () => { adjust(-1); RefreshVolatile(); });
            V6Layout.Place(left, prevRect.X, prevRect.Y);
            V6Icons.Create(left, "chevron-left", 24f, V5Tokens.TextMuted);

            var nextRect = V6SelectViewLogic.SettingsRowNext();
            var right = V6Button.Panel(row, "Next", new Vector2(nextRect.W, nextRect.H), V5Tokens.Key4, 8f,
                () => { adjust(1); RefreshVolatile(); });
            V6Layout.Place(right, nextRect.X, nextRect.Y);
            V6Icons.Create(right, "chevron-right", 24f, V5Tokens.TextMuted);
        }
    }
}
