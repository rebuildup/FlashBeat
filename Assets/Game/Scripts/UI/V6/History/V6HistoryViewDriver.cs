using UnityEngine;
using UnityEngine.EventSystems;

namespace FlashBeat.UI.V6.History
{
    // V6HistoryView の毎フレーム処理 (解像度変化の追従) を回すドライバと、
    // ホイールスクロールの受け皿 (Issue #385 Phase 2.6 で V6HistoryView.cs から分割)。
    // V6HistoryView 自体は MonoBehaviour にしない。

    /// <summary>解像度変化の追従だけを行うホスト (キー入力は読まない)。</summary>
    internal sealed class V6HistoryViewDriver : MonoBehaviour
    {
        internal V6HistoryView View;

        private void Update()
        {
            if (View != null)
            {
                View.UpdatePlacement();
            }
        }
    }

    /// <summary>ホイールスクロールの受け皿。</summary>
    internal sealed class V6HistoryScrollCatcher : MonoBehaviour, IScrollHandler
    {
        internal V6HistoryView View;
        internal int Target;

        public void OnScroll(PointerEventData eventData)
        {
            if (View != null)
            {
                View.OnScroll(Target, eventData.scrollDelta.y);
            }
        }
    }
}
