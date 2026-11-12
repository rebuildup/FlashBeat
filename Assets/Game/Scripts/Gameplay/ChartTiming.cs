using System.Globalization;
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

        /// <summary>
        /// 最後のノートの後に「終了」を見せてから Result へ移るまでの秒数
        /// (design 決定 2026-11-11 §6.2)。
        /// </summary>
        public const float ResultDelaySeconds = 0.8f;

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
        /// 生の値を正規化して返す。
        /// - fadeOut: 0 以下は既定 2.0。0〜10 秒、かつ曲長が分かるときは曲長の 25% 以下。
        /// - startTiming: 0〜10 秒。範囲外は既定値 (0) へ戻す。
        /// - beatOffset: −2〜2 秒。範囲外は既定値 (0) へ戻す。
        ///
        /// 範囲外を黙ってクランプすると、譜面作者は意図した値と実際の値のずれに
        /// 気づけない (design 決定 2026-11-11 §6.1 「既定値へ戻して警告を出す」)。
        /// 警告文は out 引数で返し、呼び出し側 (NotesManager) がログに出す —
        /// この型は UnityEngine のログに触らない (テストから純粋に呼べる)。
        /// </summary>
        public static ChartTiming Normalize(float fadeOut, float startTiming, float beatOffset, float mediaDuration)
        {
            string warning;
            return Normalize(fadeOut, startTiming, beatOffset, mediaDuration, out warning);
        }

        /// <summary>
        /// 範囲外の値を既定値へ戻したとき、その旨を <paramref name="warning"/> に入れる
        /// (対象が無ければ null)。戻り値の意味はもう一方の <see cref="Normalize"/> と同じ。
        /// </summary>
        public static ChartTiming Normalize(float fadeOut, float startTiming, float beatOffset, float mediaDuration, out string warning)
        {
            var timing = new ChartTiming();
            timing.MediaDuration = MediaTimeline.IsUsableDuration(mediaDuration) ? mediaDuration : 0f;
            warning = null;

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

            string startWarning;
            timing.StartTiming = InRangeOrZero(startTiming, 0f, MaxStartTimingSeconds, "startTiming", out startWarning);

            string beatWarning;
            timing.BeatOffset = InRangeOrZero(beatOffset, -MaxBeatOffsetSeconds, MaxBeatOffsetSeconds, "beatOffset", out beatWarning);

            warning = JoinWarnings(startWarning, beatWarning);
            return timing;
        }

        /// <summary>範囲内ならその値、範囲外 (NaN / 無限も含む) なら 0 へ戻して警告文を返す。</summary>
        private static float InRangeOrZero(float value, float min, float max, string field, out string warning)
        {
            warning = null;
            if (float.IsNaN(value) || float.IsInfinity(value) || value < min || value > max)
            {
                warning = field + "=" + value.ToString(CultureInfo.InvariantCulture)
                    + " is outside " + min.ToString(CultureInfo.InvariantCulture)
                    + ".." + max.ToString(CultureInfo.InvariantCulture)
                    + " - using the default 0";
                return 0f;
            }

            return value;
        }

        private static string JoinWarnings(string first, string second)
        {
            if (string.IsNullOrEmpty(first)) return second;
            if (string.IsNullOrEmpty(second)) return first;
            return first + "; " + second;
        }

        /// <summary>
        /// メディア長が取得できない譜面の代わりの曲長 (design 決定 2026-11-11 §6.2):
        /// 最終ノート時刻 + <see cref="FadeOut"/> + <see cref="ResultDelaySeconds"/>。
        ///
        /// 曲長が分かっている譜面はそのまま返す。分からないままにすると
        /// <see cref="FadeProgress"/> が常に 0 になり、フェードと「終了」の
        /// 位置を決められない。
        /// </summary>
        /// <param name="lastNoteTime">譜面の最終ノート時刻 (HOLD はその終端)。</param>
        public ChartTiming WithFallbackEnd(float lastNoteTime)
        {
            if (MediaDuration > 0f)
            {
                return this;
            }

            var copy = new ChartTiming();
            copy.MediaDuration = Mathf.Max(0f, lastNoteTime) + FadeOut + ResultDelaySeconds;
            copy.FadeOut = FadeOut;
            copy.StartTiming = StartTiming;
            copy.BeatOffset = BeatOffset;
            return copy;
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
    }
}
