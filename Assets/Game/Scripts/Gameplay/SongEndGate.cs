namespace FlashBeat.Gameplay
{
    /// <summary>
    /// 終端フェードと「終了」表示の間は判定キーを受け付けない (design §6.2、
    /// issue #383)。曲終了を確定した時点で閉じ、次のプレイの開始で開く。
    ///
    /// 判定を止めても総ノーツ数と判定数の合計は変えない: 残ったノーツは
    /// SongEndMachine の期限で確定する (#226 の回帰を壊さない)。
    /// </summary>
    public static class SongEndGate
    {
        /// <summary>曲終了が確定し、Result へ向かう途中か。</summary>
        public static bool BlocksJudging { get; private set; }

        /// <summary>曲終了を確定した (フェードと「終了」の表示を始める)。</summary>
        public static void Enter()
        {
            BlocksJudging = true;
        }

        /// <summary>次のプレイの開始で開く。</summary>
        public static void Reset()
        {
            BlocksJudging = false;
        }

        /// <summary>判定を 1 ステップ進めてよいか (純ロジック)。</summary>
        public static bool CanJudge(bool isPaused, bool endShown)
        {
            return !isPaused && !endShown;
        }
    }
}
