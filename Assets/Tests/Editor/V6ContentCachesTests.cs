using FlashBeat.UI.V6;
using NUnit.Framework;

/// <summary>
/// プロセス内コンテンツ キャッシュ (Issue #377) の振る舞い。
///
/// 同じ曲のジャケットは同じ Sprite を返し、無効化すると次の読み込みで
/// 実ファイルから引き直すこと。無効化の入口が <see cref="V6ContentCaches"/> の
/// 1 箇所であることを固定する。
/// </summary>
public class V6ContentCachesTests
{
    [SetUp]
    [TearDown]
    public void ResetCaches()
    {
        V6ContentCaches.InvalidateAll();
    }

    [Test]
    public void LoadSprite_ReturnsTheSameInstanceForTheSameSong()
    {
        UnityEngine.Sprite first = V6JacketResources.LoadSprite(10, "チュートリアル");
        Assert.IsNotNull(first, "テスト用ジャケット (10) が Resources に無い");

        int loadsAfterFirst = V6JacketResources.TextureLoadCount;
        UnityEngine.Sprite second = V6JacketResources.LoadSprite(10, "チュートリアル");

        Assert.AreSame(first, second);
        Assert.AreEqual(loadsAfterFirst, V6JacketResources.TextureLoadCount,
            "キャッシュに当たったのに Resources から読み直している");
    }

    [Test]
    public void InvalidateAll_MakesTheNextLoadReadAgain()
    {
        V6JacketResources.LoadSprite(10, "チュートリアル");
        int loadsBefore = V6JacketResources.TextureLoadCount;

        V6ContentCaches.InvalidateAll();
        Assert.AreEqual(0, V6ContentCaches.JacketCount);

        V6JacketResources.LoadSprite(10, "チュートリアル");
        Assert.AreEqual(loadsBefore + 1, V6JacketResources.TextureLoadCount,
            "無効化したのに Resources を読み直していない");
    }

    [Test]
    public void InvalidateAll_LeavesEveryCacheEmpty()
    {
        V6ContentCaches.InvalidateAll();

        Assert.AreEqual(0, V6JacketResources.CachedSpriteCount);
        Assert.AreEqual(0, FlashBeat.UI.V6.Select.Data.V6SelectDataSource.ChartMetaCacheCount);
        Assert.AreEqual(0, FlashBeat.UI.V6.Select.Data.V6SelectDataSource.ChartExistsCacheCount);
    }

    /// <summary>
    /// 上限の見積り: キャッシュに載るのはジャケット対応表にある曲だけ。
    /// 対応表の外の曲を渡しても増えない。
    /// </summary>
    [Test]
    public void JacketCache_OnlyHoldsSongsInTheTable()
    {
        V6ContentCaches.InvalidateAll();

        // 対応表に無い index (27 以降) はジャケットを持たない。
        Assert.IsNull(V6JacketResources.LoadSprite(999, ""));
        Assert.AreEqual(0, V6JacketResources.CachedSpriteCount);

        for (int id = 1; id <= 26; id++)
        {
            V6JacketResources.LoadSprite(id, "");
        }

        Assert.LessOrEqual(V6JacketResources.CachedSpriteCount, 26,
            "ジャケットのキャッシュが対応表の曲数を超えている");
    }
}
