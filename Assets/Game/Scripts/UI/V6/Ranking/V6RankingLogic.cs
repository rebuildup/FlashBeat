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
        // ------------------------------------------------------------------
        // フレーム配置 (design 正本: flashbeat-v6-nodes.json「Resolution Check ·
        // Select/Result/Option」= vxSO0 / s88VZk / gaI3p の実配置表)。
        // 共通フレーム rw7zx (1728×972) を等倍 (1:1) で画面中央へ置く。
        // 拡大縮小 (min(W/1728, H/972) 倍の引き伸ばし) は行わない。
        //
        //   origin = (floor((screenW − 1728) / 2), floor((screenH − 972) / 2))
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
        // 出典: flashbeat-v6-nodes.json の S1TZD (04 Ranking · v6) 実測値。
        // ビュー (V6RankingView) と EditMode テストの単一ソース。
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

        /// <summary>曲リスト (列1-2)。Main 内 (0,0) 560×780。</summary>
        public const float ListPanelW = 560f;
        public const float ListPanelH = 780f;

        /// <summary>曲リスト行 (design: (16, 48+76k) 504×72、Select 画面と同じ行仕様)。</summary>
        public const float SongRowX = 16f;
        public const float SongRowY0 = 48f;
        public const float SongRowW = 504f;
        public const float SongRowH = 72f;
        public const float SongRowStep = 76f;
        public const int ListVisibleRows = 9;

        /// <summary>SCORE RANKING パネル (Main 内 (584,24) 512×732)。</summary>
        public const float ScorePanelX = 584f;
        public const float ScorePanelY = 24f;
        public const float ScorePanelW = 512f;
        public const float ScorePanelH = 732f;

        /// <summary>SCORE RANKING の行 (design: Score·k = (8, 60+64k) 496×56、10 行)。
        /// 行内の内訳 (Pos 36×36 / Name / Value 右寄せ / Grade 56×36) は
        /// 契約 §5 の Ranking 行仕様 = 共通部品 V6Lists.RankRow に従う。</summary>
        public const float RankRowX = 8f;
        public const float RankRowY0 = 60f;
        public const float RankRowW = 496f;
        public const float RankRowH = 56f;
        public const float RankRowStep = 64f;
        public const int RankVisibleRows = 10;

        /// <summary>PLAYERS パネル (Main 内 (1144,24) 512×732)。</summary>
        public const float PlayersPanelX = 1144f;

        /// <summary>SCORE RANKING の行 k (可視 index) のパネル内部 design y (y_down)。</summary>
        public static float RankRowTop(int visibleIndex)
        {
            return RankRowY0 + RankRowStep * visibleIndex;
        }

        /// <summary>曲リスト行 k (可視 index) のリスト内部 design y (y_down)。</summary>
        public static float SongRowTop(int visibleIndex)
        {
            return SongRowY0 + SongRowStep * visibleIndex;
        }

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
        /// Local (ローカル保存の自分のベスト) は通信と無関係なので、取得中/失敗の
        /// 文言は出さない (Issue #239)。data.IsLoading / HasError にはカタログ取得や
        /// Ready エラーも流れ込むため、それを Local パネルに出すと表示の意味と
        /// 実データが食い違う。
        /// </summary>
        public static string EmptyMessage(RankingSource source, bool isLoading, bool hasError, string statusMessage)
        {
            if (source == RankingSource.Local)
            {
                return "記録がありません";
            }
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

        /// <summary>
        /// Online セグメントを出すかどうか (spec 167: cloud 曲以外では Online
        /// セグメントを非表示にする)。ローカル曲にオンラインランキングは無い。
        /// </summary>
        public static bool ShowOnlineSegment(V6SongEntry song)
        {
            return song != null && song.IsCloud;
        }

        /// <summary>
        /// 選択中の曲の「曲別ランキング」(History 画面の曲ランキングパネル / Online 1 曲分)
        /// が空のときの本文。cloud 曲はオンライン取得の状態 (失敗 / 取得中 / 空データ) を、
        /// ローカル曲はローカル記録の有無だけを出す。取得状態と対象外を混同しない。
        /// </summary>
        public static string SongRankingEmptyMessage(
            V6SongEntry song, bool isLoading, bool hasError, string statusMessage)
        {
            if (!ShowOnlineSegment(song))
            {
                return EmptyMessage(RankingSource.Local, false, false, "");
            }
            return EmptyMessage(RankingSource.Online, isLoading, hasError, statusMessage);
        }

        /// <summary>
        /// PLAYERS パネルの空状態文言。総合 (プレイヤー別) の集計 API は既存に無いため、
        /// 「データ無し」ではなく未対応であることを出す (Issue #239)。
        /// </summary>
        public static string PlayersEmptyMessage()
        {
            return "プレイヤー別の集計には対応していません";
        }

        /// <summary>
        /// SCORE RANKING パネルの空状態文言。曲が未選択のときは「記録がありません」
        /// (データ空) ではなく未選択であることを出す (Issue #239)。未選択のまま
        /// データ空と同じ文言にすると、記録の有無と選択状態を区別できなくなる。
        /// </summary>
        public static string NoSongSelectedMessage()
        {
            return "曲が選択されていません";
        }
    }
}
