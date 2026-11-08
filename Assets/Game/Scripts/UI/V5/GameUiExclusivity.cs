namespace FlashBeat.UI.V5
{
    /// <summary>
    /// GameScene の UI フェーズの単一正本 (Issue #233)。
    ///
    /// 「いま表示してよいのはどの Canvas / View か」を 1 箇所で決める。
    /// 判定は純粋関数 (Unity API に依存しない) なので、EditMode テストから
    /// 直接呼べるし、毎フレーム何度でも再適用できる。
    ///
    /// 従来の問題 (Issue #233): 旧 UI の非表示が Start 時の 1 回の名前ベース
    /// SetActive(false) だけだったため、後から生成/再有効化された Canvas が
    /// 残り、Ready と HUD が同時に表示されていた。
    /// このクラスは「状態 → 表示/非表示」を毎フレーム再適用する方式に置き換える。
    /// </summary>
    public enum GameUiPhase
    {
        /// <summary>動画・音声を読み込み中 (Space はまだ効かない)。</summary>
        Loading,
        /// <summary>再生前。Space 待ち。Ready 専用表示。</summary>
        Ready,
        /// <summary>演奏中。HUD 表示。</summary>
        Playing,
        /// <summary>ポーズ中。HUD の上にポーズを重ねる。</summary>
        Paused,
        /// <summary>読み込み / 再生に失敗。エラー表示 (選曲への導線)。</summary>
        Failed,
    }

    /// <summary>各 v5 ビューの表示 / 非表示の決定。</summary>
    public struct GameUiVisibility
    {
        public bool Ready;
        public bool Hud;
        public bool Loading;
        public bool Error;

        /// <summary>
        /// ポーズメニューが **開いてよいか** (許可フラグ)。
        /// 開いた状態そのものは PauseMenuView が所有するため、
        /// 排他制御は「許可しないフェーズでは閉じる」ことだけを行う。
        /// </summary>
        public bool Pause;
    }

    /// <summary>
    /// v6 側のモードフラグ (<see cref="FlashBeat.UI.V6.V6UiMode"/> と
    /// <c>V6PauseView.Enabled</c> の該当値を呼び出し側が渡す)。
    /// 純粋関数から v6 の静的状態を直接読まないため、テストで両モードを再現できる。
    /// </summary>
    public struct V6UiFlags
    {
        /// <summary>v6 のゲーム HUD が表示を所有する (V6UiMode.GameHud)。</summary>
        public bool GameHud;
        /// <summary>v6 のポーズが有効 (V6PauseView.Enabled)。無効のときは v5 ポーズが役割を持つ。</summary>
        public bool PauseEnabled;
    }

    public static class GameUiExclusivity
    {
        /// <summary>
        /// フェーズ判定 (純粋)。入力は既存の観測値だけ:
        /// <see cref="HudPlayState.Evaluate"/> の結果 (再生パイプライン)、
        /// 再生開始 (GManager.played)、ポーズの開閉 (PauseMenuView.IsOpen)。
        /// </summary>
        public static GameUiPhase ResolvePhase(HudPlayPhase play, bool playbackStarted, bool pauseOpen)
        {
            switch (play)
            {
                case HudPlayPhase.Failed:
                    return GameUiPhase.Failed;
                case HudPlayPhase.Preparing:
                    return GameUiPhase.Loading;
                default:
                    if (!playbackStarted) return GameUiPhase.Ready;
                    return pauseOpen ? GameUiPhase.Paused : GameUiPhase.Playing;
            }
        }

        /// <summary>
        /// 表示の排他決定 (純粋)。毎フレーム呼んで状態から再適用してよい。
        ///
        /// - v6 無効 (v5 モード): フェーズごとに 1 枚の主表示。ポーズは HUD / Ready の
        ///   上に重ねる (別 Canvas のため両方可視が正)。
        /// - v6 有効: v6 が表示を所有するので v5 の描画要素はすべて隠す。
        ///   例外は v6 ポーズ無効時の v5 ポーズだけ (未移植分の役割維持)。
        /// </summary>
        public static GameUiVisibility Resolve(GameUiPhase phase, V6UiFlags v6, bool pauseOpen)
        {
            var vis = new GameUiVisibility();

            if (v6.GameHud)
            {
                // v6 モード: Ready / HUD / Loading / Error は v6 側 (V6GameRuntime) が持つ。
                // v5 ポーズは v6 ポーズが無効なときだけ役割を持つ (開いた状態は
                // PauseMenuView 自身が保持する。排他側は閉じることだけ担う)。
                vis.Pause = !v6.PauseEnabled
                    && phase != GameUiPhase.Failed && phase != GameUiPhase.Loading;
                return vis;
            }

            switch (phase)
            {
                case GameUiPhase.Loading:
                    vis.Loading = true;
                    break;
                case GameUiPhase.Failed:
                    vis.Error = true;
                    break;
                case GameUiPhase.Ready:
                    vis.Ready = true;
                    vis.Pause = true; // Ready 中も Esc でメニュー (設定・表示・ランキング) を開ける
                    break;
                case GameUiPhase.Paused:
                    vis.Hud = true;
                    vis.Pause = true;
                    break;
                default: // Playing
                    vis.Hud = true;
                    vis.Pause = true;
                    break;
            }
            return vis;
        }

        /// <summary>
        /// 決定を実際のビューへ適用する。冪等 (同じ vis を何度適用しても同じ状態)。
        /// ビューは未生成のとき null を許す (v6 モードでは GameHudView を作らない等)。
        /// </summary>
        public static void Apply(in GameUiVisibility vis, GameReadyView ready, GameHudView hud,
            GameLoadingView loading, GameErrorView error, PauseMenuView pause)
        {
            if (ready != null) ready.SetVisible(vis.Ready);
            if (hud != null) hud.SetVisible(vis.Hud);
            if (loading != null) loading.SetVisible(vis.Loading);
            if (error != null)
            {
                if (vis.Error) error.Show();
                else error.Hide();
            }
            if (pause != null)
            {
                // ポーズメニューの **開閉は PauseMenuView 自身** が所有する
                // (Esc トグル / HUD のポーズボタン)。ここで Open() を呼ぶと、
                // 同一フレームでボタン押下→再適用 の順序で即座に閉じてしまう。
                // 排他制御が担うのは「開いてはいけないフェーズでは閉じる」ことだけ。
                if (!vis.Pause) pause.Close();
            }
        }
    }
}
