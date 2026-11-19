using System.Collections.Generic;

namespace FlashBeat.UI.V6.Select
{
    /// <summary>
    /// Result → Select の復帰で選曲画面へ引き継ぐコンテキスト (Issue #738)。
    ///
    /// 選曲画面の状態 (V6SelectDataSource / V6SelectView) はシーンとともに
    /// 破棄されるため、「直前にプレイした曲と、そのときの検索語」はここ
    /// (static = セッション級) に記録する。ビューやデータソースには持たせない。
    ///
    /// 状態の所有と遷移の境界:
    ///   - <see cref="Record"/>: 選曲画面で Ready が確定したとき
    ///     (SelectSceneManager.OnV6Ready)。
    ///   - <see cref="RequestRestore"/>: Result から選曲へ戻る遷移
    ///     (ResultSceneManager.SelectScene)。
    ///   - <see cref="TryApply"/>: 次の選曲画面の起動時
    ///     (SelectSceneManager.CreateV6SelectView)。要求は 1 回で消費される。
    ///   - <see cref="Clear"/>: 来場者交代の境界 (ResultSceneManager.TitleScene)。
    ///
    /// RequestRestore が無い入口 (アプリ起動 / Title → Select / プレイ中断の
    /// Select 遷移) では TryApply は false を返し、従来どおりの初期選択
    /// (V6SelectView が先頭の曲を選ぶ) のままにする。
    ///
    /// スクロール位置は選択 index から導出される
    /// (V6SelectViewLogic.ListScroll) ため、曲の復元でそのまま復帰する。
    /// 曲がカタログから消えていた (削除・非公開) ときはコンテキスト全体を
    /// 適用せず、初期状態 (先頭選択) にフォールバックする。
    /// </summary>
    public static class SelectReturnContext
    {
        /// <summary>復元対象の選曲状態。</summary>
        public struct Snapshot
        {
            /// <summary>直前にプレイした曲の songId (GManager カタログの index)。</summary>
            public int SongId;

            /// <summary>Ready 時点の検索語 (無ければ空文字)。</summary>
            public string Query;
        }

        static int lastSongId = -1;
        static string lastQuery = "";
        static bool restorePending;

        /// <summary>
        /// 選曲画面でプレイ開始が確定したときに、直前の選曲状態を記録する。
        /// 記録だけでは復元されない (復元には RequestRestore が必要)。
        /// </summary>
        public static void Record(int songId, string query)
        {
            lastSongId = songId;
            lastQuery = query ?? "";
        }

        /// <summary>
        /// Result → Select の遷移で呼ぶ。記録済みの選曲状態を
        /// 「次の選曲画面で復元する」要求としてマークする。
        /// まだ 1 曲もプレイしていない (記録が無い) ときは何もしない。
        /// </summary>
        public static void RequestRestore()
        {
            if (lastSongId >= 0)
            {
                restorePending = true;
            }
        }

        /// <summary>
        /// 選曲画面の起動時に復元要求を 1 回だけ取り出す。
        /// 要求が無ければ null。取り出した時点で要求は消費され、
        /// 次の画面へ持ち越さない。
        /// </summary>
        public static Snapshot? Consume()
        {
            if (!restorePending)
            {
                return null;
            }

            restorePending = false;
            return new Snapshot { SongId = lastSongId, Query = lastQuery };
        }

        /// <summary>
        /// 消費済みの復元要求をデータソースへ適用する。
        /// 適用したら true。要求が無い・データソースが無い・記録した曲が
        /// カタログに無い (削除・非公開) ときは何も適用せず false を返す。
        /// false のとき呼び出し側は従来の初期選択に任せる
        /// (定義済みフォールバック = 先頭の曲を選ぶ既存挙動)。
        /// </summary>
        public static bool TryApply(IV6SelectDataSource data)
        {
            Snapshot? snapshot = Consume();
            if (snapshot == null || data == null)
            {
                return false;
            }

            // 曲がカタログから消えていたらコンテキスト全体を捨てる
            // (検索語だけ戻すと選択曲が絞り込みで隠れた不整合な状態になる)。
            IReadOnlyList<V6SongEntry> songs = data.AllSongs;
            bool found = false;
            if (songs != null)
            {
                for (int i = 0; i < songs.Count; i++)
                {
                    V6SongEntry entry = songs[i];
                    if (entry != null && entry.SongId == snapshot.Value.SongId)
                    {
                        found = true;
                        break;
                    }
                }
            }
            if (!found)
            {
                return false;
            }

            // 検索語 → 選択の順。Select は AllSongs 基準なので絞り込みに
            // 左右されず、Ready 時点と同じ「曲 + 検索語」の組が再現される
            // (Ready 時に見えていた曲は同じ検索語でも見える)。
            if (!string.IsNullOrEmpty(snapshot.Value.Query))
            {
                data.SetQuery(snapshot.Value.Query);
            }
            data.Select(snapshot.Value.SongId);
            return true;
        }

        /// <summary>
        /// セッション境界で記録と要求を捨てる (Result → Title など、
        /// 来場者が代わる導線)。以降の選曲画面は初期状態から始まる。
        /// </summary>
        public static void Clear()
        {
            lastSongId = -1;
            lastQuery = "";
            restorePending = false;
        }

        // --- テスト用の観測口 ---------------------------------------------------

        /// <summary>復元要求が残っているか (診断・テスト用)。</summary>
        public static bool RestorePendingForAudit
        {
            get { return restorePending; }
        }

        /// <summary>記録中の songId (診断・テスト用。未記録なら -1)。</summary>
        public static int LastSongIdForAudit
        {
            get { return lastSongId; }
        }

        /// <summary>テスト間で状態を持ち越さないためのリセット。</summary>
        public static void ResetForAudit()
        {
            Clear();
        }
    }
}
