using System.Collections.Generic;
using FlashBeat.UI.V6;
using NUnit.Framework;
using UnityEngine;

namespace FlashBeat.Tests
{
    /// <summary>
    /// Issue #398: ジャケット画像の取得・所有・キャッシュを 1 箇所に固定する。
    ///
    /// 一次監査では View ごとに独立した <c>Dictionary&lt;string, Sprite&gt;</c> +
    /// <c>Resources.Load</c> + <c>Sprite.Create</c> があり、同じ画像が画面数だけ
    /// 実体化されていた。ここでは「同じ曲は同じ Sprite」「無い曲は何度も
    /// Resources を叩かない」「crop は原Texture と矩形で共有」を固定する。
    /// </summary>
    public class JacketProviderTests
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

        [Test]
        public void Local_ReturnsTheSameInstanceForTheSameSong()
        {
            Sprite first = V6JacketProvider.Local(10, "チュートリアル");
            Assert.IsNotNull(first, "テスト用ジャケット (10) が Resources に無い");

            int loads = V6JacketProvider.LocalLoadCount;
            Sprite second = V6JacketProvider.Local(10, "チュートリアル");

            Assert.AreSame(first, second, "同じ曲に別の Sprite を返している");
            Assert.AreEqual(loads, V6JacketProvider.LocalLoadCount,
                "キャッシュに当たったのに Resources から読み直している");
        }

        [Test]
        public void Local_NegativeResultIsRemembered()
        {
            // 対応表に無い曲 (存在しない index)。
            Assert.IsNull(V6JacketProvider.Local(9999, "存在しない曲"));

            Sprite again = V6JacketProvider.Local(9999, "存在しない曲");
            Assert.IsNull(again);
        }

        [Test]
        public void Local_IsSharedAcrossViewsThatUsedToHaveOwnCaches()
        {
            // History / Ranking / Select / Result は以前それぞれ別の辞書を持っていた。
            // いまは同じ provider を通るので、同一曲は同一実体になる。
            Sprite viaResources = V6JacketResources.LoadSprite(10, "チュートリアル");
            Sprite viaProvider = V6JacketProvider.Local(10, "チュートリアル");

            Assert.AreSame(viaResources, viaProvider,
                "V6JacketResources と V6JacketProvider が別実体を返している");
        }

        [Test]
        public void Crop_ReturnsTheSameInstanceForTheSameTextureAndRect()
        {
            Texture2D texture = V6JacketProvider.LocalTexture(10, "チュートリアル");
            Assert.IsNotNull(texture);

            Rect rect = V6JacketProvider.CoverCropRect(texture.width, texture.height);
            Sprite first = V6JacketProvider.Crop(texture, rect, "cover");
            Sprite second = V6JacketProvider.Crop(texture, rect, "cover");

            Assert.AreSame(first, second, "同じ原Texture + 同じ矩形で別 Sprite を作っている");
        }

        [Test]
        public void SquareCrop_UsesTheSameTextureButASeparateVariant()
        {
            Texture2D texture = V6JacketProvider.LocalTexture(10, "チュートリアル");
            Assert.IsNotNull(texture);

            Sprite full = V6JacketProvider.Local(10, "チュートリアル");
            Sprite square = V6JacketProvider.SquareCrop(texture, "square");

            Assert.IsNotNull(square);
            Assert.AreNotSame(full, square, "同じ画像を無駄に再生成してはいけないが、crop は別物");
            Assert.Less(square.rect.width, full.rect.width + 1f,
                "正方形 crop は元画像より大きくならない");
            Assert.AreEqual(square.rect.width, square.rect.height, 1f,
                "mode:cover の切り抜きは正方形");
        }

        [Test]
        public void CoverCropRect_MatchesTheLoadingViewRule()
        {
            // V6LoadingViewLogic の規則と provider の規則がずれると、
            // ローディングの見た目が静かに変わる。同じ値を固定する。
            Rect fromLogic = FlashBeat.UI.V6.Game.Loading.V6LoadingViewLogic.CoverCropRect(320, 180);
            Rect fromProvider = V6JacketProvider.CoverCropRect(320, 180);

            Assert.AreEqual(fromLogic, fromProvider);
            Assert.AreEqual(new Rect(70f, 0f, 180f, 180f), fromProvider);
        }

        [Test]
        public void CoverCropRect_ReturnsZeroForInvalidSize()
        {
            Assert.AreEqual(new Rect(0f, 0f, 0f, 0f), V6JacketProvider.CoverCropRect(0, 180));
            Assert.AreEqual(new Rect(0f, 0f, 0f, 0f), V6JacketProvider.CoverCropRect(320, -1));
        }

        [Test]
        public void RequestRemote_WithEmptyIdReportsFailureWithoutHttp()
        {
            Sprite received = null;
            bool called = false;
            V6JacketProvider.RequestRemote("", sprite =>
            {
                called = true;
                received = sprite;
            });

            Assert.IsTrue(called, "空の ID でも呼び出し側は待たされない");
            Assert.IsNull(received);
            Assert.AreEqual(0, V6JacketProvider.RemoteFetchCount, "空の ID で HTTP を起こしている");
        }

        [Test]
        public void InvalidateLocal_DropsSpritesButKeepsOwnershipSane()
        {
            Sprite first = V6JacketProvider.Local(10, "チュートリアル");
            Assert.IsNotNull(first);

            V6JacketProvider.InvalidateLocal();
            Sprite second = V6JacketProvider.Local(10, "チュートリアル");

            Assert.IsNotNull(second);
            Assert.AreNotSame(first, second, "無効化したら次の読み込みで引き直す");
        }

        [Test]
        public void DestroyAll_LeavesNoCachedSprites()
        {
            V6JacketProvider.Local(10, "チュートリアル");
            V6JacketProvider.DestroyAll();

            Assert.AreEqual(0, V6JacketProvider.CachedSpriteCount);
        }

        /// <summary>
        /// 画面ごとに独自キャッシュへ戻らないことを、ソースの構造で固定する。
        /// (index で辞書を持つ View が復活したら落ちる。)
        /// </summary>
        [Test]
        public void Views_DoNotKeepTheirOwnJacketDictionaries()
        {
            string[] files =
            {
                "Game/Scripts/UI/V6/History/V6HistoryView.cs",
                "Game/Scripts/UI/V6/Ranking/V6RankingView.cs",
            };

            var offenders = new List<string>();
            for (int i = 0; i < files.Length; i++)
            {
                string path = System.IO.Path.Combine(Application.dataPath, files[i].Replace('/', System.IO.Path.DirectorySeparatorChar));
                if (!System.IO.File.Exists(path))
                {
                    continue;
                }

                string src = System.IO.File.ReadAllText(path);
                if (src.Contains("Dictionary<string, Sprite> jacketCache"))
                {
                    offenders.Add(files[i]);
                }
            }

            Assert.IsEmpty(offenders,
                "ジャケットの独自キャッシュが復活している (V6JacketProvider に集約する): " + string.Join(", ", offenders));
        }
    }
}
