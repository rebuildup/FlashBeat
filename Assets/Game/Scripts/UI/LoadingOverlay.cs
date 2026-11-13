using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// GameScene の曲準備でどのローディング表示を使うか決める。
/// v6 有効時は v6 の V6LoadingView が単独で表示する (<see cref="GameLoadingRoute"/>)。2 枚重ねない。
/// </summary>
public static class GameLoadingRoute
{
    /// <summary>どのローディング画面を使うか。</summary>
    public enum GameLoadingScreen { Legacy, V6 }

    /// <summary>v6 のゲーム HUD が有効なら v6 が曲の準備画面を持つ。</summary>
    public static GameLoadingScreen ForGameScene(bool v6GameHud)
    {
        return v6GameHud ? GameLoadingScreen.V6 : GameLoadingScreen.Legacy;
    }

    /// <summary>現在の設定での所有者。</summary>
    public static GameLoadingScreen Current
    {
        get { return ForGameScene(FlashBeat.UI.V6.V6UiMode.GameHud); }
    }

    /// <summary>
    /// 旧 <see cref="LoadingOverlay"/> / <c>MediaLoadingOverlay</c> を出してよいか。
    /// GameScene の曲準備では v6 有効時に false になる (二重表示の回避)。
    /// </summary>
    public static bool UsesLegacyOverlay
    {
        get { return Current == GameLoadingScreen.Legacy; }
    }
}

/// <summary>
/// 全画面の上に重なるローディング表示 (コード生成 uGUI、シーン編集なし)。
///
/// 使い手は 3 つ:
/// - Initiate.Fade の非同期シーン読み込み (Fader、オーナーなし = 排他的)
/// - GameScene のメディア準備 (MusicManager、オーナー "media") ※v6 無効時のみ
/// - GameScene の譜面準備 (NotesManager、オーナー "notes") ※v6 無効時のみ
///
/// 表示制御:
/// - Show から 250ms 経っても読み込みが続いていなければ表示しない (点滅防止)
/// - 一度出たら最短 400ms 表示する
/// - スピナーは unscaled 時間で回る (タイムスケールに影響されない)
/// - 進捗バーは SetProgress の値、未指定ならスウィープで「処理中」を示す
///
/// オーナー: Show(label, owner) の owner ごとに Hide される。owner == null は
/// 排他的な全画面読み込み (シーン遷移) を意味し、他のオーナーをリセットする。
/// </summary>
public class LoadingOverlay : MonoBehaviour
{
    const float FadeInSeconds = 0.15f;
    const float FadeOutSeconds = 0.2f;
    const int SortOrder = 30000;

    /// <summary>MusicManager (メディア準備) のオーナーキー。</summary>
    public const string MediaOwnerKey = "media";

    /// <summary>NotesManager (譜面準備) のオーナーキー。</summary>
    public const string NotesOwnerKey = "notes";

    static LoadingOverlay instance;
    static readonly List<object> activeOwners = new List<object>();
    static bool subscribedToFadeSystem;

    CanvasGroup canvasGroup;
    RectTransform spinnerPivot;
    Image progressBar;
    TextMeshProUGUI subtitle;
    TMP_FontAsset font;

    // unscaled 時間で駆動する状態機械
    float requestedAtRealtime = -1f; // 最初の Show の時刻 (表示前の遅延計測)
    float shownAtRealtime = -1f;     // 実際に見え始めた時刻 (最短表示の計測)
    float progressValue = -1f;       // -1 = 不定 (スウィープ表示)
    bool fadingOut;

    /// <summary>読み込みを開始する。owner == null は排他的 (他のオーナーをリセット)。</summary>
    public static void Show(string label, object owner = null)
    {
        var overlay = EnsureInstance();
        if (owner == null)
        {
            activeOwners.Clear();
        }
        else if (!activeOwners.Contains(owner))
        {
            activeOwners.Add(owner);
        }
        overlay.subtitle.text = label ?? "";
        if (overlay.requestedAtRealtime < 0f)
        {
            overlay.requestedAtRealtime = Time.realtimeSinceStartup;
        }
        overlay.fadingOut = false;
    }

    /// <summary>
    /// 表示前の遅延 (<see cref="LoadingOverlayLogic.ShowDelayMs"/>) を待たずに、
    /// 次のフレームで出す。シーン遷移は「押した直後に何か起きた」ことが
    /// 見える必要があるため (Issue #378)。短い読み込みで点滅しないことは
    /// 最短表示時間 (<see cref="LoadingOverlayLogic.MinDisplayMs"/>) が担保する。
    /// </summary>
    public static void ShowImmediately(string label, object owner = null)
    {
        Show(label, owner);
        var overlay = EnsureInstance();
        overlay.requestedAtRealtime = Time.realtimeSinceStartup
            - (float)(LoadingOverlayLogic.ShowDelayMs / 1000.0);
    }

    /// <summary>進捗バーを更新する (0..1)。呼ばれない間はスウィープ表示。</summary>
    public static void SetProgress(float normalized, object owner = null)
    {
        if (instance == null) return;
        instance.progressValue = Mathf.Clamp01(normalized);
    }

    /// <summary>読み込み終了。owner == null は全オーナーを解除する。</summary>
    public static void Hide(object owner = null)
    {
        if (instance == null) return;
        if (owner == null)
        {
            activeOwners.Clear();
        }
        else
        {
            activeOwners.Remove(owner);
        }
        // 最短表示時間は Update 側で判定する (ここでは要求を記録するだけ)。
    }

    /// <summary>SimpleFadeSystem (Fader) からの進捗フックを受け取る。</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void SubscribeToFadeSystem()
    {
        if (subscribedToFadeSystem) return;
        subscribedToFadeSystem = true;
        Initiate.LoadingFeedback.Begin += sceneName =>
        {
            // Issue #431: v6 のゲームローディング (V6LoadingView) が表示を
            // 所有する GameScene への遷移では、旧 LoadingOverlay
            // (sortingOrder 30000 の背景 + スピナー + バー + 字幕) を出さない。
            // v6 ローディングの上に無駄な要素が被って邪魔になる。
            if (sceneName == "GameScene"
                && GameLoadingRoute.Current == GameLoadingRoute.GameLoadingScreen.V6)
            {
                return;
            }

            ShowImmediately(LoadingOverlayLogic.SceneLabel(sceneName));
        };
        Initiate.LoadingFeedback.Progress += progress => SetProgress(progress);
        Initiate.LoadingFeedback.End += () => Hide();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    /// <summary>
    /// 前のシーンのオーナー (破棄されたマネージャの名前) を掃除する。
    /// 新しいシーンのマネージャは Start で再度 Show する。最短表示時間の
    /// 保証があるので一瞬も消えない。
    /// </summary>
    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        activeOwners.Remove(MediaOwnerKey);
        activeOwners.Remove(NotesOwnerKey);
    }

    static LoadingOverlay EnsureInstance()
    {
        if (instance != null) return instance;
        var go = new GameObject("LoadingOverlay");
        instance = go.AddComponent<LoadingOverlay>();
        DontDestroyOnLoad(go);
        return instance;
    }

    void Awake()
    {
        BuildUI();
    }

    void Update()
    {
        float now = Time.realtimeSinceStartup;

        if (activeOwners.Count > 0)
        {
            fadingOut = false;
            if (shownAtRealtime < 0f)
            {
                if (LoadingOverlayLogic.ShouldBecomeVisible((now - requestedAtRealtime) * 1000.0))
                {
                    shownAtRealtime = now;
                    canvasGroup.alpha = 0f;
                    canvasGroup.blocksRaycasts = true;
                }
            }
            else
            {
                canvasGroup.alpha = Mathf.Min(1f, canvasGroup.alpha + Time.unscaledDeltaTime / FadeInSeconds);
            }

            // スピナー回転 (unscaled)
            if (spinnerPivot != null)
            {
                spinnerPivot.Rotate(0f, 0f, -360f * Time.unscaledDeltaTime / 1.2f);
            }

            // 進捗バー: 値があればそれを、なければ不定スウィープ
            if (progressBar != null)
            {
                if (progressValue >= 0f)
                {
                    progressBar.fillAmount = progressValue;
                }
                else
                {
                    // 不定インジケータ: 往復 (0 → 1 → 0)
                    float t = (now % 1.6f) / 1.6f;
                    float tri = t < 0.5f ? t * 2f : 2f - t * 2f;
                    progressBar.fillAmount = tri;
                }
            }
        }
        else if (!fadingOut)
        {
            // すべてのオーナーが解除された → フェードアウトして破棄
            fadingOut = true;
        }

        if (fadingOut)
        {
            if (shownAtRealtime >= 0f &&
                !LoadingOverlayLogic.CanHide((now - shownAtRealtime) * 1000.0))
            {
                // 最短表示時間を満たすまで待つ
                return;
            }
            canvasGroup.alpha = Mathf.Max(0f, canvasGroup.alpha - Time.unscaledDeltaTime / FadeOutSeconds);
            if (canvasGroup.alpha <= 0f)
            {
                canvasGroup.blocksRaycasts = false;
                requestedAtRealtime = -1f;
                shownAtRealtime = -1f;
                progressValue = -1f;
                fadingOut = false;
            }
        }
    }

    void BuildUI()
    {
        // Canvas (overlay, 最前面)
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortOrder;

        canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;

        // 背景 (半透明黒)
        var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(transform, false);
        var bgRect = bg.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = bgRect.offsetMax = Vector2.zero;
        var bgImage = bg.GetComponent<Image>();
        bgImage.color = new Color(0f, 0f, 0f, 0.85f);
        bgImage.raycastTarget = true;

        // スピナー (回る円)
        var spinner = new GameObject("Spinner", typeof(RectTransform), typeof(Image));
        spinner.transform.SetParent(transform, false);
        var spRect = spinner.GetComponent<RectTransform>();
        spRect.anchorMin = spRect.anchorMax = new Vector2(0.5f, 0.5f);
        spRect.sizeDelta = new Vector2(80f, 80f);
        spRect.anchoredPosition = new Vector2(0f, 40f);
        var spImage = spinner.GetComponent<Image>();
        spImage.color = Color.white;
        spImage.raycastTarget = false;
        // Unity 内蔵の Knob テクスチャをリングとして使う
        spImage.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd");
        spImage.type = Image.Type.Filled;
        spImage.fillMethod = Image.FillMethod.Radial360;
        spImage.fillOrigin = (int)Image.Origin360.Top;
        spImage.fillAmount = 1f;
        spinnerPivot = spRect;

        // 進捗バー (スピナーの下)
        var bar = new GameObject("ProgressBar", typeof(RectTransform), typeof(Image));
        bar.transform.SetParent(transform, false);
        var barRect = bar.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0.5f, 0.5f);
        barRect.anchorMax = new Vector2(0.5f, 0.5f);
        barRect.sizeDelta = new Vector2(300f, 6f);
        barRect.anchoredPosition = new Vector2(0f, -30f);
        progressBar = bar.GetComponent<Image>();
        progressBar.color = new Color(1f, 1f, 1f, 0.9f);
        progressBar.raycastTarget = false;
        progressBar.type = Image.Type.Filled;
        progressBar.fillMethod = Image.FillMethod.Horizontal;
        progressBar.fillOrigin = (int)Image.OriginHorizontal.Left;
        progressBar.fillAmount = 0f;

        // 字幕 (バーの下)
        var sub = new GameObject("Subtitle", typeof(RectTransform), typeof(TextMeshProUGUI));
        sub.transform.SetParent(transform, false);
        var subRect = sub.GetComponent<RectTransform>();
        subRect.anchorMin = new Vector2(0.5f, 0.5f);
        subRect.anchorMax = new Vector2(0.5f, 0.5f);
        subRect.sizeDelta = new Vector2(500f, 40f);
        subRect.anchoredPosition = new Vector2(0f, -70f);
        subtitle = sub.GetComponent<TextMeshProUGUI>();
        subtitle.alignment = TextAlignmentOptions.Center;
        subtitle.fontSize = 24;
        subtitle.color = Color.white;
        subtitle.raycastTarget = false;
        subtitle.enableAutoSizing = true;
        subtitle.fontSizeMin = 18;
        subtitle.fontSizeMax = 28;

        // 日本語フォントを試みる (Resources からフォントアセットを探す)
        font = Resources.Load<TMP_FontAsset>("Fonts/Japanese/NotoSansJP-Medium SDF");
        if (font != null) subtitle.font = font;
    }
}