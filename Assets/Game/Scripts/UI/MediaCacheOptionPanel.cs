using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using FlashBeat.Gameplay;

/// <summary>
/// Shows how much space the downloaded YouTube media (the media cache) uses and
/// offers a button to delete it. The panel is created at runtime on the Option
/// screen, so no scene or prefab has to be touched.
///
/// The labels are ASCII on purpose: the font that the HUD uses (LiberationSans
/// SDF) has no Japanese glyphs - the missing Japanese font is tracked in issue
/// #27 - and a Japanese label would show up as tofu boxes.
/// </summary>
public class MediaCacheOptionPanel : MonoBehaviour
{
    const string PanelName = "MediaCachePanel";

    Text sizeLabel;
    Text statusLabel;
    Button clearButton;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void RegisterSceneHook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "OptionScene") return;
        if (FindFirstObjectByType<MediaCacheOptionPanel>() != null) return;

        var go = new GameObject(PanelName);
        go.AddComponent<MediaCacheOptionPanel>();
    }

    void Start()
    {
        BuildUi();
        Refresh();
    }

    void Update()
    {
        Refresh();
    }

    void BuildUi()
    {
        var uiFont = LoadUiFont();

        var canvasGo = new GameObject("MediaCacheCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasGo.AddComponent<GraphicRaycaster>();

        var panelGo = new GameObject("MediaCachePanelBox");
        panelGo.transform.SetParent(canvasGo.transform, false);
        var panelRect = panelGo.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 0f);
        panelRect.anchorMax = new Vector2(0f, 0f);
        panelRect.pivot = new Vector2(0f, 0f);
        panelRect.anchoredPosition = new Vector2(40f, 40f);
        panelRect.sizeDelta = new Vector2(520f, 132f);

        var panelImage = panelGo.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.55f);

        sizeLabel = CreateLabel("MediaCacheSize", panelGo.transform, uiFont, 26);
        sizeLabel.text = "Media cache: 0 B";
        var sizeRect = sizeLabel.GetComponent<RectTransform>();
        sizeRect.anchorMin = new Vector2(0f, 1f);
        sizeRect.anchorMax = new Vector2(0f, 1f);
        sizeRect.pivot = new Vector2(0f, 1f);
        sizeRect.anchoredPosition = new Vector2(16f, -12f);
        sizeRect.sizeDelta = new Vector2(488f, 34f);

        statusLabel = CreateLabel("MediaCacheStatus", panelGo.transform, uiFont, 22);
        statusLabel.text = "";
        var statusRect = statusLabel.GetComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0f, 1f);
        statusRect.anchorMax = new Vector2(0f, 1f);
        statusRect.pivot = new Vector2(0f, 1f);
        statusRect.anchoredPosition = new Vector2(16f, -48f);
        statusRect.sizeDelta = new Vector2(488f, 30f);

        var buttonGo = new GameObject("MediaCacheClearButton");
        buttonGo.transform.SetParent(panelGo.transform, false);
        var buttonRect = buttonGo.AddComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0f, 0f);
        buttonRect.anchorMax = new Vector2(0f, 0f);
        buttonRect.pivot = new Vector2(0f, 0f);
        buttonRect.anchoredPosition = new Vector2(16f, 12f);
        buttonRect.sizeDelta = new Vector2(300f, 44f);

        var buttonImage = buttonGo.AddComponent<Image>();
        buttonImage.color = new Color(0.25f, 0.25f, 0.3f, 0.95f);

        clearButton = buttonGo.AddComponent<Button>();
        clearButton.targetGraphic = buttonImage;
        clearButton.onClick.AddListener(ClearCache);

        var buttonLabel = CreateLabel("MediaCacheClearLabel", buttonGo.transform, uiFont, 24);
        buttonLabel.text = "Clear media cache";
        buttonLabel.alignment = TextAnchor.MiddleCenter;
        var buttonLabelRect = buttonLabel.GetComponent<RectTransform>();
        buttonLabelRect.anchorMin = Vector2.zero;
        buttonLabelRect.anchorMax = Vector2.one;
        buttonLabelRect.offsetMin = Vector2.zero;
        buttonLabelRect.offsetMax = Vector2.zero;
    }

    static Text CreateLabel(string name, Transform parent, Font font, int fontSize)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    static Font LoadUiFont()
    {
        try
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null) return font;
        }
        catch { }

        try
        {
            var font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (font != null) return font;
        }
        catch { }

        return Font.CreateDynamicFontFromOSFont("Segoe UI", 16);
    }

    void Refresh()
    {
        if (sizeLabel == null) return;

        long bytes = MediaCacheManager.GetCacheSizeBytes();
        int songs = MediaCacheManager.GetCachedSongCount();
        sizeLabel.text = $"Media cache: {MediaCacheManager.FormatSize(bytes)} ({songs} song(s))";
    }

    void ClearCache()
    {
        long before = MediaCacheManager.GetCacheSizeBytes();
        int removed = MediaCacheManager.ClearAll();

        Debug.Log($"[MediaCacheOptionPanel] media cache cleared: {removed} song(s), {MediaCacheManager.FormatSize(before)} freed");

        if (statusLabel != null)
        {
            statusLabel.text = $"Deleted {removed} song(s), {MediaCacheManager.FormatSize(before)} freed";
        }

        Refresh();
    }
}
