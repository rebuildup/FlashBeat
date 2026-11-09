using System;

namespace FlashBeat.UI.V6.Title
{
    /// <summary>
    /// Title 画面から別画面へ飛ぶための静的フック (Issue #239)。
    ///
    /// v5 の <see cref="FlashBeat.UI.V5.TitleLinks"/> と同じ考え方:  Title 側で
    /// 遷移先を持たない機能 (履歴 / ランキング) は、自分の初期化時にこのフックを
    /// 設定する。フックが未設定の間は Title 側の該当タブを描かない
    /// (押しても何も起きないボタンを作らない)。
    ///
    /// 設定は Title シーンより前に行うこと (TitleSceneManager が起動時に見て、
    /// 設定が無いタブは描かない)。
    /// </summary>
    public static class V6TitleLinks
    {
        /// <summary>
        /// Title の History タブが押されたら呼ばれる (履歴画面を開く)。
        /// null の間は History タブを描かない。
        /// </summary>
        public static Action HistoryRequested;

        /// <summary>
        /// Title の Ranking タブ (または R キー) が押されたら呼ばれる。
        /// null の間は Ranking タブを描かない。
        /// </summary>
        public static Action RankingRequested;

        /// <summary>Title タブ / H・R キーで開いてほしい画面 (Issue #239)。</summary>
        public enum EntryScreen
        {
            None,
            History,
            Ranking
        }

        /// <summary>
        /// Title から要求された遷移先。TitleSceneManager が History / Ranking を
        /// 要求するとき設定して SelectScene へ遷移し、SelectSceneManager が
        /// Start で消費して初期モードを決める。消費したら None に戻す。
        /// </summary>
        public static EntryScreen PendingEntry = EntryScreen.None;
    }
}
