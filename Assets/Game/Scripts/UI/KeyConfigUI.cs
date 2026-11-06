using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// F2 で開閉できるキー設定オーバーレイ (シーン非依存、コードのみで生成)。
/// RuntimeInitializeOnLoadMethod で起動し、DontDestroyOnLoad で全シーンで有効。
/// 閉じているときは入力を一切奪わない (F2 の監視のみ)。
/// </summary>
public class KeyConfigUI : MonoBehaviour
{
    private const float PanelWidth = 960f;
    private const float RowFontSize = 26f;
    private const float HeaderFontSize = 32f;
    private const float MessageFontSize = 24f;

    private static KeyCode[] bindableKeys;

    private readonly List<TextMeshProUGUI> rows = new List<TextMeshProUGUI>();
    private RectTransform panel;
    private TextMeshProUGUI messageLabel;
    private TextMeshProUGUI testLabel;
    private TextMeshProUGUI hintLabel;
    private int selectedLane;
    private bool listening;
    private bool confirmReset;
    private bool open;
    private bool built;
    private TMP_FontAsset font;

    /// <summary>シーン非依存で起動する。</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindObjectOfType<KeyConfigUI>() != null) return;
        var go = new GameObject("KeyConfigUI");
        DontDestroyOnLoad(go);
        go.AddComponent<KeyConfigUI>();
    }

    private void Awake()
    {
        EnsureBuilt();
    }

    private void EnsureBuilt()
    {
        if (built) return;
        built = true;
        font = FindJapaneseFont();
        BuildCanvas();
        BuildPanel();
        BuildHint();
        SetOpen(false);
        KeyBindings.Changed += RefreshRows;
    }

    private void OnDestroy()
    {
        KeyBindings.Changed -= RefreshRows;
    }

    private void Update()
    {
        UpdateHint();

        if (Input.GetKeyDown(KeyCode.F2))
        {
            Toggle();
            return;
        }

        if (open) HandleInput();
    }

    public void Toggle()
    {
        SetOpen(!open);
    }

    private void SetOpen(bool value)
    {
        if (!value && open)
        {
            if (!KeyBindings.Save())
            {
                messageLabel.text = "空のレーンがあるため保存できません (全レーンに 1 個以上キーを割り当ててください)";
                return; // 閉じないで保存を促す
            }
        }

        open = value;
        if (panel != null) panel.gameObject.SetActive(value);
        if (value)
        {
            listening = false;
            confirmReset = false;
            selectedLane = Mathf.Clamp(selectedLane, 0, KeyBindings.LaneCount - 1);
            RefreshRows();
            UpdateMessage();
        }
    }

    private void HandleInput()
    {
        if (listening)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                listening = false;
                UpdateMessage();
                return;
            }
            CapturePressedKeys();
            return;
        }

        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            confirmReset = false;
            selectedLane = (selectedLane + KeyBindings.LaneCount - 1) % KeyBindings.LaneCount;
            RefreshRows();
            UpdateMessage();
        }
        else if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            confirmReset = false;
            selectedLane = (selectedLane + 1) % KeyBindings.LaneCount;
            RefreshRows();
            UpdateMessage();
        }
        else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            confirmReset = false;
            listening = true;
            UpdateMessage();
        }
        else if (Input.GetKeyDown(KeyCode.Backspace))
        {
            confirmReset = false;
            ClearLane();
        }
        else if (Input.GetKeyDown(KeyCode.R))
        {
            HandleReset();
        }
        else if (Input.GetKeyDown(KeyCode.Escape))
        {
            SetOpen(false);
        }

        CaptureTestKey();
    }

    private void HandleReset()
    {
        if (!confirmReset)
        {
            confirmReset = true;
            messageLabel.text = "初期化しますか? もう一度 R を押すと既定のキー配置に戻ります";
            return;
        }
        confirmReset = false;
        KeyBindings.ResetToDefaults();
        messageLabel.text = "キー割り当てを初期化しました";
    }

    private void ClearLane()
    {
        // 編集中はレーンを空にできるが、保存 (閉じる) には全レーンにキーが必要。
        if (KeyBindings.SetLane(selectedLane, new KeyCode[0]))
        {
            RefreshRows();
            messageLabel.text = "レーン " + (selectedLane + 1) + " のキーを消しました (閉じる前にキーを割り当ててください)";
        }
        else
        {
            messageLabel.text = "レーンを空にできません";
        }
    }

    /// <summary>listen モード中に押されたキーをレーンに割り当てる (トグル)。</summary>
    private void CapturePressedKeys()
    {
        KeyCode[] candidates = GetBindableKeys();
        for (int i = 0; i < candidates.Length; i++)
        {
            KeyCode key = candidates[i];
            if (!Input.GetKeyDown(key)) continue;

            ShowTestKey(key);
            if (KeyBindings.ToggleKey(selectedLane, key))
            {
                RefreshRows();
                UpdateMessage();
            }
            else
            {
                messageLabel.text = "そのキーは割り当てできません (レーンには最低 1 キー必要です)";
            }
        }
    }

    /// <summary>listen モード外でも押されたキーがどのレーンか表示する (動作確認用)。</summary>
    private void CaptureTestKey()
    {
        KeyCode[] candidates = GetBindableKeys();
        for (int i = 0; i < candidates.Length; i++)
        {
            KeyCode key = candidates[i];
            if (Input.GetKeyDown(key))
            {
                ShowTestKey(key);
            }
        }
    }

    private void ShowTestKey(KeyCode key)
    {
        int lane = FindLane(key);
        string display = GetKeyDisplayName(key);
        testLabel.text = (lane >= 0)
            ? "押したキー: " + display + " → レーン " + (lane + 1)
            : "押したキー: " + display + " → 未割り当て";
    }

    private static int FindLane(KeyCode key)
    {
        KeyCode[][] table = KeyBindings.Current;
        for (int lane = 0; lane < table.Length; lane++)
        {
            KeyCode[] keys = table[lane];
            for (int i = 0; i < keys.Length; i++)
            {
                if (keys[i] == key) return lane;
            }
        }
        return -1;
    }

    /// <summary>設定可能なキー一覧 (マウス/ジョイスティック/予約キーを除く)。一度だけ生成する。</summary>
    private static KeyCode[] GetBindableKeys()
    {
        if (bindableKeys != null) return bindableKeys;

        var keys = new List<KeyCode>();
        var all = (KeyCode[])Enum.GetValues(typeof(KeyCode));
        for (int i = 0; i < all.Length; i++)
        {
            KeyCode key = all[i];
            string name = key.ToString();
            if (name.StartsWith("Mouse") || name.StartsWith("Joystick") || name.StartsWith("Axis")) continue;
            if (KeyBindings.IsReserved(key)) continue;
            keys.Add(key);
        }
        bindableKeys = keys.ToArray();
        return bindableKeys;
    }

    private void RefreshRows()
    {
        for (int lane = 0; lane < rows.Count; lane++)
        {
            var label = rows[lane];
            if (label == null) continue;
            label.text = BuildLaneLabel(lane);
            label.color = (lane == selectedLane && open) ? new Color(1f, 0.85f, 0.3f) : Color.white;
        }
    }

    private void UpdateMessage()
    {
        if (messageLabel == null) return;
        if (listening)
        {
            messageLabel.text = "レーン " + (selectedLane + 1) + " に割り当てるキーを押してください (Esc で終了 / 同じキーで解除)";
        }
        else
        {
            messageLabel.text = "↑↓: レーン選択  Enter: キー入力  Esc: 閉じる (保存)  R: 初期化";
        }
    }

    private string BuildLaneLabel(int lane)
    {
        KeyCode[] keys = KeyBindings.GetKeys(lane);
        var sb = new StringBuilder();
        sb.Append("Lane ").Append(lane + 1).Append(":  ");
        if (keys.Length == 0) return sb.Append("(キーなし)").ToString();
        for (int i = 0; i < keys.Length; i++)
        {
            if (i > 0) sb.Append(' ');
            sb.Append(GetKeyDisplayName(keys[i]));
        }
        return sb.ToString();
    }

    /// <summary>キーの表示名 (KeyGuideUI に依存しない独自実装)。</summary>
    private static string GetKeyDisplayName(KeyCode key)
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

    private void BuildCanvas()
    {
        var canvasGO = new GameObject("KeyConfigCanvas", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        var scaler = canvasGO.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
    }

    private void BuildPanel()
    {
        var canvas = GetComponentInChildren<Canvas>().transform;

        panel = new GameObject("Panel", typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.VerticalLayoutGroup)).GetComponent<RectTransform>();
        panel.SetParent(canvas, false);
        panel.anchorMin = new Vector2(0.5f, 0.5f);
        panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.anchoredPosition = Vector2.zero;
        panel.sizeDelta = new Vector2(PanelWidth, 0f);
        panel.GetComponent<UnityEngine.UI.Image>().color = new Color(0f, 0f, 0f, 0.85f);

        var layout = panel.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
        layout.padding = new RectOffset(24, 24, 16, 16);
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        AddLabel(panel, "KeyConfigHeader", "キー設定 (F2 で開閉)", HeaderFontSize);

        for (int lane = 0; lane < KeyBindings.LaneCount; lane++)
        {
            var label = AddLabel(panel, "Lane" + lane, BuildLaneLabel(lane), RowFontSize);
            rows.Add(label);
        }

        messageLabel = AddLabel(panel, "KeyConfigMessage", string.Empty, MessageFontSize);
        testLabel = AddLabel(panel, "KeyConfigTest", string.Empty, MessageFontSize);
        testLabel.color = new Color(0.6f, 0.9f, 1f);
    }

    private void BuildHint()
    {
        var canvas = GetComponentInChildren<Canvas>().transform;
        var labelGO = new GameObject("KeyConfigHint", typeof(TextMeshProUGUI));
        labelGO.transform.SetParent(canvas, false);
        hintLabel = labelGO.GetComponent<TextMeshProUGUI>();
        hintLabel.text = "F2: キー設定";
        hintLabel.fontSize = 24f;
        hintLabel.color = new Color(1f, 1f, 1f, 0.7f);
        hintLabel.alignment = TextAlignmentOptions.Left;
        hintLabel.raycastTarget = false;
        if (font != null) hintLabel.font = font;

        var rect = hintLabel.rectTransform;
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        rect.anchoredPosition = new Vector2(16f, 16f);
        rect.sizeDelta = new Vector2(300f, 36f);
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
        return tmp;
    }

    /// <summary>日本語グリフを持つフォントを探す (KeyGuideUI と同じ方針の独自実装)。</summary>
    private static TMP_FontAsset FindJapaneseFont()
    {
        TMP_FontAsset fallback = null;
        var all = Resources.FindObjectsOfTypeAll<TextMeshProUGUI>();
        for (int i = 0; i < all.Length; i++)
        {
            var tmp = all[i];
            if (tmp == null || tmp.font == null) continue;
            string fontName = tmp.font.name;
            if (fontName != null && (fontName.Contains("Noto") || fontName.Contains("YuGoth")))
            {
                return tmp.font;
            }
            if (fallback == null) fallback = tmp.font;
        }
        return fallback;
    }

    /// <summary>Title/選曲シーンでのみヒントを表示する。</summary>
    private void UpdateHint()
    {
        if (hintLabel == null) return;
        string sceneName = SceneManager.GetActiveScene().name;
        bool show = sceneName == "TitleScene" || sceneName == "SelectScene";
        if (hintLabel.gameObject.activeSelf != show)
        {
            hintLabel.gameObject.SetActive(show);
        }
    }
}
