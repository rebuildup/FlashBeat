using System;
using System.Collections.Generic;

namespace FlashBeat.UI.V6.Select.Data
{
    /// <summary>
    /// v6 選曲画面の純ロジック (Issue #222)。検索フィルタとグレード判定。
    /// Unity エンジンに依存しないので EditMode テストから直接検証できる。
    /// グレードの閾値は scoringVersion で切り替える (Issue #758 / ADR-0034):
    /// v4 以降は <see cref="FlashBeat.UI.V6.Common.V6GradeLogic"/>、
    /// v1–v3 は V5 と共通 (FlashBeat.UI.V5.SelectViewLogic.GradeOf)。
    /// </summary>
    public static class V6SelectDataLogic
    {
        /// <summary>
        /// スコア満点の v4 参照値 (2,000,000。V6GradeLogic.ScoreMaxV4 と
        /// 同じ)。v6 の満点は譜面ごとの ScoreMaxV6(noteCount) で、
        /// この定数は v4 帯のテスト等の参照用。
        /// </summary>
        public const int ScoreMax = FlashBeat.UI.V6.Common.V6GradeLogic.ScoreMaxV4;

        /// <summary>
        /// 曲名/作家名の部分一致 (大文字小文字を無視、空クエリなら常に true)。
        /// 旧 V5 SelectViewLogic.MatchesFilter と同じ挙動。
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

        /// <summary>
        /// スコアからグレード文字列を返す (記録なしは空文字)。
        /// scoringVersion で帯を切り替える (Issue #758 / ADR-0034):
        /// v4/v5 以降は <see cref="FlashBeat.UI.V6.Common.V6GradeLogic"/>
        /// (v6 は譜面満点 ScoreMaxV6(noteCount) の割合帯、v4/v5 は満点
        /// 2,000,000 の帯)、v1–v3 は V5 SelectViewLogic.GradeOf の
        /// 閾値をそのまま使う (旧スコアを暗黙に再解釈しない)。
        /// 既定は最新の scoringVersion — ベストスコアもオンライン
        /// ランキング行も最新バージョンのスコープしか表示しないため。
        /// <paramref name="noteCount"/> は scoring v6 のグレードにだけ
        /// 使う譜面ノーツ数 (他バージョンでは無視される)。
        /// </summary>
        public static string GradeOf(int score, int scoringVersion = FlashBeat.Gameplay.Scoring.LatestVersion,
            int noteCount = 0)
        {
            if (score <= 0) return "";
            if (scoringVersion >= FlashBeat.Gameplay.Scoring.Version6)
            {
                return FlashBeat.UI.V6.Common.V6GradeLogic.GradeOf(score, scoringVersion, noteCount);
            }
            if (scoringVersion >= FlashBeat.Gameplay.Scoring.Version4)
            {
                return FlashBeat.UI.V6.Common.V6GradeLogic.GradeOfV4(score);
            }
            return SongScoreLogic.GradeOf(score) ?? "";
        }
    }
}
