using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace FlashBeat.UI.V5
{
    /// <summary>
    /// Select 画面 (#121) の純ロジック。グレード判定・フィルタ/ソート・表示フォーマット・
    /// スクロール窓計算。UnityEngine への依存は最小 (Mathf のみ) で、EditMode テストから直接検証できる。
    /// データは GManager.ActiveSongs と GManager.Hiscore のみを入力とする。
    /// </summary>
    public static class SelectViewLogic
    {
        /// <summary>スコア満点 (デザインのスコアレール / グレード判定の基準)。</summary>
        public const int ScoreMax = 1000000;

        /// <summary>
        /// スコアからグレード文字列を返す (記録なしは null)。
        /// デザイン 02/07 の実測値 (987,410=S / 921,880=A / 842,300=B / 742,000=C / 1,000,000=SS) に整合。
        /// </summary>
        public static string GradeOf(int score)
        {
            if (score <= 0) return null;
            if (score >= ScoreMax) return "SS";
            if (score >= 950000) return "S";
            if (score >= 900000) return "A";
            if (score >= 800000) return "B";
            return "C";
        }

        public enum SortMode
        {
            Recent,
            Level,
            Score,
        }

        public enum LevelMode
        {
            All,
            Level1to5,
            Level6to10,
            Level11to15,
            Level16Plus,
        }

        public enum StatusMode
        {
            All,
            Played,
            Unplayed,
        }

        /// <summary>曲名/アーティストの部分一致 (大文字小文字を無視、空なら常に true)。</summary>
        public static bool MatchesFilter(SongData song, string filter)
        {
            if (song == null) return false;
            if (string.IsNullOrEmpty(filter)) return true;
            var title = song.title ?? string.Empty;
            var artist = song.musician ?? string.Empty;
            return title.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) >= 0
                || artist.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool MatchesLevel(SongData song, LevelMode mode)
        {
            if (song == null) return false;
            switch (mode)
            {
                case LevelMode.Level1to5: return song.level >= 1 && song.level <= 5;
                case LevelMode.Level6to10: return song.level >= 6 && song.level <= 10;
                case LevelMode.Level11to15: return song.level >= 11 && song.level <= 15;
                case LevelMode.Level16Plus: return song.level >= 16;
                default: return true;
            }
        }

        public static bool MatchesStatus(SongData song, StatusMode mode, int[] hiscores)
        {
            if (song == null) return false;
            if (mode == StatusMode.All) return true;
            bool played = hiscores != null && song.id >= 0 && song.id < hiscores.Length && hiscores[song.id] > 0;
            return mode == StatusMode.Played ? played : !played;
        }

        /// <summary>
        /// 表示対象の曲 id (1..Length-1) を絞り込んで並べ替える。
        /// ソート: Recent=id 昇順 / Level=難易度降順 / Score=ハイスコア降順 (同点は id 昇順)。
        /// </summary>
        public static List<int> FilterIds(IList<SongData> songs, string filter, SortMode sort,
            LevelMode level, StatusMode status, int[] hiscores)
        {
            var ids = new List<int>();
            if (songs == null) return ids;
            for (int i = 1; i < songs.Count; i++)
            {
                var song = songs[i];
                if (!MatchesFilter(song, filter)) continue;
                if (!MatchesLevel(song, level)) continue;
                if (!MatchesStatus(song, status, hiscores)) continue;
                ids.Add(i);
            }

            SortIds(ids, sort, songs, hiscores);
            return ids;
        }

        public static void SortIds(List<int> ids, SortMode sort, IList<SongData> songs, int[] hiscores)
        {
            if (ids == null || ids.Count < 2) return;
            switch (sort)
            {
                case SortMode.Level:
                    ids.Sort((a, b) =>
                    {
                        int diff = Score(songs, b).level.CompareTo(Score(songs, a).level);
                        return diff != 0 ? diff : a.CompareTo(b);
                    });
                    break;
                case SortMode.Score:
                    ids.Sort((a, b) =>
                    {
                        int sa = HiscoreOf(hiscores, a);
                        int sb = HiscoreOf(hiscores, b);
                        int diff = sb.CompareTo(sa);
                        return diff != 0 ? diff : a.CompareTo(b);
                    });
                    break;
                default:
                    ids.Sort((a, b) => a.CompareTo(b));
                    break;
            }
        }

        /// <summary>選択行が見えるスクロール開始行 (0 基準)。</summary>
        public static int ScrollOffset(int count, int selected, int visible)
        {
            if (count <= 0 || visible <= 0) return 0;
            if (count <= visible) return 0;
            int offset = Mathf.Clamp(selected - visible / 2, 0, count - visible);
            if (selected < offset) offset = selected;
            if (selected >= offset + visible) offset = selected - visible + 1;
            return Mathf.Clamp(offset, 0, count - visible);
        }

        /// <summary>スコア表示 ("952,310")。</summary>
        public static string ScoreText(int score)
        {
            return score.ToString("N0", CultureInfo.InvariantCulture);
        }

        /// <summary>曲長表示 (秒 → "m:ss")。</summary>
        public static string DurationText(float seconds)
        {
            if (seconds <= 0f) return null;
            int total = Mathf.RoundToInt(seconds);
            return (total / 60) + ":" + (total % 60).ToString("00", CultureInfo.InvariantCulture);
        }

        private static SongData Score(IList<SongData> songs, int id)
        {
            return songs[id] ?? new SongData(id, "", "", "", 0, 0, 0, 0f);
        }

        private static int HiscoreOf(int[] hiscores, int id)
        {
            return hiscores != null && id >= 0 && id < hiscores.Length ? hiscores[id] : 0;
        }
    }
}
