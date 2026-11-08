using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// v6 画面キャプチャ機械チェック (AutoTestV6Screens) の判定式を固定する
/// EditMode テスト。フレーム origin 計算 / 矩形内包判定 (許容誤差 1px) /
/// スクロール親判定 / Unicode 記号アイコン検出を検証する。
/// 実機走査そのものはプレイヤー (-autotest v6screens) で行う。
/// </summary>
public sealed class V6FrameContainmentTests
{
    // --- フレーム origin (契約: ((w-1728)/2, (h-972)/2) 切捨て) ---

    [Test]
    public void FrameOrigin_FHD_Is96_54()
    {
        Assert.AreEqual(new Vector2(96f, 54f), V6ScreenAudit.FrameOrigin(1920, 1080));
    }

    [Test]
    public void FrameOrigin_2K_Is416_234()
    {
        Assert.AreEqual(new Vector2(416f, 234f), V6ScreenAudit.FrameOrigin(2560, 1440));
    }

    [Test]
    public void FrameOrigin_Mac2560x1664_Is416_346()
    {
        Assert.AreEqual(new Vector2(416f, 346f), V6ScreenAudit.FrameOrigin(2560, 1664));
    }

    [Test]
    public void FrameOrigin_OddResolution_Floors()
    {
        // (2000-1728)/2 = 136, (1100-972)/2 = 64 → ちょうど割れる case は
        // 奇数幅で床切りを確認する: (2001-1728)/2 = 136.5 → 136。
        Assert.AreEqual(136f, V6ScreenAudit.FrameOrigin(2001, 1100).x);
        Assert.AreEqual(64f, V6ScreenAudit.FrameOrigin(2001, 1100).y);
    }

    [Test]
    public void FrameOrigin_ScreenSmallerThanFrame_ClampsToZero()
    {
        Assert.AreEqual(new Vector2(0f, 0f), V6ScreenAudit.FrameOrigin(1280, 720));
    }

    // --- 矩形内包判定 (許容誤差 1px) ---

    [Test]
    public void Contains_ExactFit_Passes()
    {
        var outer = new Rect(96f, 54f, 1728f, 972f);
        Assert.IsTrue(V6ScreenAudit.Contains(outer, new Rect(96f, 54f, 1728f, 972f), V6ScreenAudit.TolerancePx));
    }

    [Test]
    public void Contains_InsideByOnePx_Passes()
    {
        var outer = new Rect(96f, 54f, 1728f, 972f);
        Assert.IsTrue(V6ScreenAudit.Contains(outer, new Rect(97f, 55f, 1726f, 970f), V6ScreenAudit.TolerancePx));
    }

    [Test]
    public void Contains_WithinTolerance_Passes()
    {
        // 1px はみ出しは許容 (許容誤差 1px)。
        var outer = new Rect(96f, 54f, 1728f, 972f);
        Assert.IsTrue(V6ScreenAudit.Contains(outer, new Rect(95f, 54f, 1730f, 972f), V6ScreenAudit.TolerancePx));
    }

    [Test]
    public void Contains_BeyondTolerance_Fails()
    {
        var outer = new Rect(96f, 54f, 1728f, 972f);
        // 2px はみ出しは各辺で fail。
        Assert.IsFalse(V6ScreenAudit.Contains(outer, new Rect(94f, 54f, 1728f, 972f), V6ScreenAudit.TolerancePx));
        Assert.IsFalse(V6ScreenAudit.Contains(outer, new Rect(96f, 52f, 1728f, 972f), V6ScreenAudit.TolerancePx));
        Assert.IsFalse(V6ScreenAudit.Contains(outer, new Rect(96f, 54f, 1731f, 972f), V6ScreenAudit.TolerancePx));
        Assert.IsFalse(V6ScreenAudit.Contains(outer, new Rect(96f, 54f, 1728f, 975f), V6ScreenAudit.TolerancePx));
    }

    [Test]
    public void Contains_ZeroTolerance_IsStrict()
    {
        var outer = new Rect(0f, 0f, 1920f, 1080f);
        Assert.IsTrue(V6ScreenAudit.Contains(outer, new Rect(0f, 0f, 1920f, 1080f), 0f));
        Assert.IsFalse(V6ScreenAudit.Contains(outer, new Rect(-1f, 0f, 1920f, 1080f), 0f));
    }

    [Test]
    public void Overshoot_ReportsMaxOverflow()
    {
        var outer = new Rect(0f, 0f, 100f, 100f);
        Assert.AreEqual(0f, V6ScreenAudit.Overshoot(outer, new Rect(10f, 10f, 50f, 50f)));
        // 右に 5px、上に 2px はみ出す → 最大は 5。
        Assert.AreEqual(5f, V6ScreenAudit.Overshoot(outer, new Rect(10f, 10f, 95f, 92f)));
        // 左に 3px はみ出す。
        Assert.AreEqual(3f, V6ScreenAudit.Overshoot(outer, new Rect(-3f, 0f, 50f, 50f)));
    }

    // --- WorldRect (RectTransform → 矩形) ---

    [Test]
    public void WorldRect_CoversRectTransformBounds()
    {
        var go = new GameObject("rt", typeof(RectTransform));
        try
        {
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(100f, 50f);
            var rect = V6ScreenAudit.WorldRect(rt);
            Assert.AreEqual(100f, rect.width);
            Assert.AreEqual(50f, rect.height);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void WorldRect_ScaledParent_IsReflected()
    {
        var parent = new GameObject("parent", typeof(RectTransform));
        var child = new GameObject("child", typeof(RectTransform));
        child.transform.SetParent(parent.transform, false);
        try
        {
            parent.GetComponent<RectTransform>().sizeDelta = new Vector2(1728f, 972f);
            parent.GetComponent<RectTransform>().localScale = new Vector3(1.5f, 1.5f, 1f);
            child.GetComponent<RectTransform>().anchorMin = Vector2.zero;
            child.GetComponent<RectTransform>().anchorMax = Vector2.zero;
            child.GetComponent<RectTransform>().sizeDelta = new Vector2(100f, 100f);

            var rect = V6ScreenAudit.WorldRect(child.GetComponent<RectTransform>());
            Assert.AreEqual(150f, rect.width, 0.01f);
            Assert.AreEqual(150f, rect.height, 0.01f);
        }
        finally
        {
            Object.DestroyImmediate(child);
            Object.DestroyImmediate(parent);
        }
    }

    // --- Canvas 分類 ---

    [Test]
    public void IsV6Canvas_MatchesNamePrefix()
    {
        var go = new GameObject("V6 Select Canvas", typeof(Canvas));
        var legacy = new GameObject("Canvas", typeof(Canvas));
        try
        {
            Assert.IsTrue(V6ScreenAudit.IsV6Canvas(go.GetComponent<Canvas>()));
            Assert.IsFalse(V6ScreenAudit.IsV6Canvas(legacy.GetComponent<Canvas>()));
            Assert.IsFalse(V6ScreenAudit.IsV6Canvas(null));
        }
        finally
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(legacy);
        }
    }

    [Test]
    public void IsFullScreenCanvas_MatchesHudAndDim()
    {
        var hud = new GameObject("V6GameHudCanvas", typeof(Canvas));
        var dim = new GameObject("V6GameDimCanvas", typeof(Canvas));
        var frame = new GameObject("V6 Select Canvas", typeof(Canvas));
        try
        {
            Assert.IsTrue(V6ScreenAudit.IsFullScreenCanvas(hud.GetComponent<Canvas>()));
            Assert.IsTrue(V6ScreenAudit.IsFullScreenCanvas(dim.GetComponent<Canvas>()));
            Assert.IsFalse(V6ScreenAudit.IsFullScreenCanvas(frame.GetComponent<Canvas>()));
        }
        finally
        {
            Object.DestroyImmediate(hud);
            Object.DestroyImmediate(dim);
            Object.DestroyImmediate(frame);
        }
    }

    // --- スクロール親判定 ---

    [Test]
    public void IsScrollContainer_MatchesClipComponentsAndNames()
    {
        var clipped = new GameObject("clipped", typeof(RectTransform), typeof(RectMask2D));
        var masked = new GameObject("masked", typeof(RectTransform), typeof(Mask));
        var named = new GameObject("SongList", typeof(RectTransform));
        var plain = new GameObject("Header", typeof(RectTransform));
        try
        {
            Assert.IsTrue(V6ScreenAudit.IsScrollContainer(clipped.transform));
            Assert.IsTrue(V6ScreenAudit.IsScrollContainer(masked.transform));
            Assert.IsTrue(V6ScreenAudit.IsScrollContainer(named.transform));
            Assert.IsFalse(V6ScreenAudit.IsScrollContainer(plain.transform));
            Assert.IsFalse(V6ScreenAudit.IsScrollContainer(null));
        }
        finally
        {
            Object.DestroyImmediate(clipped);
            Object.DestroyImmediate(masked);
            Object.DestroyImmediate(named);
            Object.DestroyImmediate(plain);
        }
    }

    [Test]
    public void FindScrollAncestor_WalksUpToRoot()
    {
        var root = new GameObject("root", typeof(RectTransform));
        var list = new GameObject("SongList", typeof(RectTransform));
        var row = new GameObject("Row", typeof(RectTransform));
        list.transform.SetParent(root.transform, false);
        row.transform.SetParent(list.transform, false);
        try
        {
            Assert.AreEqual(list.transform, V6ScreenAudit.FindScrollAncestor(row.transform, root.transform));
            Assert.IsNull(V6ScreenAudit.FindScrollAncestor(list.transform, root.transform));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    // --- Unicode 記号アイコン検出 ---

    [Test]
    public void IsUnicodeSymbolChar_DetectsIconGlyphs()
    {
        Assert.IsTrue(V6ScreenAudit.IsUnicodeSymbolChar('♪'));   // U+266A 音符
        Assert.IsTrue(V6ScreenAudit.IsUnicodeSymbolChar('→'));   // U+2192 矢印
        Assert.IsTrue(V6ScreenAudit.IsUnicodeSymbolChar('★'));   // U+2605 星
        Assert.IsTrue(V6ScreenAudit.IsUnicodeSymbolChar('①'));   // U+2460 囲み数字
        Assert.IsTrue(V6ScreenAudit.IsUnicodeSymbolChar('•'));   // U+2022 ブレット
        Assert.IsTrue(V6ScreenAudit.IsUnicodeSymbolChar('\uFE0F')); // バリエーションセレクタ
    }

    [Test]
    public void IsUnicodeSymbolChar_AcceptsTextAndJapanese()
    {
        Assert.IsFalse(V6ScreenAudit.IsUnicodeSymbolChar('A'));
        Assert.IsFalse(V6ScreenAudit.IsUnicodeSymbolChar('0'));
        Assert.IsFalse(V6ScreenAudit.IsUnicodeSymbolChar('あ'));
        Assert.IsFalse(V6ScreenAudit.IsUnicodeSymbolChar('漢'));
        Assert.IsFalse(V6ScreenAudit.IsUnicodeSymbolChar('、'));
    }
}
