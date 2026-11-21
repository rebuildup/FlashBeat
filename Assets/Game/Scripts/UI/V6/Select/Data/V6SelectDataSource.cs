using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using FlashBeat.Persistence;

// 譜面 JSON の型 (グローバル名前空間の Gameplay/NotesManager.cs)。この名前空間自体が
// Data という名前なので、using エイリアスで衝突を避ける。
using ChartData = global::Data;

namespace FlashBeat.UI.V6.Select.Data
{
    /// <summary>
    /// v6 選曲画面のデータ供給の実装 (Issue #222)。
    ///
    /// データ源はすべて実物 (デザインのサンプル曲名・仮スコアは使わない):
    ///   - 曲カタログ: GManager.ActiveSongs (固定表 + クラウドカタログのマージ結果)。
    ///     index 0 (noSong) と unplayable (譜面のないプレースホルダ, #95) は出さない。
    ///   - 譜面メタ (BPM / ノーツ数): Resources/&lt;曲名&gt;.json の Data を実ファイルから
    ///     読む。クラウド曲は CloudChartHolder の保持譜面 / API の noteCount。
    ///   - ベストスコア: ScorePersistence (stable songId + chartVersion 基準, #189)。
    ///     旧 GManager.Hiscore 配列は正本にしない。旧データの読み戻しは
    ///     ScorePersistence.Load 時の MigrateLegacyIfNeeded が担うので、
    ///     この経路で自動的に反映される。
    ///   - ランキング: RankingClient.GetLeaderboard (cloud 曲のみ)。
    ///   - ジャケット: Resources の実在画像のみ (V5 と同じ対応表)。無い曲は空文字。
    ///
    /// CommitReady は遷移をしない (統合担当との契約)。やるのは選択状態の確定だけ:
    /// 遷移・ResetSession・フェード・リモート譜面の取得 (CloudChartHolder.Hold) は
    /// 既存の SelectSceneManager.GameScene → StartRemoteSong 経路が行う。
    ///
    /// Issue #385: 600 行目安をわずかに超える (約 620 行) が、分割しない。
    /// カタログ構築・フィルタ・譜面メタキャッシュ・ランキング取得・コルーチン
    /// ホストが 1 つのデータ供給契約 (IV6SelectDataSource) を構成しており、
    /// 領域ごとの private 状態を共有する密結合のため partial 分割の効果が薄い。
    /// </summary>
    public sealed class V6SelectDataSource : IV6SelectDataSource
    {
        readonly List<V6SongEntry> allSongs = new List<V6SongEntry>();
        readonly List<V6SongEntry> filteredSongs = new List<V6SongEntry>();
        readonly Dictionary<string, List<V6RankingRow>> rankingCache = new Dictionary<string, List<V6RankingRow>>();
        // 譜面メタは保持中のクラウド譜面 (CloudChartHolder) からしか読めず、
        // 実行中に変わらない。シーンを跨いで使い回す (Issue #377)。
        static readonly Dictionary<string, ChartMeta> chartMetaCache = new Dictionary<string, ChartMeta>();
        readonly List<V6RankingRow> emptyRanking = new List<V6RankingRow>();

        // Issue #823: ジャケットは遠隔 (YouTube サムネイル) のみ。
        // ローカルの対応表は撤去した (V6SongBadge.ApplyJacket が videoId から解決)。

        string query = "";
        int selectedSongId = -1;
        bool catalogFetchInFlight;
        bool rankingFetchInFlight;
        string rankingFetchKey;
        readonly Queue<RankingRequest> rankingQueue = new Queue<RankingRequest>();
        bool hasError;
        string statusMessage = "";

        public event Action Changed;

        public V6SelectDataSource()
        {
            RebuildSongs();
        }

        public IReadOnlyList<V6SongEntry> AllSongs { get { return allSongs; } }

        public IReadOnlyList<V6SongEntry> FilteredSongs { get { return filteredSongs; } }

        public string Query { get { return query; } set { SetQuery(value); } }

        public V6SongEntry Selected
        {
            get
            {
                int index = IndexOfSong(allSongs, selectedSongId);
                return index >= 0 ? allSongs[index] : null;
            }
        }

        public bool IsLoading { get { return catalogFetchInFlight || rankingFetchInFlight; } }

        public bool HasError { get { return hasError; } }

        public string StatusMessage { get { return statusMessage ?? ""; } }

        public void SetQuery(string value)
        {
            string next = value ?? "";
            if (query == next) return;
            query = next;
            ApplyFilter();
            FireChanged();
        }

        public void Select(int songId)
        {
            if (IndexOfSong(allSongs, songId) < 0) return; // 存在しない songId は何もしない
            if (selectedSongId == songId) return;
            SelectInternal(songId);
        }

        public void MoveSelection(int delta)
        {
            if (filteredSongs.Count == 0) return;
            int current = IndexOfSong(filteredSongs, selectedSongId);
            int target;
            if (current < 0)
            {
                // 選択中の曲が絞り込み結果にいない: 移動方向の端から始める。
                target = delta >= 0 ? 0 : filteredSongs.Count - 1;
            }
            else
            {
                target = Math.Max(0, Math.Min(filteredSongs.Count - 1, current + delta));
            }
            SelectInternal(filteredSongs[target].SongId);
        }

        public void RefreshAsync()
        {
            RebuildSongs(); // 現在のカタログ (クラウド反映済み) を読み直す
            FireChanged();
            if (!Application.isPlaying) return; // EditMode (テスト) では通信しない
            StartCatalogRefresh();
            StartRankingFetch(Selected, true); // 取得済みでも取り直す
        }

        public IReadOnlyList<V6RankingRow> RankingFor(V6SongEntry song)
        {
            string key = song == null ? null : RankingKeyOf(song.SongId);
            if (key == null) return emptyRanking; // ローカル曲にオンラインランキングはない
            List<V6RankingRow> rows;
            return rankingCache.TryGetValue(key, out rows) ? rows : emptyRanking;
        }

        public void CommitReady()
        {
            V6SongEntry selected = Selected;
            if (selected == null)
            {
                SetError("曲が選択されていません");
                return;
            }

            // 絞り込み (検索) で隠れた曲は Ready できない (#221/#222)。
            // Selected は絞り込みをまたいで生き残るため、表示リストに見えない
            // 選択で開始すると「リストに無い曲が始まる」ことになる。
            if (IndexOfSong(filteredSongs, selected.SongId) < 0)
            {
                SetError("選択中の曲が絞り込みで隠れています (検索を変更してください)");
                return;
            }

            SongData row = GManager.GetSong(selected.SongId);
            if (row == null || row.unplayable)
            {
                SetError("この曲はプレイできません");
                return;
            }

            if (row.isRemote)
            {
                // リモート曲: 譜面の取得と保持 (CloudChartHolder.Hold)、ResetSession、
                // フェードは既存の開始経路 (SelectSceneManager.GameScene →
                // StartRemoteSong) の責務。ここでは identity が揃っていることだけ
                // 確認し、開始できないものは遷移させない (選曲画面に留まる)。
                if (string.IsNullOrEmpty(row.remoteId) || !RankingClient.IsValidSongId(row.remoteId))
                {
                    SetError("この曲はプレイできません (譜面 ID が不正です)");
                    return;
                }
                // chartVersion はカタログ行の値をそのまま使う。遷移時に
                // GManager.ResetSession → BeginPlaySession が CloudChartHolder の
                // 保持版とともに固定するので、ここで二重に固定しない。
            }
            else
            {
                // Issue #823: 譜面はクラウドカタログ経由でのみ供給される。
                // リモート化されていない行 (カタログ未取得の固定曲) は
                // ローカル譜面を持たないため開始できない。
                SetError("クラウドカタログから取得できないため開始できません (" + row.title + ")");
                return;
            }

            // 選択状態の確定。遷移は統合担当が既存の GameScene() を呼んで行う。
            GManager.songID = row.id;
            hasError = false;
            statusMessage = "";
            FireChanged();
        }

        /// <summary>
        /// 監査用 (#374): カタログ取得中フラグを直接立てる/下ろす。実通信は
        /// 行わない。画面監査 (AutoTestV6Screens) が「取得中かつ表示行なし →
        /// スケルトン」「取得中でも行があれば rows のまま (点滅しない)」の
        /// 経路を実機で再現するための口。実フェッチが走っている最中に
        /// false を書いても、その完了時に通常どおり false へ戻る。
        /// </summary>
        public void SetAuditCatalogFetching(bool fetching)
        {
            catalogFetchInFlight = fetching;
            if (fetching)
            {
                hasError = false;
                statusMessage = "カタログを取得しています…";
            }
            FireChanged();
        }

        // --- カタログ -----------------------------------------------------------

        void RebuildSongs()
        {
            allSongs.Clear();
            SongData[] songs = GManager.ActiveSongs;
            if (songs != null)
            {
                for (int i = 1; i < songs.Length; i++)
                {
                    SongData song = songs[i];
                    if (song == null || song.unplayable) continue;
                    allSongs.Add(BuildEntry(song));
                }
            }
            ApplyFilter();

            // 既定選択はしない (未選択で開始する)。未選択のまま Space (Ready) を
            // 押した場合は CommitReady が「曲が選択されていません」を表示して
            // 留まる (Issue #233)。黙って詰ませないための表示は CommitReady の責務。
        }

        static V6SongEntry BuildEntry(SongData song)
        {
            int best = GManager.BestScoreOf(song.id);
            int noteCount = NoteCountOf(song);
            return new V6SongEntry
            {
                SongId = song.id,
                Title = song.title ?? "",
                Artist = song.musician ?? "",
                Level = song.level,
                Bpm = BpmOf(song),
                NoteCount = noteCount,
                BestScore = best > 0 ? best : -1,
                // v6 のグレードは譜面満点 ScoreMaxV6(noteCount) に対する
                // 割合なので、この曲のノーツ数を渡す (ADR-0037 / Issue #825)。
                BestGrade = V6SelectDataLogic.GradeOf(best,
                    FlashBeat.Gameplay.Scoring.LatestVersion, noteCount),
                IsCloud = song.isRemote,
                // Issue #823: ローカル (Resources) ジャケットは撤去済み。
                // 遠隔 (YouTube サムネイル) は V6SongBadge.ApplyJacket が
                // 曲の videoId から解決するため、ここではパスを持たない。
                JacketResourcePath = "",
            };
        }

        void ApplyFilter()
        {
            filteredSongs.Clear();
            filteredSongs.AddRange(V6SelectDataLogic.Filter(allSongs, query));
        }

        void SelectInternal(int songId)
        {
            selectedSongId = songId;
            StartRankingFetch(Selected, false);
            FireChanged();
            PrefetchSelectedMedia(songId);
        }

        /// <summary>
        /// 選択が変わるたびに、その曲のメディアを裏でキャッシュへ落とす
        /// (Issue #377)。Ready 時に MusicManager の形式解決+ダウンロードが
        /// キャッシュヒットでスキップできるようにする。EditMode では
        /// 通信も yt-dlp も走らせない (RefreshAsync と同じガード)。
        /// </summary>
        static void PrefetchSelectedMedia(int songId)
        {
            if (!Application.isPlaying) return;
            SongData row = GManager.GetSong(songId);
            if (row == null || row.unplayable) return;
            string videoId = row.videoId;
            // MediaPrefetcher.Request の同期 prefix はキャッシュ走査
            // (TryGetReadyFiles) を含む。選択ハンドラ (メインスレッド) で直接
            // 呼ぶとディスク I/O で UI が削られるので、次フレームへ逃がす。
            // ツール探索自体は issue #642 で非同期化済み (Request は待たない)。
            // prefetch の失敗が選択フローを壊さないよう例外もここで閉じ込める。
            RunCoroutine(DeferredPrefetch(videoId));
        }

        static IEnumerator DeferredPrefetch(string videoId)
        {
            yield return null;
            try
            {
                FlashBeat.Gameplay.MediaPrefetcher.Request(videoId);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[V6SelectDataSource] media prefetch failed to start: " + e.Message);
            }
        }

        static int IndexOfSong(List<V6SongEntry> songs, int songId)
        {
            for (int i = 0; i < songs.Count; i++)
            {
                if (songs[i].SongId == songId) return i;
            }
            return -1;
        }

        // --- 譜面メタ ------------------------------------------------------------

        sealed class ChartMeta
        {
            public readonly double Bpm;
            public readonly int NoteCount;

            public ChartMeta(double bpm, int noteCount)
            {
                Bpm = bpm;
                NoteCount = noteCount;
            }
        }

        static ChartMeta ChartMetaOf(SongData song)
        {
            string cacheKey = song.isRemote && !string.IsNullOrEmpty(song.remoteId)
                ? song.remoteId + "@" + song.chartVersion.ToString(CultureInfo.InvariantCulture)
                : song.title ?? "";
            ChartMeta cached;
            if (chartMetaCache.TryGetValue(cacheKey, out cached)) return cached;

            ChartMeta meta = ReadChartMeta(song);
            ChartMetaLoadCount++;
            chartMetaCache[cacheKey] = meta;
            return meta;
        }

        /// <summary>
        /// 保持中のクラウド譜面から BPM とノーツ数を読む。読めなければ null。
        /// Issue #823: ローカル Resources の譜面 JSON は撤去した。
        /// カタログ行の値 (song.bpm / song.noteCount) が呼び出し側の
        /// フォールバックになる。
        /// </summary>
        static ChartMeta ReadChartMeta(SongData song)
        {
            if (!song.isRemote || string.IsNullOrEmpty(song.remoteId))
            {
                return null;
            }

            int heldVersion;
            string heldJson;
            if (CloudChartHolder.TryGetForSong(song.remoteId, out heldVersion, out heldJson))
            {
                // ParseChartResponse は譜面本体 (Data) を返す。失敗時は null。
                ChartData chart = CloudCatalog.ParseChartResponse(heldJson);
                if (chart != null) return ToMeta(chart);
            }
            return null;
        }

        static ChartMeta ToMeta(ChartData chart)
        {
            return new ChartMeta(chart.BPM, chart.notes != null ? chart.notes.Length : 0);
        }

        static double BpmOf(SongData song)
        {
            ChartMeta meta = ChartMetaOf(song);
            if (meta != null && meta.Bpm > 0) return meta.Bpm;
            return song.bpm; // 譜面ファイルが読めないときはカタログの値 (SBPM / クラウド)
        }

        static int NoteCountOf(SongData song)
        {
            ChartMeta meta = ChartMetaOf(song);
            if (meta != null && meta.NoteCount > 0) return meta.NoteCount;
            if (song.noteCount > 0) return song.noteCount; // クラウド曲: API の実ノーツ数
            return song.totalHits;                          // ローカル曲: カタログの総ヒット数
        }

        // --- ランキング -----------------------------------------------------------

        /// <summary>
        /// オンラインランキングの取得対象かを表すキャッシュキー (remoteId@version)。
        /// 取得対象でない (ローカル曲, remoteId/版が不正) なら null。
        /// 条件は V5 SelectView の CanFetchOnlineRanking と同じ。
        /// </summary>
        string RankingKeyOf(int songId)
        {
            SongData song = GManager.GetSong(songId);
            if (song == null || !song.isRemote) return null;
            if (string.IsNullOrEmpty(song.remoteId) || song.chartVersion <= 0) return null;
            if (!RankingClient.IsValidSongId(song.remoteId)) return null;
            return song.remoteId + "@" + song.chartVersion.ToString(CultureInfo.InvariantCulture);
        }

        void StartRankingFetch(V6SongEntry song, bool force)
        {
            if (song == null) return;
            string key = RankingKeyOf(song.SongId);
            if (key == null) return;
            if (!force && rankingCache.ContainsKey(key)) return; // 取得済み
            if (!Application.isPlaying) return; // EditMode (テスト) では通信しない

            SongData row = GManager.GetSong(song.SongId);
            if (row == null) return;

            // 取得中に別の曲へ移った場合、その要求を取りこぼさないようキューに積む。
            if (rankingFetchInFlight)
            {
                if (rankingFetchKey != key && !QueueContains(key))
                {
                    rankingQueue.Enqueue(new RankingRequest(key, row.remoteId, row.chartVersion,
                        song.NoteCount));
                }
                return;
            }

            BeginRankingFetch(new RankingRequest(key, row.remoteId, row.chartVersion,
                song.NoteCount));
        }

        /// <summary>キューに同じキーの要求が既にないか。</summary>
        bool QueueContains(string key)
        {
            foreach (var item in rankingQueue)
            {
                if (item.Key == key) return true;
            }
            return false;
        }

        void BeginRankingFetch(RankingRequest request)
        {
            rankingFetchInFlight = true;
            rankingFetchKey = request.Key;
            hasError = false;
            statusMessage = "ランキングを取得しています…";
            FireChanged();
            RunCoroutine(FetchRanking(request.Key, request.RemoteId, request.ChartVersion,
                request.NoteCount));
        }

        struct RankingRequest
        {
            public readonly string Key;
            public readonly string RemoteId;
            public readonly int ChartVersion;
            /// <summary>
            /// その曲の譜面ノーツ数。scoring v6 のグレード
            /// (ScoreMaxV6(noteCount) に対する割合) に使う (ADR-0037)。
            /// </summary>
            public readonly int NoteCount;

            public RankingRequest(string key, string remoteId, int chartVersion, int noteCount)
            {
                Key = key;
                RemoteId = remoteId;
                ChartVersion = chartVersion;
                NoteCount = noteCount;
            }
        }

        IEnumerator FetchRanking(string key, string remoteId, int chartVersion, int noteCount)
        {
            RankingClient.Result result = null;
            yield return RankingClient.GetLeaderboard(remoteId, chartVersion, r => { result = r; });

            rankingFetchInFlight = false;
            rankingFetchKey = null;
            if (result != null && result.ok && result.leaderboard != null && result.leaderboard.entries != null)
            {
                rankingCache[key] = RankingRowsOf(result.leaderboard.entries, noteCount);
                hasError = false;
                statusMessage = "";
            }
            else
            {
                hasError = true;
                statusMessage = RankingFailureMessage(result);
            }
            FireChanged();

            // 取得中に積まれた別曲の要求を消化する。
            if (rankingQueue.Count > 0)
            {
                BeginRankingFetch(rankingQueue.Dequeue());
            }
        }

        static List<V6RankingRow> RankingRowsOf(RankingClient.LeaderboardEntry[] entries, int noteCount)
        {
            var rows = new List<V6RankingRow>();
            string nickname = RankingClient.LoadNickname();
            for (int i = 0; i < entries.Length; i++)
            {
                RankingClient.LeaderboardEntry entry = entries[i];
                if (entry == null) continue;
                rows.Add(new V6RankingRow
                {
                    Position = entry.rank,
                    PlayerName = entry.nickname ?? "",
                    Score = entry.score,
                    // v6 のグレードは譜面満点 ScoreMaxV6(noteCount) に対する
                    // 割合なので、この曲のノーツ数を渡す (ADR-0037 / Issue #825)。
                    Grade = V6SelectDataLogic.GradeOf(entry.score,
                        FlashBeat.Gameplay.Scoring.LatestVersion, noteCount),
                    IsYou = !string.IsNullOrEmpty(nickname) && entry.nickname == nickname,
                });
            }
            return rows;
        }

        static string RankingFailureMessage(RankingClient.Result result)
        {
            if (result == null) return "ランキングを取得できませんでした";
            if (result.error == RankingClient.UnavailableError)
            {
                return "ネットワークに接続できないためランキングを取得できませんでした";
            }
            if (result.httpCode > 0)
            {
                return string.Format(CultureInfo.InvariantCulture,
                    "ランキングの取得に失敗しました (HTTP {0})", result.httpCode);
            }
            return string.IsNullOrEmpty(result.error)
                ? "ランキングを取得できませんでした"
                : "ランキングの取得に失敗しました (" + result.error + ")";
        }

        // --- クラウドカタログの再取得 ---------------------------------------------

        void StartCatalogRefresh()
        {
            if (V6ContentCaches.SuspendCatalogRefresh) return; // 監査計測中は再取得しない
            CloudCatalog.Config config = CloudCatalog.LoadConfig();
            if (config == null) return; // API 未設定: 固定カタログのまま (エラー扱いにしない)
            if (catalogFetchInFlight) return;

            catalogFetchInFlight = true;
            hasError = false;
            statusMessage = "カタログを取得しています…";
            FireChanged();
            RunCoroutine(FetchCatalog(config));
        }

        /// <summary>
        /// GET /api/v1/catalog を 1 回だけ取り直す (定期ループは既存の
        /// SelectSceneManager が持つ)。失敗してもローカル曲のリストはそのまま
        /// (画面は成立し続ける)。
        /// </summary>
        IEnumerator FetchCatalog(CloudCatalog.Config config)
        {
            string body = null;
            int status = 0;
            yield return CloudCatalog.GetText(CloudCatalog.CatalogUrl(config.baseUrl), (code, text) => { status = code; body = text; });

            SongData[] songs = status == 200 ? CloudCatalog.ParseCatalog(body) : null;
            catalogFetchInFlight = false;
            if (V6ContentCaches.SuspendCatalogRefresh)
            {
                // 監査計測中に完了した取得は捨てる (曲構成を計測の合間に変えない)。
                statusMessage = "";
                yield break;
            }
            if (songs != null)
            {
                GManager.ApplyRemoteCatalog(songs);
                if (GManager.songID > GManager.totalSong) GManager.songID = 1;
                RebuildSongs();
                hasError = false;
                statusMessage = "";
                StartRankingFetch(Selected, true); // 行の版が変わったかもしれないので取り直す
            }
            else
            {
                hasError = true;
                statusMessage = status == 0
                    ? "ネットワークに接続できないためカタログを更新できませんでした"
                    : string.Format(CultureInfo.InvariantCulture, "カタログの更新に失敗しました (HTTP {0})", status);
            }
            FireChanged();
        }

        // --- キャッシュの状態 (Issue #377 の計測と無効化) ------------------------

        /// <summary>譜面メタのキャッシュ件数。</summary>
        public static int ChartMetaCacheCount
        {
            get { return chartMetaCache.Count; }
        }

        /// <summary>
        /// 譜面メタを実際に読んだ回数 (キャッシュ ヒットは含まない)。
        /// </summary>
        public static int ChartMetaLoadCount { get; private set; }

        /// <summary>
        /// 譜面メタのキャッシュを捨てる。無効化の入口は
        /// <see cref="V6ContentCaches.InvalidateAll"/> に一本化する。
        /// </summary>
        internal static void ClearContentCaches()
        {
            chartMetaCache.Clear();
        }

        /// <summary>
        /// 開始経路 (SelectSceneManager.StartRemoteSong) の失敗を選曲画面へ見せる。
        /// 沈黙のまま留まると「フリーズしたように見える」(Issue #233 実機)。
        /// </summary>
        public void ReportStartFailure(string message)
        {
            SetError(message);
        }

        void SetError(string message)
        {
            hasError = true;
            statusMessage = message;
            FireChanged();
        }

        void FireChanged()
        {
            Action handler = Changed;
            if (handler != null) handler();
        }

        // --- コルーチンホスト -------------------------------------------------------

        /// <summary>
        /// コルーチンを動かすためだけのホスト (このクラスは MonoBehaviour ではない)。
        /// GameObject は実行時に作るだけなので .meta は不要 (アセットではない)。
        /// HideFlags は付けない: 通常のシーンオブジェクトなので、選曲画面を離れる
        /// (シーンがアンロードされる) ときに Unity が自動で破棄し、静的参照が
        /// 残り続けることがない。
        /// </summary>
        sealed class CoroutineHost : MonoBehaviour { }

        static CoroutineHost host;

        static Coroutine RunCoroutine(IEnumerator routine)
        {
            if (host == null)
            {
                var go = new GameObject("V6SelectDataSourceHost");
                host = go.AddComponent<CoroutineHost>();
            }
            return host.StartCoroutine(routine);
        }
    }
}
