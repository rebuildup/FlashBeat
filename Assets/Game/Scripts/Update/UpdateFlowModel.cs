namespace FlashBeat.Update
{
    /// <summary>更新フローの状態。</summary>
    public enum UpdateFlowPhase
    {
        Idle,
        Checking,
        Available,
        Downloading,
        Ready,
        Applying,
    }

    /// <summary>
    /// 更新フローの状態機械 (纯粋・Unity API に依存しない)。
    /// IO (通信・staging・プロセス起動) は UpdateChecker 側が担い、このクラスは遷移と不変条件だけを持つ。
    /// 不変条件: <see cref="BeginApply"/> は Ready からのみ遷移し、適用は必ずユーザー確認を経由する。
    /// 自動適用の経路はこのクラスに存在しない (OnApplicationQuit から呼ぶ API を提供しない)。
    /// </summary>
    public class UpdateFlowModel
    {
        public UpdateFlowPhase Phase { get; private set; } = UpdateFlowPhase.Idle;

        /// <summary>新版のタグ (例: "v0.1.3-win64-abc1234")。Available 以降で有効。</summary>
        public string LatestTag { get; private set; }

        /// <summary>新版のバージョン表示用文字列 (例: "0.1.3")。</summary>
        public string LatestVersion { get; private set; }

        /// <summary>ダウンロード進捗 0..1。Downloading 中のみ意味を持つ。</summary>
        public float DownloadProgress { get; private set; }

        /// <summary>ユーザーに見せる一行メッセージ (失敗理由など)。空なら非表示。</summary>
        public string Message { get; private set; }

        /// <summary>F4 / × でこのセッション中は通知を隠す (staging・状態は保持)。</summary>
        public bool DismissedForSession { get; private set; }

        /// <summary>確認済みか (BeginApply を呼べるのはこれが true のときの実行環境側のみを想定)。</summary>
        public bool UserConfirmed { get; private set; }

        /// <summary>チェックで新版が見つかっている (Available / Downloading / Ready / Applying)。</summary>
        public bool HasAvailableUpdate
        {
            get
            {
                return Phase == UpdateFlowPhase.Available || Phase == UpdateFlowPhase.Downloading ||
                       Phase == UpdateFlowPhase.Ready || Phase == UpdateFlowPhase.Applying;
            }
        }

        /// <summary>起動時の確認を開始する。Idle / 失敗後の Idle からのみ。</summary>
        public void StartCheck()
        {
            if (Phase != UpdateFlowPhase.Idle) { return; }
            Phase = UpdateFlowPhase.Checking;
            Message = null;
        }

        /// <summary>現行より新しい版は無い (または確認に失敗)。静かに Idle へ戻す。</summary>
        public void CheckFinishedWithoutUpdate()
        {
            if (Phase != UpdateFlowPhase.Checking) { return; }
            Phase = UpdateFlowPhase.Idle;
        }

        /// <summary>新版を検出 (Checking → Available)。同一タグの有効な staging 済みがある場合、
        /// 呼び出し側は続けて <see cref="MarkStagedReady"/> で Ready に進めてよい。</summary>
        public void CheckFoundNewVersion(string tag, string version)
        {
            if (Phase != UpdateFlowPhase.Checking) { return; }
            Phase = UpdateFlowPhase.Available;
            LatestTag = tag;
            LatestVersion = version;
            Message = null;
        }

        /// <summary>同一タグの有効な staging 済みを再利用して Ready に進める (Available からのみ)。</summary>
        public void MarkStagedReady()
        {
            if (Phase != UpdateFlowPhase.Available) { return; }
            Phase = UpdateFlowPhase.Ready;
            DownloadProgress = 1f;
        }

        /// <summary>ユーザー操作によるダウンロード開始 (Available からのみ)。自動では呼ばれない。</summary>
        public void BeginDownload()
        {
            if (Phase != UpdateFlowPhase.Available) { return; }
            Phase = UpdateFlowPhase.Downloading;
            DownloadProgress = 0f;
            Message = null;
        }

        /// <summary>ダウンロード進捗 (0..1)。Downloading 中のみ受ける。</summary>
        public void SetDownloadProgress(float progress)
        {
            if (Phase != UpdateFlowPhase.Downloading) { return; }
            if (progress < 0f) { progress = 0f; }
            if (progress > 1f) { progress = 1f; }
            DownloadProgress = progress;
        }

        /// <summary>ダウンロード + SHA-256 検証に成功 (Downloading → Ready)。</summary>
        public void DownloadSucceeded()
        {
            if (Phase != UpdateFlowPhase.Downloading) { return; }
            Phase = UpdateFlowPhase.Ready;
            DownloadProgress = 1f;
            Message = null;
        }

        /// <summary>ダウンロード / 検証に失敗。Available に戻して再試行を可能にし、現行版を維持する。</summary>
        public void DownloadFailed(string problem)
        {
            if (Phase != UpdateFlowPhase.Downloading) { return; }
            Phase = UpdateFlowPhase.Available;
            DownloadProgress = 0f;
            Message = problem ?? "更新に失敗しました";
        }

        /// <summary>ユーザーが明示的に適用を確認 (Ready からのみ)。自動適用の経路は存在しない。</summary>
        public void BeginApply()
        {
            if (Phase != UpdateFlowPhase.Ready) { return; }
            UserConfirmed = true;
            Phase = UpdateFlowPhase.Applying;
        }

        /// <summary>適用 (スクリプト起動) に失敗。Ready に戻して現行版を維持する。</summary>
        public void ApplyFailed(string problem)
        {
            if (Phase != UpdateFlowPhase.Applying) { return; }
            UserConfirmed = false;
            Phase = UpdateFlowPhase.Ready;
            Message = problem ?? "適用に失敗しました";
        }

        /// <summary>F4 / × でこのセッションの通知を隠す。状態・staging は保持される。</summary>
        public void Dismiss()
        {
            DismissedForSession = true;
        }

        /// <summary>テスト用: セッションの dismiss を解く。</summary>
        public void ResetDismiss()
        {
            DismissedForSession = false;
        }
    }
}
