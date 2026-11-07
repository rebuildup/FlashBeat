using TMPro;
using UnityEngine;

/// <summary>
/// 日本語を表示できる TMP フォント (NotoSansJP / YuGoth) を既存の TMP から拾う補助。
/// 実行時に UI を組み立てる各画面 (読み込み中画面・キーガイドなど) が共用する。
/// </summary>
public static class JapaneseFontFinder
{
    /// <summary>
    /// シーン内の既存 TMP から日本語フォントを探す。見つからなければ null (TMP の既定フォント)。
    /// </summary>
    public static TMP_FontAsset FindJapaneseFont()
    {
        TMP_FontAsset fallback = null;
        var all = Resources.FindObjectsOfTypeAll<TextMeshProUGUI>();
        for (int i = 0; i < all.Length; i++)
        {
            var tmp = all[i];
            if (tmp == null || tmp.font == null) { continue; }
            string fontName = tmp.font.name;
            if (fontName != null && (fontName.Contains("Noto") || fontName.Contains("YuGoth")))
            {
                return tmp.font;
            }
            if (fallback == null) { fallback = tmp.font; }
        }
        return fallback;
    }
}
