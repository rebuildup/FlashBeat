using System;
using System.Collections.Generic;

namespace FlashBeat.UI.V6.Select.Data
{
    /// <summary>
    /// v6 選曲画面の純ロジック (Issue #222)。検索フィルタとグレード判定。
    /// Unity エンジンに依存しないので EditMode テストから直接検証できる。
    /// グレードの閾値は V5 と共通 (FlashBeat.UI.V5.SelectViewLogic.GradeOf)。
    /// </summary>
    public static class V6SelectDataLogic
    {
        /// <summary>スコア満点 (V5 の SelectViewLogic.ScoreMax と同じ基準)。</summary>
        public const int ScoreMax = FlashBeat.UI.V5.SelectViewLogic.ScoreMax;

        /// <summary>
        /// 曲名/作家名の部分一致 (大文字小文字を無視、空クエリなら常に true)。
        /// V5 の SelectViewLogic.MatchesFilter と同じ挙動。
        /// </summary>
        public static bool MatchesQuery(string title, string artist, string query)
        {
            if (string.IsNullOrEmpty(query)) return true;
            return (title ?? string.Empty).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                || (artist ?? string.Empty).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>クエリで絞り込んだ曲リスト (カタログ順を保つ)。</summary>
        public static List<V6SongEntry> Filter(IReadOnlyList<V6SongEntry> songs, string query)
        {
            var result = new List<V6SongEntry>();
            if (songs == null) return result;
            for (int i = 0; i < songs.Count; i++)
            {
                V6SongEntry song = songs[i];
                if (song != null && MatchesQuery(song.Title, song.Artist, query)) result.Add(song);
            }
            return result;
        }

        /// <summary>スコアからグレード文字列を返す (記録なしは空文字)。V5 と同じ閾値。</summary>
        public static string GradeOf(int score)
        {
            if (score <= 0) return "";
            return FlashBeat.UI.V5.SelectViewLogic.GradeOf(score) ?? "";
        }
    }
}
