using UnityEngine;

namespace FlashBeat.UI.V6.Title
{
    /// <summary>
    /// V6TitleView の毎フレーム処理 (解像度変化 / 更新状態の変化) を回すドライバ。
    /// V6SelectViewDriver / V6ResultViewDriver と同じ分離: View 自体は MonoBehaviour にしない
    /// (統合担当に公開する API を Create/Root/Refresh/SetVisible に限定するため)。
    /// キー入力はここでも読まない (Start/Option/Exit は既存 TitleSceneManager.Update が受け持つ)。
    /// </summary>
    internal sealed class V6TitleViewDriver : MonoBehaviour
    {
        internal V6TitleView View;

        private void Update()
        {
            if (View != null)
            {
                View.Tick();
            }
        }
    }
}
