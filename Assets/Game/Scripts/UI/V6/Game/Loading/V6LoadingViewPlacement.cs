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
    /// フレーム原点の再配置 (<see cref="UpdatePlacement"/>) と映像面の
    /// 状態見直し (<see cref="Tick"/>) を持つ。partial class のため
    /// フィールド・メソッドは V6LoadingView 本体と共有する。
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
            V6CompactScreen.Place(frame, w, h);
        }

        internal void Tick()
        {
            UpdatePlacement();
            // VideoPlayer の準備完了は非同期なので、準備できた瞬間に映像面が
            // 動画に切り替わるよう毎フレーム状態を見直す。
            // 進捗・エラーの専用表示は design に無い (Issue #606) — 更新対象は映像面だけ。
            ApplyVideoSurface();
        }
    }
}
