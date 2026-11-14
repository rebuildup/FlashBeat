using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FlashBeat.Gameplay;
using FlashBeat.UI.V5;
using FlashBeat.UI.V6;
using FlashBeat.UI.V6.Common;

namespace FlashBeat.UI.V6.Game.Pause
{
    /// <summary>
    /// V6PauseView の表示更新部分 (Issue #385 Phase 2.3 で分割)。
    /// Refresh から呼ばれるヘッダー / タブ / 行 / キー割当プレビューの再描画と、
    /// 行選択・ジャケット取得を持つ。
    /// partial class のためフィールド・メソッドは V6PauseView 本体と共有する。
    /// </summary>
    public sealed partial class V6PauseView
    {
        // --- 表示の更新 ---

        private void RefreshHeader()
        {
            var song = GManager.GetSong(GManager.songID);
            songTitle.text = song != null ? song.title : "";
            songSub.text = V6PauseViewLogic.SongSub(song);
            levelValue.text = song != null ? song.level.ToString() : "0";
            notesValue.text = V6PauseViewLogic.FormatNotes(song);
            durationValue.text = V6PauseViewLogic.FormatDuration(song != null ? song.duration : 0f);

            string videoId = song != null ? song.videoId : null;
            if (!string.IsNullOrEmpty(videoId) && loadedVideoId != videoId)
            {
                loadedVideoId = videoId;
                RequestJacket(videoId);
            }
        }

        /// <summary>
        /// ジャケットは YouTube サムネイル (実曲)。失敗時はプレースホルダのまま。
        ///
        /// Issue #398: 取得は <see cref="V6JacketProvider"/> に一本化した。
        /// 以前はこの画面が独自に UnityWebRequest を回し Sprite.Create していた為、
        /// (1) 同じ映像 ID を Result / ローディングと同時に取りに行くと HTTP が重複し、
        /// (2) 生成した Texture2D と Sprite の所有者が無く、ポーズを開くたびに
        ///     native が積み上がっていた。
        /// </summary>
        private void RequestJacket(string videoId)
        {
            if (thumbImage != null)
            {
                thumbImage.sprite = null;
                thumbImage.color = V5Tokens.Key2;
            }

            if (string.IsNullOrEmpty(videoId))
            {
                return;
            }

            V6JacketProvider.RequestRemote(videoId, sprite =>
            {
                // ポーズを閉じた / 別の曲へ移った後に届いた結果は捨てる。
                if (thumbImage == null || loadedVideoId != videoId)
                {
                    return;
                }

                if (sprite == null)
                {
                    return; // プレースホルダのまま (design の空枠)
                }

                thumbImage.sprite = sprite;
                thumbImage.preserveAspect = true;
                thumbImage.color = Color.white;
            });
        }

        private void RefreshTabs()
        {
            for (int i = 0; i < tabPanels.Count && i < tabLabels.Count; i++)
            {
                bool active = i == selectedTab;
                var img = tabPanels[i].GetComponent<Image>();
                if (img != null)
                {
                    img.color = active ? V5Tokens.Key6 : V5Tokens.Key2;
                }

                tabLabels[i].color = active ? V5Tokens.Text : V5Tokens.TextMuted;
                tabLabels[i].fontStyle = active ? FontStyles.Bold : FontStyles.Normal;
            }

            for (int i = 0; i < pages.Count; i++)
            {
                pages[i].SetActive(i == selectedTab);
            }

            if (settingsTitle != null && selectedTab < TabNames.Length)
            {
                settingsTitle.text = TabNames[selectedTab];
            }
        }

        private void RefreshRows()
        {
            for (int page = 0; page < pageRows.Count; page++)
            {
                foreach (var row in pageRows[page])
                {
                    if (row.Read != null)
                    {
                        row.Value.text = row.Read();
                    }
                }
            }

            RefreshRowHighlight();
        }

        private void RefreshRowHighlight()
        {
            for (int page = 0; page < pageRows.Count; page++)
            {
                var rows = pageRows[page];
                for (int i = 0; i < rows.Count; i++)
                {
                    var panel = rows[i].Value.transform.parent.gameObject;
                    var img = panel.GetComponent<Image>();
                    if (img == null)
                    {
                        continue;
                    }

                    bool selected = page == selectedTab && i == selectedRow;
                    img.color = selected ? V5Tokens.Key6 : V5Tokens.Key1;
                }
            }
        }

        private void SelectRow(int page, int index)
        {
            if (page != selectedTab)
            {
                SelectTab(page);
                return;
            }

            selectedRow = V6PauseViewLogic.ClampIndex(index, pageRows[selectedTab].Count);
            RefreshRowHighlight();
            if (volumeSlider != null && selectedTab == VolumePage)
            {
                volumeSlider.Refresh();
            }
        }

        /// <summary>KeyBindings の変更 (キー設定 / レイアウト) をプレビューへ即時反映。</summary>
        private void RefreshKeyBindings()
        {
            // 見出しは RefreshPreviewForPage がタブごとに決める (Sound では「音量」)。
            KeyCode[][] bindings = KeyBindings.Current;
            for (int i = 0; i < keyLabels.Count && i < keyCaps.Count; i++)
            {
                char c = KeyCharAt(i);
                int lane = V6PauseViewLogic.LaneOfKey(bindings, c);
                keyLabels[i].text = lane >= 0 ? (lane + 1).ToString() : "";
                if (keyCaps[i] != null)
                {
                    keyCaps[i].color = lane >= 0 ? V5Tokens.Key4 : V5Tokens.Key2;
                }
            }
        }

        /// <summary>JIS 全段を通しで並べたときの i 番目の刻印。</summary>
        private static char KeyCharAt(int index)
        {
            int i = index;
            for (int row = 0; row < V6PauseViewLogic.JisKeyRows.Length; row++)
            {
                char[] keys = V6PauseViewLogic.JisKeyRows[row];
                if (i < keys.Length)
                {
                    return keys[i];
                }

                i -= keys.Length;
            }

            return '\0';
        }
    }
}
