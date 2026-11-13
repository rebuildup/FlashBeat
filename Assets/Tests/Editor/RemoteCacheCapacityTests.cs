using System.Collections;
using System.Collections.Generic;
using FlashBeat.UI.V6;
using NUnit.Framework;
using UnityEngine;

namespace FlashBeat.Tests
{
    /// <summary>
    /// Issue #416: 遠隔画像キャッシュ (V6JacketProvider.remoteCache) の
    /// 容量上限・LRU 退避・負キャッシュ・dedupe を固定する。
    ///
    /// HTTP は張らない。退避の検証には <see cref="V6JacketProvider.SeedRemoteEntryForAudit"/>
    /// でエントリを直接注入する (本番コードからは呼ばない監査用 API)。
    /// キー比較は StringComparer.Ordinal (provider 本体と同じ)。
    /// </summary>
    public class RemoteCacheCapacityTests
    {
        [SetUp]
        public void SetUp()
        {
            V6JacketProvider.DestroyAll();
            V6JacketProvider.ResetCountersForAudit();
        }

        [TearDown]
        public void TearDown()
        {
            V6JacketProvider.DestroyAll();
        }

        private static Texture2D NewTexture(int size = 4)
        {
            return new Texture2D(size, size, TextureFormat.RGBA32, false);
        }

        private static void SetEntryField(string videoId, string fieldName, object value)
        {
            var cacheField = typeof(V6JacketProvider).GetField("remoteCache",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.IsNotNull(cacheField, "remoteCache フィールドが見つからない");
            var dict = (IDictionary)cacheField.GetValue(null);
            Assert.IsTrue(dict.Contains(videoId), "エントリが無い: " + videoId);
            var entry = dict[videoId];
            var field = entry.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(field, "フィールドが見つからない: " + fieldName);
            field.SetValue(entry, value);
        }

        [Test]
        public void RemoteCache_HoldsAtMostCapacityEntries()
        {
            var textures = new List<Texture2D>();
            for (int i = 0; i < V6JacketProvider.RemoteCacheCapacity + 10; i++)
            {
                var tex = NewTexture();
                textures.Add(tex);
                V6JacketProvider.SeedRemoteEntryForAudit("vid-" + i, tex, i);
            }

            Assert.AreEqual(V6JacketProvider.RemoteCacheCapacity, V6JacketProvider.RemoteCacheEntryCount,
                "上限を超えて保持している");
            Assert.AreEqual(10, V6JacketProvider.RemoteFetchEvictedCount,
                "退避回数が上限超過分と一致しない");
        }

        [Test]
        public void EvictedEntry_TextureIsDestroyed_OldestFirst()
        {
            var textures = new List<Texture2D>();
            for (int i = 0; i < V6JacketProvider.RemoteCacheCapacity; i++)
            {
                var tex = NewTexture();
                textures.Add(tex);
                V6JacketProvider.SeedRemoteEntryForAudit("vid-" + i, tex, i);
            }

            // 最古の vid-0 由来の crop Sprite を作っておく (退避で消えるはず)。
            Texture2D oldest = textures[0];
            Rect rect = new Rect(0f, 0f, oldest.width, oldest.height);
            Sprite crop = V6JacketProvider.Crop(oldest, rect, "evicted-crop");
            Assert.IsNotNull(crop);

            // 1 つ追加すると最古 (vid-0) が退避される。
            V6JacketProvider.SeedRemoteEntryForAudit("vid-new", NewTexture(), V6JacketProvider.RemoteCacheCapacity);

            Assert.IsTrue(oldest == null, "退避された Texture が破棄されていない");
            Assert.IsTrue(crop == null, "退避された Texture 由来の crop Sprite が破棄されていない");
            Assert.IsFalse(textures[1] == null, "2 番目に古い Texture まで消えている");
            Assert.AreEqual(1, V6JacketProvider.RemoteFetchEvictedCount);
        }

        [Test]
        public void CapacityEviction_CountsNegativeEntriesToo()
        {
            // 負キャッシュ (Failed) のエントリも上限の数に入る。
            // 容量を負エントリだけで溢れさせても、辞書の成長は上限で止まる。
            for (int i = 0; i < V6JacketProvider.RemoteCacheCapacity; i++)
            {
                V6JacketProvider.SeedRemoteEntryForAudit("neg-" + i, null, i);
                SetEntryField("neg-" + i, "Failed", true);
                SetEntryField("neg-" + i, "FailedAt", Time.unscaledTime);
            }

            Assert.AreEqual(V6JacketProvider.RemoteCacheCapacity, V6JacketProvider.RemoteCacheEntryCount);

            V6JacketProvider.SeedRemoteEntryForAudit("neg-overflow", null, V6JacketProvider.RemoteCacheCapacity);

            Assert.AreEqual(V6JacketProvider.RemoteCacheCapacity, V6JacketProvider.RemoteCacheEntryCount);
            Assert.AreEqual(1, V6JacketProvider.RemoteFetchEvictedCount);
        }

        [Test]
        public void NegativeCache_StillSuppressesHttpForRecentFailures()
        {
            V6JacketProvider.SeedRemoteEntryForAudit("vid-fail", null, 0f);
            SetEntryField("vid-fail", "Failed", true);
            SetEntryField("vid-fail", "FailedAt", Time.unscaledTime);

            int fetchesBefore = V6JacketProvider.RemoteFetchCount;
            int negativeBefore = V6JacketProvider.NegativeCacheHitCount;

            Sprite received = null;
            bool called = false;
            V6JacketProvider.RequestRemote("vid-fail", sprite =>
            {
                called = true;
                received = sprite;
            });

            Assert.IsTrue(called, "負キャッシュでもコールバックは同期で返る");
            Assert.IsNull(received);
            Assert.AreEqual(fetchesBefore, V6JacketProvider.RemoteFetchCount,
                "負キャッシュのはずが HTTP を起こしている");
            Assert.AreEqual(negativeBefore + 1, V6JacketProvider.NegativeCacheHitCount);
        }

        [Test]
        public void CachedRemoteTexture_ReturnsTheSameSpriteInstanceForTheSameId()
        {
            var tex = NewTexture();
            V6JacketProvider.SeedRemoteEntryForAudit("vid-same", tex, 0f);

            Sprite first = null, second = null;
            V6JacketProvider.RequestRemote("vid-same", sprite => first = sprite);
            V6JacketProvider.RequestRemote("vid-same", sprite => second = sprite);

            Assert.IsNotNull(first);
            Assert.IsNotNull(second);
            Assert.AreSame(first, second, "同じ映像 ID に別の Sprite を返している");
        }

        [Test]
        public void Eviction_DoesNotInterruptInFlightRequests()
        {
            // in-flight (HTTP 応答待ち) のエントリを最古アクセスで 1 件作る。
            // in-flight は LastAccess が最古でも退避してはいけない。
            V6JacketProvider.SeedRemoteEntryForAudit("vid-inflight", null, 0f);
            SetEntryField("vid-inflight", "InFlight", true);

            // 容量を溢れさせる (inflight 1 + bulk 55 = 56 件)。
            for (int i = 1; i <= V6JacketProvider.RemoteCacheCapacity + 5; i++)
            {
                V6JacketProvider.SeedRemoteEntryForAudit("vid-bulk-" + i, NewTexture(), i);
            }

            // in-flight を除いた最古 (vid-bulk-1 ...) が退避され、上限に収まる。
            Assert.AreEqual(V6JacketProvider.RemoteCacheCapacity, V6JacketProvider.RemoteCacheEntryCount);
            Assert.AreEqual(6, V6JacketProvider.RemoteFetchEvictedCount);

            // in-flight のエントリは残っているので、待ちのコールバックは発火しない。
            bool called = false;
            V6JacketProvider.RequestRemote("vid-inflight", _ => called = true);
            Assert.IsFalse(called,
                "in-flight のエントリが退避/完了扱いされ、待ちのコールバックが発火している");
        }
    }
}
