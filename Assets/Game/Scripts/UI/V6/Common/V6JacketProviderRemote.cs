using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace FlashBeat.UI.V6
{
    /// <summary>
    /// V6JacketProvider の遠隔 (YouTube hqdefault) 経路 (Issue #385 Phase 2.11 で分割)。
    /// UnityWebRequest による非同期取得、同時要求の dedupe、負キャッシュ、
    /// LRU 容量上限 (<see cref="RemoteCacheCapacity"/>) と退避、監査用シードを持つ。
    /// partial class のためフィールド・メソッドは V6JacketProvider 本体と共有する。
    /// </summary>
    public static partial class V6JacketProvider
    {
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

        // --- 遠隔 (YouTube) ---
        // Issue #416: 上限は RemoteCacheCapacity 件。超えたら LastAccess が
        // 最も古い非 in-flight エントリを退避し、Texture を破棄する。
        // キーは映像 ID の ordinal 比較 (StringComparer.Ordinal)。

        private static readonly Dictionary<string, RemoteEntry> remoteCache =
            new Dictionary<string, RemoteEntry>(StringComparer.Ordinal);

        private static readonly Queue<Action> remoteQueue = new Queue<Action>();
        private static int remoteInFlight;

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
                WantsSprite = true,
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
            RequestRemoteCore(videoId, null, 0, true, null, onReady);
        }

        /// <summary>
        /// Issue #824: ゲーム開始前の背景に使う、再生対象動画そのものの
        /// サムネイル (poster) を Texture で取る。
        ///
        /// ジャケット用の hqdefault (4:3) とは別エントリにキャッシュする
        /// (キーは <c>videoId + "|poster"</c>)。候補は 16:9 を優先して
        /// maxresdefault → sddefault → hqdefault の順に試し、実在しない
        /// maxresdefault が返す 120×90 のプレースホルダは
        /// <see cref="BackdropMinWidth"/> 未満として弾いて次へ進む。
        /// すべて失敗したら null (呼び出し側は既定の背景のままにする)。
        /// </summary>
        public static void RequestBackdropTexture(string videoId, Action<Texture2D> onReady)
        {
            if (string.IsNullOrEmpty(videoId) || onReady == null)
            {
                if (onReady != null) onReady(null);
                return;
            }

            string[] urls = new string[BackdropThumbUrls.Length];
            for (int i = 0; i < BackdropThumbUrls.Length; i++)
            {
                urls[i] = string.Format(BackdropThumbUrls[i], videoId);
            }

            RequestRemoteCore(videoId + "|poster", urls, BackdropMinWidth, false, null, onReady);
        }

        /// <summary>
        /// 遠隔取得の共通経路。<paramref name="cacheKey"/> は remoteCache の
        /// キー (ジャケットは映像 ID そのもの、poster は "|poster" 付き)。
        /// <paramref name="urls"/> が null なら従来の hqdefault 1 本。
        /// <paramref name="minWidth"/> 未満の画像 (存在しない maxresdefault の
        /// プレースホルダ等) は失敗として次の URL を試す。0 ならサイズ不問。
        /// </summary>
        private static void RequestRemoteCore(
            string cacheKey, string[] urls, int minWidth, bool wantsSprite,
            Action<Sprite> onSprite, Action<Texture2D> onTexture)
        {
            RemoteEntry entry = GetOrEnqueueRemote(cacheKey);

            // 取得済み
            if (entry.Texture != null)
            {
                if (onSprite != null) onSprite(SpriteFor(entry.Texture, cacheKey));
                if (onTexture != null) onTexture(entry.Texture);
                return;
            }

            // 負キャッシュ (失敗直後)
            if (entry.Failed && Time.unscaledTime - entry.FailedAt < NegativeCacheSeconds)
            {
                NegativeCacheHitCount++;
                if (onSprite != null) onSprite(null);
                if (onTexture != null) onTexture(null);
                return;
            }

            // 同じキーを既に取りに行っている: 結果を待つ列に並ぶだけ (HTTP は増えない)。
            if (entry.InFlight)
            {
                RemoteDedupCount++;
                if (onSprite != null) entry.Waiting.Add(onSprite);
                if (onTexture != null) entry.WaitingTexture.Add(onTexture);
                return;
            }

            entry.InFlight = true;
            if (onSprite != null) entry.Waiting.Add(onSprite);
            if (onTexture != null) entry.WaitingTexture.Add(onTexture);

            // URL 組み立て済みならそのまま、未指定なら映像 ID から hqdefault を作る。
            // cacheKey ("|poster" 付き) ではなく映像 ID を URL に使う必要があるため、
            // poster 経路は urls を必ず渡す。
            string videoId = cacheKey.EndsWith("|poster", StringComparison.Ordinal)
                ? cacheKey.Substring(0, cacheKey.Length - "|poster".Length)
                : cacheKey;
            EnqueueRemote(new RemoteRequest
            {
                VideoId = videoId,
                Urls = urls,
                MinWidth = minWidth,
                WantsSprite = wantsSprite,
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

            /// <summary>候補 URL (組み立て済み)。null なら hqdefault 1 本。</summary>
            public string[] Urls;

            /// <summary>受理する最小画像幅。プレースホルダを弾く (0 = 不問)。</summary>
            public int MinWidth;

            /// <summary>結果を crop Sprite にも変換するか (Texture だけの用途では false)。</summary>
            public bool WantsSprite;

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
            // Issue #824: 複数候補がある要求 (poster) は順に試し、
            // サイズ条件を満たす最初の画像を採用する。候補が無ければ
            // 従来どおり hqdefault 1 本。
            string[] urls = request.Urls;
            if (urls == null || urls.Length == 0)
            {
                urls = new[] { string.Format(YouTubeThumbUrl, request.VideoId) };
            }

            Texture2D texture = null;
            for (int i = 0; i < urls.Length && texture == null; i++)
            {
                using (var req = UnityWebRequestTexture.GetTexture(urls[i]))
                {
                    // Issue #398: 応答しない要求は remoteInFlight を占有し、
                    // 残りのキューが永遠に進まない原因になる (CodeRabbit review)。
                    req.timeout = RemoteRequestTimeoutSeconds;
                    yield return req.SendWebRequest();

                    if (req.result != UnityWebRequest.Result.Success)
                    {
                        continue;
                    }

                    var candidate = DownloadHandlerTexture.GetContent(req);
                    if (candidate == null)
                    {
                        continue;
                    }

                    if (candidate.width < request.MinWidth)
                    {
                        // 存在しない maxresdefault 等が返すプレースホルダ。
                        // 次の候補を試すため破棄する。
                        V6AssetOwner.DestroyObject(candidate);
                        continue;
                    }

                    texture = candidate;
                }
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

            Sprite sprite = request.WantsSprite && texture != null
                ? SpriteFor(texture, request.VideoId)
                : null;

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
    }
}
