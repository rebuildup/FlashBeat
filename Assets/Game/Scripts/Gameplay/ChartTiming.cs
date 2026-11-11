using UnityEngine;

namespace FlashBeat.Gameplay
{
    /// <summary>
    /// 譜面の時間設定 (Issue #383)。譜面 JSON の任意フィールド
    /// <c>fadeOut</c> / <c>startTiming</c> / <c>beatOffset</c> を実行時に正規化したもの。
    ///
    /// 既存譜面は 3 フィールドを持たない (JsonUtility は 0 を入れる)。0 は
    /// 「未指定」として既定値へ読み替えるため、既存譜面は従来どおり動く。
    ///
    /// <c>offset</c> (NoteEditor が書き出すサンプル単位の音声アラインメント) は
    /// 実行時の判定には使わない。拍補正は <see cref="BeatOffset"/> に一本化する。
    /// </summary>
    public sealed class ChartTiming
    {
        /// <summary>fadeOut 未指定時の既定値 (design 決定 2026-11-11)。</summary>
        public const float DefaultFadeOutSeconds = 2f;
        public const float MaxFadeOutSeconds = 10f;
        public const float MaxStartTimingSeconds = 10f;
        public const float MaxBeatOffsetSeconds = 2f;

        /// <summary>メディア終端の何秒前からフェードを始めるか。</summary>
        public float FadeOut { get; private set; }

        /// <summary>開始入力後、音声と譜面を始めるまでの待ち時間。</summary>
        public float StartTiming { get; private set; }

        /// <summary>BPM グリッドと音源のずれ補正。全ノート時刻に加算する。</summary>
        public float BeatOffset { get; private set; }

        /// <summary>曲長 (秒)。不明なら 0。</summary>
        public float MediaDuration { get; private set; }

        private ChartTiming()
        {
        }

        /// <summary>既定値 (フィールド未指定の譜面)。</summary>
        public static ChartTiming Default
        {
            get { return Normalize(0f, 0f, 0f, 0f); }
        }

        /// <summary>
        /// 生の値をクランプして返す。
        /// - fadeOut: 0 以下は既定 2.0。0〜10 秒、かつ曲長が分かるときは曲長の 25% 以下。
        /// - startTiming: 0〜10 秒。
        /// - beatOffset: −2〜2 秒。
        /// </summary>
        public static ChartTiming Normalize(float fadeOut, float startTiming, float beatOffset, float mediaDuration)
        {
            var timing = new ChartTiming();
            timing.MediaDuration = MediaTimeline.IsUsableDuration(mediaDuration) ? mediaDuration : 0f;

            float fade = float.IsNaN(fadeOut) || float.IsInfinity(fadeOut) ? 0f : fadeOut;
            if (fade <= 0f)
            {
                fade = DefaultFadeOutSeconds;
            }

            fade = Mathf.Clamp(fade, 0f, MaxFadeOutSeconds);
            if (timing.MediaDuration > 0f)
            {
                fade = Mathf.Min(fade, timing.MediaDuration * 0.25f);
            }

            timing.FadeOut = fade;
            timing.StartTiming = Mathf.Clamp(Sanitize(startTiming), 0f, MaxStartTimingSeconds);
            timing.BeatOffset = Mathf.Clamp(Sanitize(beatOffset), -MaxBeatOffsetSeconds, MaxBeatOffsetSeconds);
            return timing;
        }

        /// <summary>この曲のフェード開始位置 (メディア秒)。曲長不明なら 0。</summary>
        public float FadeStartTime()
        {
            if (MediaDuration <= 0f)
            {
                return 0f;
            }

            return Mathf.Max(0f, MediaDuration - FadeOut);
        }

        /// <summary>フェードの進行度 (0 = 開始前、1 = 終端)。</summary>
        public float FadeProgress(float mediaTime)
        {
            if (MediaDuration <= 0f)
            {
                return 0f;
            }

            if (FadeOut <= 0f)
            {
                return mediaTime >= MediaDuration ? 1f : 0f;
            }

            return Mathf.Clamp01((mediaTime - FadeStartTime()) / FadeOut);
        }

        private static float Sanitize(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return 0f;
            }

            return value;
        }
    }
}
