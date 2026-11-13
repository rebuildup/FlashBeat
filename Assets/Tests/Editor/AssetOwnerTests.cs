using FlashBeat.UI.V6;
using NUnit.Framework;
using UnityEngine;

namespace FlashBeat.Tests
{
    /// <summary>
    /// Issue #398 / #401: 実行時生成 native オブジェクトの所有者。
    ///
    /// 何を固定するか:
    ///   - 共有アセットは 1 度だけ生成され、同じキーでは同じ実体
    ///   - スコープは「自分が作ったものだけ」を破棄する
    ///   - 共有アセットはスコープに巻き込まれない
    ///   - Dispose 後に足したものは放置せず、その場で破棄する (リークを作らない)
    /// </summary>
    public class AssetOwnerTests
    {
        /// <summary>テストで生成した native リソース。プロセスが shared 参照を追跡しないので自分で破棄する。</summary>
        private readonly System.Collections.Generic.List<Object> created =
            new System.Collections.Generic.List<Object>();

        [TearDown]
        public void TearDown()
        {
            V6AssetOwner.DestroyShared();
            for (int i = 0; i < created.Count; i++)
            {
                Object.DestroyImmediate(created[i]);
            }

            created.Clear();
        }

        private Texture2D CreatedTexture(int size = 4)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            created.Add(tex);
            return tex;
        }

        [Test]
        public void Shared_CreatesOnlyOnce()
        {
            Texture2D sharedTexture = CreatedTexture(4);
            int created = 0;
            Sprite first = V6AssetOwner.Shared("test/sprite", () =>
            {
                created++;
                return Sprite.Create(sharedTexture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 100f);
            });
            Sprite second = V6AssetOwner.Shared("test/sprite", () =>
            {
                created++;
                return Sprite.Create(sharedTexture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 100f);
            });

            Assert.IsNotNull(first);
            Assert.AreSame(first, second, "同じキーで別実体を作っている");
            Assert.AreEqual(1, created, "生成関数が 2 回呼ばれている");
        }

        [Test]
        public void Shared_DoesNotCacheNull()
        {
            Sprite first = V6AssetOwner.Shared<Sprite>("test/missing", () => null);
            Assert.IsNull(first);

            bool called = false;
            Sprite second = V6AssetOwner.Shared<Sprite>("test/missing", () =>
            {
                called = true;
                return null;
            });

            Assert.IsNull(second);
            Assert.IsTrue(called, "null を固定すると素材が後から来ても回復できない");
        }

        [Test]
        public void Shared_ReportsWhetherItExists()
        {
            Assert.IsFalse(V6AssetOwner.HasShared("test/has"));
            V6AssetOwner.Shared("test/has", () => CreatedTexture(2));
            Assert.IsTrue(V6AssetOwner.HasShared("test/has"));
        }

        [Test]
        public void Scope_DisposesOnlyWhatItOwns()
        {
            var shared = V6AssetOwner.Shared("test/shared", () => CreatedTexture(2));
            Assert.IsNotNull(shared);

            Sprite own;
            using (var scope = V6AssetOwner.OpenScope("test view"))
            {
                var texture = scope.Track(new Texture2D(4, 4));
                own = scope.Track(Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 100f));
                Assert.AreEqual(2, scope.Count);
            }

            Assert.IsTrue(own == null, "スコープを閉じたら自分が作った Sprite は破棄される");
            Assert.IsTrue(shared != null, "共有アセットはスコープに巻き込まれない");
        }

        [Test]
        public void Scope_TrackAfterDisposeDoesNotLeak()
        {
            var scope = V6AssetOwner.OpenScope("closed view");
            scope.Dispose();

            var texture = scope.Track(CreatedTexture(2));

            Assert.IsNull(texture, "Dispose 後の Track は所有しない (破棄して返す)");
            Assert.AreEqual(0, scope.Count);
        }

        [Test]
        public void Scope_ReleaseHandsOverOwnership()
        {
            var texture = CreatedTexture(2);
            var scope = V6AssetOwner.OpenScope("handover");
            scope.Track(texture);
            Assert.AreEqual(1, scope.Count);

            scope.Release(texture);
            Assert.AreEqual(0, scope.Count);
            scope.Dispose();

            Assert.IsTrue(texture != null, "所有権を手放したものを破棄している");
            Object.DestroyImmediate(texture);
        }

        [Test]
        public void Scope_DisposeIsIdempotent()
        {
            var scope = V6AssetOwner.OpenScope("twice");
            scope.Track(CreatedTexture(2));
            scope.Dispose();
            Assert.DoesNotThrow(() => scope.Dispose());
            Assert.IsTrue(scope.IsDisposed);
        }
    }
}
