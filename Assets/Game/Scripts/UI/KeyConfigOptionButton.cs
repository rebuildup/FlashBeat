using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Option 画面に「キー設定」ボタンを追加する (シーン本体には触れない、コードのみで生成)。
/// MediaCacheOptionPanel と同じ方式: sceneLoaded フックで OptionScene のときだけ生成する。
/// クリック、または Tab でフォーカスして Enter で KeyConfigUI を開く (Issue #128)。
/// </summary>
public class KeyConfigOptionButton : MonoBehaviour
{
    const string PanelName = "KeyConfigOptionPanel";

    /// <summary>MediaCachePanel (左下 40,40 / 520x132) の右隣に並べる。</summary>
    const float PanelX = 600f;
    const float PanelY = 40f;
    const float PanelWidth = 520f;
    const float PanelHeight = 132f;

    /// <summary>アクセント色 #3FA9F5 (KeyConfigUI と同じ枠色)。</summary>
    static readonly Color AccentColor = new Color(0.247f, 0.663f, 0.961f);

    Button button;
    bool focusedByTab;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void RegisterSceneHook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // v6 Option は Play タブの「キー設定」行から KeyConfigUI を開く。
        // 旧 F2 ボタンは設計に無いため生成しない。
        if (FlashBeat.UI.V6.V6UiMode.LegacyOverlaysHidden) return;
        if (scene.name != "OptionScene") return;
        if (FindFirstObjectByType<KeyConfigOptionButton>() != null) return;

        var go = new GameObject(PanelName);
        go.AddComponent<KeyConfigOptionButton>();
    }

    void Start()
    {
        BuildUi();
    }

    void Update()
    {
        if (button == null) return;

        // KeyConfigUI が開いている間は何もしない (Esc / Enter / 矢印はオーバーレイに渡す)。
        if (KeyConfigUI.IsOpen)
        {
            focusedByTab = false;
            return;
        }

        if (Input.GetKeyDown(KeyCode.Tab))
        {
            FocusByTab();
        }

        if (focusedByTab && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
        {
            focusedByTab = false;
            OpenKeyConfig();
            return;
        }

        // Tab で選んだあと、別のものが選択されたらフォーカス扱いをやめる
        if (focusedByTab && EventSystem.current != null
            && EventSystem.current.currentSelectedGameObject != button.gameObject)
        {
            focusedByTab = false;
        }
    }

    void FocusByTab()
    {
        focusedByTab = true;
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(button.gameObject);
        }
    }

    void OpenKeyConfig()
    {
        KeyConfigUI.Open();
    }

    void BuildUi()
    {
        var canvasGo = new GameObject("KeyConfigOptionCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasGo.AddComponent<GraphicRaycaster>();

        var panelGo = new GameObject("KeyConfigPanelBox");
        panelGo.transform.SetParent(canvasGo.transform, false);
        var panelRect = panelGo.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 0f);
        panelRect.anchorMax = new Vector2(0f, 0f);
        panelRect.pivot = new Vector2(0f, 0f);
        panelRect.anchoredPosition = new Vector2(PanelX, PanelY);
        panelRect.sizeDelta = new Vector2(PanelWidth, PanelHeight);

        var panelImage = panelGo.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.55f);

        var caption = CreateLabel("KeyConfigCaption", panelGo.transform, 22);
        caption.text = "でも開けます";
        var captionRect = caption.GetComponent<RectTransform>();
        captionRect.anchorMin = new Vector2(0f, 1f);
        captionRect.anchorMax = new Vector2(0f, 1f);
        captionRect.pivot = new Vector2(0f, 1f);
        captionRect.anchoredPosition = new Vector2(16f, -12f);
        captionRect.sizeDelta = new Vector2(488f, 30f);
        // [F2] キャップを左に置いて、キーボード操作だと分かるようにする。
        var cap = FlashBeat.UI.V5.ShortcutKeyCap.Create(panelRect, "F2", 18f);
        var capRect = cap.GetComponent<RectTransform>();
        capRect.anchorMin = new Vector2(0f, 1f);
        capRect.anchorMax = new Vector2(0f, 1f);
        capRect.pivot = new Vector2(0f, 1f);
        capRect.anchoredPosition = new Vector2(16f, -12f);
        captionRect.anchoredPosition = new Vector2(16f + capRect.sizeDelta.x + 8f, -12f);

        var buttonGo = new GameObject("KeyConfigOpenButton");
        buttonGo.transform.SetParent(panelGo.transform, false);
        var buttonRect = buttonGo.AddComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0f, 0f);
        buttonRect.anchorMax = new Vector2(0f, 0f);
        buttonRect.pivot = new Vector2(0f, 0f);
        buttonRect.anchoredPosition = new Vector2(16f, 12f);
        buttonRect.sizeDelta = new Vector2(300f, 44f);

        var buttonImage = buttonGo.AddComponent<Image>();
        buttonImage.color = new Color(0.25f, 0.25f, 0.3f, 0.95f);

        AddAccentBorder(buttonGo.transform);

        button = buttonGo.AddComponent<Button>();
        button.targetGraphic = buttonImage;
        button.onClick.AddListener(OpenKeyConfig);

        var buttonLabel = CreateLabel("KeyConfigButtonLabel", buttonGo.transform, 24);
        buttonLabel.text = "キー設定";
        buttonLabel.alignment = TMPro.TextAlignmentOptions.Center;
        var buttonLabelRect = buttonLabel.GetComponent<RectTransform>();
        buttonLabelRect.anchorMin = Vector2.zero;
        buttonLabelRect.anchorMax = Vector2.one;
        buttonLabelRect.offsetMin = Vector2.zero;
        buttonLabelRect.offsetMax = Vector2.zero;
    }

    /// <summary>ボタンの 1px 枠 (#3FA9F5)。KeyConfigUI.BuildBorder と同じ 4 本の Image 方式。</summary>
    static void AddAccentBorder(Transform parent)
    {
        AddBorderBar(parent, "BorderTop", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 1f));
        AddBorderBar(parent, "BorderBottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 1f));
        AddBorderBar(parent, "BorderLeft", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(1f, 0f));
        AddBorderBar(parent, "BorderRight", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(1f, 0f));
    }

    static void AddBorderBar(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 sizeDelta)
    {
        var bar = new GameObject(name, typeof(Image)).GetComponent<RectTransform>();
        bar.SetParent(parent, false);
        bar.anchorMin = anchorMin;
        bar.anchorMax = anchorMax;
        bar.pivot = pivot;
        bar.anchoredPosition = Vector2.zero;
        bar.sizeDelta = sizeDelta;
        var image = bar.GetComponent<Image>();
        image.color = AccentColor;
        image.raycastTarget = false;
    }

    /// <summary>日本語ラベル (KeyConfigUI.FindJapaneseFont と同じ TMP フォントを使う)。</summary>
    static TextMeshProUGUI CreateLabel(string name, Transform parent, int fontSize)
    {
        var go = new GameObject(name, typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.color = Color.white;
        text.horizontalAlignment = HorizontalAlignmentOptions.Left;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        var font = KeyConfigUI.FindJapaneseFont();
        if (font != null) text.font = font;
        return text;
    }
}
