namespace FlashBeat.UI.V5
{
    /// <summary>
    /// Title 画面から未実装画面への静的フック (Issue #120 フィードバック対応)。
    /// 画面を持たない機能 (ランキング等) は、自分の初期化時にこのフックを設定する。
    /// フックが未設定の間は Title 側の該当ボタンを表示しない。
    /// </summary>
    public static class TitleLinks
    {
        /// <summary>
        /// Title の Ranking ボタンが押されたら呼ばれる。ランキング クライアント側が設定する。
        /// null の間は Ranking ボタンを非表示にする。
        /// </summary>
        public static System.Action RankingRequested;
    }
}
