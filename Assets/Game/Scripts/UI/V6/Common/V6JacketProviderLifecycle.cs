using System.Collections.Generic;
using UnityEngine;

namespace FlashBeat.UI.V6
{
    /// <summary>
    /// V6JacketProvider の無効化 / 破棄領域 (Issue #385 Phase 2.11 で分割)。
    /// ローカルキャッシュの無効化、全保持物の破棄 (アプリ終了 / EditMode テスト用)、
    /// 計測カウンタのリセットを持つ。partial class のためフィールド・メソッドは
    /// V6JacketProvider 本体と共有する。
    /// </summary>
    public static partial class V6JacketProvider
    {
        // ------------------------------------------------------------------
        // 無効化 / 破棄
        // ------------------------------------------------------------------

        /// <summary>
        /// すべての保持物を破棄する。**アプリ終了時 / EditMode テストのみ**。
        /// 実行中に呼ぶと生きている Image が白くなる。
        /// Issue #823: ローカル (Resources) ジャケット経路は撤去済み。
        /// localCropCache は遠隔でない Texture 由来の crop を保持する。
        /// </summary>
        public static void DestroyAll()
        {
            DestroySprites(localCropCache);
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

        /// <summary>
        /// 遠隔取得の未完了数 (in-flight + キュー滞留)。計測用。
        /// Issue #863: fetch は MaxConcurrentRequests で絞られ超過分がキューに
        /// 残るため、RemoteFetchCount のスナップショット比較はキュー消化を
        /// 待ってから取る必要がある。
        /// </summary>
        public static int RemotePendingCountForAudit
        {
            get { return remoteInFlight + remoteQueue.Count; }
        }

        /// <summary>計測点を 0 に戻す (テスト用)。</summary>
        public static void ResetCountersForAudit()
        {
            RemoteFetchCount = 0;
            RemoteDedupCount = 0;
            NegativeCacheHitCount = 0;
            RemoteFailureCount = 0;
            CropCreateCount = 0;
            RemoteFetchEvictedCount = 0;
        }
    }
}
