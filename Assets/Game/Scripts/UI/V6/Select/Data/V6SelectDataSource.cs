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
    /// </summary>
    public sealed class V6SelectDataSource : IV6SelectDataSource
    {
        readonly List<V6SongEntry> allSongs = new List<V6SongEntry>();
        readonly List<V6SongEntry> filteredSongs = new List<V6SongEntry>();
        readonly Dictionary<string, List<V6RankingRow>> rankingCache = new Dictionary<string, List<V6RankingRow>>();
        static readonly Dictionary<string, ChartMeta> chartMetaCache = new Dictionary<string, ChartMeta>();
        readonly Dictionary<string, bool> chartExistsCache = new Dictionary<string, bool>();
        readonly List<V6RankingRow> emptyRanking = new List<V6RankingRow>();

        // 実在するジャケットのみ (V5 SelectView の JacketResourceNames と同じ対応表)。
        // 無い曲は空文字を返し、固定画像を流用しない。
        static readonly Dictionary<string, string> JacketResourceNames = new Dictionary<string, string>
        {
            { "Poison", "Poison" },
            { "Calamity", "Calamity" },
            { "まにまに", "mani" },
            { "エウタナシア", "euta" },
            { "終焉逃避行", "syuen" },
            { "4th smile", "4th smile" },
        };

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
            else if (!LocalChartExists(row.title))
            {
                SetError("譜面が見つからないため開始できません (" + row.title + ")");
                return;
            }

            // 選択状態の確定。遷移は統合担当が既存の GameScene() を呼んで行う。
            GManager.songID = row.id;
            hasError = false;
            statusMessage = "";
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
        }

        static V6SongEntry BuildEntry(SongData song)
        {
            int best = GManager.BestScoreOf(song.id);
            return new V6SongEntry
            {
                SongId = song.id,
                Title = song.title ?? "",
                Artist = song.musician ?? "",
                Level = song.level,
                Bpm = BpmOf(song),
                NoteCount = NoteCountOf(song),
                BestScore = best > 0 ? best : -1,
                BestGrade = V6SelectDataLogic.GradeOf(best),
                IsCloud = song.isRemote,
                JacketResourcePath = JacketResourcePathOf(song.id, song.title),
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
            chartMetaCache[cacheKey] = meta;
            return meta;
        }

        /// <summary>
        /// 譜面の実ファイル (保持中のクラウド譜面 or Resources/&lt;曲名&gt;.json) から
        /// BPM とノーツ数を読む。読めなければ null。
        /// </summary>
        static ChartMeta ReadChartMeta(SongData song)
        {
            if (song.isRemote && !string.IsNullOrEmpty(song.remoteId))
            {
                int heldVersion;
                string heldJson;
                if (CloudChartHolder.TryGetForSong(song.remoteId, out heldVersion, out heldJson))
                {
                    // ParseChartResponse は譜面本体 (Data) を返す。失敗時は null。
                    ChartData chart = CloudCatalog.ParseChartResponse(heldJson);
                    if (chart != null) return ToMeta(chart);
                }
            }

            TextAsset asset = string.IsNullOrEmpty(song.title) ? null : Resources.Load<TextAsset>(song.title);
            if (asset == null) return null;
            try
            {
                ChartData chart = JsonUtility.FromJson<ChartData>(asset.text);
                return chart == null ? null : ToMeta(chart);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[V6SelectData] chart parse failed for '{song.title}': {e.Message}");
                return null;
            }
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

        /// <summary>
        /// ジャケットの Resources パス。
        ///
        /// v6 では全곡のジャケットを Resources/V6/select/jacket/ に置いてある
        /// (Pencil 設計の正式データ。ファイル名は "&lt;カタログ index&gt;-&lt;slug&gt;.jpg"、
        /// index は GManager.SongName の並びと一致する)。
        /// 対応する画像が無い曲 (灰Φ倶楽部: 22) は空文字を返し、固定画像を流用しない。
        /// </summary>
        static string JacketResourcePathOf(int songId, string title)
        {
            string v6 = V6JacketFileOf(songId);
            if (!string.IsNullOrEmpty(v6)) return "V6/select/jacket/" + v6;

            if (string.IsNullOrEmpty(title)) return "";
            string name;
            if (JacketResourceNames.TryGetValue(title, out name)) return "V5/select/jacket/" + name;
            return "";
        }

        /// <summary>カタログ index → ジャケットファイル名 (画像が無い index は空文字)。</summary>
        static string V6JacketFileOf(int songId)
        {
            switch (songId)
            {
                case 1: return "01-rosen.jpg";
                case 2: return "02-mani.jpg";
                case 3: return "03-who.jpg";
                case 4: return "04-tweet.jpg";
                case 5: return "05-shuumatsu.jpg";
                case 6: return "06-euta.jpg";
                case 7: return "07-chartreuse.jpg";
                case 8: return "08-psyched.jpg";
                case 9: return "09-dogbite.jpg";
                case 10: return "10-tutorial.jpg";
                case 11: return "11-kyoukiranmai.jpg";
                case 12: return "12-poison.jpg";
                case 13: return "13-selfie.jpg";
                case 14: return "14-4thsmile.jpg";
                case 15: return "15-calamity.jpg";
                case 16: return "16-panopticon.jpg";
                case 17: return "17-guru.jpg";
                case 18: return "18-abun.jpg";
                case 19: return "19-mushin.jpg";
                case 20: return "20-haruarashi.jpg";
                case 21: return "21-bibibibi.jpg";
                // 22 (灰Φ倶楽部) に対応するジャケットは無い (设计の 22-haihai は別の曲)。
                case 23: return "23-override.jpg";
                case 24: return "24-igaku.jpg";
                case 25: return "25-tekkyu.jpg";
                case 26: return "26-ikken.jpg";
                default: return "";
            }
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
                    rankingQueue.Enqueue(new RankingRequest(key, row.remoteId, row.chartVersion));
                }
                return;
            }

            BeginRankingFetch(new RankingRequest(key, row.remoteId, row.chartVersion));
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
            RunCoroutine(FetchRanking(request.Key, request.RemoteId, request.ChartVersion));
        }

        struct RankingRequest
        {
            public readonly string Key;
            public readonly string RemoteId;
            public readonly int ChartVersion;

            public RankingRequest(string key, string remoteId, int chartVersion)
            {
                Key = key;
                RemoteId = remoteId;
                ChartVersion = chartVersion;
            }
        }

        IEnumerator FetchRanking(string key, string remoteId, int chartVersion)
        {
            RankingClient.Result result = null;
            yield return RankingClient.GetLeaderboard(remoteId, chartVersion, r => { result = r; });

            rankingFetchInFlight = false;
            rankingFetchKey = null;
            if (result != null && result.ok && result.leaderboard != null && result.leaderboard.entries != null)
            {
                rankingCache[key] = RankingRowsOf(result.leaderboard.entries);
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

        static List<V6RankingRow> RankingRowsOf(RankingClient.LeaderboardEntry[] entries)
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
                    Grade = V6SelectDataLogic.GradeOf(entry.score),
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

        // --- Ready 確定 ------------------------------------------------------------

        bool LocalChartExists(string title)
        {
            if (string.IsNullOrEmpty(title)) return false;
            bool exists;
            if (chartExistsCache.TryGetValue(title, out exists)) return exists;
            exists = Resources.Load<TextAsset>(title) != null;
            chartExistsCache[title] = exists;
            return exists;
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
        /// HideAndDontSave でシーン遷移 (フェード中) をまたいでも止まらない。
        /// </summary>
        sealed class CoroutineHost : MonoBehaviour { }

        static CoroutineHost host;

        static Coroutine RunCoroutine(IEnumerator routine)
        {
            if (host == null)
            {
                var go = new GameObject("V6SelectDataSourceHost");
                go.hideFlags = HideFlags.HideAndDontSave;
                host = go.AddComponent<CoroutineHost>();
            }
            return host.StartCoroutine(routine);
        }
    }
}
