using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace FlashBeat.UI.V6
{
    /// <summary>
    /// ジャケット / サムネイルの**唯一の取得経路** (Issue #398)。
    ///
    /// 一次監査で見つかった重複:
    ///   - <c>V6HistoryView</c> / <c>V6RankingView</c> が各々
    ///     <c>Resources.Load&lt;Texture2D&gt;</c> + <c>Sprite.Create</c> の辞書を持つ。
    ///   - <c>V6PauseView</c> が YouTube サムネイルを独自に UnityWebRequest で取り、
    ///     <c>Sprite.Create</c> する。失敗時の再試行制御も無い。
    ///   - <c>ResultSceneManager</c> が v6 有効でも別経路で同じ画像を取る。
    ///
    /// ここに集約するもの:
    ///   - **ローカル (Resources)** と **遠隔 (YouTube hqdefault)** の 2 経路。
    ///     ローカルを優先し、無い曲だけ遠隔へフォールバックする。
    ///   - **原 Texture は 1 枚だけ**保持し、表示 variant (crop / 角丸) は
    ///     原 Texture から作る。同じ画像を何度もデコードしない。
    ///   - **同時要求の dedupe**: 同じ映像 ID を 2 画面が同時に欲しがっても
    ///     HTTP は 1 回。結果は全員に配る。
    ///   - **負キャッシュ**: 取得失敗を一定時間覚え、失敗のたびに再試行しない。
    ///   - **所有者の明示**: Texture / Sprite はここが所有する。呼び出し側は
    ///     Destroy しない (View の破棄で消えてよいのは View が作ったものだけ)。
    ///
    /// ここに集約しないもの:
    ///   - 画面ごとの見た目 (16:9 のまま / 正方形 crop / 角丸) は variant 引数で表す。
    ///   - ランキング行の「ジャケット無しの空枠」は各 View のデザイン判断なので
    ///     View 側に残す (この provider は「画像が無い」を null で返すだけ)。
    /// </summary>
    public static class V6JacketProvider
    {
        /// <summary>YouTube サムネイルの URL (Pause / Result が使っていたものと同じ)。</summary>
        private const string YouTubeThumbUrl = "https://img.youtube.com/vi/{0}/hqdefault.jpg";

        /// <summary>
        /// 通信失敗を覚えておく時間 (秒)。この間は同じ映像 ID を取りに行かない。
        /// 0 にすると毎回再試行になる (busy loop の原因)。
        /// </summary>
        public const float NegativeCacheSeconds = 60f;

        /// <summary>遠隔画像の同時リクエスト上限。超えた分は順番待ち。</summary>
        public const int MaxConcurrentRequests = 2;

        private sealed class RemoteEntry
        {
            public Texture2D Texture;
            public bool Failed;
            public float FailedAt;
            public bool InFlight;
            public readonly List<Action<Sprite>> Waiting = new List<Action<Sprite>>();
        }

        private sealed class SpriteKey
        {
            public int TextureId;
            public int CropX;
            public int CropY;
            public int CropW;
            public int CropH;

            public override bool Equals(object obj)
            {
                var other = obj as SpriteKey;
                if (other == null) return false;
                return TextureId == other.TextureId
                    && CropX == other.CropX && CropY == other.CropY
                    && CropW == other.CropW && CropH == other.CropH;
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = TextureId;
                    hash = hash * 397 ^ CropX;
                    hash = hash * 397 ^ CropY;
                    hash = hash * 397 ^ CropW;
                    hash = hash * 397 ^ CropH;
                    return hash;
                }
            }
        }

        // --- ローカル (Resources) ---

        /// <summary>songId で引く Sprite (原画像の全面)。キーはローカル songId。</summary>
        private static readonly Dictionary<int, Sprite> localSpriteCache = new Dictionary<int, Sprite>();

        /// <summary>songId ごとに「Resources に無い」ことを覚える (毎回 Load しない)。</summary>
        private static readonly HashSet<int> localMissing = new HashSet<int>();

        // --- 遠隔 (YouTube) ---

        private static readonly Dictionary<string, RemoteEntry> remoteCache =
            new Dictionary<string, RemoteEntry>(StringComparer.Ordinal);

        private static readonly Queue<Action> remoteQueue = new Queue<Action>();
        private static int remoteInFlight;

        // --- crop variant (原 Texture からの切り抜き) ---

        private static readonly Dictionary<SpriteKey, Sprite> cropCache = new Dictionary<SpriteKey, Sprite>();

        // --- 計測点 (Issue #397 の before/after 用) ---

        /// <summary>ローカル画像を実際に Resources から読んだ回数。</summary>
        public static int LocalLoadCount { get; private set; }

        /// <summary>遠隔画像の HTTP を実際に開始した回数。</summary>
        public static int RemoteFetchCount { get; private set; }

        /// <summary>同じ映像 ID の同時要求を 1 本にまとめた回数 (dedupe)。</summary>
        public static int RemoteDedupCount { get; private set; }

        /// <summary>負キャッシュで HTTP を抑止した回数。</summary>
        public static int NegativeCacheHitCount { get; private set; }

        /// <summary>遠隔画像が失敗した回数 (HTTP/デコード)。</summary>
        public static int RemoteFailureCount { get; private set; }

        /// <summary>crop variant を実際に Sprite.Create した回数。</summary>
        public static int CropCreateCount { get; private set; }

        /// <summary>このプロセスが保持している Sprite の枚数 (リーク監査用)。</summary>
        public static int CachedSpriteCount
        {
            get { return localSpriteCache.Count + cropCache.Count; }
        }

        /// <summary>このプロセスが保持している遠隔 Texture の枚数。</summary>
        public static int RemoteTextureCount
        {
            get
            {
                int n = 0;
                foreach (var pair in remoteCache)
                {
                    if (pair.Value != null && pair.Value.Texture != null) n++;
                }

                return n;
            }
        }

        // ------------------------------------------------------------------
        // ローカル (Resources) 経路
        // ------------------------------------------------------------------

        /// <summary>
        /// ローカルのジャケット Sprite (原画像の全面)。
        /// 無い曲は null。null も覚える (毎回 Resources.Load しない)。
        ///
        /// 戻り値は **provider が所有** する。呼び出し側は Destroy しない。
        /// </summary>
        public static Sprite Local(int songId, string title)
        {
            Sprite cached;
            if (localSpriteCache.TryGetValue(songId, out cached))
            {
                // Unity の偽 null (実体が外部で破棄された) は作り直す。
                if (cached != null)
                {
                    return cached;
                }

                localSpriteCache.Remove(songId);
            }

            if (localMissing.Contains(songId))
            {
                return null;
            }

            Texture2D texture = V6JacketResources.LoadTexture(songId, title);
            if (texture == null)
            {
                localMissing.Add(songId);
                return null;
            }

            LocalLoadCount++;
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), 100f);
            sprite.name = "V6Jacket-" + songId;
            localSpriteCache[songId] = sprite;
            return sprite;
        }

        /// <summary>
        /// ローカルのジャケット Texture (原画像)。
        /// 無い曲は null。<see cref="V6LoadingView"/> のように Texture そのものが要る
        /// 呼び出し向け。**Unity が所有する** ので破棄しない。
        /// </summary>
        public static Texture2D LocalTexture(int songId, string title)
        {
            return V6JacketResources.LoadTexture(songId, title);
        }

        // ------------------------------------------------------------------
        // crop variant
        // ------------------------------------------------------------------

        /// <summary>
        /// 原 Texture から矩形を切り抜いた Sprite。同じ (Texture, Rect) は同じ実体を返す。
        ///
        /// design の "cover" 指定 (ローディング 56×56 等) を、原画像の再デコードなしで
        /// 作るためのもの。戻り値は provider が所有する。
        /// </summary>
        public static Sprite Crop(Texture2D texture, Rect rect, string name)
        {
            if (texture == null || rect.width <= 0f || rect.height <= 0f)
            {
                return null;
            }

            var key = new SpriteKey
            {
                TextureId = texture.GetEntityId().GetHashCode(),
                CropX = Mathf.RoundToInt(rect.x),
                CropY = Mathf.RoundToInt(rect.y),
                CropW = Mathf.RoundToInt(rect.width),
                CropH = Mathf.RoundToInt(rect.height),
            };

            Sprite cached;
            if (cropCache.TryGetValue(key, out cached) && cached != null)
            {
                return cached;
            }

            var sprite = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), 100f);            if (!string.IsNullOrEmpty(name))
            {
                sprite.name = name;
            }

            CropCreateCount++;
            cropCache[key] = sprite;
            return sprite;
        }

        /// <summary>
        /// 正方形の中央 crop (design の mode:"cover")。
        /// <see cref="V6LoadingViewLogic.CoverCropRect"/> と同じ規則を provider 側にも置き、
        /// 呼び出し側が Rect を計算しなくて済むようにする。
        /// </summary>
        public static Sprite SquareCrop(Texture2D texture, string name)
        {
            if (texture == null)
            {
                return null;
            }

            return Crop(texture, CoverCropRect(texture.width, texture.height), name);
        }

        /// <summary>中心を正方形に取る crop 矩形 (テクスチャ座標、原点は左下)。</summary>
        public static Rect CoverCropRect(int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                return new Rect(0f, 0f, 0f, 0f);
            }

            float side = Mathf.Min(width, height);
            return new Rect((width - side) * 0.5f, (height - side) * 0.5f, side, side);
        }

        // ------------------------------------------------------------------
        // 遠隔 (YouTube) 経路
        // ------------------------------------------------------------------

        /// <summary>
        /// YouTube のサムネイルを非同期で取る。取得できたら <paramref name="onReady"/> を
        /// Sprite 付きで呼ぶ。取得できなければ null で呼ぶ (呼び出し側は
        /// プレースホルダのままにする)。
        ///
        /// 同じ映像 ID の同時要求は 1 本の HTTP にまとめる。失敗は
        /// <see cref="NegativeCacheSeconds"/> のあいだ覚える。
        ///
        /// <paramref name="onReady"/> は **呼び出し元の MonoBehaviour** が生存している
        /// 間だけ呼ばれる必要がある。呼び出し側で <c>isActiveAndEnabled</c> を確認すること
        /// (provider はコルーチンを回すホストを知らないため、生存確認はできない)。
        /// </summary>
        public static void RequestRemote(string videoId, Action<Sprite> onReady)
        {
            if (string.IsNullOrEmpty(videoId) || onReady == null)
            {
                if (onReady != null) onReady(null);
                return;
            }

            RemoteEntry entry;
            if (!remoteCache.TryGetValue(videoId, out entry))
            {
                entry = new RemoteEntry();
                remoteCache[videoId] = entry;
            }

            // 取得済み
            if (entry.Texture != null)
            {
                onReady(SpriteFor(entry.Texture, videoId));
                return;
            }

            // 負キャッシュ (失敗直後)
            if (entry.Failed && Time.unscaledTime - entry.FailedAt < NegativeCacheSeconds)
            {
                NegativeCacheHitCount++;
                onReady(null);
                return;
            }

            // 同じ映像 ID を既に取りに行っている: 結果を待つ列に並ぶだけ (HTTP は増えない)。
            if (entry.InFlight)
            {
                RemoteDedupCount++;
                entry.Waiting.Add(onReady);
                return;
            }

            entry.InFlight = true;
            entry.Waiting.Add(onReady);

            EnqueueRemote(new RemoteRequest
            {
                VideoId = videoId,
                Entry = entry,
            });
        }

        /// <summary>取得済みの遠隔 Sprite (無ければ null)。同期で分かる範囲だけ返す。</summary>
        public static Sprite RemoteIfReady(string videoId)
        {
            if (string.IsNullOrEmpty(videoId)) return null;
            RemoteEntry entry;
            if (!remoteCache.TryGetValue(videoId, out entry) || entry == null || entry.Texture == null)
            {
                return null;
            }

            return SpriteFor(entry.Texture, videoId);
        }

        /// <summary>
        /// 既に取得済みの Texture を provider に預ける (呼び出し側が自前で取った場合の
        /// 移行用)。以後は同じ映像 ID で HTTP を起こさない。
        /// </summary>
        public static void AdoptRemoteTexture(string videoId, Texture2D texture)
        {
            if (string.IsNullOrEmpty(videoId) || texture == null) return;

            RemoteEntry entry;
            if (!remoteCache.TryGetValue(videoId, out entry))
            {
                entry = new RemoteEntry();
                remoteCache[videoId] = entry;
            }

            if (entry.Texture == null)
            {
                entry.Texture = texture;
                entry.Failed = false;
            }
        }

        private struct RemoteRequest
        {
            public string VideoId;
            public RemoteEntry Entry;
        }

        private static void EnqueueRemote(RemoteRequest request)
        {
            if (remoteInFlight < MaxConcurrentRequests)
            {
                StartRemote(request);
                return;
            }

            remoteQueue.Enqueue(() => StartRemote(request));
        }

        private static void StartRemote(RemoteRequest request)
        {
            remoteInFlight++;
            RemoteFetchCount++;

            // コルーチンを回すホスト。プレイ中は常に 1 つある。
            var host = RemoteHost.Instance;
            host.StartCoroutine(FetchRemote(request));
        }

        private static IEnumerator FetchRemote(RemoteRequest request)
        {
            string url = string.Format(YouTubeThumbUrl, request.VideoId);
            using (var req = UnityWebRequestTexture.GetTexture(url))
            {
                yield return req.SendWebRequest();

                bool ok = req.result == UnityWebRequest.Result.Success;
                Texture2D texture = null;
                if (ok)
                {
                    texture = DownloadHandlerTexture.GetContent(req);
                }

                RemoteEntry entry = request.Entry;
                entry.InFlight = false;

                if (texture != null)
                {
                    entry.Texture = texture;
                    entry.Failed = false;
                }
                else
                {
                    entry.Failed = true;
                    entry.FailedAt = Time.unscaledTime;
                    RemoteFailureCount++;
                }

                Sprite sprite = texture != null ? SpriteFor(texture, request.VideoId) : null;

                var waiting = new List<Action<Sprite>>(entry.Waiting);
                entry.Waiting.Clear();
                for (int i = 0; i < waiting.Count; i++)
                {
                    // 呼び出し側が破棄されている可能性があるので、例外は握りつぶさず
                    // 1 件ずつ守る (1 画面の失敗で他画面の画像まで失わない)。
                    try
                    {
                        waiting[i](sprite);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning("[V6JacketProvider] onReady が例外を投げました: " + e.Message);
                    }
                }
            }

            remoteInFlight--;
            if (remoteQueue.Count > 0 && remoteInFlight < MaxConcurrentRequests)
            {
                remoteQueue.Dequeue()();
            }
        }

        /// <summary>遠隔 Texture の全面 Sprite (映像 ID ごとに 1 枚)。</summary>
        private static Sprite SpriteFor(Texture2D texture, string videoId)
        {
            var key = new SpriteKey
            {
                TextureId = texture.GetEntityId().GetHashCode(),
                CropX = 0,
                CropY = 0,
                CropW = texture.width,
                CropH = texture.height,
            };

            Sprite cached;
            if (cropCache.TryGetValue(key, out cached) && cached != null)
            {
                return cached;
            }

            var sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), 100f);
            sprite.name = "V6RemoteJacket-" + videoId;
            cropCache[key] = sprite;
            return sprite;
        }

        // ------------------------------------------------------------------
        // 無効化 / 破棄
        // ------------------------------------------------------------------

        /// <summary>
        /// ローカル画像キャッシュを捨てる (Resources の中身が変わる操作の後)。
        /// <see cref="V6ContentCaches.InvalidateAll"/> から呼ばれる。
        /// </summary>
        public static void InvalidateLocal()
        {
            localSpriteCache.Clear();
            localMissing.Clear();
            cropCache.Clear();
        }

        /// <summary>
        /// すべての保持物を破棄する。**アプリ終了時 / EditMode テストのみ**。
        /// 実行中に呼ぶと生きている Image が白くなる。
        /// </summary>
        public static void DestroyAll()
        {
            localSpriteCache.Clear();
            localMissing.Clear();

            foreach (var pair in cropCache)
            {
                if (pair.Value != null)
                {
                    V6AssetOwner.DestroyObject(pair.Value);
                }
            }

            cropCache.Clear();

            foreach (var pair in remoteCache)
            {
                var entry = pair.Value;
                if (entry == null || entry.Texture == null) continue;
                V6AssetOwner.DestroyObject(entry.Texture);
                entry.Texture = null;
            }

            remoteCache.Clear();
            remoteQueue.Clear();
            remoteInFlight = 0;
        }

        /// <summary>計測点を 0 に戻す (テスト用)。</summary>
        public static void ResetCountersForAudit()
        {
            LocalLoadCount = 0;
            RemoteFetchCount = 0;
            RemoteDedupCount = 0;
            NegativeCacheHitCount = 0;
            RemoteFailureCount = 0;
            CropCreateCount = 0;
        }
    }

    /// <summary>
    /// 遠隔画像取得のコルーチンを回すための、シーンに依存しないホスト
    /// (Issue #398)。DontDestroyOnLoad なので画面遷移でも消えない。
    /// </summary>
    internal sealed class RemoteHost : MonoBehaviour
    {
        private static RemoteHost instance;

        internal static RemoteHost Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("V6JacketProviderHost");
                    // EditMode テストでは DontDestroyOnLoad が使えない
                    // ("can only be used in play mode")。テストでも provider が
                    // 動く必要があるので、再生中だけ永続化する。
                    if (Application.isPlaying)
                    {
                        DontDestroyOnLoad(go);
                    }

                    instance = go.AddComponent<RemoteHost>();
                }

                return instance;
            }
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(instance, this))
            {
                instance = null;
            }
        }
    }
}
