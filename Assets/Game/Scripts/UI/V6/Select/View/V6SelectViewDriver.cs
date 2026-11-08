using UnityEngine;
using UnityEngine.EventSystems;

namespace FlashBeat.UI.V6.Select.View
{
    /// <summary>
    /// V6SelectView の毎フレーム処理 (キー入力 / 解像度変化) を回すドライバ。
    /// V6SelectView 自体は MonoBehaviour にしない (統合担当に公開する API を
    /// brief の Create/Root/Refresh/SetVisible/FocusSearch に限定するため)。
    /// </summary>
    internal sealed class V6SelectViewDriver : MonoBehaviour
    {
        internal V6SelectView View;

        private void Update()
        {
            // 旧 (v5) 側の Canvas / ルート ("V5 Select Canvas" など) も毎フレーム隠し直す
            // (v5 は Start/OnEnable で自前の Canvas を後から生成するため 1 回では足りない)。
            V6UiMode.HideLegacyV5Canvases();

            if (View != null)
            {
                View.Tick();
            }
        }
    }

    /// <summary>ホイールスクロールを V6SelectView へ回す (曲リスト / スコアランキング)。</summary>
    internal sealed class V6SelectScrollCatcher : MonoBehaviour, IScrollHandler
    {
        internal V6SelectView View;
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
