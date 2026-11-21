using System.Collections.Generic;
using FlashBeat.UI;
using FlashBeat.UI.V5;

namespace FlashBeat.UI.V6.Option
{
    /// <summary>
    /// Option 画面 Game Preview (Issue #607) の歌詞同期。
    ///
    /// 本編と同じ部品だけを使う (プレビュー専用の歌詞仕様は作らない):
    ///
    /// - 読み込みは <see cref="ChartLyrics.Resolve"/> (本編
    ///   GameSceneManager と同一経路。Issue #823 でローカル Resources
    ///   経路は撤去済み)。
    /// - 行への変換は <see cref="LyricsTimeline.Build"/>、時刻判定は
    ///   <see cref="LyricsTimeline.IndexAt"/> (本編 V6GameHudView /
    ///   GameSceneManager と同じ)。
    /// - 対象は Select で選択中の曲 (<see cref="GManager.songID"/> /
    ///   <see cref="GManager.ActiveSongs"/>)。読み取りのみで、
    ///   GManager / PlaySession / MusicManager の状態は変更しない。
    ///
    /// 疑似メディア時刻は <see cref="V6OptionPreviewChart.LoopSeconds"/>
    /// 秒周期なので、ループ内 (先頭 40 秒) にある歌詞行だけが繰り返し出る。
    /// 歌詞データが無い曲・譜面未取得の曲では空を返し、UI は何も出さない。
    /// </summary>
    public static class V6OptionPreviewLyrics
    {
        private static SongData loadedSong;
        private static List<LyricLine> lines = new List<LyricLine>();

        /// <summary>
        /// 選択中の曲の歌詞行 (メディアタイムライン上の表示区間)。
        /// 曲 (インスタンス) が替わったときだけ読み直す。Issue #823 で
        /// ローカル Resources 経路は撤去し、クラウド譜面の保持状態が
        /// 変わり得るため、タイトルではなく SongData の参照でキャッシュする
        /// (同名の別版・別曲へ歌詞が混ざらない)。
        /// 歌詞データが無い・曲が無い場合は空リスト。
        /// </summary>
        public static List<LyricLine> LinesForCurrentSong()
        {
            SongData song = CurrentSong();
            if (song == null)
            {
                if (loadedSong != null)
                {
                    loadedSong = null;
                    lines = new List<LyricLine>();
                }
                return lines;
            }

            if (song == loadedSong)
            {
                return lines;
            }

            loadedSong = song;
            ChartLyricsResult result = ChartLyrics.Resolve(song);
            TextData data = result != null ? result.Lyrics : null;
            lines = LyricsTimeline.Build(
                data != null ? data.StartTime : null,
                data != null ? data.EndTime : null,
                data != null ? data.Mondai : null,
                data != null ? data.Furigana : null);
            return lines;
        }

        /// <summary>
        /// 疑似メディア時刻 <paramref name="mediaTime"/> に表示する歌詞本文。
        /// 歌詞 OFF (<see cref="PlaySession.LyricsEnabled"/> = false)、
        /// 歌詞データ無し、表示区間の外では null を返す。
        /// 本編 (V6HudLyrics.UpdateLyrics) と同じく本文だけを使う
        /// (振り仮名・ローマ字は出さない)。
        /// </summary>
        public static string CurrentLine(float mediaTime)
        {
            if (!PlaySession.LyricsEnabled)
            {
                return null;
            }

            List<LyricLine> songLines = LinesForCurrentSong();
            int index = LyricsTimeline.IndexAt(songLines, mediaTime);
            if (index < 0 || index >= songLines.Count)
            {
                return null;
            }

            LyricLine line = songLines[index];
            return line.HasText ? line.Text : null;
        }

        /// <summary>選択中の曲 (無ければ null)。読み取りのみ。</summary>
        private static SongData CurrentSong()
        {
            SongData[] songs = GManager.ActiveSongs;
            int id = GManager.songID;
            if (songs == null || id < 0 || id >= songs.Length)
            {
                return null;
            }

            return songs[id];
        }
    }
}
