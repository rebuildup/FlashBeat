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
            // v6 Result が有効な間、旧 (v5) Result の残骸 (シーン配置 Canvas / 3D の
            // サムネイル Panel など) を毎フレーム隠す (Title / Select と同じ考え方)。
            FlashBeat.UI.V6.V6UiMode.HideLegacyResultUi();

            if (View != null)
            {
                View.Tick();
            }
        }
    }

    /// <summary>ランキング取得 (RankingClient のコルーチン) を動かすホスト。</summary>
    internal sealed class V6ResultCoroutineHost : MonoBehaviour { }
}
