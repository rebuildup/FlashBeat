using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using FlashBeat.UI.V6.Common;

/// <summary>
/// #96 / #523: フレーム (1728×972) より小さい画面でのコンパクト配置。
/// V6CompactScreen.Place が通常解像度では従来の 1:1 中央配置を保ち、
/// コンパクト解像度ではパンビューポートで全要素を到達可能にすること、
/// および監査の ClassifyElementClip がパン配下を到達可能 (Inside) と
/// 分類することを固定する。
/// </summary>
public sealed class V6CompactScreenTests
{
    private GameObject root;
    private RectTransform frame;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("root", typeof(RectTransform));
        var rootRt = root.GetComponent<RectTransform>();
        rootRt.anchorMin = new Vector2(0.5f, 0.5f);
        rootRt.anchorMax = new Vector2(0.5f, 0.5f);
        rootRt.pivot = new Vector2(0.5f, 0.5f);
        rootRt.sizeDelta = new Vector2(1024f, 768f);

        var frameGo = new GameObject("V6 Test Frame 1728x972", typeof(RectTransform));
        frameGo.transform.SetParent(root.transform, false);
        frame = frameGo.GetComponent<RectTransform>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(root);
    }

    // --- IsCompact ---

    [Test]
    public void IsCompact_SmallScreens_AreCompact()
    {
        Assert.IsTrue(V6CompactScreen.IsCompact(1024, 768));
        Assert.IsTrue(V6CompactScreen.IsCompact(1280, 720));
        Assert.IsTrue(V6CompactScreen.IsCompact(1366, 768));
        Assert.IsTrue(V6CompactScreen.IsCompact(1600, 900));
        // 幅は足りるが高さが足りない場合もコンパクト。
        Assert.IsTrue(V6CompactScreen.IsCompact(2000, 900));
    }

    [Test]
    public void IsCompact_DesignResolutions_AreNotCompact()
    {
        Assert.IsFalse(V6CompactScreen.IsCompact(1920, 1080));
        Assert.IsFalse(V6CompactScreen.IsCompact(2560, 1440));
        Assert.IsFalse(V6CompactScreen.IsCompact(2560, 1664));
        // フレームと同寸法・それ以上は通常配置。
        Assert.IsFalse(V6CompactScreen.IsCompact(1728, 972));
        Assert.IsFalse(V6CompactScreen.IsCompact(1800, 1000));
    }

    // --- ClampPan / ClampAxis ---

    [Test]
    public void ClampAxis_ContentBiggerThanView_RangeIsViewMinusContentToZero()
    {
        // 1024×768 の表示域に 1728×972 のコンテンツ: x ∈ [-704, 0], y ∈ [-204, 0]。
        Assert.AreEqual(-704f, V6CompactScreen.ClampAxis(-9999f, 1024f, 1728f));
        Assert.AreEqual(0f, V6CompactScreen.ClampAxis(9999f, 1024f, 1728f));
        Assert.AreEqual(-704f, V6CompactScreen.ClampAxis(-704f, 1024f, 1728f));
        Assert.AreEqual(0f, V6CompactScreen.ClampAxis(0f, 1024f, 1728f));
        Assert.AreEqual(-204f, V6CompactScreen.ClampAxis(-9999f, 768f, 972f));
        Assert.AreEqual(0f, V6CompactScreen.ClampAxis(9999f, 768f, 972f));
    }

    [Test]
    public void ClampAxis_ContentSmallerThanView_Centers()
    {
        // 2000×900: 幅はコンテンツより広い → 中央 (2000-1728)/2 = 136。
        Assert.AreEqual(136f, V6CompactScreen.ClampAxis(0f, 2000f, 1728f));
        Assert.AreEqual(136f, V6CompactScreen.ClampAxis(5000f, 2000f, 1728f));
        // 高さはコンテンツより狭い → クランプ範囲 [-72, 0]。
        Assert.AreEqual(-72f, V6CompactScreen.ClampAxis(-9999f, 900f, 972f));
        Assert.AreEqual(0f, V6CompactScreen.ClampAxis(9999f, 900f, 972f));
    }

    /// <summary>
    /// 受入の中核: コンパクト解像度の全対象で、パン範囲の両端まで動かせば
    /// コンテンツの全域 (4 隅) が必ずビューポートに入る。
    /// </summary>
    [Test]
    public void ClampPan_CoversWholeContent_OnEveryCompactResolution()
    {
        int[][] sizes = { new[] { 1024, 768 }, new[] { 1280, 720 }, new[] { 1366, 768 }, new[] { 1600, 900 } };
        foreach (var s in sizes)
        {
            // 最小パン (左上を右下へ) = view - content: コンテンツ右下端が
            // ビューポート右下端に一致 → 右端・下端が見える。
            Vector2 min = V6CompactScreen.ClampPan(-1e9f, -1e9f, s[0], s[1]);
            Assert.AreEqual(s[0] - 1728f, min.x, 0.01f, $"w={s[0]}");
            Assert.AreEqual(s[1] - 972f, min.y, 0.01f, $"h={s[1]}");
            // 最大パン (0) = コンテンツ左上がビューポート左上 → 左端・上端が見える。
            Vector2 max = V6CompactScreen.ClampPan(1e9f, 1e9f, s[0], s[1]);
            Assert.AreEqual(0f, max.x, 0.01f);
            Assert.AreEqual(0f, max.y, 0.01f);
        }
    }

    // --- Place: 通常解像度 ---

    [Test]
    public void Place_NormalResolution_KeepsCenteredOrigin_NoViewport()
    {
        V6CompactScreen.Place(frame, 1920, 1080);

        Assert.IsNull(V6CompactScreen.FindViewport(frame));
        Assert.AreSame(root.transform, frame.parent);
        // anchor/pivot (0,1)・anchoredPosition = ToLocal(origin (96,54))。
        Assert.AreEqual(new Vector2(96f, -54f), frame.anchoredPosition);
        Assert.AreEqual(new Vector2(1728f, 972f), frame.sizeDelta);
        Assert.AreEqual(Vector3.one, frame.localScale);
    }

    [Test]
    public void Place_2KResolution_MatchesDesignOrigin()
    {
        V6CompactScreen.Place(frame, 2560, 1440);
        Assert.AreEqual(new Vector2(416f, -234f), frame.anchoredPosition);
    }

    // --- Place: コンパクト解像度 ---

    [Test]
    public void Place_CompactResolution_CreatesPanViewport()
    {
        V6CompactScreen.Place(frame, 1024, 768);

        var viewport = V6CompactScreen.FindViewport(frame);
        Assert.IsNotNull(viewport);
        Assert.AreEqual(V6CompactScreen.ViewportName, viewport.name);
        Assert.IsNotNull(viewport.GetComponent<RectMask2D>());
        var image = viewport.GetComponent<Image>();
        Assert.IsNotNull(image);
        Assert.IsTrue(image.raycastTarget);
        Assert.AreEqual(0f, image.color.a);
        var pan = viewport.GetComponent<V6FramePan>();
        Assert.IsNotNull(pan);
        Assert.AreSame(frame, pan.Content);
        Assert.AreSame(viewport, pan.Viewport);
        // フレームはビューポートの子でなければならない
        // (RectMask2D のクリップと監査のスクロール親判定が効く)。
        Assert.AreSame(viewport, frame.parent);
        // 初期パンは (0,0): 左上 (Header 側) が見える位置。
        Assert.AreEqual(Vector2.zero, frame.anchoredPosition);
    }

    [Test]
    public void Place_CompactResolution_IsIdempotent()
    {
        V6CompactScreen.Place(frame, 1024, 768);
        V6CompactScreen.Place(frame, 1024, 768);

        int viewports = 0;
        foreach (Transform child in root.transform)
        {
            if (child.name == V6CompactScreen.ViewportName) { viewports++; }
        }
        Assert.AreEqual(1, viewports);
    }

    [Test]
    public void Place_CompactThenNormal_RestoresFrameAndDestroysViewport()
    {
        V6CompactScreen.Place(frame, 1024, 768);
        Assert.IsNotNull(V6CompactScreen.FindViewport(frame));

        V6CompactScreen.Place(frame, 1920, 1080);

        Assert.IsNull(V6CompactScreen.FindViewport(frame));
        Assert.AreSame(root.transform, frame.parent);
        Assert.AreEqual(new Vector2(96f, -54f), frame.anchoredPosition);
        // ビューポート自体も消えている。
        Assert.IsNull(root.transform.Find(V6CompactScreen.ViewportName));
    }

    [Test]
    public void Place_CompactResolutionChange_ClampsPanIntoNewRange()
    {
        V6CompactScreen.Place(frame, 1024, 768);
        var pan = V6CompactScreen.FindViewport(frame).GetComponent<V6FramePan>();
        pan.Pan = new Vector2(-9999f, -9999f);
        // anchoredPosition は (pan.x, -pan.y)。
        Assert.AreEqual(new Vector2(-704f, 204f), frame.anchoredPosition);

        // 画面が変わるとパンは新しい範囲へクランプされる (1280×720 → x[-448,0] y[-252,0])。
        // x の -704 は範囲外なので -448 へ、y の -204 は範囲内なので維持される。
        V6CompactScreen.Place(frame, 1280, 720);
        Assert.AreEqual(new Vector2(-448f, 204f), frame.anchoredPosition);
    }

    // --- V6FramePan ---

    [Test]
    public void FramePan_Pan_ClampsAndAppliesToContent()
    {
        V6CompactScreen.Place(frame, 1024, 768);
        var pan = V6CompactScreen.FindViewport(frame).GetComponent<V6FramePan>();

        pan.Pan = new Vector2(5000f, 5000f);
        Assert.AreEqual(new Vector2(0f, 0f), pan.Pan);
        Assert.AreEqual(new Vector2(0f, 0f), frame.anchoredPosition);

        pan.Pan = new Vector2(-5000f, -5000f);
        Assert.AreEqual(new Vector2(-704f, -204f), pan.Pan);
        Assert.AreEqual(new Vector2(-704f, 204f), frame.anchoredPosition);

        pan.Pan = new Vector2(-352f, -102f);
        Assert.AreEqual(new Vector2(-352f, 102f), frame.anchoredPosition);
    }

    // --- 監査分類: パン配下は画面外でも到達可能 (Inside) ---

    [Test]
    public void ClassifyElementClip_InsidePanViewport_IsReachable()
    {
        V6CompactScreen.Place(frame, 1024, 768);

        // フッター相当: design (24, 900) に 300×60 の要素。画面 (0,0,1024,768) では
        // 完全に画面外だが、パンで到達できるので Inside でなければならない。
        var elGo = new GameObject("FooterButton", typeof(RectTransform));
        elGo.transform.SetParent(frame, false);
        var elRt = elGo.GetComponent<RectTransform>();
        V6Layout.Place(elRt, new Vector2(24f, 900f), new Vector2(300f, 60f));
        try
        {
            var screen = new Rect(0f, 0f, 1024f, 768f);
            var clip = V6ScreenAudit.ClassifyElementClip(
                elGo.transform, root.transform, elRt, screen);
            Assert.AreEqual(V6ScreenAudit.ScreenClip.Inside, clip);
        }
        finally
        {
            Object.DestroyImmediate(elGo);
        }
    }

    [Test]
    public void ClassifyElementClip_OutsidePanContent_IsOutside()
    {
        V6CompactScreen.Place(frame, 1024, 768);

        // コンテンツ (フレーム 1728×972) の外に置かれた要素はパンでも届かない。
        var elGo = new GameObject("Outsider", typeof(RectTransform));
        elGo.transform.SetParent(root.transform, false);
        var elRt = elGo.GetComponent<RectTransform>();
        elRt.anchorMin = new Vector2(0f, 1f);
        elRt.anchorMax = new Vector2(0f, 1f);
        elRt.pivot = new Vector2(0f, 1f);
        elRt.anchoredPosition = new Vector2(2000f, -100f);
        elRt.sizeDelta = new Vector2(100f, 50f);
        try
        {
            var screen = new Rect(0f, 0f, 1024f, 768f);
            var clip = V6ScreenAudit.ClassifyElementClip(
                elGo.transform, root.transform, elRt, screen);
            Assert.AreEqual(V6ScreenAudit.ScreenClip.Outside, clip);
        }
        finally
        {
            Object.DestroyImmediate(elGo);
        }
    }

    [Test]
    public void ClassifyElementClip_NoScrollAncestor_UsesScreen()
    {
        // パンもスクロール親も無い要素は従来どおり実画面が基準。
        var elGo = new GameObject("Plain", typeof(RectTransform));
        elGo.transform.SetParent(root.transform, false);
        var elRt = elGo.GetComponent<RectTransform>();
        elRt.anchorMin = new Vector2(0f, 1f);
        elRt.anchorMax = new Vector2(0f, 1f);
        elRt.pivot = new Vector2(0f, 1f);
        elRt.anchoredPosition = new Vector2(0f, -900f);
        elRt.sizeDelta = new Vector2(100f, 50f);
        try
        {
            var screen = new Rect(0f, 0f, 1024f, 768f);
            var clip = V6ScreenAudit.ClassifyElementClip(
                elGo.transform, root.transform, elRt, screen);
            Assert.AreEqual(V6ScreenAudit.ScreenClip.Outside, clip);
        }
        finally
        {
            Object.DestroyImmediate(elGo);
        }
    }

    // --- V6FramePan 入力 (#550) ---

    private V6FramePan NewPan()
    {
        var go = new GameObject("pan", typeof(RectTransform), typeof(V6FramePan));
        var pan = go.GetComponent<V6FramePan>();
        pan.ViewportSize = new Vector2(1024f, 768f);
        return pan;
    }

    [Test]
    public void OnScroll_WheelUp_MovesTowardContentStart()
    {
        // pan.y は content 上端の y_down 位置 (0=先頭)。ホイール上
        // (scrollDelta.y>0) はリストと同じく先頭方向 = pan.y 増加。
        var pan = NewPan();
        try
        {
            pan.Pan = new Vector2(0f, -120f);
            pan.OnScroll(new PointerEventData(null) { scrollDelta = new Vector2(0f, 1f) });
            Assert.AreEqual(0f, pan.Pan.y, 0.01f, "wheel up pans toward the top of the content");

            pan.Pan = new Vector2(0f, -120f);
            pan.OnScroll(new PointerEventData(null) { scrollDelta = new Vector2(0f, -1f) });
            Assert.AreEqual(-204f, pan.Pan.y, 0.01f,
                "wheel down pans toward the bottom of the content (clamped at view-content)");
        }
        finally
        {
            Object.DestroyImmediate(pan.gameObject);
        }
    }

    [Test]
    public void OnScroll_HorizontalDelta_MovesRightContentIntoView()
    {
        var pan = NewPan();
        try
        {
            pan.Pan = Vector2.zero;
            pan.OnScroll(new PointerEventData(null) { scrollDelta = new Vector2(1f, 0f) });
            Assert.AreEqual(-120f, pan.Pan.x, 0.01f,
                "scroll right reveals the right side of the content");
        }
        finally
        {
            Object.DestroyImmediate(pan.gameObject);
        }
    }

    [Test]
    public void OnBeginDrag_OnInputField_DoesNotStartPanning()
    {
        // テキスト入力上のドラッグは文字選択が優先。パンを始めてはいけない。
        var pan = NewPan();
        var inputGo = new GameObject("input", typeof(RectTransform), typeof(InputField));
        try
        {
            var e = new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Left,
                pointerPress = inputGo,
            };
            pan.OnBeginDrag(e);
            pan.OnDrag(e);
            Assert.AreEqual(Vector2.zero, pan.Pan, "drag on an InputField must not pan the frame");
        }
        finally
        {
            Object.DestroyImmediate(inputGo);
            Object.DestroyImmediate(pan.gameObject);
        }
    }

    /// <summary>
    /// #560: uGUI は <see cref="IEndDragHandler"/> 経由でしか OnEndDrag を
    /// 呼ばない。interface が無いとドラッグ終了後も dragging が true のまま
    /// 残り、PageUp/PageDown パンが恒久的に無効化される。
    /// </summary>
    [Test]
    public void DragLifecycle_EndDragReleasesPanning()
    {
        var pan = NewPan();
        try
        {
            Assert.IsInstanceOf<IEndDragHandler>(pan,
                "V6FramePan must implement IEndDragHandler or OnEndDrag is never called");

            var e = new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Left,
                pointerPress = pan.gameObject,
            };
            pan.OnBeginDrag(e);
            Assert.IsTrue(pan.DraggingForAudit);

            ((IEndDragHandler)pan).OnEndDrag(e);
            Assert.IsFalse(pan.DraggingForAudit,
                "dragging must be released on drag end so PageUp/PageDown work again");
        }
        finally
        {
            Object.DestroyImmediate(pan.gameObject);
        }
    }

    [Test]
    public void OnBeginDrag_NonLeftButton_DoesNotStartPanning()
    {
        var pan = NewPan();
        try
        {
            var e = new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Right,
                pointerPress = pan.gameObject,
            };
            pan.OnBeginDrag(e);
            pan.OnDrag(e);
            Assert.AreEqual(Vector2.zero, pan.Pan);
        }
        finally
        {
            Object.DestroyImmediate(pan.gameObject);
        }
    }

    /// <summary>
    /// #609: モーダル表示中はパンを止める (Dialog がフレームの子なので、
    /// パンを許すとモーダルごと画面外へ動く)。PanSuspended=true の間は
    /// ホイール / ドラッグ / キーのすべてが無効。
    /// </summary>
    [Test]
    public void PanSuspended_BlocksScrollDragAndKeyPan()
    {
        var pan = NewPan();
        try
        {
            pan.Pan = new Vector2(-352f, -102f);
            Vector2 before = pan.Pan;
            pan.PanSuspended = true;

            pan.OnScroll(new PointerEventData(null) { scrollDelta = new Vector2(0f, 1f) });
            Assert.AreEqual(before, pan.Pan, "scroll must be ignored while suspended");

            var e = new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Left,
                pointerPress = pan.gameObject,
            };
            pan.OnBeginDrag(e);
            Assert.IsFalse(pan.DraggingForAudit, "drag must not start while suspended");

            pan.PanSuspended = false;
            pan.OnScroll(new PointerEventData(null) { scrollDelta = new Vector2(0f, 1f) });
            Assert.AreNotEqual(before, pan.Pan, "scroll works again after resume");
        }
        finally
        {
            Object.DestroyImmediate(pan.gameObject);
        }
    }
}
