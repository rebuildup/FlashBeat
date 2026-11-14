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

    /// <summary>V6LoadingView の毎フレーム処理 (1:1 配置 + 不定インジケータ)。</summary>
    internal sealed class V6LoadingViewDriver : MonoBehaviour
    {
        internal V6LoadingView View;

        private void Update()
        {
            if (View != null)
            {
                View.Tick();
            }
        }
    }
}
