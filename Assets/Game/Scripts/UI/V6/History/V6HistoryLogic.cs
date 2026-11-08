using System;
using System.Collections.Generic;
using FlashBeat.UI.V6.Select;
using FlashBeat.UI.V6.Select.Data;

namespace FlashBeat.UI.V6.History
{
    /// <summary>
    /// v6 履歴画面 (03 History · v6 / Pencil ノード q9JZcH) の純ロジック。
    /// Unity エンジンに依存せず EditMode テストから直接検証できる。
    /// データは v6 選曲と同じ V6SongEntry (実カタログ + 実保存スコア) を使い、
    /// デザイン例の固定値は一切扱わない。
    /// </summary>
    public static class V6HistoryLogic
    {
        /// <summary>履歴リストの並び順。design ヘッダーの Sort チップ ("スコア順") に対応。</summary>
        public enum HistorySort
        {
            /// <summary>スコアが高い順 (同点はカタログ順)。</summary>
            Score,

            /// <summary>カタログ順 (曲 ID 昇順)。</summary>
            Catalog,
        }

        /// <summary>次の並び順 (トグル)。</summary>
        public static HistorySort NextSort(HistorySort mode)
        {
            return mode == HistorySort.Score ? HistorySort.Catalog : HistorySort.Score;
        }

        /// <summary>並び順の表示ラベル (design の Sort チップの文言)。</summary>
        public static string SortLabel(HistorySort mode)
        {
            return mode == HistorySort.Score ? "スコア順" : "カタログ順";
        }

        /// <summary>
        /// プレイ履歴のある曲 (ベストスコアが記録されている曲) だけを取り出す。
        /// 未プレイの曲は履歴に出さない。ScoreStore は 0 点を記録しない
        /// (Submit が 0 以下を弾く) ので、0 は未プレイとして扱う。
        /// </summary>
        public static List<V6SongEntry> Played(IReadOnlyList<V6SongEntry> songs)
        {
            var played = new List<V6SongEntry>();
            if (songs == null)
            {
                return played;
            }
            for (int i = 0; i < songs.Count; i++)
            {
                V6SongEntry song = songs[i];
                if (song != null && song.BestScore > 0)
                {
                    played.Add(song);
                }
            }
            return played;
        }

        /// <summary>並び順を適用する (破壊的)。</summary>
        public static void Sort(List<V6SongEntry> played, HistorySort mode)
        {
            if (played == null)
            {
                return;
            }
            if (mode == HistorySort.Score)
            {
                // スコア降順。同点のときはカタログ順 (SongId 昇順) を保つ。
                played.Sort((a, b) =>
                {
                    int byScore = b.BestScore.CompareTo(a.BestScore);
                    return byScore != 0 ? byScore : a.SongId.CompareTo(b.SongId);
                });
            }
            else
            {
                played.Sort((a, b) => a.SongId.CompareTo(b.SongId));
            }
        }

        /// <summary>並び替え済みの履歴リストを作る (Played + Sort)。</summary>
        public static List<V6SongEntry> HistoryList(IReadOnlyList<V6SongEntry> songs, HistorySort mode)
        {
            List<V6SongEntry> played = Played(songs);
            Sort(played, mode);
            return played;
        }

        /// <summary>プレイヤー情報「累計」: 記録済みスコアの合計。</summary>
        public static int TotalScore(IReadOnlyList<V6SongEntry> played)
        {
            int total = 0;
            if (played == null)
            {
                return 0;
            }
            for (int i = 0; i < played.Count; i++)
            {
                V6SongEntry song = played[i];
                if (song != null && song.BestScore > 0)
                {
                    total += song.BestScore;
                }
            }
            return total;
        }

        /// <summary>プレイヤー情報「S以上」: グレードが S / SS の曲数。</summary>
        public static int CountGradeAtLeastS(IReadOnlyList<V6SongEntry> played)
        {
            int count = 0;
            if (played == null)
            {
                return 0;
            }
            for (int i = 0; i < played.Count; i++)
            {
                V6SongEntry song = played[i];
                if (song != null && (string.Equals(song.BestGrade, "S", StringComparison.Ordinal)
                    || string.Equals(song.BestGrade, "SS", StringComparison.Ordinal)))
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>ヘッダー右の件数表示 (design: "4 plays")。</summary>
        public static string PlaysLabel(int playedCount)
        {
            return playedCount + " plays";
        }

        /// <summary>
        /// プレイ情報パネルに出せる「直前のプレイ」が存在するか。
        /// GManager の play 用静的フィールドは次の遷移の ResetSession で消えるため、
        /// 全部 0 のときは「表示できるプレイがない」として空表示にする。
        /// </summary>
        public static bool HasLastPlay(int score, int perfect, int great, int bad, int miss)
        {
            return score > 0 || perfect > 0 || great > 0 || bad > 0 || miss > 0;
        }

        /// <summary>MAX COMBO の表示 ("246/296")。総ノーツ数が不明なら "–"。</summary>
        public static string ComboLabel(int maxCombo, int noteCount)
        {
            if (noteCount <= 0)
            {
                return "–";
            }
            return maxCombo + "/" + noteCount;
        }
    }
}
