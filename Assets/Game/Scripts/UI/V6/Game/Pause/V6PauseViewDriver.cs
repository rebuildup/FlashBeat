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

    /// <summary>V6PauseView の毎フレーム処理 (1:1 配置 + 実データの再描画)。</summary>
    internal sealed class V6PauseViewDriver : MonoBehaviour
    {
        internal V6PauseView View;

        private void Update()
        {
            if (View != null)
            {
                View.Tick();
            }
        }
    }
}
