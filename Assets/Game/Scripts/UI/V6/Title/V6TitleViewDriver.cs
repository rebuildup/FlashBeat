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

        // 旧更新通知バナー (UpdateNoticeCanvas) は UpdateNoticeUI が最初の Update で
        // 生成するので、Start 時点ではまだ無い。見つかって隠せるまで毎フレーム試す。
        private bool updateNoticeHidden;

        private void Update()
        {
            // 旧 (v5) TitleView は Start/OnEnable で自前のメニュー ("Menu GameStart" など) を
            // 生成する。V6TitleView.Create 時の 1 回だけでは後から生成された分が残るので、
            // 毎フレーム隠し直す (戻り値での早期終了はしない)。
            V6UiMode.HideLegacyTitleUi();

            if (!updateNoticeHidden)
            {
                updateNoticeHidden = V6UiMode.HideLegacyUpdateNotice();
            }

            if (View != null)
            {
                View.Tick();
            }
        }
    }
}
