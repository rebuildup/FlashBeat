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
    ///   - **容量上限 (Issue #416)**: 保持は <see cref="RemoteCacheCapacity"/> 件まで。
    ///     超えたら LastAccess が最も古い非 in-flight エントリを退避し、
    ///     Texture と由来 crop Sprite を破棄する。in-flight は退避しない。
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

        /// <summary>遠隔要求のタイムアウト (秒)。これを過ぎると失敗扱い。</summary>
        public const int RemoteRequestTimeoutSeconds = 30;

        /// <summary>
        /// 遠隔画像キャッシュの上限 (エントリ数)。Issue #416。
        ///
        /// 成功した Texture をプロセス寿命で無制限に保持すると、
        /// 多曲を巡ったときに native メモリが単調増加する。
        /// 上限を超えたら LastAccess が最も古いエントリを退避し、
        /// Texture (とそこから作った crop Sprite) を破棄する。
        /// 曲数 26 前後の通常運用ではまず溢れないが、ストレス条件での上限を保証する。
        /// </summary>
        public const int RemoteCacheCapacity = 50;

        /// <summary>遠隔キャッシュから容量超過で退避した回数 (Issue #416)。</summary>
        public static int RemoteFetchEvictedCount { get; private set; }

        private sealed class RemoteEntry
        {
            public Texture2D Texture;
            public bool Failed;
            public float FailedAt;
            public bool InFlight;

            /// <summary>最後にアクセスした時刻 (unscaledTime)。LRU 退避の順序キー (Issue #416)。</summary>
            public float LastAccess;

            public readonly List<Action<Sprite>> Waiting = new List<Action<Sprite>>();
            public readonly List<Action<Texture2D>> WaitingTexture = new List<Action<Texture2D>>();
        }

        /// <summary>
        /// crop variant のキャッシュキー。**原 Texture は参照そのもの**で比較する
        /// (ID のハッシュ値では分けられない: ハッシュ衝突で別のテクスチャの
        /// 切り抜きが返ると、誤画像がキャッシュに残り続ける)。
        /// </summary>
        private sealed class SpriteKey
        {
            public Texture2D Texture;
            public float CropX;
            public float CropY;
            public float CropW;
            public float CropH;

            public override bool Equals(object obj)
            {
                var other = obj as SpriteKey;
                if (other == null) return false;
                // Rect の丸めをしない (設計由来の float 差をそのまま鍵に含む)。
                return ReferenceEquals(Texture, other.Texture)
                    && CropX == other.CropX && CropY == other.CropY
                    && CropW == other.CropW && CropH == other.CropH;
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    // 参照のハッシュ値を容器として使い、比較は Equals が担う。
                    int hash = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(Texture);
                    hash = hash * 397 ^ CropX.GetHashCode();
                    hash = hash * 397 ^ CropY.GetHashCode();
                    hash = hash * 397 ^ CropW.GetHashCode();
                    hash = hash * 397 ^ CropH.GetHashCode();
                    return hash;
                }
            }
        }

        // --- ローカル (Resources) ---

        /// <summary>songId で引く Sprite (原画像の全面)。キーはローカル songId。</summary>
        private static readonly Dictionary<string, Sprite> localSpriteCache =
            new Dictionary<string, Sprite>(StringComparer.Ordinal);

        /// <summary>解決したパスごとに「Resources に無い」ことを覚える (毎回 Load しない)。</summary>
        private static readonly HashSet<string> localMissing = new HashSet<string>(StringComparer.Ordinal);

        // --- 遠隔 (YouTube) ---
        // Issue #416: 上限は RemoteCacheCapacity 件。超えたら LastAccess が
        // 最も古い非 in-flight エントリを退避し、Texture を破棄する。
        // キーは映像 ID の ordinal 比較 (StringComparer.Ordinal)。

        private static readonly Dictionary<string, RemoteEntry> remoteCache =
            new Dictionary<string, RemoteEntry>(StringComparer.Ordinal);

        private static readonly Queue<Action> remoteQueue = new Queue<Action>();
        private static int remoteInFlight;

        // --- crop variant (原 Texture からの切り抜き) ---

        private static readonly Dictionary<SpriteKey, Sprite> localCropCache = new Dictionary<SpriteKey, Sprite>();
        private static readonly Dictionary<SpriteKey, Sprite> remoteCropCache = new Dictionary<SpriteKey, Sprite>();

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

        /// <summary>
        /// このプロセスが保持している **ローカル** Sprite の枚数 (リーク監査用)。
        /// Resources の中身が変わらないことを見るときの計算に使う (遠隔Sprite は含まない)。
        /// </summary>
        public static int CachedSpriteCount
        {
            get { return localSpriteCache.Count + localCropCache.Count; }
        }

        /// <summary>このプロセスが保持している遠隔エントリの枚数 (上限の検証用、Issue #416)。</summary>
        public static int RemoteCacheEntryCount
        {
            get { return remoteCache.Count; }
        }

        /// <summary>このプロセスが保持している遠隔 Sprite の枚数。</summary>
        public static int RemoteCachedSpriteCount
        {
            get { return remoteCropCache.Count; }
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
            string resolved = V6JacketResources.PathOf(songId, title);
            if (string.IsNullOrEmpty(resolved))
            {
                // 曲に画像が無いことが確定している (毎回表を引かないよう覚える)。
                return null;
            }

            Sprite cached;
            if (localSpriteCache.TryGetValue(resolved, out cached))
            {
                // Unity の偽 null (実体が外部で破棄された) は作り直す。
                if (cached != null)
                {
                    return cached;
                }

                localSpriteCache.Remove(resolved);
            }

            if (localMissing.Contains(resolved))
            {
                return null;
            }

            Texture2D texture = Resources.Load<Texture2D>(resolved);
            if (texture == null)
            {
                localMissing.Add(resolved);
                return null;
            }

            LocalLoadCount++;
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), 100f);
            sprite.name = "V6Jacket-" + resolved;
            localSpriteCache[resolved] = sprite;
            return sprite;
        }

        /// <summary>
        /// ローカルのジャケット Texture (原画像)。
        /// 無い曲は null。<see cref="V6LoadingView"/> のように Texture そのものが要る
        /// 呼び出し向け。**Unity が所有する** ので破棄しない。
        /// </summary>
        public static Texture2D LocalTexture(int songId, string title)
        {
            string resolved = V6JacketResources.PathOf(songId, title);
            return string.IsNullOrEmpty(resolved) ? null : Resources.Load<Texture2D>(resolved);
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
            return Crop(texture, rect, name, false);
        }

        /// <summary>
        /// 既知の原 Texture からの切り抜き。このメソッドは既存の公開 API として残す。
        /// ローカル/遠隔の区別は <see cref="IsRemoteTexture"/> で判定する
        /// (遠隔テクスチャは <see cref="RequestRemote"/> の破棄後も他画面が参照しうるので、
        /// 遠隔由来の切り抜きは InvalidateLocal では消さない)。
        /// </summary>
        private static Sprite Crop(Texture2D texture, Rect rect, string name, bool forceRemote)
        {
            if (texture == null || rect.width <= 0f || rect.height <= 0f)
            {
                return null;
            }

            bool remote = forceRemote || IsRemoteTexture(texture);
            var cache = remote ? remoteCropCache : localCropCache;
            var key = new SpriteKey
            {
                Texture = texture,
                CropX = rect.x,
                CropY = rect.y,
                CropW = rect.width,
                CropH = rect.height,
            };

            Sprite cached;
            if (cache.TryGetValue(key, out cached) && cached != null)
            {
                return cached;
            }

            var sprite = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), 100f);
            if (!string.IsNullOrEmpty(name))
            {
                sprite.name = name;
            }

            CropCreateCount++;
            cache[key] = sprite;
            return sprite;
        }

        /// <summary>このテクスチャが遠隔取得のものか (remoteCache に載っているか)。</summary>
        private static bool IsRemoteTexture(Texture2D texture)
        {
            if (texture == null) return false;
            foreach (var pair in remoteCache)
            {
                var entry = pair.Value;
                if (entry != null && ReferenceEquals(entry.Texture, texture))
                {
                    return true;
                }
            }

            return false;
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

        /// <summary>
        /// 既知の原 Texture を、そのプロジェクトに合う設定で包む（推奨の正方形 crop）。
        /// design の mode:"cover"（セル・56 の枠など）を、再デコードせずに得るためのもの。
        /// </summary>
        public static Sprite SquareCrop(Texture2D texture, string name)
        {
            if (texture == null)
            {
                return null;
            }

            return Crop(texture, CoverCropRect(texture.width, texture.height), name, false);
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

            RemoteEntry entry = GetOrEnqueueRemote(videoId);

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

        /// <summary>
        /// 同上。結果を Texture で直接受け取る (Result のバックドロップなど、
        /// スプライトではなくテクスチャそのものが要る用途)。
        ///
        /// 同じ映像 ID で <see cref="RequestRemote"/> と同時に待てば、
        /// HTTP は 1 本で済む。Texture は provider が所有し、呼び出し側は
        /// Destroy しない (Dispose は provider だけ)。
        /// </summary>
        public static void RequestRemoteTexture(string videoId, Action<Texture2D> onReady)
        {
            if (string.IsNullOrEmpty(videoId) || onReady == null)
            {
                if (onReady != null) onReady(null);
                return;
            }

            RemoteEntry entry = GetOrEnqueueRemote(videoId);

            // 取得済み
            if (entry.Texture != null)
            {
                onReady(entry.Texture);
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
                entry.WaitingTexture.Add(onReady);
                return;
            }

            entry.InFlight = true;
            entry.WaitingTexture.Add(onReady);

            EnqueueRemote(new RemoteRequest
            {
                VideoId = videoId,
                Entry = entry,
            });
        }

        private static RemoteEntry GetOrEnqueueRemote(string videoId)
        {
            RemoteEntry entry;
            if (!remoteCache.TryGetValue(videoId, out entry))
            {
                entry = new RemoteEntry { LastAccess = Time.unscaledTime };
                remoteCache[videoId] = entry;
            }
            else
            {
                // LRU の順序キーを更新する (退避は「最後に触られていない」順)。
                entry.LastAccess = Time.unscaledTime;
            }

            return entry;
        }

        /// <summary>
        /// 容量超過を解消する (Issue #416)。エントリが <see cref="RemoteCacheCapacity"/> を
        /// 超えていたら、LastAccess が最も古い非 in-flight エントリから退避する。
        /// 退避したエントリの Texture と、そこから生成した crop Sprite を破棄する。
        /// in-flight (HTTP 応答待ち) のエントリは退避しない
        /// (待ちリストのコールバックを失わないため)。全員が in-flight の場合は
        /// 一時的に上限を超えたままにする。
        /// </summary>
        private static void EnforceRemoteCacheCapacity()
        {
            while (remoteCache.Count > RemoteCacheCapacity)
            {
                string oldestKey = null;
                float oldestAccess = float.MaxValue;
                foreach (var pair in remoteCache)
                {
                    RemoteEntry entry = pair.Value;
                    if (entry == null || entry.InFlight)
                    {
                        continue;
                    }

                    if (entry.LastAccess < oldestAccess)
                    {
                        oldestAccess = entry.LastAccess;
                        oldestKey = pair.Key;
                    }
                }

                if (oldestKey == null)
                {
                    // 退避できるエントリが無い (全部 in-flight)。これ以上は進めない。
                    return;
                }

                EvictRemoteEntry(oldestKey);
            }
        }

        /// <summary>1 エントリを退避する。Texture と、その Texture 由来の crop Sprite を破棄する。</summary>
        private static void EvictRemoteEntry(string videoId)
        {
            RemoteEntry entry;
            if (!remoteCache.TryGetValue(videoId, out entry))
            {
                return;
            }

            remoteCache.Remove(videoId);
            RemoteFetchEvictedCount++;

            Texture2D texture = entry.Texture;
            if (texture != null)
            {
                // remoteCropCache から、この Texture 由来の Sprite を取り除く。
                // ここを残すと、破棄済み Texture を参照する Sprite が返り続ける。
                var doomed = new List<SpriteKey>();
                foreach (var pair in remoteCropCache)
                {
                    if (ReferenceEquals(pair.Key.Texture, texture))
                    {
                        doomed.Add(pair.Key);
                    }
                }

                for (int i = 0; i < doomed.Count; i++)
                {
                    Sprite sprite = remoteCropCache[doomed[i]];
                    if (sprite != null)
                    {
                        V6AssetOwner.DestroyObject(sprite);
                    }

                    remoteCropCache.Remove(doomed[i]);
                }

                V6AssetOwner.DestroyObject(texture);
                entry.Texture = null;
            }
        }

        /// <summary>
        /// 監査・テスト用 (Issue #416): HTTP を迂回して遠隔キャッシュにエントリを注入する。
        /// 退避ロジックと上限の検証専用で、本番コードからは呼ばないこと。
        /// <paramref name="lastAccess"/> は LRU 順の制御に使う (unscaledTime 相当の値)。
        /// </summary>
        public static RemoteEntryHandle SeedRemoteEntryForAudit(string videoId, Texture2D texture, float lastAccess)
        {
            if (string.IsNullOrEmpty(videoId))
            {
                return RemoteEntryHandle.Invalid;
            }

            RemoteEntry entry;
            if (!remoteCache.TryGetValue(videoId, out entry))
            {
                entry = new RemoteEntry();
                remoteCache[videoId] = entry;
            }

            entry.Texture = texture;
            entry.Failed = false;
            entry.InFlight = false;
            entry.LastAccess = lastAccess;
            EnforceRemoteCacheCapacity();
            return new RemoteEntryHandle(remoteCache.ContainsKey(videoId));
        }

        /// <summary>SeedRemoteEntryForAudit の結果ハンドル。エントリが退避で消えたかを返す。</summary>
        public readonly struct RemoteEntryHandle
        {
            public static readonly RemoteEntryHandle Invalid = new RemoteEntryHandle(false);

            private readonly bool retained;

            internal RemoteEntryHandle(bool retained)
            {
                this.retained = retained;
            }

            /// <summary>退避されずにキャッシュに残っているか。</summary>
            public bool Retained
            {
                get { return retained; }
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
                // Issue #398: 応答しない要求は remoteInFlight を占有し、
                // 残りのキューが永遠に進まない原因になる (CodeRabbit review)。
                req.timeout = RemoteRequestTimeoutSeconds;
                yield return req.SendWebRequest();

                bool ok = req.result == UnityWebRequest.Result.Success;
                Texture2D texture = null;
                if (ok)
                {
                    texture = DownloadHandlerTexture.GetContent(req);
                }

                RemoteEntry entry = request.Entry;
                entry.InFlight = false;
                entry.LastAccess = Time.unscaledTime;

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

                // Issue #416: 成功・失敗どちらの経路でも上限を強制する。
                // 失敗エントリは Texture を持たないが、辞書自体の膨張も防ぐ。
                EnforceRemoteCacheCapacity();

                var waiting = new List<Action<Sprite>>(entry.Waiting);
                var waitingTextures = new List<Action<Texture2D>>(entry.WaitingTexture);
                entry.Waiting.Clear();
                entry.WaitingTexture.Clear();
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

                for (int i = 0; i < waitingTextures.Count; i++)
                {
                    try
                    {
                        waitingTextures[i](texture);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning("[V6JacketProvider] onReadyTexture が例外を投げました: " + e.Message);
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
            return Crop(texture, new Rect(0f, 0f, texture.width, texture.height),
                "V6RemoteJacket-" + videoId, true);
        }

        // ------------------------------------------------------------------
        // 無効化 / 破棄
        // ------------------------------------------------------------------

        /// <summary>
        /// このメソッドで Local / localCropCache から外した Sprite は provider が所有して
        /// いるので、すべてここで破棄する (原 Texture は Resources/Unity 所有で破棄しない)。
        /// </summary>
        public static void InvalidateLocal()
        {
            localMissing.Clear();
            DestroySprites(localSpriteCache);
            DestroySprites(localCropCache);
        }

        /// <summary>
        /// すべての保持物を破棄する。**アプリ終了時 / EditMode テストのみ**。
        /// 実行中に呼ぶと生きている Image が白くなる。
        /// </summary>
        public static void DestroyAll()
        {
            InvalidateLocal();
            DestroySprites(remoteCropCache);

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

        /// <summary>辞書中の Sprite（この provider が生成したもの）を破棄して空にする。</summary>
        private static void DestroySprites(Dictionary<string, Sprite> cache)
        {
            foreach (var pair in cache)
            {
                if (pair.Value != null)
                {
                    V6AssetOwner.DestroyObject(pair.Value);
                }
            }

            cache.Clear();
        }

        /// <summary>辞書中の Sprite（この provider が生成したもの）を破棄して空にする。</summary>
        private static void DestroySprites(Dictionary<SpriteKey, Sprite> cache)
        {
            foreach (var pair in cache)
            {
                if (pair.Value != null)
                {
                    V6AssetOwner.DestroyObject(pair.Value);
                }
            }

            cache.Clear();
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
            RemoteFetchEvictedCount = 0;
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
