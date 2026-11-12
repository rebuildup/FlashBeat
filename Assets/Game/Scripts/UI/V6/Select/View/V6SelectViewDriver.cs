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

        private void Start()
        {
            // Issue #396: 起動時に 1 回だけ全走査で掃除する (登録を通らない
            // 旧オブジェクトが残っている場合の保険)。以後は登録済みの
            // Canvas 参照だけを見る。
            V6UiMode.HideLegacyV5CanvasesOnce();
        }

        private void Update()
        {
            // 旧 (v5) 側の Canvas / ルート ("V5 Select Canvas" など) を隠す。
            // Issue #396: 毎フレームのシーン全列挙ではなく、生成時に登録された
            // Canvas 参照だけを見る (配列確保も全走査もしない)。
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
