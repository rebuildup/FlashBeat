using FlashBeat.Update;
using UnityEngine;

namespace FlashBeat.UI
{
    /// <summary>Title 画面の『アップデートあり』通知に対する操作。</summary>
    public enum UpdateNoticeAction
    {
        /// <summary>何もしない (更新しない)。</summary>
        None,

        /// <summary>ダウンロードを開始する (ユーザー操作時のみ)。</summary>
        Download,

        /// <summary>適用を開始する (ユーザー操作時のみ)。</summary>
        Apply,
    }

    /// <summary>
    /// Title 画面 (ホーム) の『アップデートあり』通知の
    /// 「表示するか」「何と書くか」「押したら何をするか」を決める純粋ロジック (issue #118)。
    ///
    /// 純粋クラス (Unity のランタイム状態に依存しない) にしてあるので、EditMode テストで
    /// 「起動だけでは更新しない」「Title 以外では更新しない」を決定論的に検証できる。
    /// 実際の描画と入力は <c>UpdateNoticeUI</c> がこのクラスの結果に従う。
    /// </summary>
    public static class UpdateNoticePresenter
    {
        /// <summary>通知を出すシーン (タイトル画面 = ホーム)。</summary>
        public const string TitleSceneName = "TitleScene";

        /// <summary>
        /// 通知を表示してよいか。
        /// - TitleScene だけ (ゲームプレイ中は表示しない・中断しない)
        /// - このセッションで dismissed されていない
        /// - 更新候補がある / 適用中 / 前回の更新結果の通知がある のいずれか
        /// </summary>
        public static bool ShouldShow(string activeSceneName, UpdateFlowModel model)
        {
            if (model == null) { return false; }
            if (activeSceneName != TitleSceneName) { return false; }
            if (model.DismissedForSession) { return false; }
            if (model.HasAvailableUpdate) { return true; }
            if (model.Phase == UpdateFlowPhase.Applying) { return true; }
            return !string.IsNullOrEmpty(model.LastResultNotice);
        }

        /// <summary>
        /// 通知そのものの操作が何になるか。
        /// Available → ダウンロード、Ready → 適用。それ以外は「更新しない」。
        /// dismissed 済みのセッションでは通知が消えているので何もしない。
        /// **ユーザーが明示的に押した操作は <see cref="ResolveUserAction"/> を使う**
        /// (通知を閉じたことは「更新しない」宣言ではない)。
        /// </summary>
        public static UpdateNoticeAction ResolveAction(UpdateFlowModel model)
        {
            if (model == null) { return UpdateNoticeAction.None; }
            if (model.DismissedForSession) { return UpdateNoticeAction.None; }
            return ResolveUserAction(model);
        }

        /// <summary>
        /// ユーザーが明示的に押した操作 (クリック / キー) が何になるか。
        /// dismiss (F4) は「このセッションは通知を出さない」だけで、更新を
        /// 禁じるものではない。v6 タイトルの「更新して再起動」ボタンは通知とは
        /// 別の要素として常に表示されるため、dismiss 後も押せる必要がある
        /// (押しても何も起きないボタンを作らない — Issue #118 / #167)。
        /// </summary>
        public static UpdateNoticeAction ResolveUserAction(UpdateFlowModel model)
        {
            if (model == null) { return UpdateNoticeAction.None; }
            if (model.Phase == UpdateFlowPhase.Available) { return UpdateNoticeAction.Download; }
            if (model.Phase == UpdateFlowPhase.Ready) { return UpdateNoticeAction.Apply; }
            return UpdateNoticeAction.None;
        }

        /// <summary>通知の文言 (空文字 = 表示なし)。</summary>
        public static string BuildText(UpdateFlowModel model)
        {
            if (model == null) { return string.Empty; }

            string phaseText;
            switch (model.Phase)
            {
                case UpdateFlowPhase.Available:
                    phaseText = "アップデートあり v" + (model.LatestVersion ?? "?") +
                                "  [F3: 今すぐ更新]  (押さなければ更新しません)";
                    break;
                case UpdateFlowPhase.Downloading:
                    phaseText = "アップデート v" + (model.LatestVersion ?? "?") + " を準備中... " +
                                Mathf.RoundToInt(model.DownloadProgress * 100f) + "%";
                    break;
                case UpdateFlowPhase.Ready:
                    phaseText = "更新の準備ができました。終了して適用 [F3] (アプリを終了して再起動します)";
                    break;
                case UpdateFlowPhase.Applying:
                    phaseText = "適用中... 終了すると更新されます";
                    break;
                default:
                    phaseText = null;
                    break;
            }

            var resultNotice = model.LastResultNotice;
            string text;
            if (!string.IsNullOrEmpty(resultNotice) && !string.IsNullOrEmpty(phaseText))
            {
                text = resultNotice + "\n" + phaseText;
            }
            else
            {
                text = string.IsNullOrEmpty(resultNotice) ? phaseText : resultNotice;
            }
            if (text == null) { text = string.Empty; }
            if (!string.IsNullOrEmpty(model.Message))
            {
                text += "\n" + model.Message;
            }
            return text;
        }
    }
}
