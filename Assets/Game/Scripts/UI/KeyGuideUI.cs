using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// ゲーム中に開閉できる 8 レーン分のキーガイド HUD。
/// 表示内容は KeyBindings (正本) から常に生成されるため、判定と表示がずれない。
/// キー割り当てが変わると KeyBindings.Changed で自動更新される。
/// GameSceneManager がシーン開始時に生成し、Tab キーまたは HUD ボタンで開閉する。
/// </summary>
public class KeyGuideUI : MonoBehaviour
{
    private const float PanelWidth = 780f;
    private const float PanelHeight = 400f;
    private const float RowFontSize = 24f;
    private const float HeaderFontSize = 28f;

    private readonly List<TextMeshProUGUI> rows = new List<TextMeshProUGUI>();
    private RectTransform panel;
    private TMP_FontAsset font;
    private bool visible;
    private bool built;

    /// <summary>シーンにガイドを生成する。GameSceneManager から呼ばれる。</summary>
    public static KeyGuideUI Create(Transform parent)
    {
        var go = new GameObject("KeyGuideUI");
        if (parent != null) go.transform.SetParent(parent, false);
        var guide = go.AddComponent<KeyGuideUI>();
        // EditMode では Awake が呼ばれないため、ここでも確実に組み立てる。
        guide.EnsureBuilt();
        return guide;
    }

    private void Awake()
    {
        EnsureBuilt();
    }

    /// <summary>UI を組み立てる (2 回呼んでも作らない)。</summary>
    public void EnsureBuilt()
    {
        if (built) { return; }
        built = true;
        BuildUI();
        SetVisible(false); // 既定では非表示 (ゲームプレイを邪魔しない)
        KeyBindings.Changed += RefreshRows;
    }

    private void OnDestroy()
    {
        KeyBindings.Changed -= RefreshRows;
    }

    /// <summary>キー割り当てが変わったら表示を更新する (KeyBindings.Changed)。</summary>
    private void RefreshRows()
    {
        for (int lane = 0; lane < rows.Count; lane++)
        {
            if (rows[lane] != null) rows[lane].text = BuildLaneLabel(lane);
        }
    }

    private void Update()
    {
        // キー設定オーバーレイの開いた・閉じたフレームは Tab を捨てる (Issue #167)。
        // 開いている間の停止自体は KeyConfigUI.SuspendScene が担う。
        if (!FlashBeat.UI.SceneInputGate.InputAllowed) { return; }
        if (Input.GetKeyDown(KeyCode.Tab)) Toggle();
    }

    /// <summary>ガイドを表示中かどうか (テスト・状態表示用)。</summary>
    public bool IsVisible
    {
        get { return visible; }
    }

    public void Toggle()
    {
        SetVisible(!visible);
    }

    public void SetVisible(bool value)
    {
        visible = value;
        if (panel != null) panel.gameObject.SetActive(value);
    }

    private void BuildUI()
    {
        font = JapaneseFontFinder.FindJapaneseFont();
        var canvasGO = new GameObject("KeyGuideCanvas", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 2000;

        panel = new GameObject("Panel", typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.VerticalLayoutGroup)).GetComponent<RectTransform>();
        panel.GetComponent<UnityEngine.UI.Image>().color = new Color(0f, 0f, 0f, 0.72f);
        panel.SetParent(canvasGO.transform, false);
        // 右端の中央に置く (上部の更新通知・下部のボタンと重ならない位置)。
        panel.anchorMin = new Vector2(1f, 0.5f);
        panel.anchorMax = new Vector2(1f, 0.5f);
        panel.pivot = new Vector2(1f, 0.5f);
        panel.anchoredPosition = new Vector2(-16f, 0f);
        panel.sizeDelta = new Vector2(PanelWidth, PanelHeight);

        var layout = panel.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 8, 8);
        layout.spacing = 4f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        // 高さは LayoutElement の minHeight に従わせる (既定 100px のままだと
        // 8 行がパネルの下にはみ出してしまう)。
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var header = AddLabel(panel, "KeyGuideHeader", "キーガイド", HeaderFontSize);
        // 開閉キーは文章ではなくキーキャップで示す ([Tab])。
        FlashBeat.UI.V5.ShortcutKeyCap.AttachToLabel(header, "Tab", HeaderFontSize * 0.8f);
        AddLabel(panel, "KeyGuideNote", "上から順にレーン 1 〜 8 (左端がレーン 1)。", 18f);

        for (int lane = 0; lane < KeyBindings.LaneCount; lane++)
        {
            rows.Add(AddLabel(panel, "Lane" + lane, BuildLaneLabel(lane), RowFontSize));
        }

        CreateToggleButton(canvasGO.transform);
    }

    private TextMeshProUGUI AddLabel(RectTransform parent, string name, string text, float fontSize)
    {
        var labelGO = new GameObject(name, typeof(TextMeshProUGUI));
        labelGO.transform.SetParent(parent, false);
        var tmp = labelGO.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.raycastTarget = false;
        if (font != null) tmp.font = font;
        var size = labelGO.AddComponent<UnityEngine.UI.LayoutElement>();
        size.minHeight = fontSize + 6f;
        size.preferredHeight = fontSize + 6f;
        return tmp;
    }

    private void CreateToggleButton(Transform parent)
    {
        var buttonGO = new GameObject("KeyGuideToggle", typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
        buttonGO.transform.SetParent(parent, false);
        var rect = buttonGO.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-16f, 16f);
        rect.sizeDelta = new Vector2(190f, 40f);
        buttonGO.GetComponent<UnityEngine.UI.Image>().color = new Color(0f, 0f, 0f, 0.4f);
        buttonGO.GetComponent<UnityEngine.UI.Button>().onClick.AddListener(Toggle);

        var labelGO = new GameObject("Label", typeof(TextMeshProUGUI));
        labelGO.transform.SetParent(buttonGO.transform, false);
        var tmp = labelGO.GetComponent<TextMeshProUGUI>();
        tmp.text = "キーガイド";
        tmp.fontSize = 22f;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.rectTransform.anchorMin = Vector2.zero;
        tmp.rectTransform.anchorMax = Vector2.one;
        tmp.rectTransform.offsetMin = new Vector2(14f, 0f);
        tmp.rectTransform.offsetMax = new Vector2(-64f, 0f);
        tmp.rectTransform.sizeDelta = Vector2.zero;
        tmp.raycastTarget = false;

        // 開閉キー [Tab] をボタン内に示す。
        FlashBeat.UI.V5.ShortcutKeyCap.AttachToControl(rect, "Tab", 18f, 10f);
    }

    /// <summary>レーンの見出し行 ("レーン N:") を返す。テストからも参照される。</summary>
    public static string BuildLaneHeader(int lane)
    {
        return "レーン " + (lane + 1) + ":";
    }

    /// <summary>KeyBindings のレーン割り当てを表示用に整形して返す (表示名は GetKeyDisplayName)。</summary>
    public static string BuildLaneLabel(int lane)
    {
        KeyCode[] keys = KeyBindings.GetKeys(lane);
        var sb = new StringBuilder();
        sb.Append(BuildLaneHeader(lane));
        sb.Append(' ');
        for (int i = 0; i < keys.Length; i++)
        {
            if (i > 0) sb.Append(' ');
            sb.Append(GetKeyDisplayName(keys[i]));
        }
        return sb.ToString();
    }

    /// <summary>キーの表示名。記号キーは JIS 配列上の刻印、文字キーは Enum 名をそのまま使う。</summary>
    public static string GetKeyDisplayName(KeyCode key)
    {
        switch (key)
        {
            case KeyCode.Alpha0: return "0";
            case KeyCode.Alpha1: return "1";
            case KeyCode.Alpha2: return "2";
            case KeyCode.Alpha3: return "3";
            case KeyCode.Alpha4: return "4";
            case KeyCode.Alpha5: return "5";
            case KeyCode.Alpha6: return "6";
            case KeyCode.Alpha7: return "7";
            case KeyCode.Alpha8: return "8";
            case KeyCode.Alpha9: return "9";
            case KeyCode.Minus: return "-";
            case KeyCode.Equals: return "=";
            case KeyCode.Caret: return "^";
            case KeyCode.At: return "@";
            case KeyCode.LeftBracket: return "[";
            case KeyCode.RightBracket: return "]";
            case KeyCode.Semicolon: return ";";
            case KeyCode.Colon: return ":";
            case KeyCode.Backslash: return "\\";
            case KeyCode.BackQuote: return "`";
            case KeyCode.Slash: return "/";
            case KeyCode.Underscore: return "_";
            case KeyCode.Asterisk: return "*";
            case KeyCode.Comma: return ",";
            case KeyCode.Period: return ".";
            case KeyCode.Less: return "<";
            case KeyCode.Greater: return ">";
            default: return key.ToString();
        }
    }

    /// <summary>生成済みガイドの指定レーン行のテキストを返す (未生成や範囲外は null)。</summary>
    public string GetRowLabel(int lane)
    {
        if (lane < 0 || lane >= rows.Count) return null;
        return rows[lane] != null ? rows[lane].text : null;
    }
}
