using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;
using FlashBeat.UI.V5;
using FlashBeat.UI.V6;
using FlashBeat.UI.V6.Common;

namespace FlashBeat.UI.V6.Game.Loading
{
    /// <summary>
    /// V6LoadingView の 1:1 配置と毎フレーム更新 (Issue #385 Phase 2.8 で分割)。
    /// フレーム原点の再配置 (<see cref="UpdatePlacement"/>) と進捗バーの更新
    /// (<see cref="Tick"/>) を持つ。partial class のためフィールド・メソッドは
    /// V6LoadingView 本体と共有する。
    /// </summary>
    public sealed partial class V6LoadingView
    {
        // --- 配置 / 毎フレーム ---

        /// <summary>
        /// 共通フレームを 1:1 (等倍) で画面中央に置く
        /// (<see cref="V6LoadingViewLogic.FrameOrigin"/>、ポーズと同じ規約)。
        /// 等比拡大 (min(W/1728, H/972)) は行わない。
        /// </summary>
        private void UpdatePlacement()
        {
            if (frame == null)
            {
                return;
            }

            int w = Screen.width;
            int h = Screen.height;
            Vector2 origin = V6LoadingViewLogic.FrameOrigin(w, h);
            frame.localScale = Vector3.one;
            // anchor/pivot (0,0) = 親 (全画面 root) の左下基準。design 原点は左上。
            frame.anchoredPosition = new Vector2(origin.x, h - origin.y - FrameH);
        }

        internal void Tick()
        {
            UpdatePlacement();
            // VideoPlayer の準備完了は非同期なので、準備できた瞬間に映像面が
            // 動画に切り替わるよう毎フレーム状態を見直す。
            ApplyVideoSurface();
            if (fill != null && !errorShown && progressGroup != null && progressGroup.activeSelf)
            {
                if (V6LoadingViewLogic.HasProgress(progress))
                {
                    // 実進捗: 塗り幅が割合に比例 (0 でも 0 幅で「既知」を出す)。
                    fill.anchoredPosition = Vector2.zero;
                    fill.sizeDelta = new Vector2(
                        V6LoadingViewLogic.DeterminateFillWidth(progress.Value, TrackW), TrackH);
                }
                else
                {
                    // 進捗不明: 不定インジケータ (往復)。
                    fill.sizeDelta = new Vector2(FillW, TrackH);
                    float x = V6LoadingViewLogic.IndeterminateX(Time.unscaledTime, TrackW, FillW);
                    fill.anchoredPosition = new Vector2(x, 0f);
                }
            }
        }
    }
}
