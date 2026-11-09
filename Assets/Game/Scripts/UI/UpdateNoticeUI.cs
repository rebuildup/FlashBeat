using FlashBeat.UI;
using FlashBeat.Update;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

/// <summary>
/// Title シーン (ホーム) にだけ出す『アップデートあり』通知 (issue #118: 更新は明示式・任意)。
/// コード構築の uGUI overlay (シーン編集なし)。DontDestroyOnLoad で常駐し、アクティブシーンが
/// TitleScene の間だけ表示する。ゲームプレーは中断しない。
/// - F3 キー **と** ピルのマウスクリック (文字キーは使わない・オーナーは非 QWERTY 配置) でトリガー。
///   Available ならダウンロード開始、Ready なら適用 (アプリを終了して再起動)。
/// - F4 キー または × ボタンでこのセッションは非表示 (押さなければ更新しない選択)。
/// - 何もしなければダウンロードも適用も行われない。
/// - 前回の更新結果 (Update.ps1 の last-result.json、issue #125): 失敗 / 中止なら
///   『前回の更新に失敗しました: &lt;理由&gt; ...』、成功なら『更新しました vX』を Title で一度だけ
///   表示し、表示した時点で結果ファイルを last-result-shown.json へリネームする (次回は出ない)。
/// Canvas: ScaleWithScreenSize 1920x1080 / match 0.5。sortingOrder は Title UI より上、
/// キーガイド (KeyGuideUI, 500) より下の 400。
/// </summary>
public class UpdateNoticeUI : MonoBehaviour
{
    private const float SafeMargin = 24f;
    private const float PillWidth = 1180f;
    private const float PillHeight = 150f; // 2〜3 行 (前回の更新結果 + アップデートあり + 補足) が枠内に収まる高さ
    // 600: Title の Canvas (V5 Title Canvas, 500) は不透明な全画面 Backdrop を持つため、
    // 400 以下だとピルが背景の裏に隠れて見えない (Issue #233: 実機で更新経路が消える)。
    private const int SortingOrder = 600;
    private const string TitleSceneName = UpdateNoticePresenter.TitleSceneName;

    private UpdateChecker checker;
    private UpdateFlowModel model;
    private GameObject pill;
    private TMPro.TextMeshProUGUI label;
    private TMPro.TMP_FontAsset noticeFont;
    private string renderedText;
    private bool built;
    private bool resultNoticeMarked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        var go = new GameObject("FlashBeatUpdateNotice");
        DontDestroyOnLoad(go);
        go.AddComponent<UpdateNoticeUI>();
    }

    private void Update()
    {
        if (checker == null)
        {
            // UpdateChecker 側の Bootstrap と同じタイミングなので、まだ無ければ生成して待つ。
            UpdateChecker.EnsureExists();
            checker = FindAnyObjectByType<UpdateChecker>();
            if (checker == null) { return; }
            model = checker.Model;
        }
        if (!built) { BuildUI(); }

        bool onTitle = SceneManager.GetActiveScene().name == TitleSceneName;
        bool hasResultNotice = model != null && !string.IsNullOrEmpty(model.LastResultNotice);
        bool visible = UpdateNoticePresenter.ShouldShow(SceneManager.GetActiveScene().name, model);
        if (pill != null && pill.activeSelf != visible) { pill.SetActive(visible); }
        if (visible)
        {
            EnsureFont();
            RefreshText();
            // Title で一度表示できたら結果ファイルを mark-as-shown (issue #125)。
            // リネーム後も文言はこのセッションの間だけ表示を続ける (次回起動では出ない)。
            if (hasResultNotice && !resultNoticeMarked)
            {
                resultNoticeMarked = true;
                UpdateResultFile.MarkShown();
            }
        }

        // F3/F4 are update keys ONLY on the Title screen: other screens use them for their own functions
        // (e.g. Select: ranking), and pressing F3 there must never start a download.
        if (onTitle)
        {
            if (Input.GetKeyDown(KeyCode.F3)) { Trigger(); }
            else if (Input.GetKeyDown(KeyCode.F4)) { Dismiss(); }
        }
    }

    /// <summary>F3 / ピルクリックの共通トリガー。Available → ダウンロード、Ready → 適用。</summary>
    public void Trigger()
    {
        var action = UpdateNoticePresenter.ResolveAction(model);
        if (action == UpdateNoticeAction.None) { return; }
        if (checker == null) { return; }
        if (action == UpdateNoticeAction.Download)
        {
            checker.RequestDownload();
        }
        else if (action == UpdateNoticeAction.Apply)
        {
            checker.RequestApply();
        }
    }

    /// <summary>F4 / × 押下。このセッションの通知を隠す (更新はしない、staging は保持)。</summary>
    public void Dismiss()
    {
        if (model != null && (model.HasAvailableUpdate || !string.IsNullOrEmpty(model.LastResultNotice)))
        {
            model.Dismiss();
        }
    }

    private void RefreshText()
    {
        var text = UpdateNoticePresenter.BuildText(model);
        if (text != renderedText)
        {
            renderedText = text;
            if (label != null) { label.text = text; }
        }
    }

    private void BuildUI()
    {
        built = true;
        var canvasGO = new GameObject("UpdateNoticeCanvas", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;

        var scaler = canvasGO.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        // アクセント枠 (外側) + 不透明ダークピル (内側)。
        pill = new GameObject("UpdateNoticePill",
            typeof(UnityEngine.UI.Image),
            typeof(UnityEngine.UI.Button));
        pill.transform.SetParent(canvasGO.transform, false);
        var pillRect = (RectTransform)pill.transform;
        pillRect.anchorMin = new Vector2(1f, 1f);
        pillRect.anchorMax = new Vector2(1f, 1f);
        pillRect.pivot = new Vector2(1f, 1f);
        pillRect.anchoredPosition = new Vector2(-SafeMargin, -SafeMargin);
        pillRect.sizeDelta = new Vector2(PillWidth, PillHeight);
        pill.GetComponent<UnityEngine.UI.Image>().color = new Color(0.96f, 0.65f, 0.14f); // アクセント
        pill.SetActive(false);

        var body = new GameObject("Body", typeof(UnityEngine.UI.Image));
        body.transform.SetParent(pill.transform, false);
        var bodyRect = (RectTransform)body.transform;
        bodyRect.anchorMin = Vector2.zero;
        bodyRect.anchorMax = Vector2.one;
        bodyRect.offsetMin = new Vector2(4f, 4f);
        bodyRect.offsetMax = new Vector2(-4f, -4f);
        var bodyImage = body.GetComponent<UnityEngine.UI.Image>();
        bodyImage.color = new Color(0f, 0f, 0f, 0.88f); // 不透明ダーク
        bodyImage.raycastTarget = false; // クリックは外側ピル (Button) で受ける

        var labelGO = new GameObject("Label", typeof(TMPro.TextMeshProUGUI));
        labelGO.transform.SetParent(body.transform, false);
        label = labelGO.GetComponent<TMPro.TextMeshProUGUI>();
        label.fontSize = 28;
        label.enableAutoSizing = true;
        label.fontSizeMin = 18f;
        label.fontSizeMax = 28f;
        label.alignment = TMPro.TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;
        var labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(16f, 4f);
        labelRect.offsetMax = new Vector2(-56f, -4f); // 右側は × ボタン分空ける

        var closeGO = new GameObject("Close", typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
        closeGO.transform.SetParent(pill.transform, false);
        var closeRect = (RectTransform)closeGO.transform;
        closeRect.anchorMin = new Vector2(1f, 0.5f);
        closeRect.anchorMax = new Vector2(1f, 0.5f);
        closeRect.pivot = new Vector2(0.5f, 0.5f);
        closeRect.anchoredPosition = new Vector2(-28f, 0f);
        closeRect.sizeDelta = new Vector2(40f, 40f);
        closeGO.GetComponent<UnityEngine.UI.Image>().color = new Color(0.2f, 0.2f, 0.2f, 0.9f);
        var closeButton = closeGO.GetComponent<UnityEngine.UI.Button>();
        closeButton.onClick.AddListener(Dismiss);
        var closeLabelGO = new GameObject("Label", typeof(TMPro.TextMeshProUGUI));
        closeLabelGO.transform.SetParent(closeGO.transform, false);
        var closeLabel = closeLabelGO.GetComponent<TMPro.TextMeshProUGUI>();
        closeLabel.text = "×";
        closeLabel.fontSize = 26;
        closeLabel.alignment = TMPro.TextAlignmentOptions.Center;
        closeLabel.color = Color.white;
        closeLabel.raycastTarget = false;
        var closeLabelRect = closeLabel.GetComponent<RectTransform>();
        closeLabelRect.anchorMin = Vector2.zero;
        closeLabelRect.anchorMax = Vector2.one;
        closeLabelRect.sizeDelta = Vector2.zero;

        pill.GetComponent<UnityEngine.UI.Button>().onClick.AddListener(Trigger);
    }

    /// <summary>表示中のシーンの TMP テキストから日本語フォントを借りる (Title に TMP がある前提)。
    /// 見つかるまで表示のたびに試す。見つからない場合は TMP の既定フォントのまま (要フォント確認)。</summary>
    private void EnsureFont()
    {
        if (label == null || noticeFont != null) { return; }
        foreach (var text in FindObjectsByType<TMPro.TextMeshProUGUI>(FindObjectsSortMode.None))
        {
            if (text != label && text.isActiveAndEnabled && text.font != null)
            {
                noticeFont = text.font;
                label.font = noticeFont;
                return;
            }
        }
    }
}
