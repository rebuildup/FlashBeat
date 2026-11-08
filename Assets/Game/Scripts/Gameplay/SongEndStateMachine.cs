namespace FlashBeat.Gameplay
{
    /// <summary>
    /// 曲の終了状態 (Issue #226)。
    /// </summary>
    public enum SongEndState
    {
        /// <summary>再生中。終端条件は未成立。</summary>
        Playing,
        /// <summary>メディア終端または曲長に達した。ノーツの確定待ち。</summary>
        Finishing,
        /// <summary>終端に達し、ノーツの確定も済んだ (期限切れの強制完了を含む)。
        /// 呼び出し側は ResultScene へ遷移してよい。</summary>
        Completed,
        /// <summary>メディアの破綻で曲を完走できない。Result へは遷移しない。</summary>
        Failed
    }

    /// <summary>曲を完走できなかった理由。</summary>
    public enum SongEndFailure
    {
        None,
        /// <summary>再生中に VideoPlayer からエラー通知を受けた。</summary>
        MediaError,
        /// <summary>メディアの時計が長時間停止した (デコードの破綻)。</summary>
        MediaStalled
    }

    /// <summary>SongEndMachine.Update に渡す 1 フレーム分の観測値。</summary>
    public struct SongEndTick
    {
        /// <summary>終端条件の成立 (メディア終端 または 曲長到達)。判定は呼び出し側。</summary>
        public bool EndReached;

        /// <summary>judge が見ている時刻 (Time.time - G.StartTime)。
        /// メディア終端で時計が固定された後も進み続ける。</summary>
        public float JudgeTime;

        /// <summary>譜面上で最後のノーツが確定する時刻 (Miss 窓 / HOLD 猶予込み)。</summary>
        public float SettleDeadline;

        /// <summary>未確定のノーツ数 (未判定 + 保持中)。</summary>
        public int PendingNotes;

        /// <summary>譜面の全ノーツ数。</summary>
        public int TotalNotes;
    }

    /// <summary>
    /// 曲終了の状態機械 (Issue #226)。
    ///
    /// 旧実装は MediaTimeline.HasSongEnded (カタログの duration だけ) で Result へ
    /// 遷移していた。そのため (a) 末尾ノーツの判定窓が流れる前に Result へ進んで
    /// 判定合計がノーツ数に届かず、(b) 動画がメタデータより先に終わると時計が止まり
    /// Result に進めない、という問題があった。
    ///
    /// 遷移ルール:
    /// - Playing → Finishing: 終端条件の成立 (メディア終端 または 曲長)。
    ///   どちらを信頼するか: 実際のメディアが正 (譜面はそれに合わせて作られている)。
    ///   メディアが先に終わればそこが終端、duration が先なら duration が終端。
    ///   duration が不正 (0 以下 / NaN / Infinity) の場合は duration を終了条件に
    ///   使わない (MediaTimeline.HasSongEnded / FallbackSongEnd が保証する)。
    /// - Finishing → Completed: 全ノーツの確定 (pending 0)。メディアが終端で止まって
    ///   も judge の時計は進み続ける (MusicManager が StartTime の更新を止めて固定)。
    ///   それにより末尾ノーツは既定の Miss 窓で確定し、保持中の HOLD は
    ///   中断 / 期限切れとして確定する。ここでは判定の内訳は見ず、件数だけ見る。
    /// - Finishing → Completed (強制): judge 時刻が SettleDeadline + 猶予を過ぎても
    ///   pending が残っている場合 (判定会計の揺れ、無効レーンで除外されたノーツなど)。
    ///   未確定ノーツを残したまま完了する — 成功偽装ではなく、合計が合わない結果は
    ///   Result / ランキング側の合計チェックで拒否される。呼び出し側は
    ///   ForcedByDeadline をログに出す。
    /// - Fail(): メディアの破綻 (再生中のエラー通知、長時間のストール)。
    ///   Completed にはならず、既存のエラー導線 (GameErrorView / Esc / Retry) へ流す。
    ///
    /// Input / Time / MonoBehaviour に依存しないため、EditMode テストから
    /// 時刻を明示して直接検証できる。
    /// </summary>
    public sealed class SongEndMachine
    {
        /// <summary>
        /// SettleDeadline を過ぎてから強制完了するまでの猶予。
        /// 末尾ノーツの Miss 窓 (0.2s) と HOLD の猶予 (0.15s) は SettleDeadline 側に
        /// 含まれる。この猶予は判定会計の揺れとフレーム落ちの吸収分。
        /// </summary>
        public const float SettleGraceSeconds = 2f;

        public SongEndState State { get; private set; }

        /// <summary>Fail() された理由。Completed のままでは None のまま。</summary>
        public SongEndFailure Failure { get; private set; }

        /// <summary>未確定ノーツを残したまま期限切れで完了した (呼び出し側がログに出す)。</summary>
        public bool ForcedByDeadline { get; private set; }

        public SongEndMachine()
        {
            State = SongEndState.Playing;
            Failure = SongEndFailure.None;
        }

        /// <summary>
        /// 1 フレーム分状態を進める。
        /// </summary>
        /// <returns>true: Completed に確定した。呼び出し側は Result へ遷移する。</returns>
        public bool Update(in SongEndTick tick)
        {
            if (State == SongEndState.Completed || State == SongEndState.Failed)
            {
                return false;
            }

            if (State == SongEndState.Playing)
            {
                if (!tick.EndReached)
                {
                    return false;
                }
                State = SongEndState.Finishing;
            }

            // Finishing: ノーツの確定を待つ。メディアがまだ再生中 (曲長による終端)
            // なら judge は通常どおり判定を続け、ここで待つ間に末尾ノーツが確定する。
            if (AllSettled(tick))
            {
                State = SongEndState.Completed;
                return true;
            }

            if (tick.JudgeTime >= tick.SettleDeadline + SettleGraceSeconds)
            {
                ForcedByDeadline = true;
                State = SongEndState.Completed;
                return true;
            }

            return false;
        }

        /// <summary>
        /// メディアの破綻。Completed にはならない (すでに Completed なら無視する —
        /// 遷移は確定済みで取り消せない)。
        /// </summary>
        public void Fail(SongEndFailure reason)
        {
            if (reason == SongEndFailure.None ||
                State == SongEndState.Completed ||
                State == SongEndState.Failed)
            {
                return;
            }

            Failure = reason;
            State = SongEndState.Failed;
        }

        static bool AllSettled(in SongEndTick tick)
        {
            return tick.TotalNotes <= 0 || tick.PendingNotes <= 0;
        }
    }
}
