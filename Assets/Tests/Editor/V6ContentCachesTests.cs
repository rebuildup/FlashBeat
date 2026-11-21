using FlashBeat.UI.V6;
using NUnit.Framework;

/// <summary>
/// プロセス内コンテンツ キャッシュ (Issue #377) の振る舞い。
///
/// Issue #823: ジャケットはローカル Resources から撤去し、YouTube
/// サムネイルの遠隔キャッシュ (LRU 上限付き、<see cref="V6JacketProvider"/>)
/// に一本化した。<see cref="V6ContentCaches.InvalidateAll"/> の対象は
/// Resources 由来の譜面メタだけになった。
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
    public void InvalidateAll_LeavesEveryCacheEmpty()
    {
        V6ContentCaches.InvalidateAll();

        Assert.AreEqual(0, FlashBeat.UI.V6.Select.Data.V6SelectDataSource.ChartMetaCacheCount);
    }

    /// <summary>
    /// ジャケットの遠隔キャッシュは InvalidateAll の対象外
    /// (Resources 由来ではなく、LRU 上限で管理される)。
    /// </summary>
    [Test]
    public void InvalidateAll_KeepsTheRemoteJacketCache()
    {
        V6JacketProvider.DestroyAll();
        var texture = new UnityEngine.Texture2D(4, 4, UnityEngine.TextureFormat.RGBA32, false);
        try
        {
            V6JacketProvider.SeedRemoteEntryForAudit("audit-video", texture, 0f);
            Assert.AreEqual(1, V6ContentCaches.JacketCount);

            V6ContentCaches.InvalidateAll();

            Assert.AreEqual(1, V6ContentCaches.JacketCount,
                "遠隔ジャケットキャッシュは InvalidateAll で消えてはいけない");
        }
        finally
        {
            V6JacketProvider.DestroyAll();
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
