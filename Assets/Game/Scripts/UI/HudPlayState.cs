namespace FlashBeat.UI
{
    /// <summary>GameScene の HUD に表示する再生パイプラインの状態。</summary>
    public enum HudPlayPhase
    {
        /// <summary>表示なし(準備完了で Space 待ち、またはプレイ中)。</summary>
        Hidden,
        /// <summary>動画・音声を読み込み中(Space はまだ効かない)。</summary>
        Preparing,
        /// <summary>読み込み/再生に失敗。選曲画面へ戻る導線を出す。</summary>
        Failed
    }

    /// <summary>
    /// GameScene の状態(HUD 表示用)を判定する純ロジック。
    /// 入力は MusicManager (Gameplay) を変更せずに観測できる値だけ:
    /// VideoPlayer のエラー、GManager.played、ロード用インジケータの Active 状態。
    /// 4 引数の Evaluate は、パイプラインが「この曲は再生できない」と判定した
    /// こと (MusicManager.SelectReturnPending) も観測して 5 引数版に渡す。
    /// </summary>
    public static class HudPlayState
    {
        /// <summary>
        /// ロードがこの秒数を超えたら失敗扱いにする。
        /// MusicManager が Space の開始を許可するフォールバック (30秒) より
        /// 長くして、遅い回線での誤検出を避ける。
        /// </summary>
        public const float FailureTimeoutSeconds = 45f;

        public static HudPlayPhase Evaluate(bool videoError, bool playbackStarted, bool mediaLoading, float loadingSeconds)
        {
            return Evaluate(videoError, playbackStarted, mediaLoading, loadingSeconds, MusicManager.SelectReturnPending);
        }

        /// <summary>
        /// 純粋な判定 (テスト可能な入力だけを取る)。
        /// pipelineFailed は「パイプラインが曲を再生不能と判定した」こと:
        /// MusicManager が選曲画面への自動復帰を予定している状態なので、
        /// ロード中でもすぐに失敗として表示する (タイムアウトを待たない)。
        /// ただし Space で開始済みなら曲は壁時計で完走するので非表示のまま。
        /// </summary>
        public static HudPlayPhase Evaluate(bool videoError, bool playbackStarted, bool mediaLoading, float loadingSeconds, bool pipelineFailed)
        {
            if (videoError)
            {
                return HudPlayPhase.Failed;
            }
            if (playbackStarted)
            {
                return HudPlayPhase.Hidden;
            }
            if (pipelineFailed)
            {
                return HudPlayPhase.Failed;
            }
            if (mediaLoading)
            {
                return loadingSeconds >= FailureTimeoutSeconds ? HudPlayPhase.Failed : HudPlayPhase.Preparing;
            }
            return HudPlayPhase.Hidden;
        }

        public static string Message(HudPlayPhase phase)
        {
            switch (phase)
            {
                case HudPlayPhase.Preparing:
                    return "準備中…\n動画と音声を読み込んでいます";
                case HudPlayPhase.Failed:
                    return "読み込み / 再生に失敗しました\nEsc キーか下のボタンで選曲画面へ戻れます";
                default:
                    return "";
            }
        }

        /// <summary>「選曲にもどる」の導線(Esc + ボタン)を出すかどうか。</summary>
        public static bool ShowBackEscape(HudPlayPhase phase)
        {
            return phase == HudPlayPhase.Preparing || phase == HudPlayPhase.Failed;
        }
    }
}
