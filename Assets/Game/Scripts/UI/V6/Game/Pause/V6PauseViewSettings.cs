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
    /// V6PauseView の設定値・実データ参照部分 (Issue #385 Phase 2.3 で分割)。
    /// Sound / Data タブの行が読み書きする値 (音量・ベストスコア・判定集計・
    /// 総ノーツ数) へのアクセスを持つ。
    /// partial class のためフィールド・メソッドは V6PauseView 本体と共有する。
    /// </summary>
    public sealed partial class V6PauseView
    {
        /// <summary>選択行 (Sound タブ) の音量対象 index (<see cref="V6SettingRow.VolumeIndex"/>)。
        /// 音量行以外が選ばれているときは先頭 (Main) を返す。</summary>
        private int SelectedVolumeIndex()
        {
            if (settingsScreen == null)
            {
                return 0;
            }

            var rows = settingsScreen.CurrentRows;
            int selected = settingsScreen.SelectedRow;
            if (selected >= 0 && selected < rows.Count && rows[selected].VolumeIndex >= 0)
            {
                return rows[selected].VolumeIndex;
            }

            return 0;
        }

        /// <summary>スライダーのドラッグ結果を GManager へ書き、即時反映する。
        /// 保存はドラッグ終了時だけ (ドラッグ中は毎フレーム書かない)。
        /// Issue #733: 値モデルは Option と同じ <see cref="V6VolumeSettings"/>
        /// (キー操作は 0.5 秒まとめて保存、ドラッグ確定・画面を離れるときは即保存)。
        /// Issue #733 フォローアップ: 共有設定画面のスライダーも同じ値モデルを
        /// 直接編集するため、この経路は検証ハーネス互換のために残す。</summary>
        private void SetSelectedPauseVolume(float value, bool final)
        {
            volumeSettings.Set(SelectedVolumeIndex(), value, final);
            if (settingsScreen != null)
            {
                settingsScreen.Refresh();
            }
        }

        private static int BestScore()
        {
            int id = GManager.songID;
            if (id >= 0 && id < GManager.Hiscore.Length)
            {
                return GManager.Hiscore[id];
            }

            // 固定テーブル外 (cloud 追加曲) は安定キーの保存先から読む (issue-162 A-6)。
            return GManager.BestScoreOf(id);
        }

        private static int PlayedSongs()
        {
            int played = 0;
            for (int i = 1; i < GManager.Hiscore.Length; i++)
            {
                if (GManager.Hiscore[i] > 0)
                {
                    played++;
                }
            }

            return played;
        }

        /// <summary>判定済みノーツ数 (進捗の分子)。GManager の判定カウンタの和。</summary>
        private static int JudgedNotes()
        {
            return GManager.perfect + GManager.great + GManager.bad + GManager.miss;
        }

        private NotesManager notesManager;

        /// <summary>譜面の総ノーツ数 (進捗の分母)。NotesManager.noteNum (生成した実ノーツ数)。</summary>
        private int TotalNotes()
        {
            if (notesManager == null)
            {
                notesManager = UnityEngine.Object.FindFirstObjectByType<NotesManager>();
            }

            return notesManager != null ? notesManager.noteNum : 0;
        }
    }
}
