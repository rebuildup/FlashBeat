using System;
using System.Collections.Generic;

namespace FlashBeat.UI.V6.Select
{
    /// <summary>
    /// v6 選曲画面が 1 曲分の実データを表す値オブジェクト (Issue #221/#222)。
    /// 曲名・作家名・譜面情報はすべて実カタログ/実保存値から作る。
    /// 固定データ (サンプル曲名・仮スコア) を入れてはならない。
    /// </summary>
    public sealed class V6SongEntry
    {
        /// <summary>安定した曲 ID (GManager / CloudCatalog の songId)。</summary>
        public int SongId { get; set; }

        /// <summary>曲名。</summary>
        public string Title { get; set; } = "";

        /// <summary>作家名。</summary>
        public string Artist { get; set; } = "";

        /// <summary>難易度 (GManager.Slevel 相当)。</summary>
        public int Level { get; set; }

        /// <summary>ジャケットの Resources 相対パス (無ければ空文字)。</summary>
        public string JacketResourcePath { get; set; } = "";

        /// <summary>ベストスコア (未プレイなら -1)。</summary>
        public int BestScore { get; set; } = -1;

        /// <summary>ベストグレード (未プレイなら空文字)。</summary>
        public string BestGrade { get; set; } = "";

        /// <summary>BPM (不明なら 0)。</summary>
        public double Bpm { get; set; }

        /// <summary>ノーツ数 (不明なら 0)。</summary>
        public int NoteCount { get; set; }

        /// <summary>クラウド配信の曲かどうか。</summary>
        public bool IsCloud { get; set; }
    }

    /// <summary>ランキング 1 行 (実 API / 実保存値から作る)。</summary>
    public sealed class V6RankingRow
    {
        public int Position { get; set; }
        public string PlayerName { get; set; } = "";
        public int Score { get; set; }
        public string Grade { get; set; } = "";

        /// <summary>自分の記録か (表示は "YOU")。</summary>
        public bool IsYou { get; set; }
    }

    /// <summary>
    /// v6 選曲画面のデータ供給 (#222 が実装, #221 のビューはこれだけを参照する)。
    /// ビュー層はこの interface だけを介して実データを見る (実装詳細に依存しない)。
    /// </summary>
    public interface IV6SelectDataSource
    {
        /// <summary>実カタログ全件 (検索絞り込み前)。</summary>
        IReadOnlyList<V6SongEntry> AllSongs { get; }

        /// <summary>現在の検索/絞り込み結果。</summary>
        IReadOnlyList<V6SongEntry> FilteredSongs { get; }

        /// <summary>検索文字列。代入で即時に絞り込みが更新される。</summary>
        string Query { get; set; }

        /// <summary>選択中の曲 (未選択なら null)。</summary>
        V6SongEntry Selected { get; }

        /// <summary>読み込み中か。</summary>
        bool IsLoading { get; }

        /// <summary>読み込み/取得エラーの有無。true のときは StatusMessage に理由が入る。</summary>
        bool HasError { get; }

        /// <summary>画面に出す状態メッセージ (空文字なら表示なし)。</summary>
        string StatusMessage { get; }

        /// <summary>データが変わったときに通知。ビューは再描画の起点にする。</summary>
        event Action Changed;

        /// <summary>検索文字列を設定して絞り込み直す。</summary>
        void SetQuery(string query);

        /// <summary>songId を選ぶ (存在しなければ何もしない)。</summary>
        void Select(int songId);

        /// <summary>選択を delta 行だけ移動する (リスト端では端に留まる)。</summary>
        void MoveSelection(int delta);

        /// <summary>クラウドカタログ/ランキングの再取得。</summary>
        void RefreshAsync();

        /// <summary>指定曲のランキング (取得失敗時は空リスト)。</summary>
        IReadOnlyList<V6RankingRow> RankingFor(V6SongEntry song);

        /// <summary>
        /// Ready 確定。選択中の曲を実セッションとしてゲームを開始する
        /// (譜面・songId・chartVersion を確定してゲーム画面へ遷移)。
        /// </summary>
        void CommitReady();
    }
}
