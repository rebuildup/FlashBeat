using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>
/// 旧キー設定オーバーレイ (シーン非依存、コードのみで生成)。
/// v6 の Option / Pause は <see cref="FlashBeat.UI.V6.Common.V6KeyConfigModal"/>
/// (design vo4kB) を使う (Issue #376)。この画面は旧 v5 経路 (KeyConfigOptionButton /
/// v5 OptionView) の互換実装として残す。
///
/// F2 の入力監視は廃止した (決定 §4.1: キー設定への到達経路は画面内の行だけ)。
/// 開閉は <see cref="Open"/> / <see cref="Close"/> を呼ぶ。
///
/// Issue #167: SelectScene では開かない (Select 画面の v5 凡例は「F2 履歴」)。
/// </summary>
public class KeyConfigUI : MonoBehaviour
{
    private const float PanelWidth = 1100f;
    private const float PanelHeight = 900f;
    private const float RowHeight = 66f;
    private const float ActionRowHeight = 44f;
    private const float ActionRowFontSize = 22f;
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
    private RectTransform laneRowsRoot;
    private RectTransform actionRowsRoot;
    private readonly List<UnityEngine.UI.Image> tabBackgrounds = new List<UnityEngine.UI.Image>();
    private readonly List<TextMeshProUGUI> actionRows = new List<TextMeshProUGUI>();
    private readonly List<UnityEngine.UI.Image> actionRowBackgrounds = new List<UnityEngine.UI.Image>();
    private readonly List<UnityEngine.UI.Image> actionAccentBars = new List<UnityEngine.UI.Image>();
    private int page; // 0 = レーン, 1 = メニュー操作
    private int selectedLane;
    private int selectedAction;
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

    public static int TestSelectedAction
    {
        get
        {
            var ui = FindObjectOfType<KeyConfigUI>();
            return ui != null ? ui.selectedAction : -1;
        }
    }

    /// <summary>現在のページ (0 = レーン, 1 = メニュー操作)。</summary>
    public static int TestPage
    {
        get
        {
            var ui = FindObjectOfType<KeyConfigUI>();
            return ui != null ? ui.page : -1;
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

    public static GameObject TestActionRowObject(int index)
    {
        var ui = FindObjectOfType<KeyConfigUI>();
        if (ui == null || index < 0 || index >= ui.actionRows.Count || ui.actionRows[index] == null) { return null; }
        return ui.actionRows[index].transform.parent.gameObject;
    }


    /// <summary>
    /// キー設定を開けるシーン。SelectScene は入れない (Issue #167):
    /// Select 画面の F2 は v5 凡例どおり「履歴」(SelectView) が担う。
    /// </summary>
    private static readonly string[] AllowedScenes = { "TitleScene", "OptionScene" };

    /// <summary>純粋関数: そのシーン名でキー設定を開けるか (EditMode テストから検証する)。</summary>
    public static bool IsAllowedSceneName(string sceneName)
    {
        for (int i = 0; i < AllowedScenes.Length; i++)
        {
            if (AllowedScenes[i] == sceneName) return true;
        }
        return false;
    }

    /// <summary>
    /// いま開けるか。正本 uDNC0 (Pause) は「キー設定」行の値が F2 で、
    /// 説明文も「F2 でも開けます」と明記している。ポーズ中
    /// (<see cref="FlashBeat.Gameplay.PauseGate.IsPaused"/>) は判定・譜面が
    /// 止まっているので、GameScene でもポーズ経由に限り開ける。
    /// 演奏中 (ポーズしていない GameScene) は開かない (音符を叩くキーを奪うため)。
    /// </summary>
    public static bool IsAllowedNow(string sceneName)
    {
        if (IsAllowedSceneName(sceneName)) return true;
        return sceneName == "GameScene" && FlashBeat.Gameplay.PauseGate.IsPaused;
    }

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
        return IsAllowedNow(SceneManager.GetActiveScene().name);
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
        KeyActions.Changed += RefreshRows;
    }

    private void OnDestroy()
    {
        KeyBindings.Changed -= RefreshRows;
        KeyActions.Changed -= RefreshRows;
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
            if (typeName.EndsWith("SceneManager")
                // v5 の View (Issue #167): コード生成 UI 側の Update も止める。
                // 止めないとキー設定中に裏の画面が同じキー (矢印 / Enter / Esc / Tab) を
                // 二重に処理し、Esc で閉じた瞬間に遷移するなどの事故になる。
                || typeName == "TitleView" || typeName == "SelectView" || typeName == "ResultView"
                || typeName == "OptionView"
                // Tab で開くキーガイド / F3・F4 の更新通知もオーバーレイ中は止める。
                || typeName == "KeyGuideUI" || typeName == "UpdateNoticeUI"
                // キー設定中の F5 でランキングボードが重ねて開かないようにする。
                || typeName == "ResultRankingOverlay")
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
            bool lanesSaved = KeyBindings.Save();
            bool actionsSaved = KeyActions.Save();
            if (!lanesSaved || !actionsSaved)
            {
                messageLabel.text = !lanesSaved
                    ? "空のレーンがあるため保存できません (全レーンに 1 個以上キーを割り当ててください)"
                    : "キーが空の操作があるため保存できません (全ての操作に 1 個以上キーを割り当ててください)";
                return; // 閉じないで保存を促す
            }
        }

        open = value;
        IsOpen = value;
        // Issue #167: 開いたフレーム / 閉じたフレームは背面シーンに同じキーを
        // 処理させない (Esc で閉じた瞬間に Option → Title へ遷移する等の防止)。
        FlashBeat.UI.SceneInputGate.BlockThisFrame();
        if (value) { SuspendScene(); } else { ResumeScene(); }
        if (panel != null) panel.gameObject.SetActive(value);
        if (value)
        {
            listening = false;
            confirmReset = false;
            selectedLane = Mathf.Clamp(selectedLane, 0, KeyBindings.LaneCount - 1);
            selectedAction = Mathf.Clamp(selectedAction, 0, KeyActions.All.Length - 1);
            RefreshRows();
            UpdateMessage();
        }
    }

    private void HandleInput()
    {
        // ページ切替 (←→ / PageUp / PageDown。矢印・PageUp/Down は予約キーなので割り当て対象にならない)。
        if (KeyDown(KeyCode.LeftArrow) || KeyDown(KeyCode.PageUp)) { SwitchPage(page - 1); return; }
        if (KeyDown(KeyCode.RightArrow) || KeyDown(KeyCode.PageDown)) { SwitchPage(page + 1); return; }

        // 行移動 (割り当て中でも有効。矢印・Tab は予約キーなので割り当て対象にならない)。
        if (KeyDown(KeyCode.UpArrow)) { MoveSelection(-1); return; }
        if (KeyDown(KeyCode.DownArrow) || KeyDown(KeyCode.Tab)) { MoveSelection(1); return; }
        float wheel = Input.mouseScrollDelta.y;
        if (wheel > 0.5f) { MoveSelection(-1); return; }
        if (wheel < -0.5f) { MoveSelection(1); return; }

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

        if (KeyDown(KeyCode.Return) || KeyDown(KeyCode.KeypadEnter))
        {
            confirmReset = false;
            listening = true;
            RefreshRows();
            UpdateMessage();
        }
        else if (KeyDown(KeyCode.Backspace))
        {
            confirmReset = false;
            ClearSelection();
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

    private int RowCount()
    {
        return page == 0 ? KeyBindings.LaneCount : KeyActions.All.Length;
    }

    /// <summary>ページを切り替える (割り当て中なら終了する)。</summary>
    private void SwitchPage(int target)
    {
        int next = target % 2;
        if (next < 0) { next += 2; }
        if (next == page) { return; }
        confirmReset = false;
        listening = false;
        page = next;
        RefreshRows();
        UpdateMessage();
    }

    /// <summary>選択行を移動する。割り当て中なら終了して選択だけ動かす (Enter / クリックで再開)。</summary>
    private void MoveSelection(int delta)
    {
        int count = RowCount();
        confirmReset = false;
        listening = false;
        if (page == 0)
        {
            selectedLane = (selectedLane + delta + count) % count;
        }
        else
        {
            selectedAction = (selectedAction + delta + count) % count;
        }
        RefreshRows();
        UpdateMessage();
    }

    /// <summary>行クリック: その行を選んで割り当て開始。選択中の行を再度クリックすると割り当て終了。</summary>
    private void OnRowClicked(int lane)
    {
        confirmReset = false;
        if (page == 0 && selectedLane == lane && listening)
        {
            listening = false;
        }
        else
        {
            page = 0;
            selectedLane = lane;
            listening = true;
        }
        RefreshRows();
        UpdateMessage();
    }

    /// <summary>メニュー操作行クリック: その操作を選んで割り当て開始。選択中の行を再度クリックすると終了。</summary>
    private void OnActionRowClicked(int index)
    {
        confirmReset = false;
        if (page == 1 && selectedAction == index && listening)
        {
            listening = false;
        }
        else
        {
            page = 1;
            selectedAction = index;
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
        if (page == 0)
        {
            KeyBindings.ResetToDefaults();
            messageLabel.text = "キー割り当てを初期化しました";
        }
        else
        {
            KeyActions.ResetToDefaults();
            messageLabel.text = "メニュー操作の割り当てを初期化しました";
        }
    }

    /// <summary>選択中の行 (レーンまたは操作) のキーを消す。
    /// 編集中は空にできるが、保存 (閉じる) には全レーン・全操作にキーが必要。</summary>
    private void ClearSelection()
    {
        if (page == 0)
        {
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
        else
        {
            KeyActions.KeyAction action = KeyActions.All[selectedAction];
            KeyActions.Set(action, new KeyCode[0]);
            RefreshRows();
            messageLabel.text = "『" + KeyActions.GetDisplayName(action) + "』のキーを消しました (閉じる前にキーを割り当ててください)";
        }
    }

    /// <summary>listen モード中に押されたキーを選択中の行に割り当てる (トグル)。
    /// メニュー操作ページでは予約キー (Esc/矢印/Tab 以外) も候補にする —
    /// 演奏中に使う操作 (再生開始など) は予約キーしか割り当てできないため。</summary>
    private void CapturePressedKeys()
    {
        KeyCode[] candidates = GetBindableKeys();
        for (int i = 0; i < candidates.Length; i++)
        {
            KeyCode key = candidates[i];
            if (!KeyDown(key)) continue;

            ShowTestKey(key);
            if (page == 0)
            {
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
            else
            {
                KeyActions.KeyAction action = KeyActions.All[selectedAction];
                if (ToggleActionKey(action, key))
                {
                    RefreshRows();
                    UpdateMessage();
                }
                else
                {
                    messageLabel.text = KeyActions.LastError;
                }
            }
        }

        if (page == 1)
        {
            for (int i = 0; i < ReservedCandidates.Length; i++)
            {
                KeyCode key = ReservedCandidates[i];
                if (!KeyDown(key)) continue;

                ShowTestKey(key);
                KeyActions.KeyAction action = KeyActions.All[selectedAction];
                if (ToggleActionKey(action, key))
                {
                    RefreshRows();
                    UpdateMessage();
                }
                else
                {
                    messageLabel.text = KeyActions.LastError;
                }
            }
        }
    }

    /// <summary>メニュー操作ページで listen 中に割り当て可能な予約キー
    /// (Esc は listen 中止、矢印/Tab/PageUp/PageDown はページ内移動に使うため対象外)。</summary>
    private static readonly KeyCode[] ReservedCandidates = new KeyCode[]
    {
        KeyCode.Space, KeyCode.Return, KeyCode.KeypadEnter, KeyCode.Backspace,
        KeyCode.F1, KeyCode.F3, KeyCode.F4, KeyCode.F5, KeyCode.F6, KeyCode.F7,
        KeyCode.F8, KeyCode.F9, KeyCode.F10, KeyCode.F11, KeyCode.F12,
        KeyCode.F13, KeyCode.F14, KeyCode.F15
    };

    /// <summary>操作へのキー トグル: 割り当て済みなら外し、未割り当てなら足す。
    /// 最後の 1 個を外すのは許可する (閉じる時の保存が空操作を拒否する)。</summary>
    private bool ToggleActionKey(KeyActions.KeyAction action, KeyCode key)
    {
        KeyCode[] currentKeys = KeyActions.Get(action);
        bool had = false;
        int index = -1;
        for (int i = 0; i < currentKeys.Length; i++)
        {
            if (currentKeys[i] == key) { had = true; index = i; break; }
        }

        var next = new KeyCode[had ? currentKeys.Length - 1 : currentKeys.Length + 1];
        int w = 0;
        for (int i = 0; i < currentKeys.Length; i++)
        {
            if (i != index) { next[w++] = currentKeys[i]; }
        }
        if (!had) { next[next.Length - 1] = key; }
        return KeyActions.Set(action, next);
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
        if (laneRowsRoot != null && laneRowsRoot.gameObject.activeSelf != (page == 0))
        {
            laneRowsRoot.gameObject.SetActive(page == 0);
        }
        if (actionRowsRoot != null && actionRowsRoot.gameObject.activeSelf != (page == 1))
        {
            actionRowsRoot.gameObject.SetActive(page == 1);
        }
        if (tabBackgrounds.Count == 2)
        {
            tabBackgrounds[0].color = (page == 0) ? SelectedRowColor : IdleRowColor;
            tabBackgrounds[1].color = (page == 1) ? SelectedRowColor : IdleRowColor;
        }

        for (int lane = 0; lane < rows.Count; lane++)
        {
            var label = rows[lane];
            if (label == null) continue;
            bool selected = page == 0 && lane == selectedLane && open;
            string text = BuildLaneLabel(lane);
            if (page == 0 && lane == selectedLane && listening) text += "  キーを押してください…";
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

        for (int i = 0; i < actionRows.Count; i++)
        {
            var label = actionRows[i];
            if (label == null) continue;
            bool selected = page == 1 && i == selectedAction && open;
            string text = BuildActionLabel(i);
            if (page == 1 && i == selectedAction && listening) text += "  キーを押してください…";
            label.text = text;
            label.color = selected ? new Color(1f, 0.85f, 0.3f) : Color.white;
            if (actionRowBackgrounds.Count > i && actionRowBackgrounds[i] != null)
            {
                actionRowBackgrounds[i].color = selected ? SelectedRowColor : IdleRowColor;
            }
            if (actionAccentBars.Count > i && actionAccentBars[i] != null)
            {
                actionAccentBars[i].gameObject.SetActive(selected);
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
        UpdateBlinkBars(accentBars, selectedLane, page == 0);
        UpdateBlinkBars(actionAccentBars, selectedAction, page == 1);
    }

    private void UpdateBlinkBars(List<UnityEngine.UI.Image> bars, int selectedIndex, bool activePage)
    {
        if (bars.Count == 0) return;
        for (int i = 0; i < bars.Count; i++)
        {
            var bar = bars[i];
            if (bar == null) continue;
            if (i == selectedIndex && listening && open && activePage)
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
            messageLabel.text = (page == 0)
                ? "レーン " + (selectedLane + 1) + ": キーを押して割り当て (同じキーで解除 / ↑↓・クリックで他のレーン / Esc 終了)"
                : "『" + KeyActions.GetDisplayName(KeyActions.All[selectedAction]) + "』: キーを押して割り当て (同じキーで解除 / ↑↓・クリックで他の操作 / Esc 終了)";
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

    private string BuildActionLabel(int index)
    {
        if (index < 0 || index >= KeyActions.All.Length) return string.Empty;
        KeyActions.KeyAction action = KeyActions.All[index];
        return KeyActions.GetDisplayName(action) + ":  " + GetKeysText(KeyActions.Get(action));
    }

    /// <summary>キー配列の表示名 (スラッシュ区切り)。ヘルプ表示 (push space など) と共有する。</summary>
    public static string GetKeysText(KeyCode[] keys)
    {
        if (keys == null || keys.Length == 0) { return "(キーなし)"; }
        var sb = new StringBuilder();
        for (int i = 0; i < keys.Length; i++)
        {
            if (i > 0) sb.Append(" / ");
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

        var header = AddLabel(content, "KeyConfigHeader", "キー設定", HeaderFontSize, TextAlignmentOptions.Center);
        header.rectTransform.gameObject.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 56f;
        // 閉じるキーは文章ではなく [F2] キャップで示す。
        FlashBeat.UI.V5.ShortcutKeyCap.AttachCapsToLabel(header, new[] { "F2" }, HeaderFontSize * 0.62f);

        // 既定のキー配置かカスタムかを常時表示する (Issue #128)
        layoutLabel = AddLabel(content, "KeyConfigLayoutStatus", string.Empty, MessageFontSize, TextAlignmentOptions.Center);
        layoutLabel.color = new Color(1f, 1f, 1f, 0.85f);
        layoutLabel.rectTransform.gameObject.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 34f;

        // ページ切替タブ (クリックでも ←→ / PageUp / PageDown でも切替)
        var tabs = new GameObject("PageTabs", typeof(UnityEngine.UI.HorizontalLayoutGroup)).GetComponent<RectTransform>();
        tabs.SetParent(content, false);
        var tabsLayout = tabs.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        tabsLayout.spacing = 8f;
        tabsLayout.childAlignment = TextAnchor.UpperLeft;
        tabsLayout.childControlWidth = false;
        tabsLayout.childControlHeight = false;
        tabsLayout.childForceExpandWidth = false;
        tabsLayout.childForceExpandHeight = false;
        var tabsSize = tabs.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
        tabsSize.preferredHeight = 40f;
        AddPageTab(tabs, "LaneTab", "レーンキー", 0);
        AddPageTab(tabs, "ActionTab", "メニュー操作", 1);

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
        laneRowsRoot = rowsRoot;

        for (int lane = 0; lane < KeyBindings.LaneCount; lane++)
        {
            AddRow(rowsRoot, lane);
        }

        // メニュー操作行 (ページ 2。最初は非表示)
        var actionsRoot = new GameObject("ActionRows", typeof(UnityEngine.UI.VerticalLayoutGroup)).GetComponent<RectTransform>();
        actionsRoot.SetParent(content, false);
        actionsRoot.anchorMin = new Vector2(0f, 1f);
        actionsRoot.anchorMax = new Vector2(1f, 1f);
        actionsRoot.pivot = new Vector2(0.5f, 1f);
        var actionsLayout = actionsRoot.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
        actionsLayout.spacing = 4f;
        actionsLayout.childAlignment = TextAnchor.UpperLeft;
        actionsLayout.childControlWidth = true;
        actionsLayout.childControlHeight = true;
        actionsLayout.childForceExpandWidth = true;
        actionsLayout.childForceExpandHeight = false;
        var actionsSize = actionsRoot.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
        actionsSize.preferredHeight = KeyActions.All.Length * ActionRowHeight + (KeyActions.All.Length - 1) * actionsLayout.spacing;
        actionsRoot.gameObject.SetActive(false);
        actionRowsRoot = actionsRoot;

        for (int i = 0; i < KeyActions.All.Length; i++)
        {
            AddActionRow(actionsRoot, i);
        }

        messageLabel = AddLabel(content, "KeyConfigMessage", string.Empty, MessageFontSize, TextAlignmentOptions.Left);
        messageLabel.rectTransform.gameObject.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 40f;

        testLabel = AddLabel(content, "KeyConfigTest", string.Empty, MessageFontSize, TextAlignmentOptions.Left);
        testLabel.color = new Color(0.6f, 0.9f, 1f);
        testLabel.rectTransform.gameObject.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 34f;

        // 説明文の羅列ではなく、キーキャップで「どのキーが何をするか」を見せる。
        var footerA = FlashBeat.UI.V5.ShortcutKeyCap.BuildCapRow(content, new[]
        {
            ("←→", "切替"),
            ("PgUp", "切替"),
            ("PgDn", "切替"),
            ("Tab", "選択"),
            ("↑↓", "選択"),
        }, FooterFontSize, FooterFontSize);
        footerA.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 32f;

        var footerB = FlashBeat.UI.V5.ShortcutKeyCap.BuildCapRow(content, new[]
        {
            ("Enter", "割り当て"),
            ("Esc", "終了"),
            ("BS", "クリア"),
            ("R", "初期化"),
            ("F2", "閉じる"),
        }, FooterFontSize, FooterFontSize);
        footerB.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 32f;

        // 保存先の設定ファイルパス (オペレーターがバックアップ/確認できるように表示する)
        var filePathLabel = AddLabel(content, "KeyConfigFilePath", "設定ファイル: " + KeyConfigFile.FilePath, 14f, TextAlignmentOptions.Left);
        filePathLabel.color = new Color(1f, 1f, 1f, 0.5f);
        filePathLabel.rectTransform.gameObject.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 20f;

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

    /// <summary>ページ切替タブ (背景色でアクティブページを示す)。</summary>
    private void AddPageTab(RectTransform parent, string name, string text, int pageIndex)
    {
        var tab = new GameObject(name, typeof(UnityEngine.UI.Image)).GetComponent<RectTransform>();
        tab.SetParent(parent, false);
        var layout = tab.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
        layout.preferredHeight = 36f;
        layout.minWidth = 200f;
        var background = tab.GetComponent<UnityEngine.UI.Image>();
        background.color = IdleRowColor;
        background.raycastTarget = true;
        var tabButton = tab.gameObject.AddComponent<UnityEngine.UI.Button>();
        tabButton.transition = UnityEngine.UI.Selectable.Transition.None;
        tabButton.onClick.AddListener(() => SwitchPage(pageIndex));

        var labelGO = new GameObject("Text", typeof(TextMeshProUGUI));
        labelGO.transform.SetParent(tab, false);
        var tmp = labelGO.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 22f;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        if (font != null) tmp.font = font;
        var labelRect = tmp.rectTransform;
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        tabBackgrounds.Add(background);
    }

    /// <summary>メニュー操作行 (レーン行と同じ構成: 背景 + アクセント バー + テキスト + クリック)。</summary>
    private void AddActionRow(RectTransform parent, int index)
    {
        var row = new GameObject("Action" + index, typeof(UnityEngine.UI.Image)).GetComponent<RectTransform>();
        row.SetParent(parent, false);
        var rowSize = row.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
        rowSize.preferredHeight = ActionRowHeight;
        rowSize.minHeight = ActionRowHeight;
        var background = row.GetComponent<UnityEngine.UI.Image>();
        background.color = IdleRowColor;
        background.raycastTarget = true;
        var rowButton = row.gameObject.AddComponent<UnityEngine.UI.Button>();
        rowButton.transition = UnityEngine.UI.Selectable.Transition.None;
        int actionIndex = index;
        rowButton.onClick.AddListener(() => OnActionRowClicked(actionIndex));

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
        tmp.text = BuildActionLabel(index);
        tmp.fontSize = ActionRowFontSize;
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

        actionRows.Add(tmp);
        actionRowBackgrounds.Add(background);
        actionAccentBars.Add(barImage);
    }

    private void BuildHint()
    {
        var canvas = GetComponentInChildren<Canvas>().transform;
        var labelGO = new GameObject("KeyConfigHint", typeof(TextMeshProUGUI));
        labelGO.transform.SetParent(canvas, false);
        hintLabel = labelGO.GetComponent<TextMeshProUGUI>();
        hintLabel.text = "キー設定";
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

        // [F2] キャップを左に置いて、キーボードで呼べる操作だと分かるようにする。
        var cap = FlashBeat.UI.V5.ShortcutKeyCap.Create(rect.parent as RectTransform, "F2", 18f);
        var capRect = cap.GetComponent<RectTransform>();
        capRect.anchorMin = new Vector2(0f, 0f);
        capRect.anchorMax = new Vector2(0f, 0f);
        capRect.pivot = new Vector2(0f, 0f);
        capRect.anchoredPosition = new Vector2(16f, 16f);
        rect.anchoredPosition = new Vector2(16f + capRect.sizeDelta.x + 8f, 16f);
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

    /// <summary>Title 画面でのみヒントを表示する (Select の F2 は履歴 / Option はキー設定ボタン)。</summary>
    private void UpdateHint()
    {
        if (hintLabel == null) return;
        string sceneName = SceneManager.GetActiveScene().name;
        // v6 タイトルはフッターにキー凡例 (決定 / 曲送り / 戻る) を持つので
        // 旧ヒントは二重表示になる (Update 毎フレームで再表示される遅延生成物。
        // v6 モードではここで確実に隠す)。
        bool v6Title = FlashBeat.UI.V6.Title.V6TitleView.Enabled;
        bool show = !open && sceneName == "TitleScene" && !v6Title;
        if (hintLabel.gameObject.activeSelf != show)
        {
            hintLabel.gameObject.SetActive(show);
        }
    }
}
