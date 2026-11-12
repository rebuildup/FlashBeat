namespace FlashBeat.Gameplay
{
    /// <summary>
    /// 譜面の <c>startTiming</c> 待ちを「始めるか / もう待たないか」の判定
    /// (Issue #383、PR #387 レビュー指摘)。
    ///
    /// <c>startTiming</c> は開始入力から音声・譜面を始めるまでの待ち時間で、
    /// 待っている間は判定とノーツ進行を止める。実装は「待ちコルーチンが
    /// 走っている (pending)」と「待ちが済んだ (satisfied)」を別に持つ必要がある。
    ///
    /// 1 つのフラグで兼ねると、待ち終わりに <see cref="MusicManager"/> が
    /// 開始処理を呼んだとき、そのフラグを戻した直後の同じ条件が再び成立し、
    /// 待ちコルーチンが永久に再起動して再生が始まらない (startTiming を持つ
    /// 譜面が開始できなくなる)。判定をここへ出して EditMode から固定する。
    /// </summary>
    public static class StartTimingGate
    {
        /// <summary>
        /// いま <c>startTiming</c> の待ちを始めるべきか。
        /// 待ち時間が 0 以下 (未指定) なら待たない。待ちが走っている間も、
        /// 一度済んだ後も待たない。
        /// </summary>
        public static bool ShouldWait(float startTiming, bool pending, bool satisfied)
        {
            if (float.IsNaN(startTiming) || float.IsInfinity(startTiming))
            {
                return false;
            }

            return startTiming > 0f && !pending && !satisfied;
        }
    }
}
