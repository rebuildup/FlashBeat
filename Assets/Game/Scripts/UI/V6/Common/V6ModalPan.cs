using UnityEngine;

namespace FlashBeat.UI.V6.Common
{
    /// <summary>
    /// コンパクト画面でモーダルダイアログを開いたときのパン退避 (issue #748)。
    ///
    /// モーダルは 1728×972 フレームの子なので、コンパクト表示のパン次第で
    /// Dialog がビューポートの外・一部クリップになる。開くとき
    /// <see cref="V6FramePan.SuspendForModal"/> でダイアログ矩形を登録すると、
    /// パン入力が止まり Dialog 中心がビューポート中心へ来る。閉じるとき
    /// <see cref="V6FramePan.ResumeModal"/> で元のパン位置へ戻る。
    ///
    /// かつては Result の登録モーダルだけが独自に同じ処理を持っていた
    /// (Issue #609)。Option / Pause の確認ダイアログと KeyConfig モーダルも
    /// 同じ機構を使うため、フレーム → ビューポート → V6FramePan の
    /// 参照取得をここへ集約する。パン状態はビューポート側
    /// (<see cref="V6FramePan"/>) がスタックで持つので、1 画面に複数の
    /// モーダルが重なっても開閉順に正しく復帰する。
    ///
    /// 通常解像度 (ビューポート無し) では何もしない。
    /// </summary>
    public static class V6ModalPan
    {
        /// <summary>
        /// frame (1728×972 の V6 フレーム) 配下でモーダルを開くときに呼ぶ。
        /// dialogRect はフレームローカル座標のダイアログ矩形
        /// (左上原点の design px)。コンパクト画面でなければ何もしない。
        /// </summary>
        public static void Suspend(RectTransform frame, Rect dialogRect)
        {
            PanOf(frame)?.SuspendForModal(dialogRect);
        }

        /// <summary>
        /// <see cref="Suspend"/> と対になる。最後に開いたモーダルを閉じる
        /// ときに呼ぶ。コンパクトでなかった・ビューポートが既に破棄されて
        /// いる場合は何もしない。
        /// </summary>
        public static void Resume(RectTransform frame)
        {
            PanOf(frame)?.ResumeModal();
        }

        /// <summary>frame がコンパクトビューポート内にあり、かつパンが有効か。</summary>
        public static bool IsSuspended(RectTransform frame)
        {
            var pan = PanOf(frame);
            return pan != null && pan.ModalPanDepthForAudit > 0;
        }

        private static V6FramePan PanOf(RectTransform frame)
        {
            var viewport = V6CompactScreen.FindViewport(frame);
            return viewport != null ? viewport.GetComponent<V6FramePan>() : null;
        }
    }
}
