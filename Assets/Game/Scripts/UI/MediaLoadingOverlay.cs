using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 動画 (YouTube) のダウンロード・準備中に画面へ出す「読み込み中」表示。
///
/// - シーンは編集せず、実行時に uGUI を組み立てる (v5 UI ラインと衝突しないため)。
/// - Canvas は sortingOrder 6000 (KeyConfigUI 5000 / RankingCanvas 4000 より上)。
/// - 入力は妨げない (raycastTarget = false)。失敗時は理由をutum、一定時間後に自動で隠す。
/// </summary>
public class MediaLoadingOverlay : MonoBehaviour
{
    /// <summary>Canvas の描画順 (他 UI より手前)。</summary>
    public const int CanvasSortingOrder = 6000;

    /// <summary>失敗表示を自動で消すまでの秒数。</summary>
    public const float ErrorAutoHideSeconds = 3.5f;

    private static MediaLoadingOverlay instance;

    private GameObject root;
    private GameObject canvasObject;
    private TextMeshProUGUI titleLabel;
    private TextMeshProUGUI statusLabel;
    private TextMeshProUGUI hintLabel;
    private RectTransform spinner;
    private float spinnerAngle;
    private float errorHideAt = -1f;

    /// <summary>表示中かどうか。</summary>
    public bool IsVisible
    {
        get { return root != null && root.activeSelf; }
    }

    /// <summary>見出しの文字列 (テスト・状態表示用)。</summary>
    public string TitleText
    {
        get { return titleLabel != null ? titleLabel.text : string.Empty; }
    }

    /// <summary>状態文の文字列 (テスト・状態表示用)。</summary>
    public string StatusText
    {
        get { return statusLabel != null ? statusLabel.text : string.Empty; }
    }

    /// <summary>補助文の文字列 (テスト・状態表示用)。</summary>
    public string HintText
    {
        get { return hintLabel != null ? hintLabel.text : string.Empty; }
    }

    /// <summary>スパインの回転角 (テストで「動いている」ことを確認するため)。</summary>
    public float SpinnerAngle
    {
        get { return spinnerAngle; }
    }

    /// <summary>オーバーレイを 1 つだけ作る。既にあればそれを使う。</summary>
    public static MediaLoadingOverlay Ensure()
    {
        if (instance != null) { return instance; }
        instance = FindFirstObjectByType<MediaLoadingOverlay>();
        if (instance != null) { return instance; }
        var go = new GameObject("MediaLoadingOverlay");
        instance = go.AddComponent<MediaLoadingOverlay>();
        instance.Build();
        return instance;
    }

    /// <summary>テスト用: 生成済みインスタンスを破棄する。</summary>
    public static void ClearForTests()
    {
        if (instance != null)
        {
            // EditMode では Destroy が効かないため DestroyImmediate を使う。
            if (Application.isPlaying)
            {
                if (instance.root != null) { Destroy(instance.root); }
                Destroy(instance.gameObject);
            }
            else
            {
                if (instance.root != null) { DestroyImmediate(instance.root); }
                DestroyImmediate(instance.gameObject);
            }
        }
        instance = null;
    }

    /// <summary>読み込み中を表示する。status は状態文 (例: 「動画をダウンロード中…」)。</summary>
    public void Show(string status)
    {
        if (root == null) { return; }
        errorHideAt = -1f;
        root.SetActive(true);
        if (titleLabel != null)
        {
            titleLabel.text = "読み込み中";
            titleLabel.color = Color.white;
        }
        if (statusLabel != null)
        {
            statusLabel.text = status ?? string.Empty;
            statusLabel.color = new Color(1f, 1f, 1f, 0.85f);
        }
    }

    /// <summary>読み込み中表示を隠す。</summary>
    public void Hide()
    {
        errorHideAt = -1f;
        if (root != null) { root.SetActive(false); }
    }

    /// <summary>失敗を表示する。一定時間後に自動で隠す (画面が固まったままにならないように)。</summary>
    public void ShowError(string detail)
    {
        if (root == null) { return; }
        root.SetActive(true);
        if (titleLabel != null)
        {
            titleLabel.text = "読み込みに失敗しました";
            titleLabel.color = new Color(1f, 0.45f, 0.45f, 1f);
        }
        if (statusLabel != null)
        {
            statusLabel.text = string.IsNullOrEmpty(detail) ? "音源を取得できませんでした" : detail;
            statusLabel.color = new Color(1f, 0.8f, 0.8f, 1f);
        }
        errorHideAt = Time.time + ErrorAutoHideSeconds;
    }

    void Update()
    {
        if (spinner != null && IsVisible)
        {
            spinnerAngle += 180f * Time.deltaTime;
            spinner.localRotation = Quaternion.Euler(0f, 0f, spinnerAngle);
        }
        if (errorHideAt > 0f && Time.time >= errorHideAt)
        {
            errorHideAt = -1f;
            Hide();
        }
    }

    void Build()
    {
        try
        {
            TMP_FontAsset font = JapaneseFontFinder.FindJapaneseFont();

            var canvasGO = new GameObject("MediaLoadingCanvas", typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);
            canvasObject = canvasGO;
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = CanvasSortingOrder;

            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var rootGO = new GameObject("MediaLoadingRoot", typeof(RectTransform));
            root = rootGO;
            var rootRect = rootGO.GetComponent<RectTransform>();
            rootRect.SetParent(canvasGO.transform, false);
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            // 全画面の暗転。クリックは奥へ通す (raycastTarget = false)。
            var backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
            backdrop.transform.SetParent(rootRect, false);
            var backdropRect = backdrop.GetComponent<RectTransform>();
            backdropRect.anchorMin = Vector2.zero;
            backdropRect.anchorMax = Vector2.one;
            backdropRect.offsetMin = Vector2.zero;
            backdropRect.offsetMax = Vector2.zero;
            var backdropImage = backdrop.GetComponent<Image>();
            backdropImage.color = new Color(0.02f, 0.03f, 0.06f, 0.94f);
            backdropImage.raycastTarget = false;

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(rootRect, false);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(900f, 340f);
            var panelImage = panel.GetComponent<Image>();
            panelImage.color = new Color(0.05f, 0.07f, 0.12f, 0.85f);
            panelImage.raycastTarget = false;

            titleLabel = AddLabel(panelRect, "LoadingTitle", "読み込み中", 44f, TextAlignmentOptions.Center, font);
            var titleRect = titleLabel.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 0.5f);
            titleRect.anchorMax = new Vector2(1f, 0.5f);
            titleRect.pivot = new Vector2(0.5f, 0.5f);
            titleRect.anchoredPosition = new Vector2(0f, 70f);
            titleRect.sizeDelta = new Vector2(-80f, 60f);

            statusLabel = AddLabel(panelRect, "LoadingStatus", string.Empty, 26f, TextAlignmentOptions.Center, font);
            var statusRect = statusLabel.rectTransform;
            statusRect.anchorMin = new Vector2(0f, 0.5f);
            statusRect.anchorMax = new Vector2(1f, 0.5f);
            statusRect.pivot = new Vector2(0.5f, 0.5f);
            statusRect.anchoredPosition = new Vector2(0f, -10f);
            statusRect.sizeDelta = new Vector2(-80f, 40f);

            // スピナー: 回す白の四角 (スプライト不要)。
            var spinnerGO = new GameObject("Spinner", typeof(RectTransform), typeof(Image));
            spinnerGO.transform.SetParent(panelRect, false);
            var spinnerRect = spinnerGO.GetComponent<RectTransform>();
            spinnerRect.anchorMin = new Vector2(0.5f, 0.5f);
            spinnerRect.anchorMax = new Vector2(0.5f, 0.5f);
            spinnerRect.pivot = new Vector2(0.5f, 0.5f);
            spinnerRect.anchoredPosition = new Vector2(0f, -70f);
            spinnerRect.sizeDelta = new Vector2(40f, 40f);
            var spinnerImage = spinnerGO.GetComponent<Image>();
            spinnerImage.color = new Color(0.25f, 0.66f, 0.96f, 1f);
            spinnerImage.raycastTarget = false;
            spinner = spinnerRect;

            // 補助文 (「しばらくお待ちください」)。
            hintLabel = AddLabel(panelRect, "LoadingHint", "しばらくお待ちください…", 22f, TextAlignmentOptions.Center, font);
            var hintRect = hintLabel.rectTransform;
            hintRect.anchorMin = new Vector2(0f, 0.5f);
            hintRect.anchorMax = new Vector2(1f, 0.5f);
            hintRect.pivot = new Vector2(0.5f, 0.5f);
            hintRect.anchoredPosition = new Vector2(0f, -125f);
            hintRect.sizeDelta = new Vector2(-80f, 32f);
            hintLabel.color = new Color(1f, 1f, 1f, 0.6f);

            root.SetActive(false);
        }
        catch (System.Exception e)
        {
            // 表示の失敗でゲームが動かなくならないように。
            Debug.LogWarning("[MediaLoadingOverlay] build failed: " + e.Message);
        }
    }

    static TextMeshProUGUI AddLabel(RectTransform parent, string name, string text, float fontSize,
        TextAlignmentOptions alignment, TMP_FontAsset font)
    {
        var labelGO = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        labelGO.transform.SetParent(parent, false);
        var tmp = labelGO.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = Color.white;
        tmp.alignment = alignment;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.raycastTarget = false;
        if (font != null) { tmp.font = font; }
        return tmp;
    }

}
