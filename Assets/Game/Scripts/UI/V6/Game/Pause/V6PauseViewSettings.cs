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
        /// <summary>KeyBindings 未初期化 (null) は既定扱い (IsCustomLayout は null を
        /// 「既定と異なる」と扱うのでここで防ぐ)。</summary>
        private static bool IsCustomLayout()
        {
            return KeyBindings.Current != null && KeyConfigUI.IsCustomLayout();
        }

        /// <summary>Volume の行の値変更 (GManager へ書き、音も即時反映)。</summary>
        private enum VolumeTarget
        {
            Main = 0,
            Se = 2,
        }

        private void AdjustVolume(int dir, VolumeTarget target)
        {
            // Issue #375 §3.2: 1% 刻み、Shift 併用で 10%。Option と同じ値モデル。
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            switch (target)
            {
                case VolumeTarget.Main:
                    GManager.mainVolume = V6VolumeSlider.Nudge(GManager.mainVolume, dir, shift);
                    break;
                case VolumeTarget.Se:
                    GManager.effectVolume = V6VolumeSlider.Nudge(GManager.effectVolume, dir, shift);
                    break;
            }

            ApplyVolumes();
            OptionSettings.Save();
            if (volumeSlider != null)
            {
                volumeSlider.Refresh();
            }
        }

        /// <summary>選択行 (Sound の 0 = Main / 1 = SE) の音量。</summary>
        private float SelectedPauseVolume()
        {
            return selectedRow == 1 ? GManager.effectVolume : GManager.mainVolume;
        }

        /// <summary>スライダーのドラッグ結果を GManager へ書き、即時反映する。
        /// 保存はドラッグ終了時だけ (ドラッグ中は毎フレーム書かない)。</summary>
        private void SetSelectedPauseVolume(float value, bool final)
        {
            float clamped = Mathf.Clamp01(value);
            if (selectedRow == 1)
            {
                GManager.effectVolume = clamped;
            }
            else
            {
                GManager.mainVolume = clamped;
            }

            ApplyVolumes();
            if (final)
            {
                OptionSettings.Save();
            }

            RefreshRows();
            if (volumeSlider != null)
            {
                volumeSlider.Refresh();
            }
        }

        /// <summary>音量は各所有者が起動時にしか読まないので、シーン内の AudioSource にも即時反映
        /// (v5 PauseMenuView.ApplyVolumes と同じ挙動)。</summary>
        private void ApplyVolumes()
        {
            AudioListener.volume = GManager.mainVolume;

            var music = UnityEngine.Object.FindFirstObjectByType<MusicManager>();
            if (music != null)
            {
                var src = music.GetComponent<AudioSource>();
                if (src != null)
                {
                    src.volume = GManager.mainVolume;
                }
            }

            var judge = UnityEngine.Object.FindFirstObjectByType<Judge>();
            if (judge != null)
            {
                var src = judge.GetComponent<AudioSource>();
                if (src != null)
                {
                    src.volume = GManager.effectVolume;
                }
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
