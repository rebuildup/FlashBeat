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
    /// (<see cref="V6VolumeSettings"/> — Issue #733 で Pause と共有) と
    /// 表示同期 (UpdateVolumeVisuals) を持つ。
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
            V6SettingRow row = SelectedRowDesc();
            if (row == null || row.VolumeIndex < 0)
            {
                return;
            }

            int index = row.VolumeIndex;
            volumeSlider = V6VolumeSlider.Build(panel, "Volume Slider",
                new Vector2(V6OptionLogic.VolumeSliderX, V6OptionLogic.VolumeSliderY),
                () => V6VolumeSettings.Get(index),
                (value, final) =>
                {
                    volumeSettings.Set(index, value, final);
                    UpdateVolumeVisuals();
                });
            volumeSliderRowIndex = index;
        }

        /// <summary>スライダと選択行の値表示だけを更新する (レイアウトは作り直さない)。</summary>
        private void UpdateVolumeVisuals()
        {
            if (volumeSlider != null)
            {
                volumeSlider.Refresh();
            }

            V6SettingRow row = SelectedRowDesc();
            if (row == null || row.VolumeIndex < 0)
            {
                return;
            }

            if (selectedRowValueText != null)
            {
                selectedRowValueText.text = V6SettingsCatalog.FormatVolume(V6VolumeSettings.Get(row.VolumeIndex));
            }
        }
    }
}
