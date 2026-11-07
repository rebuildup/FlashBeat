using System.Collections.Generic;

namespace FlashBeat.UI
{
    /// <summary>
    /// 歌詞の1行。時刻はメディア(動画)のタイムライン上(秒)。
    /// </summary>
    public readonly struct LyricLine
    {
        /// <summary>この行を出し始める時刻 (メディアタイム)。</summary>
        public readonly float Start;

        /// <summary>この行を消す時刻 (メディアタイム)。</summary>
        public readonly float End;

        /// <summary>表示する本文 (空行なら Nothing)。</summary>
        public readonly string Text;

        /// <summary>ふりがな (未設定なら null)。</summary>
        public readonly string Furigana;

        public LyricLine(float start, float end, string text, string furigana)
        {
            Start = start;
            End = end;
            Text = text;
            Furigana = furigana;
        }

        /// <summary>
        /// 表示する文字があるか。全角スペースだけの行は「ここでは歌詞を消す」合図として
        /// 文字なし(=パネルを出さない)として扱う。
        /// </summary>
        public bool HasText
        {
            get { return !string.IsNullOrWhiteSpace(Text); }
        }
    }

    /// <summary>
    /// 譜面 (chart.lyrics) の歌詞を「メディアタイムライン上の表示区間」へ変換し、
    /// いま表示すべき行を判定する純ロジック (Issue #30 §2 歌詞同期)。
    ///
    /// 譜面の lyrics は旧形式の並列配列で、配列ごとに長さが違う:
    ///   - startTime / endTime / mondai / furigana / romaji
    ///   - 曲によっては endTime が無い、空配列である、開始より前の値になっている
    ///     (区間が1つずれている旧データ) ことがある。
    /// ここでは表示区間が必ず「開始 < 終了」になるよう補正し、判定は中央に閉じている。
    ///
    /// 時刻はすべて動画 (VideoPlayer.time) と同じタイムラインで、フレーム時間の
    /// 累積 (Time.deltaTime) には依存しない。ノーツと同じクロック
    /// (GManager.step_time) を使うので、シークやバッファリングでずれない。
    /// </summary>
    public static class LyricsTimeline
    {
        /// <summary>
        /// endTime が無い、または不正な行を何秒まで表示するかの上限。
        /// 次の行が始まる前に消すので、間奏中も古い歌詞が残りすぎない。
        /// </summary>
        public const float DefaultLineDuration = 4f;

        /// <summary>
        /// 譜面の歌詞配列を表示区間つきの行へ変換する。
        /// 長さの違う配列は短い方に合わせ、表示区間が壊れている行は捨てる。
        /// </summary>
        public static List<LyricLine> Build(float[] startTimes, float[] endTimes, string[] texts, string[] furiganas)
        {
            var lines = new List<LyricLine>();
            if (startTimes == null || texts == null) return lines;

            int count = startTimes.Length < texts.Length ? startTimes.Length : texts.Length;
            for (int i = 0; i < count; i++)
            {
                float start = startTimes[i];
                float end = ResolveEnd(i, start, startTimes, endTimes);
                if (end <= start) continue;

                string furigana = furiganas != null && i < furiganas.Length ? furiganas[i] : null;
                lines.Add(new LyricLine(start, end, texts[i], furigana));
            }
            return lines;
        }

        /// <summary>
        /// 指定時刻に表示されている行のインデックスを返す。どれも表示されていなければ -1。
        /// 区間が重なったときは、後に始まった行 (開始が最新) を採用する。
        /// </summary>
        public static int IndexAt(IList<LyricLine> lines, float mediaTime)
        {
            if (lines == null || lines.Count == 0) return -1;

            int found = -1;
            float latestStart = float.NegativeInfinity;
            for (int i = 0; i < lines.Count; i++)
            {
                LyricLine line = lines[i];
                if (mediaTime < line.Start || mediaTime >= line.End) continue;
                if (line.Start >= latestStart)
                {
                    latestStart = line.Start;
                    found = i;
                }
            }
            return found;
        }

        /// <summary>表示区間を持つ行があるか (歌詞データが有効かどうか)。</summary>
        public static bool HasLines(IList<LyricLine> lines)
        {
            return lines != null && lines.Count > 0;
        }

        /// <summary>
        /// その行を消す時刻を決める。
        /// 1. endTime が開始より後なら、それを使う。
        /// 2. endTime が無い、または開始以前なら、次の行の開始か既定時間まで表示する。
        /// </summary>
        static float ResolveEnd(int index, float start, float[] startTimes, float[] endTimes)
        {
            if (endTimes != null && index < endTimes.Length && endTimes[index] > start)
            {
                return endTimes[index];
            }

            float end = start + DefaultLineDuration;
            if (startTimes != null && index + 1 < startTimes.Length)
            {
                float nextStart = startTimes[index + 1];
                if (nextStart > start && nextStart < end) end = nextStart;
            }
            return end;
        }
    }
}
