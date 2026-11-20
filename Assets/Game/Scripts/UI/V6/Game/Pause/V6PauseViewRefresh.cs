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
    /// Refresh から呼ばれるヘッダー (曲情報・ジャケット) の再描画を持つ。
    ///
    /// Issue #733 フォローアップ: タブ・設定行・プレビューの更新は
    /// 共有部品 <see cref="V6SettingsScreen"/> (Option と同一実装) が担うため、
    /// ここには Pause 固有のヘッダー更新だけが残る。
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
    }
}
