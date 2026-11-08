using System;
using System.Collections.Generic;
using FlashBeat.UI.V6.Select;
using FlashBeat.UI.V6.Select.Data;

namespace FlashBeat.UI.V6.Ranking
{
    /// <summary>
    /// v6 ランキング画面 (04 Ranking · v6 / Pencil ノード S1TZD) の純ロジック。
    /// Unity エンジンに依存せず EditMode テストから直接検証できる。
    /// </summary>
    public static class V6RankingLogic
    {
        /// <summary>SCORE RANKING パネルのセグメント (design ヘッド右の Local / Online)。</summary>
        public enum RankingSource
        {
            /// <summary>ローカル保存のベストスコア (ScorePersistence)。</summary>
            Local,

            /// <summary>オンラインランキング (RankingClient.GetLeaderboard)。</summary>
            Online,
        }

        /// <summary>次のセグメント (トグル)。</summary>
        public static RankingSource NextSource(RankingSource source)
        {
            return source == RankingSource.Local ? RankingSource.Online : RankingSource.Local;
        }

        /// <summary>セグメントの表示ラベル (design どおり英字)。</summary>
        public static string SourceLabel(RankingSource source)
        {
            return source == RankingSource.Local ? "Local" : "Online";
        }

        /// <summary>
        /// Local セグメントの行を作る。ローカル保存は「自分のベスト」しか
        /// 持っていないので、記録があれば YOU 1 行、無ければ空リスト (空状態表示)。
        /// </summary>
        public static List<V6RankingRow> LocalRows(V6SongEntry song)
        {
            var rows = new List<V6RankingRow>();
            if (song == null || song.BestScore <= 0)
            {
                return rows;
            }
            rows.Add(new V6RankingRow
            {
                Position = 1,
                PlayerName = "YOU",
                Score = song.BestScore,
                Grade = song.BestGrade,
                IsYou = true
            });
            return rows;
        }

        /// <summary>
        /// ランキング行が空のときの本文。エラー中はその理由 (StatusMessage)、
        /// 読み込み中は「取得中」、それ以外はデータ無し。
        /// </summary>
        public static string EmptyMessage(RankingSource source, bool isLoading, bool hasError, string statusMessage)
        {
            if (hasError && !string.IsNullOrEmpty(statusMessage))
            {
                return statusMessage;
            }
            if (isLoading)
            {
                return "ランキングを取得しています…";
            }
            if (hasError)
            {
                return "ランキングを取得できませんでした";
            }
            return source == RankingSource.Online ? "NO DATA" : "記録がありません";
        }
    }
}
