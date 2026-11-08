using UnityEngine;

namespace FlashBeat.UI.V6.Result
{
    /// <summary>
    /// V6ResultView の毎フレーム処理 (キー入力 / 解像度変化) を回すドライバ。
    /// V6SelectViewDriver と同じ分離: View 自体は MonoBehaviour にしない
    /// (統合担当に公開する API を brief の Create/Root/Refresh/SetVisible に限定するため)。
    /// </summary>
    internal sealed class V6ResultViewDriver : MonoBehaviour
    {
        internal V6ResultView View;

        private void Update()
        {
            if (View != null)
            {
                View.Tick();
            }
        }
    }

    /// <summary>ランキング取得 (RankingClient のコルーチン) を動かすホスト。</summary>
    internal sealed class V6ResultCoroutineHost : MonoBehaviour { }
}
