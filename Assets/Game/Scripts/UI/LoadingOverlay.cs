using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 全画面の上に重なるローディング表示 (コード生成 uGUI、シーン編集なし)。
///
/// 使い手は 3 つ:
/// - Initiate.Fade の非同期シーン読み込み (Fader、オーナーなし = 排他的)
/// - GameScene のメディア準備 (MusicManager、オーナー "media")
/// - GameScene の譜面準備 (NotesManager、オーナー "notes")
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
    float requestedAtRealtime = -1f; // 最初の Show の时刻 (表示前の遅延計測)
    float shownAtRealtime = -1f;     // 実際に見え始めた时刻 (最短表示の計測)
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
        Initiate.LoadingFeedback.Begin += sceneName => Show(LoadingOverlayLogic.SceneLabel(sceneName));
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
        }
        else if (shownAtRealtime < 0f)
        {
            // 表示前に全オーナーが外れた: 表示要求をキャンセル
            requestedAtRealtime = -1f;
        }
        else if (LoadingOverlayLogic.CanHide((now - shownAtRealtime) * 1000.0))
        {
            fadingOut = true;
        }

        if (fadingOut)
        {
            canvasGroup.alpha -= Time.unscaledDeltaTime / FadeOutSeconds;
            if (canvasGroup.alpha <= 0f)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.blocksRaycasts = false;
                shownAtRealtime = -1f;
                requestedAtRealtime = -1f;
                progressValue = -1f;
                fadingOut = false;
            }
        }

        if (canvasGroup.alpha > 0f)
        {
            // スピナー: unscaled 時間で等速回転
            if (spinnerPivot != null)
            {
                float angle = spinnerPivot.localRotation.eulerAngles.z - 360f * Time.unscaledDeltaTime;
                spinnerPivot.localRotation = Quaternion.Euler(0f, 0f, angle);
            }

            // 進捗バー: 指定値 or 不定スウィープ
            if (progressBar != null)
            {
                progressBar.fillAmount = progressValue >= 0f
                    ? progressValue
                    : 0.15f + 0.7f * Mathf.PingPong(now * 0.4f, 1f);
            }
        }
    }

    void BuildUI()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortOrder;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        gameObject.AddComponent<GraphicRaycaster>();

        canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;

        // 日本語フォント: 他のコード生成 UI (KeyGuideUI) と同じ方法で取得する
        font = FindFirstObjectByType<TextMeshProUGUI>()?.font;
        if (font == null) font = TMP_Settings.defaultFontAsset;

        // 暗幕 (全画面)
        var dim = CreateChild("Dim", transform);
        Stretch(dim);
        var dimImage = dim.gameObject.AddComponent<Image>();
        dimImage.color = new Color(0f, 0f, 0f, 0.65f);

        // 中央パネル
        var panel = CreateChild("Panel", transform);
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(560f, 240f);
        panel.anchoredPosition = Vector2.zero;

        // スピナー: 中心ピボットの周りを回る短いバー
        spinnerPivot = CreateChild("SpinnerPivot", panel);
        spinnerPivot.anchorMin = spinnerPivot.anchorMax = new Vector2(0.5f, 0.5f);
        spinnerPivot.sizeDelta = Vector2.zero;
        spinnerPivot.anchoredPosition = new Vector2(0f, 55f);
        var spinner = CreateChild("Spinner", spinnerPivot);
        spinner.anchorMin = spinner.anchorMax = new Vector2(0.5f, 0.5f);
        spinner.sizeDelta = new Vector2(72f, 10f);
        spinner.anchoredPosition = new Vector2(0f, 36f);
        var spinnerImage = spinner.gameObject.AddComponent<Image>();
        spinnerImage.color = new Color(0.55f, 0.8f, 1f);

        // 見出し
        var title = CreateChild("Title", panel);
        title.anchorMin = title.anchorMax = new Vector2(0.5f, 0.5f);
        title.sizeDelta = new Vector2(520f, 50f);
        title.anchoredPosition = new Vector2(0f, -15f);
        var titleText = title.gameObject.AddComponent<TextMeshProUGUI>();
        titleText.text = "読み込み中…";
        titleText.fontSize = 32f;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = Color.white;
        if (font != null) titleText.font = font;

        // 進捗バー
        var barBack = CreateChild("ProgressBack", panel);
        barBack.anchorMin = barBack.anchorMax = new Vector2(0.5f, 0.5f);
        barBack.sizeDelta = new Vector2(420f, 14f);
        barBack.anchoredPosition = new Vector2(0f, -60f);
        var barBackImage = barBack.gameObject.AddComponent<Image>();
        barBackImage.color = new Color(1f, 1f, 1f, 0.2f);
        var barFill = CreateChild("ProgressFill", barBack);
        barFill.anchorMin = Vector2.zero;
        barFill.anchorMax = Vector2.one;
        barFill.offsetMin = new Vector2(2f, 2f);
        barFill.offsetMax = new Vector2(-2f, -2f);
        progressBar = barFill.gameObject.AddComponent<Image>();
        progressBar.type = Image.Type.Filled;
        progressBar.fillMethod = Image.FillMethod.Horizontal;
        progressBar.fillAmount = 0f;
        progressBar.color = new Color(0.3f, 0.7f, 1f);

        // 読み込み先 / 段階の文言
        var sub = CreateChild("Subtitle", panel);
        sub.anchorMin = sub.anchorMax = new Vector2(0.5f, 0.5f);
        sub.sizeDelta = new Vector2(520f, 30f);
        sub.anchoredPosition = new Vector2(0f, -92f);
        subtitle = sub.gameObject.AddComponent<TextMeshProUGUI>();
        subtitle.fontSize = 22f;
        subtitle.alignment = TextAlignmentOptions.Center;
        subtitle.color = new Color(0.85f, 0.85f, 0.85f);
        if (font != null) subtitle.font = font;
    }

    static RectTransform CreateChild(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        return rect;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
