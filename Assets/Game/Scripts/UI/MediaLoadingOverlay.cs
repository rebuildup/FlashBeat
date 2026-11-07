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
            statusLabel.color = new Color(1f, 1f, 1f, 0.75f);
        }
        if (hintLabel != null)
        {
            hintLabel.text = "しばらくお待ちください…";
            hintLabel.color = new Color(1f, 1f, 1f, 0.4f);
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
            // 色はゲームのトーン (純黒) に合わせる。紺のカードパネルは置かない。
            var backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
            backdrop.transform.SetParent(rootRect, false);
            var backdropRect = backdrop.GetComponent<RectTransform>();
            backdropRect.anchorMin = Vector2.zero;
            backdropRect.anchorMax = Vector2.one;
            backdropRect.offsetMin = Vector2.zero;
            backdropRect.offsetMax = Vector2.zero;
            var backdropImage = backdrop.GetComponent<Image>();
            backdropImage.color = new Color(0f, 0f, 0f, 0.96f);
            backdropImage.raycastTarget = false;

            // 内容の置き場 (塗りパネルは置かない。v5 の UI は「線と文字」だけ)。
            var contentGO = new GameObject("Content", typeof(RectTransform));
            contentGO.transform.SetParent(rootRect, false);
            var contentRect = contentGO.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0.5f, 0.5f);
            contentRect.anchorMax = new Vector2(0.5f, 0.5f);
            contentRect.pivot = new Vector2(0.5f, 0.5f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(1000f, 420f);

            // 見出し (白・字間広め)。
            titleLabel = AddLabel(contentRect, "LoadingTitle", string.Empty, 46f, TextAlignmentOptions.Center, font);
            // はみ出しても消えないよう Overflow にする (Ellipsis だと枠が足りないと空になる)。
            titleLabel.overflowMode = TextOverflowModes.Overflow;
            titleLabel.characterSpacing = 8f;
            var titleRect = titleLabel.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 0.5f);
            titleRect.anchorMax = new Vector2(1f, 0.5f);
            titleRect.pivot = new Vector2(0.5f, 0.5f);
            titleRect.anchoredPosition = new Vector2(0f, 96f);
            titleRect.sizeDelta = new Vector2(-80f, 72f);

            // 見出し下の細いアクセント線 (他 UI と同じ #3FA9F5)。
            var ruleGO = new GameObject("AccentRule", typeof(RectTransform), typeof(Image));
            ruleGO.transform.SetParent(contentRect, false);
            var ruleRect = ruleGO.GetComponent<RectTransform>();
            ruleRect.anchorMin = new Vector2(0.5f, 0.5f);
            ruleRect.anchorMax = new Vector2(0.5f, 0.5f);
            ruleRect.pivot = new Vector2(0.5f, 0.5f);
            ruleRect.anchoredPosition = new Vector2(0f, 48f);
            ruleRect.sizeDelta = new Vector2(180f, 2f);
            var ruleImage = ruleGO.GetComponent<Image>();
            ruleImage.color = new Color(0.247f, 0.663f, 0.961f, 0.85f);
            ruleImage.raycastTarget = false;

            // スピナー: 細い 2 本の棒が回る (塗り Evel の四角ではない)。
            var spinnerGO = new GameObject("Spinner", typeof(RectTransform));
            spinnerGO.transform.SetParent(contentRect, false);
            var spinnerRect = spinnerGO.GetComponent<RectTransform>();
            spinnerRect.anchorMin = new Vector2(0.5f, 0.5f);
            spinnerRect.anchorMax = new Vector2(0.5f, 0.5f);
            spinnerRect.pivot = new Vector2(0.5f, 0.5f);
            spinnerRect.anchoredPosition = new Vector2(0f, -14f);
            spinnerRect.sizeDelta = new Vector2(44f, 44f);
            spinner = spinnerRect;
            AddSpinnerBar(spinnerRect, "SpinnerBarA", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 2f));
            AddSpinnerBar(spinnerRect, "SpinnerBarB", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(2f, 0f));

            // 状態文 (白・やや薄)。
            statusLabel = AddLabel(contentRect, "LoadingStatus", string.Empty, 24f, TextAlignmentOptions.Center, font);
            statusLabel.overflowMode = TextOverflowModes.Overflow;
            var statusRect = statusLabel.rectTransform;
            statusRect.anchorMin = new Vector2(0f, 0.5f);
            statusRect.anchorMax = new Vector2(1f, 0.5f);
            statusRect.pivot = new Vector2(0.5f, 0.5f);
            statusRect.anchoredPosition = new Vector2(0f, -72f);
            statusRect.sizeDelta = new Vector2(-80f, 40f);

            // 補助文 (「しばらくお待ちください」)。ゲーム内の補助文と同じ薄さ。
            hintLabel = AddLabel(contentRect, "LoadingHint", string.Empty, 18f, TextAlignmentOptions.Center, font);
            hintLabel.overflowMode = TextOverflowModes.Overflow;
            var hintRect = hintLabel.rectTransform;
            hintRect.anchorMin = new Vector2(0f, 0.5f);
            hintRect.anchorMax = new Vector2(1f, 0.5f);
            hintRect.pivot = new Vector2(0.5f, 0.5f);
            hintRect.anchoredPosition = new Vector2(0f, -116f);
            hintRect.sizeDelta = new Vector2(-80f, 36f);
            hintLabel.color = new Color(1f, 1f, 1f, 0.4f);

            root.SetActive(false);
        }
        catch (System.Exception e)
        {
            // 表示の失敗でゲームが動かなくならないように。
            Debug.LogWarning("[MediaLoadingOverlay] build failed: " + e.Message);
        }
    }

    /// <summary>スピナーの細い棒 1 本 (アクセント色)。</summary>
    private void AddSpinnerBar(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 pivot, Vector2 sizeDelta)
    {
        var bar = new GameObject(name, typeof(RectTransform), typeof(Image));
        bar.transform.SetParent(parent, false);
        var rect = bar.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = sizeDelta;
        var image = bar.GetComponent<Image>();
        image.color = new Color(0.247f, 0.663f, 0.961f, 1f);
        image.raycastTarget = false;
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
