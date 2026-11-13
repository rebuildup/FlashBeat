using System.Collections.Generic;
using FlashBeat.UI.V6;
using FlashBeat.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace FlashBeat.Tests
{
    /// <summary>
    /// Issue #416: Title → Select → Game → Result → Retry を複数周したときに
    /// キャッシュが単調増加しないことを、コード構造レベルで検証する。
    ///
    /// 実機の Memory Profiler による before/after 計測はディスプレイが必要で
    /// このテストでは代替できない (Issue #416 の「実機で検証」ゲートは未達のまま)。
    /// ここで固定するのは「再入場してもキャッシュ件数が収束する」という
    /// 構造的性質で、native オブジェクトが漏れなく解放経路を持つことの根拠になる。
    /// </summary>
    public class NativeReentryStabilityTests
    {
        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            V6ContentCaches.InvalidateAll();
            V6JacketProvider.DestroyAll();
            V6AssetOwner.DestroyShared();
            for (int i = 0; i < created.Count; i++)
            {
                Object.DestroyImmediate(created[i]);
            }

            created.Clear();
        }

        private Texture2D NewTexture(int size = 8)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            created.Add(tex);
            return tex;
        }

        /// <summary>1 周分の「画面遷移で触る」生成経路を再現する。</summary>
        private void SimulateOneRound(int round, Texture2D logo)
        {
            // Title: ロゴ (FromTexture 共有) と角丸パーツ。
            V6Sprites.FromTexture(logo, "reentry-logo");
            V6Sprites.Rounded(8);
            V6Sprites.Rounded(16);

            // Select: 26 曲分のジャケット (Resources) と角丸セル。v6 の総数に相当。
            V6JacketProvider.Local(10, "チュートリアル");
            V6Sprites.Rounded(24);

            // Game: 角丸 HUD パーツとアイコン参照。
            V6Sprites.Rounded(4);
            V6Sprites.Icon("__reentry_test_icon_a");
            V6Sprites.Icon("__reentry_test_icon_b");

            // Result: 角丸リザルトパネル。
            V6Sprites.Rounded(12);
            V6Sprites.FromTexture(logo, "reentry-logo"); // 同じ組み合わせは同一実体

            // Retry: 同じ経路をもう一度 (ここで 2 周目の Retry)。
            V6Sprites.Rounded(8);
            V6Sprites.Rounded(16);
            V6JacketProvider.Local(10, "チュートリアル");
        }

        [Test]
        public void ThreeRounds_TitleSelectGameResultRetry_CacheCountsStabilize()
        {
            V6AssetOwner.DestroyShared();
            V6JacketProvider.DestroyAll();
            V6JacketProvider.ResetCountersForAudit();

            Texture2D logo = NewTexture(16);

            int[] sharedCount = new int[3];
            int[] createCount = new int[3];
            int[] iconCount = new int[3];
            int[] localJacketSprites = new int[3];

            for (int round = 0; round < 3; round++)
            {
                SimulateOneRound(round, logo);
                sharedCount[round] = V6Sprites.SharedCount;
                createCount[round] = V6Sprites.CreateCount;
                iconCount[round] = V6Sprites.IconCacheCount;
                localJacketSprites[round] = V6JacketProvider.CachedSpriteCount;
            }

            Assert.AreEqual(sharedCount[0], sharedCount[1],
                "2 周目で共有アセットが増えている (再入場でリークしている)");
            Assert.AreEqual(sharedCount[1], sharedCount[2],
                "3 周目で共有アセットが増えている (再入場でリークしている)");

            Assert.AreEqual(createCount[0], createCount[1],
                "2 周目に新規生成が発生している");
            Assert.AreEqual(createCount[1], createCount[2],
                "3 周目に新規生成が発生している");

            Assert.AreEqual(iconCount[0], iconCount[2],
                "アイコンの参照キャッシュが往復ごとに増えている");
            Assert.AreEqual(localJacketSprites[0], localJacketSprites[2],
                "ローカルジャケットのキャッシュが往復ごとに増えている");
        }

        [Test]
        public void InvalidateAll_DropsLocalCaches_KeepsSharedOwnershipIntact()
        {
            V6AssetOwner.DestroyShared();
            V6JacketProvider.DestroyAll();
            V6JacketProvider.ResetCountersForAudit();

            Texture2D logo = NewTexture(16);
            Sprite rounded = V6Sprites.Rounded(8);
            Sprite fromLogo = V6Sprites.FromTexture(logo, "reentry-logo");
            V6JacketProvider.Local(10, "チュートリアル");
            int sharedBefore = V6Sprites.SharedCount;

            V6ContentCaches.InvalidateAll();

            Assert.AreEqual(0, V6JacketProvider.CachedSpriteCount,
                "InvalidateAll 後もローカルジャケットのキャッシュが残っている");
            Assert.AreEqual(sharedBefore, V6Sprites.SharedCount,
                "InvalidateAll が共有アセット (プロセス寿命) を巻き込んで破棄した");
            Assert.IsFalse(rounded == null, "破棄されたはずのない角丸が破棄されている");
            Assert.IsFalse(fromLogo == null, "破棄されたはずのないロゴが破棄されている");
            Assert.IsTrue(V6AssetOwner.IsSharedAsset(rounded));
            Assert.IsTrue(V6AssetOwner.IsSharedAsset(fromLogo));
        }

        [Test]
        public void Scope_RefusesToTrackSharedAssets()
        {
            V6AssetOwner.DestroyShared();
            Sprite shared = V6Sprites.Rounded(8);
            Assert.IsTrue(V6AssetOwner.IsSharedAsset(shared));

            using (var scope = V6AssetOwner.OpenScope("reentry-test"))
            {
                // Track の拒否はエラーログとして表面化する (テストフレームワークが
                // 期待しない Error ログを失敗扱いするので、ここで期待を登録する)。
                UnityEngine.TestTools.LogAssert.Expect(LogType.Error,
                    new System.Text.RegularExpressions.Regex(@"共有オーナー所有のアセットをスコープに Track"));
                Sprite tracked = scope.Track(shared);
                Assert.AreSame(shared, tracked,
                    "共有アセットはスコープの所有物ではないが、呼び出し側の参照は生きたまま返す");
                Assert.AreEqual(0, scope.Count,
                    "共有アセットがスコープに Track されている (Dispose で誤破棄される)");
            }

            Assert.IsFalse(shared == null,
                "スコープの Dispose が共有アセットを破棄した");
        }

        [Test]
        public void Scope_TracksAndDisposesViewOwnedAssets()
        {
            var viewOwned = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            using (var scope = V6AssetOwner.OpenScope("reentry-test"))
            {
                Assert.AreEqual(0, scope.Count);
                scope.Track(viewOwned);
                Assert.AreEqual(1, scope.Count);
            }

            // Dispose でスコープ所有の Texture だけが破棄される。
            Assert.IsTrue(viewOwned == null,
                "スコープの Dispose で View 所有の Texture が破棄されていない");
        }

        [Test]
        public void DestroyShared_ReleasesRoundedTexturesAndSprites()
        {
            V6AssetOwner.DestroyShared();
            Sprite rounded = V6Sprites.Rounded(8);
            Texture2D original = rounded.texture;
            Assert.IsTrue(V6AssetOwner.IsSharedAsset(rounded));
            Assert.IsTrue(V6AssetOwner.IsSharedAsset(original));

            V6AssetOwner.DestroyShared();

            Assert.IsTrue(rounded == null, "DestroyShared で角丸 Sprite が解放されていない");
            Assert.IsTrue(original == null, "DestroyShared で角丸の原 Texture2D が解放されていない");
            Assert.AreEqual(0, V6Sprites.SharedCount);
        }

        [Test]
        public void FromTexture_SpriteIsReleasedButUnityTextureSurvives()
        {
            V6AssetOwner.DestroyShared();
            Texture2D unityOwned = NewTexture(8);
            Sprite sprite = V6Sprites.FromTexture(unityOwned, "reentry-texture-lifetime");
            Assert.IsTrue(V6AssetOwner.IsSharedAsset(sprite));

            V6AssetOwner.DestroyShared();

            Assert.IsTrue(sprite == null, "FromTexture の Sprite が DestroyShared で解放されていない");
            Assert.IsFalse(unityOwned == null,
                "原 Texture は Unity 所有なので DestroyShared で破棄してはいけない");
        }
    }
}
