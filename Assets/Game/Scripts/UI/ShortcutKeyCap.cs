using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ショートカットキーを「キーキャップ」として操作のそばに描く補助。
///
/// オーナーの要望: 「キーガイドは tab では不十分。全てのショートカットキー
/// コントロールにキーボード入力とわかる形の表示をすること」。
/// キーガイド (Tab) を開かなくても、何の操作がどのキーで動くかが画面上で分かる。
///
/// - 見た目: ゲームのトーン (純黒・白・アクセント #3FA9F5) の細い枠のキーキャップ。
/// - 位置: 操作ラベルの右隣 (テキストの実際の幅から計算する)。
/// - 割当が変わったら `KeyActions.Changed` で描き直す (既定値のままじゃない)。
/// </summary>
public static class ShortcutKeyCap
{
    /// <summary>アクセント色 #3FA9F5。</summary>
    public static readonly Color AccentColor = new Color(0.247f, 0.663f, 0.961f);

    /// <summary>枠の線幅 (ピクセル)。</summary>
    private const float BorderWidth = 2f;

    /// <summary>キー名の中でいちばん広い文字の分だけ余白を足す。</summary>
    private const float HorizontalPadding = 14f;

    /// <summary>キーキャップと操作名の間隔。</summary>
    public const float Gap = 16f;

    /// <summary>キーの表示名 (キーキャップの中に入る文字)。</summary>
    public static string KeyName(KeyCode key)
    {
        switch (key)
        {
            case KeyCode.Space: return "Space";
            case KeyCode.Escape: return "Esc";
            case KeyCode.Tab: return "Tab";
            case KeyCode.Return: return "Enter";
            case KeyCode.KeypadEnter: return "Enter";
            case KeyCode.Backspace: return "Backspace";
            case KeyCode.LeftArrow: return "←";
            case KeyCode.RightArrow: return "→";
            case KeyCode.UpArrow: return "↑";
            case KeyCode.DownArrow: return "↓";
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
            case KeyCode.LeftBracket: return "[";
            case KeyCode.RightBracket: return "]";
            case KeyCode.Semicolon: return ";";
            case KeyCode.Quote: return "'";
            case KeyCode.Comma: return ",";
            case KeyCode.Period: return ".";
            case KeyCode.Slash: return "/";
            case KeyCode.Backslash: return "\\";
            default:
                string name = key.ToString();
                if (name.StartsWith("Alpha")) { return name.Substring(5); }
                if (name.StartsWith("Keypad")) { return name.Substring(6); }
                return name;
        }
    }

    /// <summary>複数のキーを 1 つのキャップにまとめる (「R / F5」)。</summary>
    public static string KeysText(KeyCode[] keys)
    {
        if (keys == null || keys.Length == 0) { return ""; }
        var parts = new List<string>();
        for (int i = 0; i < keys.Length; i++)
        {
            string name = KeyName(keys[i]);
            if (!parts.Contains(name)) { parts.Add(name); }
        }
        return string.Join(" / ", parts.ToArray());
    }

    /// <summary>
    /// ラベルの右隣にキーキャップを付ける。付け直しのときは前のキャップを消す。
    /// </summary>
    public static void Attach(TextMeshProUGUI label, KeyCode[] keys)
    {
        if (label == null) { return; }

        // 前回のキャップを消す (描き直しのため)。
        var old = label.rectTransform.Find("KeyCap");
        if (old != null) { Object.Destroy(old.gameObject); }

        string text = KeysText(keys);
        if (string.IsNullOrEmpty(text)) { return; }

        float fontSize = Mathf.Max(14f, label.fontSize * 0.62f);
        float height = Mathf.Max(22f, label.fontSize * 1.35f);

        // キーキャップ (枠だけ。塗りは置かない)。
        var capGO = new GameObject("KeyCap", typeof(RectTransform));
        var capRect = capGO.GetComponent<RectTransform>();
        capRect.SetParent(label.rectTransform, false);
        capRect.anchorMin = new Vector2(0f, 0.5f);
        capRect.anchorMax = new Vector2(0f, 0.5f);
        capRect.pivot = new Vector2(0f, 0.5f);
        capRect.sizeDelta = new Vector2(200f, height);

        // 枠 (4 本の細い Image)。
        AddBorder(capRect, "Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, BorderWidth));
        AddBorder(capRect, "Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, BorderWidth));
        AddBorder(capRect, "Left", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(BorderWidth, 0f));
        AddBorder(capRect, "Right", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(BorderWidth, 0f));

        // キー名。
        var textGO = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGO.transform.SetParent(capRect, false);
        var tmp = textGO.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = new Color(1f, 1f, 1f, 0.9f);
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.raycastTarget = false;
        tmp.characterSpacing = 4f;
        var font = JapaneseFontFinder.FindJapaneseFont();
        if (font != null) { tmp.font = font; }
        var textRect = tmp.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(HorizontalPadding, 0f);
        textRect.offsetMax = new Vector2(-HorizontalPadding, 0f);

        // キー名の幅に合わせてキャップを縮める。
        float textWidth = tmp.GetPreferredValues(text, 10000f, fontSize).x;
        float width = textWidth + HorizontalPadding * 2f;
        capRect.sizeDelta = new Vector2(width, height);

        // ラベルのテキストの右隣に置く。ラベルの揃えによって開始位置が変わる。
        float labelWidth = label.GetPreferredValues(label.text, 10000f, label.fontSize).x;
        float left = 0f;
        if ((label.alignment & TextAlignmentOptions.Center) == TextAlignmentOptions.Center)
        {
            left = (label.rectTransform.rect.width - labelWidth) * 0.5f;
        }
        else if ((label.alignment & TextAlignmentOptions.Right) == TextAlignmentOptions.Right)
        {
            left = label.rectTransform.rect.width - labelWidth;
        }
        capRect.anchoredPosition = new Vector2(left + labelWidth + Gap, 0f);
    }

    /// <summary>
    /// 画面内の、ラベルの文字が一致する操作にキーキャップを付ける。
    /// (シーンを編集せずに済ませるため、文字で探す)
    /// </summary>
    public static void AttachByText(string labelText, KeyCode[] keys)
    {
        TextMeshProUGUI label = FindByText(labelText);
        if (label == null)
        {
            Debug.LogWarning("[ShortcutKeyCap] no label '" + labelText + "'");
            return;
        }
        Attach(label, keys);
    }

    /// <summary>現在の割当 (KeyActions) を使ってラベルにキャップを付ける。</summary>
    public static void AttachByText(string labelText, KeyActions.KeyAction action)
    {
        AttachByText(labelText, KeyActions.Get(action));
    }

    /// <summary>画面内の同じ文字のラベルを探す。</summary>
    public static TextMeshProUGUI FindByText(string labelText)
    {
        var labels = Object.FindObjectsOfType<TextMeshProUGUI>();
        for (int i = 0; i < labels.Length; i++)
        {
            if (labels[i] != null && labels[i].text == labelText) { return labels[i]; }
        }
        return null;
    }

    /// <summary>枠 1 本 (伸縮スプライトを使わず Image で描く)。</summary>
    private static void AddBorder(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
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
        image.color = new Color(1f, 1f, 1f, 0.55f);
        image.raycastTarget = false;
    }
}
