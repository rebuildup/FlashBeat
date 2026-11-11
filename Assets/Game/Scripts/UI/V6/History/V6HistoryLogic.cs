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
        // ------------------------------------------------------------------
        // フレーム配置 (design 正本: flashbeat-v6-nodes.json「Resolution Check ·
        // Select/Result/Option」= vxSO0 / s88VZk / gaI3p の実配置表)
        //
        // 共通フレーム rw7zx は 1728×972 の固定解像度で、各解像度とも等倍 (1:1) で
        // 画面中央へ配置する (引き伸ばし = min(W/1728, H/972) の等比スケールは行わない)。
        //
        //   origin = (floor((screenW − 1728) / 2), floor((screenH − 972) / 2))
        //
        //   FHD 1920×1080 → (96, 54) / 2K 2560×1440 → (416, 234) / Mac 2560×1664 → (416, 346)
        // ------------------------------------------------------------------

        /// <summary>共通フレームの design 幅 (px)。V6Frame.DesignW と同値。</summary>
        public const int FrameW = 1728;

        /// <summary>共通フレームの design 高さ (px)。V6Frame.DesignH と同値。</summary>
        public const int FrameH = 972;

        /// <summary>フレーム左上原点の x (画面左端から)。1:1 中央配置の floor((screenW − 1728) / 2)。</summary>
        public static int FrameOriginX(int screenW)
        {
            return FloorDiv(screenW - FrameW, 2);
        }

        /// <summary>フレーム左上原点の y (画面上端から)。1:1 中央配置の floor((screenH − 972) / 2)。</summary>
        public static int FrameOriginY(int screenH)
        {
            return FloorDiv(screenH - FrameH, 2);
        }

        /// <summary>負の被除数でも数学的な floor で割る (C# の整数除算は 0 方向へ切捨て)。</summary>
        private static int FloorDiv(int a, int b)
        {
            int q = a / b;
            if (a % b != 0 && (a < 0) != (b < 0))
            {
                q--;
            }

            return q;
        }

        // ------------------------------------------------------------------
        // design 座標定数 (design px・フレーム左上原点 / y 下向き)。
        // 出典: flashbeat-v6-nodes.json の q9JZcH (03 History · v6) 実測値。
        // ビュー (V6HistoryView) と EditMode テストの単一ソース。
        // ------------------------------------------------------------------

        /// <summary>Main ゾーン (契約 §4 共通)。</summary>
        public const float MainX = 24f;
        public const float MainY = 96f;
        public const float MainH = 780f;

        /// <summary>フッターゾーン (契約 §4 共通: (24,876) 1680×96)。</summary>
        public const float FooterY = 876f;
        public const float FooterH = 96f;

        /// <summary>フッター右のボタン (design: 補助操作 (840+358,20) 197×56 / 主操作 (840+567,20) 273×56)。</summary>
        public const float FooterSettingsButtonX = 1198f;
        public const float FooterGotoButtonX = 1407f;
        public const float FooterButtonY = 20f;
        public const float FooterButtonH = 56f;

        /// <summary>履歴リスト (列1-2)。Main 内 (0,0) 560×780。</summary>
        public const float ListPanelW = 560f;
        public const float ListPanelH = 780f;

        /// <summary>履歴行 (design: (16, 48+72k) 496×64、Pos 36×36 r18 / Title w220 fs24 / Score w120 fs24 / Grade 56×36)。</summary>
        public const float HistoryRowX = 16f;
        public const float HistoryRowY0 = 48f;
        public const float HistoryRowW = 496f;
        public const float HistoryRowH = 64f;
        public const float HistoryRowStep = 72f;
        public const int HistoryVisibleRows = 10;

        // 履歴行の内部座標 (design q9JZcH/F676m/BuztA 496×64、親は行・y_down)。
        // 旧実装は Pos x14 / Title x64 / Score x296 で design より 2〜4px 右にずれていた。
        public const float HistoryPosX = 12f;
        public const float HistoryPosY = 14f;
        public const float HistoryTitleX = 60f;
        public const float HistoryTitleY = 15f;
        public const float HistoryTitleW = 220f;
        public const float HistoryScoreX = 292f;
        public const float HistoryScoreY = 15f;
        public const float HistoryScoreW = 120f;
        public const float HistoryGradeX = 424f;
        public const float HistoryGradeY = 14f;

        /// <summary>行内の Title / Score の高さ (design 35)。</summary>
        public const float HistoryInnerH = 35f;

        /// <summary>全行が縦中央スタックで収まる最大行数 (780 − 88 = 692 / 72 → 9)。</summary>
        public const int HistoryMaxCenteredRows = 9;

        /// <summary>
        /// 履歴リスト先頭行のリスト内部 design y (y_down)。
        /// design q9JZcH では 4 行がリスト中央 (254/326/398/470) に並ぶ — 行スタックを
        /// 「上余白 254 ≒ 下余白 246」の縦中央に置いており、上寄せではない。
        /// 収まるとき (count ≤ 9) は (780 − count*72)/2 + 8、収まらないときはスクロール前提の
        /// <see cref="HistoryRowY0"/> を返す。design 4 行なら 254 で一致する。
        /// </summary>
        public static float HistoryFirstRowY(int count)
        {
            if (count <= 0 || count > HistoryMaxCenteredRows)
            {
                return HistoryRowY0;
            }
            return (ListPanelH - count * HistoryRowStep) * 0.5f + 8f;
        }

        // ヘッダー右 (design q9JZcH/Y8lcc 情報ゾーン、親はヘッダーゾーン (24,0) 1680×96)。
        // 旧実装は Sort 132×44 @ (840,26)、Count 右端 1680 で design より大きかった。
        public const float HeaderSortX = 864f;
        public const float HeaderSortY = 33.5f;
        public const float HeaderSortW = 104f;
        public const float HeaderSortH = 29f;
        public const float HeaderSortTextX = 12f;
        public const float HeaderSortTextW = 80f;
        public const float HeaderCountX = 1376f;
        public const float HeaderCountY = 24f;
        public const float HeaderCountW = 280f;
        public const float HeaderCountH = 48f;

        /// <summary>選択中の曲パネル (列3-4)。Main 内 (572,0) 536×780。</summary>
        public const float CenterPanelX = 572f;
        public const float CenterPanelW = 536f;

        /// <summary>記録情報ゾーン (列5-6)。Main 内 (1120,0)、パネルは x16 w528。</summary>
        public const float RightColumnX = 1120f;
        public const float RightPanelX = 16f;
        public const float RightPanelW = 528f;

        /// <summary>曲ランキング情報パネル (記録情報内 (16,452) 528×304) とその行。</summary>
        public const float SongRankPanelY = 452f;
        public const float SongRankPanelH = 304f;

        /// <summary>曲ランキング行 (design: Score·k = (8, 60+60k) 496×52)。</summary>
        public const float SongRankRowX = 8f;
        public const float SongRankRowY0 = 60f;
        public const float SongRankRowW = 496f;
        public const float SongRankRowH = 52f;
        public const float SongRankRowStep = 60f;
        public const int SongRankVisibleRows = 4;

        /// <summary>履歴行 k (可視 index) のリスト内部 design y (y_down)。</summary>
        public static float HistoryRowTop(int visibleIndex)
        {
            return HistoryRowY0 + HistoryRowStep * visibleIndex;
        }

        /// <summary>曲ランキング行 k (可視 index) のパネル内部 design y (y_down)。</summary>
        public static float SongRankRowTop(int visibleIndex)
        {
            return SongRankRowY0 + SongRankRowStep * visibleIndex;
        }

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

        /// <summary>
        /// ヘッダー右の件数表示。値は「記録のある曲数」でありプレイ回数ではないので、
        /// 意味どおりの文言にする (design 例の "N plays" は実値と合わないため不使用)。
        /// </summary>
        public static string RecordedCountLabel(int recordedSongCount)
        {
            return "記録 " + recordedSongCount + "曲";
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
