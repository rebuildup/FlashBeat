using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>
/// F2 で開閉できるキー設定オーバーレイ (シーン非依存、コードのみで生成)。
/// RuntimeInitializeOnLoadMethod で起動し、DontDestroyOnLoad で全シーンで有効。
/// 閉じているときは入力を一切奪わない (F2 の監視のみ)。
/// </summary>
public class KeyConfigUI : MonoBehaviour
{
    private const float PanelWidth = 1100f;
    private const float PanelHeight = 900f;
    private const float RowHeight = 66f;
    private const float RowFontSize = 26f;
    private const float HeaderFontSize = 32f;
    private const float MessageFontSize = 24f;
    private const float FooterFontSize = 20f;

    /// <summary>アクセント色 #3FA9F5 (枠・選択バー)。</summary>
    private static readonly Color AccentColor = new Color(0.247f, 0.663f, 0.961f);
    /// <summary>パネル本体 #1B1B1B (不透明)。</summary>
    private static readonly Color PanelColor = new Color(0.106f, 0.106f, 0.106f, 1f);
    private static readonly Color SelectedRowColor = new Color(0.247f, 0.663f, 0.961f, 0.25f);

    private static KeyCode[] bindableKeys;

    private readonly List<TextMeshProUGUI> rows = new List<TextMeshProUGUI>();
    private readonly List<UnityEngine.UI.Image> rowBackgrounds = new List<UnityEngine.UI.Image>();
    private readonly List<UnityEngine.UI.Image> accentBars = new List<UnityEngine.UI.Image>();
    private RectTransform panel;
    private TextMeshProUGUI messageLabel;
    private TextMeshProUGUI testLabel;
    private TextMeshProUGUI hintLabel;
    private TextMeshProUGUI layoutLabel;
    private int selectedLane;
    private bool listening;
    private bool confirmReset;
    private bool open;
    private readonly List<MonoBehaviour> suspended = new List<MonoBehaviour>();
    private bool savedSendNavigation = true;
    private bool navigationSuspended;
    private GameObject createdEventSystem;
    private static readonly Color IdleRowColor = new Color(0f, 0f, 0f, 0.01f); // alpha>0: still hit by the raycaster

    /// <summary>キー設定を開いている間は true。背後のシーンはキー入力を受け取らない。</summary>
    public static bool IsOpen { get; private set; }

    // ---- Test hooks: the -autotest runner injects keys here (inert without the flag) ----
    // key -> frame in which it was injected; the key is visible to Update in the NEXT frame only
    // (a coroutine resumes after this frame's Update, so same-frame visibility would be lost).
    private static readonly Dictionary<KeyCode, int> injectedDown = new Dictionary<KeyCode, int>();

    public static void InjectKeyDown(KeyCode key)
    {
        injectedDown[key] = Time.frameCount;
    }

    private static bool KeyDown(KeyCode key)
    {
        int frame;
        return Input.GetKeyDown(key) || (injectedDown.TryGetValue(key, out frame) && Time.frameCount == frame + 1);
    }

    public static int TestSelectedLane
    {
        get
        {
            var ui = FindObjectOfType<KeyConfigUI>();
            return ui != null ? ui.selectedLane : -1;
        }
    }

    public static bool TestListening
    {
        get
        {
            var ui = FindObjectOfType<KeyConfigUI>();
            return ui != null && ui.listening;
        }
    }

    public static GameObject TestRowObject(int lane)
    {
        var ui = FindObjectOfType<KeyConfigUI>();
        if (ui == null || lane < 0 || lane >= ui.rows.Count || ui.rows[lane] == null) { return null; }
        return ui.rows[lane].transform.parent.gameObject;
    }


    private static readonly string[] AllowedScenes = { "TitleScene", "SelectScene", "OptionScene" };

    /// <summary>
    /// 既定のキー割り当て。KeyBindings.DefaultKeys が private のため同じ構成をここに持つ
    /// (v0.1.2 で Judge.LaneKeys を KeyBindings が複製したのと同じ先例)。
    /// 変更したら KeyBindings 側も直すこと。KeyConfigLayoutTests がずれを検出する。
    /// </summary>
    public static readonly KeyCode[][] DefaultLayout = new KeyCode[][]
    {
        new KeyCode[] { KeyCode.Alpha1, KeyCode.Q, KeyCode.A, KeyCode.Z },
        new KeyCode[] { KeyCode.Alpha2, KeyCode.W, KeyCode.S, KeyCode.X },
        new KeyCode[] { KeyCode.Alpha3, KeyCode.E, KeyCode.D, KeyCode.C },
        new KeyCode[] { KeyCode.Alpha4, KeyCode.Alpha5, KeyCode.R, KeyCode.T, KeyCode.F, KeyCode.G, KeyCode.V, KeyCode.B },
        new KeyCode[] { KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Y, KeyCode.U, KeyCode.H, KeyCode.J, KeyCode.N, KeyCode.M },
        new KeyCode[] { KeyCode.Alpha8, KeyCode.I, KeyCode.K, KeyCode.Less, KeyCode.Comma },
        new KeyCode[] { KeyCode.Alpha9, KeyCode.O, KeyCode.L, KeyCode.Greater, KeyCode.Period },
        new KeyCode[] { KeyCode.Alpha0, KeyCode.Equals, KeyCode.Caret, KeyCode.Backslash, KeyCode.P, KeyCode.At, KeyCode.LeftBracket, KeyCode.Semicolon, KeyCode.Colon, KeyCode.RightBracket, KeyCode.Slash, KeyCode.Underscore, KeyCode.Minus, KeyCode.Asterisk, KeyCode.BackQuote }
    };

    /// <summary>
    /// 現在の割り当てが既定と同じか (レーン位置は固定、レーン内のキー順は問わない)。
    /// 純粋関数: NUnit テスト (KeyConfigLayoutTests) から直接検証する。
    /// </summary>
    public static bool IsDefaultLayout(KeyCode[][] current, KeyCode[][] defaults)
    {
        if (current == null || defaults == null || current.Length != defaults.Length) return false;

        for (int lane = 0; lane < current.Length; lane++)
        {
            KeyCode[] a = current[lane];
            KeyCode[] b = defaults[lane];
            if (a == null || b == null || a.Length != b.Length) return false;

            var seen = new HashSet<KeyCode>(a);
            if (seen.Count != a.Length) return false; // 重複は既定にない (保険)
            for (int i = 0; i < b.Length; i++)
            {
                if (!seen.Contains(b[i])) return false;
            }
        }
        return true;
    }

    /// <summary>表示用: 現在の割り当てが既定かどうか。</summary>
    public static bool IsCustomLayout()
    {
        return !IsDefaultLayout(KeyBindings.Current, DefaultLayout);
    }
    private static bool IsAllowedScene()
    {
        string name = SceneManager.GetActiveScene().name;
        for (int i = 0; i < AllowedScenes.Length; i++)
        {
            if (AllowedScenes[i] == name) return true;
        }
        return false;
    }
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
        UpdateBlink();

        if (open && !IsAllowedScene())
        {
            SetOpen(false); // 遷移などで想定外のシーンになったら閉じる
            return;
        }

        if (KeyDown(KeyCode.F2))
        {
            if (open || IsAllowedScene()) { Toggle(); }
            return;
        }

        if (open) HandleInput();
    }

    public void Toggle()
    {
        SetOpen(!open);
    }

    /// <summary>Option 画面などから開く。ゲーム中 (Game/Result) では開かない。</summary>
    public static void Open()
    {
        var ui = FindObjectOfType<KeyConfigUI>();
        if (ui != null && !ui.open && IsAllowedScene()) { ui.SetOpen(true); }
    }

    public static void Close()
    {
        var ui = FindObjectOfType<KeyConfigUI>();
        if (ui != null && ui.open) { ui.SetOpen(false); }
    }

    /// <summary>
    /// 背後のシーンのキー処理を止める。タイトルは E=終了 / O=Option / Space=開始、選曲は F/D/S/A/J/K/L=移動 を
    /// Input.GetKeyDown で直接読むため、割り当てのためにキーを押すとアプリが終了したり遷移してしまう。
    /// 開いている間だけ XxxSceneManager を無効化し、閉じたら元に戻す。
    /// </summary>
    private void SuspendScene()
    {
        ResumeScene();
        // シーンに EventSystem が無いとクリックが届かない。無ければ開いている間だけ作る。
        if (EventSystem.current == null)
        {
            createdEventSystem = new GameObject("KeyConfigEventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            createdEventSystem.transform.SetParent(transform, false);
        }
        // Enter / 矢印は EventSystem 経由でも選択中のボタンを押してしまう (Submit)。開いている間は止める。
        if (EventSystem.current != null)
        {
            savedSendNavigation = EventSystem.current.sendNavigationEvents;
            EventSystem.current.sendNavigationEvents = false;
            EventSystem.current.SetSelectedGameObject(null);
            navigationSuspended = true;
        }
        MonoBehaviour[] all = FindObjectsOfType<MonoBehaviour>();
        for (int i = 0; i < all.Length; i++)
        {
            MonoBehaviour behaviour = all[i];
            if (behaviour == null || behaviour == this || !behaviour.enabled) continue;
            string typeName = behaviour.GetType().Name;
            if (typeName.EndsWith("SceneManager"))
            {
                behaviour.enabled = false;
                suspended.Add(behaviour);
            }
        }
    }

    private void ResumeScene()
    {
        if (navigationSuspended && EventSystem.current != null)
        {
            EventSystem.current.sendNavigationEvents = savedSendNavigation;
        }
        navigationSuspended = false;
        if (createdEventSystem != null)
        {
            Destroy(createdEventSystem);
            createdEventSystem = null;
        }
        for (int i = 0; i < suspended.Count; i++)
        {
            if (suspended[i] != null) { suspended[i].enabled = true; }
        }
        suspended.Clear();
    }

    private void OnDisable()
    {
        ResumeScene();
        IsOpen = false;
    }

    private void SetOpen(bool value)
    {
        if (!value && open)
        {
            if (!KeyBindings.Save())
            {
                messageLabel.text = "空のレーンがあるため閉じられません (全レーンに 1 つ以上キーを割り当ててください)";
                return; // 閉じないで保存を促す
            }
        }

        open = value;
        IsOpen = value;
        if (value) { SuspendScene(); } else { ResumeScene(); }
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
        // レーン移動 (割り当て中でも有効。矢印・Tab は予約キーなので割り当て対象にならない)。
        if (KeyDown(KeyCode.UpArrow)) { MoveLane(-1); return; }
        if (KeyDown(KeyCode.DownArrow) || KeyDown(KeyCode.Tab)) { MoveLane(1); return; }
        float wheel = Input.mouseScrollDelta.y;
        if (wheel > 0.5f) { MoveLane(-1); return; }
        if (wheel < -0.5f) { MoveLane(1); return; }

        if (listening)
        {
            if (KeyDown(KeyCode.Escape))
            {
                listening = false;
                UpdateMessage();
                return;
            }
            CapturePressedKeys();
            return;
        }

        if (KeyDown(KeyCode.UpArrow))
        {
            confirmReset = false;
            selectedLane = (selectedLane + KeyBindings.LaneCount - 1) % KeyBindings.LaneCount;
            RefreshRows();
            UpdateMessage();
        }
        else if (KeyDown(KeyCode.DownArrow))
        {
            confirmReset = false;
            selectedLane = (selectedLane + 1) % KeyBindings.LaneCount;
            RefreshRows();
            UpdateMessage();
        }
        else if (KeyDown(KeyCode.Return) || KeyDown(KeyCode.KeypadEnter))
        {
            confirmReset = false;
            listening = true;
            UpdateMessage();
        }
        else if (KeyDown(KeyCode.Backspace))
        {
            confirmReset = false;
            ClearLane();
        }
        else if (KeyDown(KeyCode.R))
        {
            HandleReset();
        }
        else if (KeyDown(KeyCode.Escape))
        {
            SetOpen(false);
        }

        CaptureTestKey();
    }

    /// <summary>レーンを移動する。割り当て中なら終了して選択だけ動かす (Enter / クリックで再開)。</summary>
    private void MoveLane(int delta)
    {
        confirmReset = false;
        listening = false;
        selectedLane = (selectedLane + delta + KeyBindings.LaneCount) % KeyBindings.LaneCount;
        RefreshRows();
        UpdateMessage();
    }

    /// <summary>行クリック: そのレーンを選んで割り当て開始。選択中の行を再度クリックすると割り当て終了。</summary>
    private void OnRowClicked(int lane)
    {
        confirmReset = false;
        if (selectedLane == lane && listening)
        {
            listening = false;
        }
        else
        {
            selectedLane = lane;
            listening = true;
        }
        RefreshRows();
        UpdateMessage();
    }

    private void HandleReset()
    {
        if (!confirmReset)
        {
            confirmReset = true;
            messageLabel.text = "初期化しますか? もう一度 R を押すと初期化します";
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
            if (!KeyDown(key)) continue;

            ShowTestKey(key);
            if (KeyBindings.ToggleKey(selectedLane, key))
            {
                RefreshRows();
                UpdateMessage();
            }
            else
            {
                messageLabel.text = "そのキーは割り当てできません (レーンからキーがなくなります)";
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
            if (KeyDown(key))
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
            bool selected = lane == selectedLane && open;
            string text = BuildLaneLabel(lane);
            if (lane == selectedLane && listening) text += "  キーを押してください…";
            label.text = text;
            label.color = selected ? new Color(1f, 0.85f, 0.3f) : Color.white;
            if (rowBackgrounds.Count > lane && rowBackgrounds[lane] != null)
            {
                rowBackgrounds[lane].color = selected ? SelectedRowColor : IdleRowColor;
            }
            if (accentBars.Count > lane && accentBars[lane] != null)
            {
                accentBars[lane].gameObject.SetActive(selected);
            }
        }
        UpdateLayoutStatus();
    }

    /// <summary>ヘッダー下の表示: 既定のキー配置かカスタムか (Issue #128)。</summary>
    private void UpdateLayoutStatus()
    {
        if (layoutLabel == null) return;
        if (IsCustomLayout())
        {
            layoutLabel.text = "現在: カスタム";
            layoutLabel.color = new Color(1f, 0.85f, 0.3f, 0.95f);
        }
        else
        {
            layoutLabel.text = "現在: 初期設定";
            layoutLabel.color = new Color(1f, 1f, 1f, 0.85f);
        }
    }

    /// <summary>listen モード中はアクセント バーを点滅させる。</summary>
    private void UpdateBlink()
    {
        if (accentBars.Count == 0) return;
        for (int lane = 0; lane < accentBars.Count; lane++)
        {
            var bar = accentBars[lane];
            if (bar == null) continue;
            if (lane == selectedLane && listening && open)
            {
                float a = 0.45f + 0.55f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f));
                bar.color = new Color(AccentColor.r, AccentColor.g, AccentColor.b, a);
            }
            else
            {
                bar.color = AccentColor;
            }
        }
    }

    private void UpdateMessage()
    {
        if (messageLabel == null) return;
        if (listening)
        {
            messageLabel.text = "レーン " + (selectedLane + 1) + ": キーを押して割り当て (同じキーで解除 / ↑↓・クリックで他のレーン / Esc 終了)";
            messageLabel.color = AccentColor;
        }
        else
        {
            messageLabel.text = string.Empty;
            messageLabel.color = Color.white;
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
        var canvasGO = new GameObject("KeyConfigCanvas", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;

        var scaler = canvasGO.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
    }

    private void BuildPanel()
    {
        var canvas = GetComponentInChildren<Canvas>().transform;

        // 全画面の暗転バックドロップ (レイキャストを遮ってモーダルにする)
        panel = new GameObject("KeyConfigModal", typeof(UnityEngine.UI.Image)).GetComponent<RectTransform>();
        panel.SetParent(canvas, false);
        panel.anchorMin = Vector2.zero;
        panel.anchorMax = Vector2.one;
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.offsetMin = Vector2.zero;
        panel.offsetMax = Vector2.zero;
        var backdrop = panel.GetComponent<UnityEngine.UI.Image>();
        backdrop.color = new Color(0f, 0f, 0f, 0.85f);
        backdrop.raycastTarget = true;

        // 中央のパネル本体 (不透明 #1B1B1B)
        var body = new GameObject("Panel", typeof(UnityEngine.UI.Image)).GetComponent<RectTransform>();
        body.SetParent(panel, false);
        body.anchorMin = new Vector2(0.5f, 0.5f);
        body.anchorMax = new Vector2(0.5f, 0.5f);
        body.pivot = new Vector2(0.5f, 0.5f);
        body.anchoredPosition = Vector2.zero;
        body.sizeDelta = new Vector2(PanelWidth, PanelHeight);
        var bodyImage = body.GetComponent<UnityEngine.UI.Image>();
        bodyImage.color = PanelColor;
        bodyImage.raycastTarget = true;
        BuildBorder(body);

        // 内容 (縦並びレイアウト)
        var content = new GameObject("Content", typeof(UnityEngine.UI.VerticalLayoutGroup)).GetComponent<RectTransform>();
        content.SetParent(body, false);
        content.anchorMin = Vector2.zero;
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(0.5f, 0.5f);
        content.offsetMin = new Vector2(28f, 24f);
        content.offsetMax = new Vector2(-28f, -24f);
        var layout = content.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var header = AddLabel(content, "KeyConfigHeader", "キー設定 (F2 で閉じる)", HeaderFontSize, TextAlignmentOptions.Center);
        header.rectTransform.gameObject.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 56f;

        // 既定のキー配置かカスタムかを常時表示する (Issue #128)
        layoutLabel = AddLabel(content, "KeyConfigLayoutStatus", string.Empty, MessageFontSize, TextAlignmentOptions.Center);
        layoutLabel.color = new Color(1f, 1f, 1f, 0.85f);
        layoutLabel.rectTransform.gameObject.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 34f;

        // 8 レーン行 (固定行高 + 選択ハイライト)
        var rowsRoot = new GameObject("Rows", typeof(UnityEngine.UI.VerticalLayoutGroup)).GetComponent<RectTransform>();
        rowsRoot.SetParent(content, false);
        rowsRoot.anchorMin = new Vector2(0f, 1f);
        rowsRoot.anchorMax = new Vector2(1f, 1f);
        rowsRoot.pivot = new Vector2(0.5f, 1f);
        var rowsLayout = rowsRoot.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
        rowsLayout.spacing = 4f;
        rowsLayout.childAlignment = TextAnchor.UpperLeft;
        rowsLayout.childControlWidth = true;
        rowsLayout.childControlHeight = true;
        rowsLayout.childForceExpandWidth = true;
        rowsLayout.childForceExpandHeight = false;
        var rowsSize = rowsRoot.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
        rowsSize.preferredHeight = KeyBindings.LaneCount * RowHeight + (KeyBindings.LaneCount - 1) * rowsLayout.spacing;

        for (int lane = 0; lane < KeyBindings.LaneCount; lane++)
        {
            AddRow(rowsRoot, lane);
        }

        messageLabel = AddLabel(content, "KeyConfigMessage", string.Empty, MessageFontSize, TextAlignmentOptions.Left);
        messageLabel.rectTransform.gameObject.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 40f;

        testLabel = AddLabel(content, "KeyConfigTest", string.Empty, MessageFontSize, TextAlignmentOptions.Left);
        testLabel.color = new Color(0.6f, 0.9f, 1f);
        testLabel.rectTransform.gameObject.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 34f;

        var footer = AddLabel(content, "KeyConfigFooter",
            "↑↓・クリック: レーン　Enter: 割り当て　Esc: 終了　BS: クリア　R: 初期化　F2: 閉じる",
            FooterFontSize, TextAlignmentOptions.Left);
        footer.color = new Color(1f, 1f, 1f, 0.75f);
        footer.rectTransform.gameObject.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 44f;

        RefreshRows();
        UpdateMessage();
    }

    /// <summary>パネルの 2px 枠 (#3FA9F5)。伸縮スプライトを使わず 4 本の Image で描く。</summary>
    private void BuildBorder(RectTransform body)
    {
        AddBorderBar(body, "BorderTop", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 2f));
        AddBorderBar(body, "BorderBottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 2f));
        AddBorderBar(body, "BorderLeft", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(2f, 0f));
        AddBorderBar(body, "BorderRight", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(2f, 0f));
    }

    private void AddBorderBar(RectTransform body, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 sizeDelta)
    {
        var bar = new GameObject(name, typeof(UnityEngine.UI.Image)).GetComponent<RectTransform>();
        bar.SetParent(body, false);
        bar.anchorMin = anchorMin;
        bar.anchorMax = anchorMax;
        bar.pivot = pivot;
        bar.anchoredPosition = Vector2.zero;
        bar.sizeDelta = sizeDelta;
        var image = bar.GetComponent<UnityEngine.UI.Image>();
        image.color = AccentColor;
        image.raycastTarget = false;
    }

    /// <summary>レーン行 (固定高・ハイライト背景 + 左端アクセント バー + テキスト)。</summary>
    private void AddRow(RectTransform parent, int lane)
    {
        var row = new GameObject("Lane" + lane, typeof(UnityEngine.UI.Image)).GetComponent<RectTransform>();
        row.SetParent(parent, false);
        var rowSize = row.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
        rowSize.preferredHeight = RowHeight;
        rowSize.minHeight = RowHeight;
        var background = row.GetComponent<UnityEngine.UI.Image>();
        background.color = IdleRowColor;
        background.raycastTarget = true;
        var rowButton = row.gameObject.AddComponent<UnityEngine.UI.Button>();
        rowButton.transition = UnityEngine.UI.Selectable.Transition.None;
        int laneIndex = lane;
        rowButton.onClick.AddListener(() => OnRowClicked(laneIndex));

        var bar = new GameObject("Accent", typeof(UnityEngine.UI.Image)).GetComponent<RectTransform>();
        bar.SetParent(row, false);
        bar.anchorMin = new Vector2(0f, 0f);
        bar.anchorMax = new Vector2(0f, 1f);
        bar.pivot = new Vector2(0f, 0.5f);
        bar.anchoredPosition = Vector2.zero;
        bar.sizeDelta = new Vector2(8f, 0f);
        var barImage = bar.GetComponent<UnityEngine.UI.Image>();
        barImage.color = AccentColor;
        barImage.raycastTarget = false;
        bar.gameObject.SetActive(false);

        var labelGO = new GameObject("Text", typeof(TextMeshProUGUI));
        labelGO.transform.SetParent(row, false);
        var tmp = labelGO.GetComponent<TextMeshProUGUI>();
        tmp.text = BuildLaneLabel(lane);
        tmp.fontSize = RowFontSize;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.margin = new Vector4(24f, 0f, 12f, 0f);
        tmp.raycastTarget = false;
        if (font != null) tmp.font = font;
        var labelRect = tmp.rectTransform;
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        rows.Add(tmp);
        rowBackgrounds.Add(background);
        accentBars.Add(barImage);
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

    private TextMeshProUGUI AddLabel(RectTransform parent, string name, string text, float fontSize, TextAlignmentOptions alignment)
    {
        var labelGO = new GameObject(name, typeof(TextMeshProUGUI));
        labelGO.transform.SetParent(parent, false);
        var tmp = labelGO.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = Color.white;
        tmp.alignment = alignment;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.raycastTarget = false;
        if (font != null) tmp.font = font;
        var size = labelGO.AddComponent<UnityEngine.UI.LayoutElement>();
        size.minHeight = fontSize + 6f;
        return tmp;
    }

    /// <summary>日本語グリフを持つフォントを探す (KeyGuideUI と同じ方針の独自実装)。
    /// KeyConfigOptionButton からも使うため public。</summary>
    public static TMP_FontAsset FindJapaneseFont()
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
        bool show = !open && (sceneName == "TitleScene" || sceneName == "SelectScene");
        if (hintLabel.gameObject.activeSelf != show)
        {
            hintLabel.gameObject.SetActive(show);
        }
    }
}
