using UnityEngine;

namespace FlashBeat.UI.V6.Option
{
    /// <summary>
    /// V6OptionView の毎フレーム処理 (キー入力 / 解像度変化) を回すドライバ。
    /// V6OptionView 自体は MonoBehaviour にしない (公開 API を
    /// brief の Create / Root / Refresh / SetVisible に限定するため)。
    /// </summary>
    internal sealed class V6OptionViewDriver : MonoBehaviour
    {
        internal V6OptionView View;

        private void Update()
        {
            if (View != null)
            {
                View.Tick();
            }
        }
    }
}
