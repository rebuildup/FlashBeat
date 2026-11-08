namespace FlashBeat.UI.V6.Game.Loading
{
    /// <summary>
    /// v6 曲ローディング画面 (Issue #224, design `g4Q3F`) の純ロジック (EditMode テスト対象)。
    /// design に進捗表現が無い (メイン空) ので、不定インジケータの位置計算だけをここに置く。
    /// </summary>
    public static class V6LoadingViewLogic
    {
        /// <summary>不定インジケータの往復周期 (秒)。v5 GameLoadingView と同じ。</summary>
        public const float CycleSeconds = 1.6f;

        /// <summary>
        /// 不定インジケータの塗りの x (0..trackW-fillW)。往復 (0 → 端 → 0)。
        /// </summary>
        public static float IndeterminateX(float time, float trackW, float fillW)
        {
            if (trackW <= 0f || fillW <= 0f)
            {
                return 0f;
            }

            float range = trackW - fillW;
            if (range <= 0f)
            {
                return 0f;
            }

            float phase = (time % CycleSeconds) / CycleSeconds;
            float tri = phase < 0.5f ? phase * 2f : 2f - phase * 2f;
            return tri * range;
        }

        /// <summary>ロード中の見出し。</summary>
        public static string LoadingHeadline()
        {
            return "準備中…";
        }

        /// <summary>ロード中の補足。</summary>
        public static string LoadingDetail()
        {
            return "動画と音声を読み込んでいます";
        }
    }
}
