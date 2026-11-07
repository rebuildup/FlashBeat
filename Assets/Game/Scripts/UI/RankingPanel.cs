using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Code-built ranking UI (#134), scene independent (KeyConfigUI pattern):
/// no scene edits, no prefabs, the Japanese font is borrowed from an existing
/// TMP text at runtime. Owns one overlay canvas with
/// - the read-only Top 10 board (F5 on Title/Select/Result),
/// - the submit modal on the result screen (ランキングに登録),
/// - the persistent hint (F5: ランキング).
/// Panels are opaque and centered; the canvas scales with the screen
/// (1920x1080 reference, match 0.5) so the layout holds from 1024x768 up.
/// </summary>
public class RankingPanel : MonoBehaviour
{
    private const int EntryRows = 10;
    private const float BoardWidth = 960f;
    private const float BoardHeight = 780f;
    private const float ModalWidth = 720f;
    private const float ModalHeight = 420f;
    private const float RowHeight = 44f;
    private const float RowFontSize = 26f;
    private const float HeaderFontSize = 32f;
    private const float MessageFontSize = 24f;
    private const float FooterFontSize = 20f;

    /// <summary>アクセント色 #3FA9F5 (枠・ハイライト)。</summary>
    private static readonly Color AccentColor = new Color(0.247f, 0.663f, 0.961f);
    /// <summary>パネル本体 #1B1B1B (不透明)。</summary>
    private static readonly Color PanelColor = new Color(0.106f, 0.106f, 0.106f, 1f);
    private static readonly Color HighlightColor = new Color(0.247f, 0.663f, 0.961f, 0.25f);

    private bool built;
    private TMP_FontAsset font;

    private RectTransform boardRoot;
    private RectTransform modalRoot;
    private TextMeshProUGUI hintLabel;
    private readonly List<TextMeshProUGUI> rows = new List<TextMeshProUGUI>();
    private readonly List<UnityEngine.UI.Image> rowBackgrounds = new List<UnityEngine.UI.Image>();
    private TextMeshProUGUI yourRankLabel;
    private TextMeshProUGUI nameLabel;
    private TextMeshProUGUI modalMessage;

    public bool BoardVisible => boardRoot != null && boardRoot.gameObject.activeSelf;
    public bool ModalVisible => modalRoot != null && modalRoot.gameObject.activeSelf;

    public void EnsureBuilt()
    {
        if (built) return;
        built = true;
        font = FindJapaneseFont();
        BuildCanvas();
        BuildBoard();
        BuildModal();
        BuildHint();
        boardRoot.gameObject.SetActive(false);
        modalRoot.gameObject.SetActive(false);
        if (hintLabel != null) hintLabel.gameObject.SetActive(false);
    }

    /// <summary>日本語グリフを持つフォントを探す (既存の TMP テキストから借りる)。</summary>
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

    private void BuildCanvas()
    {
        var canvasGO = new GameObject("RankingCanvas", typeof(Canvas),
            typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // シーン UI の上、KeyConfigUI (5000) の下。
        canvas.sortingOrder = 4000;

        var scaler = canvasGO.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
    }

    /// <summary>
    /// 全画面のバックドロップ + 中央の不透明パネル。
    /// 戻り値は「バックドロップを含むルート」— 表示/非表示はこのルートで切り替える。
    /// (パネル本体だけを隠すと全画面の暗転バックドロップが残り、画面が暗くなったままになる)
    /// </summary>
    private RectTransform BuildBackdrop(string name, float width, float height, out RectTransform body)
    {
        var backdrop = new GameObject(name, typeof(UnityEngine.UI.Image)).GetComponent<RectTransform>();
        backdrop.SetParent(GetComponentInChildren<Canvas>().transform, false);
        backdrop.anchorMin = Vector2.zero;
        backdrop.anchorMax = Vector2.one;
        backdrop.pivot = new Vector2(0.5f, 0.5f);
        backdrop.offsetMin = Vector2.zero;
        backdrop.offsetMax = Vector2.zero;
        var backdropImage = backdrop.GetComponent<UnityEngine.UI.Image>();
        // 閉じているときはこのルートごと非表示になるので、暗転は残らない。
        // 表示中は背後の画面が薄く見える程度にし、操作はレイキャストでブロックする。
        backdropImage.color = new Color(0f, 0f, 0f, 0.65f);
        backdropImage.raycastTarget = true;

        body = new GameObject("Panel", typeof(UnityEngine.UI.Image)).GetComponent<RectTransform>();
        body.SetParent(backdrop, false);
        body.anchorMin = new Vector2(0.5f, 0.5f);
        body.anchorMax = new Vector2(0.5f, 0.5f);
        body.pivot = new Vector2(0.5f, 0.5f);
        body.anchoredPosition = Vector2.zero;
        body.sizeDelta = new Vector2(width, height);
        var bodyImage = body.GetComponent<UnityEngine.UI.Image>();
        bodyImage.color = PanelColor;
        bodyImage.raycastTarget = true;
        BuildBorder(body);
        return backdrop;
    }

    /// <summary>パネルの 2px 枠 (#3FA9F5)。伸縮スプライトを使わず 4 本の Image で描く。</summary>
    private static void BuildBorder(RectTransform body)
    {
        AddBorderBar(body, "BorderTop", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 2f));
        AddBorderBar(body, "BorderBottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 2f));
        AddBorderBar(body, "BorderLeft", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(2f, 0f));
        AddBorderBar(body, "BorderRight", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(2f, 0f));
    }

    private static void AddBorderBar(RectTransform body, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 sizeDelta)
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

    private void BuildBoard()
    {
        RectTransform body;
        boardRoot = BuildBackdrop("RankingBoard", BoardWidth, BoardHeight, out body);

        var content = new GameObject("Content", typeof(UnityEngine.UI.VerticalLayoutGroup)).GetComponent<RectTransform>();
        content.SetParent(body, false);
        content.anchorMin = Vector2.zero;
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(0.5f, 0.5f);
        content.offsetMin = new Vector2(28f, 24f);
        content.offsetMax = new Vector2(-28f, -24f);
        var layout = content.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var header = AddLabel(content, "RankingHeader", "ランキング Top 10", HeaderFontSize, TextAlignmentOptions.Center);
        header.rectTransform.gameObject.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 48f;

        yourRankLabel = AddLabel(content, "YourRank", string.Empty, MessageFontSize, TextAlignmentOptions.Center);
        yourRankLabel.color = new Color(1f, 0.85f, 0.3f);
        yourRankLabel.rectTransform.gameObject.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 34f;

        var rowsRoot = new GameObject("Rows", typeof(UnityEngine.UI.VerticalLayoutGroup)).GetComponent<RectTransform>();
        rowsRoot.SetParent(content, false);
        rowsRoot.anchorMin = new Vector2(0f, 1f);
        rowsRoot.anchorMax = new Vector2(1f, 1f);
        rowsRoot.pivot = new Vector2(0.5f, 1f);
        var rowsLayout = rowsRoot.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
        rowsLayout.spacing = 2f;
        rowsLayout.childAlignment = TextAnchor.UpperLeft;
        rowsLayout.childControlWidth = true;
        rowsLayout.childControlHeight = true;
        rowsLayout.childForceExpandWidth = true;
        rowsLayout.childForceExpandHeight = false;
        var rowsSize = rowsRoot.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
        rowsSize.preferredHeight = EntryRows * RowHeight + (EntryRows - 1) * rowsLayout.spacing;

        for (int i = 0; i < EntryRows; i++)
        {
            var row = new GameObject("Row" + i, typeof(UnityEngine.UI.Image)).GetComponent<RectTransform>();
            row.SetParent(rowsRoot, false);
            var rowSize = row.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            rowSize.preferredHeight = RowHeight;
            rowSize.minHeight = RowHeight;
            var background = row.GetComponent<UnityEngine.UI.Image>();
            background.color = Color.clear;
            background.raycastTarget = false;

            var labelGO = new GameObject("Text", typeof(TextMeshProUGUI));
            labelGO.transform.SetParent(row, false);
            var tmp = labelGO.GetComponent<TextMeshProUGUI>();
            tmp.text = string.Empty;
            tmp.fontSize = RowFontSize;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.margin = new Vector4(16f, 0f, 12f, 0f);
            tmp.raycastTarget = false;
            if (font != null) tmp.font = font;
            var labelRect = tmp.rectTransform;
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            rows.Add(tmp);
            rowBackgrounds.Add(background);
        }

        var footer = AddLabel(content, "RankingFooter", "閉じる: F5 / Esc", FooterFontSize, TextAlignmentOptions.Center);
        footer.color = new Color(1f, 1f, 1f, 0.75f);
        footer.rectTransform.gameObject.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 32f;

        // boardRoot は BuildBackdrop の戻り値 (バックドロップを含むルート) のまま。
    }

    private void BuildModal()
    {
        RectTransform body;
        modalRoot = BuildBackdrop("RankingSubmitModal", ModalWidth, ModalHeight, out body);

        var content = new GameObject("Content", typeof(UnityEngine.UI.VerticalLayoutGroup)).GetComponent<RectTransform>();
        content.SetParent(body, false);
        content.anchorMin = Vector2.zero;
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(0.5f, 0.5f);
        content.offsetMin = new Vector2(28f, 24f);
        content.offsetMax = new Vector2(-28f, -24f);
        var layout = content.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var title = AddLabel(content, "ModalTitle", "ランキングに登録", HeaderFontSize, TextAlignmentOptions.Center);
        title.rectTransform.gameObject.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 52f;

        nameLabel = AddLabel(content, "NameLabel", string.Empty, MessageFontSize, TextAlignmentOptions.Left);
        nameLabel.rectTransform.gameObject.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 40f;

        modalMessage = AddLabel(content, "ModalMessage", string.Empty, MessageFontSize, TextAlignmentOptions.Left);
        modalMessage.rectTransform.gameObject.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 40f;

        var footer = AddLabel(content, "ModalFooter", "Enter: 送信　Backspace: けす　Esc: スキップ", FooterFontSize, TextAlignmentOptions.Center);
        footer.color = new Color(1f, 1f, 1f, 0.75f);
        footer.rectTransform.gameObject.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 32f;

        // modalRoot は BuildBackdrop の戻り値 (バックドロップを含むルート) のまま。
    }

    private void BuildHint()
    {
        var labelGO = new GameObject("RankingHint", typeof(TextMeshProUGUI));
        labelGO.transform.SetParent(GetComponentInChildren<Canvas>().transform, false);
        hintLabel = labelGO.GetComponent<TextMeshProUGUI>();
        hintLabel.text = "F5: ランキング";
        hintLabel.fontSize = 24f;
        hintLabel.color = new Color(1f, 1f, 1f, 0.7f);
        hintLabel.alignment = TextAlignmentOptions.Left;
        hintLabel.raycastTarget = false;
        if (font != null) hintLabel.font = font;

        var rect = hintLabel.rectTransform;
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        rect.anchoredPosition = new Vector2(16f, 56f);
        rect.sizeDelta = new Vector2(300f, 36f);
    }

    // --- updates -------------------------------------------------------------

    public void ShowHint(bool show)
    {
        if (hintLabel == null) return;
        if (hintLabel.gameObject.activeSelf != show)
        {
            hintLabel.gameObject.SetActive(show);
        }
    }

    public void SetBoardLoading()
    {
        for (int i = 0; i < rows.Count; i++)
        {
            rows[i].text = "…";
            rows[i].color = Color.white;
            if (i < rowBackgrounds.Count && rowBackgrounds[i] != null)
            {
                rowBackgrounds[i].color = Color.clear;
            }
        }
        yourRankLabel.text = string.Empty;
    }

    /// <summary>Top 10 を表示し、myRank (0 なら非表示) の行をハイライトする。entries が null のときは行を書き換えない (ローディング表示を維持)。</summary>
    public void ShowBoard(List<RankingClient.LeaderboardEntry> entries, int myRank, string myNickname)
    {
        EnsureBuilt();
        if (entries != null)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                bool mine = false;
                string text = string.Empty;
                if (i < entries.Count && entries[i] != null)
                {
                    RankingClient.LeaderboardEntry entry = entries[i];
                    text = (entry.rank > 0 ? entry.rank + "位  " : (i + 1) + "位  ")
                        + (string.IsNullOrEmpty(entry.nickname) ? "-----" : entry.nickname)
                        + "  " + entry.score.ToString("N0");
                    mine = myRank > 0 && entry.rank == myRank
                        || myRank <= 0 && entry.nickname == myNickname;
                }
                rows[i].text = text;
                rows[i].color = mine ? new Color(1f, 0.85f, 0.3f) : Color.white;
                if (i < rowBackgrounds.Count && rowBackgrounds[i] != null)
                {
                    rowBackgrounds[i].color = mine ? HighlightColor : Color.clear;
                }
            }
        }
        if (yourRankLabel != null)
        {
            yourRankLabel.text = myRank > 0 ? "あなたの順位: " + myRank + "位" : string.Empty;
            yourRankLabel.color = new Color(1f, 0.85f, 0.3f);
        }
        boardRoot.gameObject.SetActive(true);
    }

    /// <summary>ボード内の 1 行メッセージ (読み込み失敗など)。行の内容は壊さない。</summary>
    public void SetBoardMessage(string text)
    {
        if (yourRankLabel == null) return;
        yourRankLabel.text = text;
        yourRankLabel.color = new Color(1f, 0.6f, 0.6f);
    }

    public void HideBoard()
    {
        if (boardRoot != null) boardRoot.gameObject.SetActive(false);
    }

    public void ShowModal(string nicknameBuffer)
    {
        EnsureBuilt();
        nameLabel.text = "なまえ: " + nicknameBuffer + "|";
        modalMessage.text = string.Empty;
        modalMessage.color = Color.white;
        modalRoot.gameObject.SetActive(true);
    }

    public void SetNameText(string nicknameBuffer)
    {
        if (!ModalVisible || nameLabel == null) return;
        nameLabel.text = "なまえ: " + nicknameBuffer + "|";
    }

    public void SetModalMessage(string text, Color color)
    {
        if (!ModalVisible || modalMessage == null) return;
        modalMessage.text = text;
        modalMessage.color = color;
    }

    public void HideModal()
    {
        if (modalRoot != null) modalRoot.gameObject.SetActive(false);
    }
}
