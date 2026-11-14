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
    ///
    /// Issue #385 Phase 2.11: 領域ごとに partial class へ分割した
    /// (ローカル Resources 経路 = V6JacketProviderLocal.cs、
    /// crop variant = V6JacketProviderCrop.cs、遠隔 YouTube 経路と LRU 退避 =
    /// V6JacketProviderRemote.cs、無効化/破棄 = V6JacketProviderLifecycle.cs)。
    /// このファイルは定数・計測カウンタ・コルーチンホスト (RemoteHost) を持つ。
    /// </summary>
    public static partial class V6JacketProvider
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
