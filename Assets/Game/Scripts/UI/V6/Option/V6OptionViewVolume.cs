using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FlashBeat.UI.V5;
using FlashBeat.UI.V6;
using FlashBeat.UI.V6.Common;

namespace FlashBeat.UI.V6.Option
{
    /// <summary>
    /// V6OptionView の音量スライダ部分 (Issue #385 Phase 2.5 で分割、Issue #375)。
    /// Sound タブの V6VolumeSlider 結線 (BuildVolumePreview)、音量値モデル
    /// (GetVolume/SetVolume)、表示同期 (UpdateVolumeVisuals) と、キー操作後の
    /// 遅延保存 (FlushPendingVolumeSave/SavePendingVolumeNow) を持つ。
    /// partial class のためフィールド・メソッドは V6OptionView 本体と共有する。
    /// </summary>
    public sealed partial class V6OptionView
    {
        /// <summary>
        /// Sound タブ: 選択行の音量を共通スライダ部品 (<see cref="V6VolumeSlider"/>、
        /// design o0zh7 J7tBYP) で出す。値ラベル / トラック / フィル / ノブ / ヒントを
        /// プレビューパネル内 (24,344) に置き、フォーカス行の音量と双方向に同期する。
        /// ドラッグは押した位置へジャンプし、離した時点で保存する (Issue #375 §3.2)。
        /// </summary>
        private void BuildVolumePreview(GameObject panel)
        {
            Row row = SelectedRowDesc();
            if (row == null || row.VolumeIndex < 0)
            {
                return;
            }

            int index = row.VolumeIndex;
            volumeSlider = V6VolumeSlider.Build(panel, "Volume Slider",
                new Vector2(V6OptionLogic.VolumeSliderX, V6OptionLogic.VolumeSliderY),
                () => GetVolume(index),
                (value, final) =>
                {
                    SetVolume(index, value, final);
                    UpdateVolumeVisuals();
                });
            volumeSliderRowIndex = index;
        }

        /// <summary>音量 (0..1)。対象 index は <see cref="Row.VolumeIndex"/>。</summary>
        private static float GetVolume(int index)
        {
            switch (index)
            {
                case 0: return GManager.mainVolume;
                case 1: return GManager.effectVolume;
                case 2: return GManager.BGMVolume;
                default: return 0f;
            }
        }

        /// <summary>
        /// 音量を書き、即時に音へ反映する。保存は final (ドラッグ終了) のときだけ行い、
        /// キー操作中は 0.5 秒後にまとめる (毎フレーム PlayerPrefs を書かない)。
        /// </summary>
        private void SetVolume(int index, float value, bool final)
        {
            float clamped = Mathf.Clamp01(value);
            switch (index)
            {
                case 0:
                    GManager.mainVolume = clamped;
                    break;
                case 1:
                    GManager.effectVolume = clamped;
                    break;
                case 2:
                    GManager.BGMVolume = clamped;
                    break;
                default:
                    return;
            }

            OptionSceneManager.ApplyVolumes();
            if (final)
            {
                volumeSaveAt = 0f;
                OptionSettings.Save();
                return;
            }

            volumeSaveAt = Time.unscaledTime + VolumeSaveDelay;
        }

        /// <summary>スライダと選択行の値表示だけを更新する (レイアウトは作り直さない)。</summary>
        private void UpdateVolumeVisuals()
        {
            if (volumeSlider != null)
            {
                volumeSlider.Refresh();
            }

            Row row = SelectedRowDesc();
            if (row == null || row.VolumeIndex < 0)
            {
                return;
            }

            if (selectedRowValueText != null)
            {
                selectedRowValueText.text = V6OptionLogic.FormatVolume(GetVolume(row.VolumeIndex));
            }
        }

        /// <summary>キー操作で予約された保存を、猶予が過ぎていれば実行する。</summary>
        private void FlushPendingVolumeSave()
        {
            if (volumeSaveAt <= 0f || Time.unscaledTime < volumeSaveAt)
            {
                return;
            }

            SavePendingVolumeNow();
        }

        /// <summary>猶予を待たずに予約済みの音量保存を実行する (画面を離れるとき)。</summary>
        private void SavePendingVolumeNow()
        {
            if (volumeSaveAt <= 0f)
            {
                return;
            }

            volumeSaveAt = 0f;
            OptionSettings.Save();
        }
    }
}
