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
}
