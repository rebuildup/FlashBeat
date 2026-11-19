using TMPro;
using UnityEngine;

namespace FlashBeat.UI.V6.Common
{
    /// <summary>
    /// フッターのキー凡例 (KeyCap + Label) の共通実装。
    ///
    /// design 正本の実測値 (Select OENRv / History q9JZcH / Ranking S1TZD / Result zcSHh /
    /// Option B6If6Z の全画面で一致):
    /// - 凡例ゾーン (0,30)、項目の高さ 32
    /// - KeyCap: h32 / 角丸 4 / 背景 $k4 #3A3A3A / padding [0,8] / Key fs22 $text-muted
    /// - 項内 gap 8、項目間 gap 24
    ///
    /// 幅は design の固定値を写すのではなく TMP の実測 (GetPreferredValues) で詰める。
    /// 0.62em の ASCII 概算は Latin を過大評価し ("Enter" 実測 55px に対し概算 68px)、
    /// KeyCap が design より広くなって凡例全体が右へずれる原因だった。
    /// Title だけは別設計 (h36 / pad[0,10] / 項内 gap10) なのでこの既定値を使わない。
    /// </summary>
    public static class V6Legend
    {
        /// <summary>凡例項目の高さ (design 全画面共通 32)。</summary>
        public const float Height = 32f;
        /// <summary>KeyCap の左右 padding (design pad[0,8])。</summary>
        public const float CapPadX = 8f;
        /// <summary>KeyCap と Label の間隔 (design 項内 gap 8)。</summary>
        public const float InnerGap = 8f;
        /// <summary>項目間の間隔 (design zone gap 24)。</summary>
        public const float ItemGap = 24f;
        /// <summary>Key / Label の文字サイズ (design fs22)。</summary>
        public const float FontSize = 22f;
        /// <summary>凡例の上端 y (フッターゾーン内、design 全画面共通 30)。</summary>
        public const float Y = 30f;

        // --- アイコン凡例 (IconItem) の寸法 ---
        /// <summary>アイコン 1 個の一辺 (design 実測 20)。</summary>
        private const float IconSize = 20f;
        /// <summary>アイコン同士の間隔。</summary>
        private const float IconGap = 4f;

        /// <summary>
        /// 凡例 1 項目を design 座標 (x, y) に置き、次の項目の x を返す。
        /// </summary>
        public static float Item(GameObject parent, float x, float y, string key, string label)
        {
            return Item(parent, x, y, key, label, Height, CapPadX, InnerGap, ItemGap, FontSize,
                V6Tokens.Key4, V6Tokens.TextMuted);
        }

        /// <summary>Title など別設計の凡例向け (高さ / gap / padding を明示する版)。</summary>
        public static float Item(GameObject parent, float x, float y, string key, string label,
            float height, float capPadX, float innerGap, float itemGap, float fontSize,
            Color capFill, Color textColor)
        {
            if (string.IsNullOrEmpty(key))
            {
                return x;
            }

            float keyEstimate = Mathf.Ceil(V6Ui.LabelAdvanceEm(key) * fontSize) + capPadX * 2f;
            var cap = V6Controls.KeyCap(parent, key, new Vector2(x, y),
                new Vector2(keyEstimate, height), capPadX, capFill, textColor);
            var capRt = cap.GetComponent<RectTransform>();
            var keyTmp = cap.GetComponentInChildren<TextMeshProUGUI>();
            float keyW = V6Controls.MeasureWidth(keyTmp, key, keyEstimate - capPadX * 2f)
                + capPadX * 2f;
            capRt.sizeDelta = new Vector2(keyW, height);

            float labelEstimate = Mathf.Ceil(V6Ui.LabelAdvanceEm(label) * fontSize);
            float labelX = x + keyW + innerGap;
            var labelTmp = V6Controls.Text(parent, "Legend Label " + label, label,
                new Vector2(labelX, y), new Vector2(labelEstimate, height), fontSize, textColor,
                TextAlignmentOptions.Left);
            float labelW = V6Controls.MeasureWidth(labelTmp, label, labelEstimate);
            labelTmp.rectTransform.sizeDelta = new Vector2(labelW, height);

            return labelX + labelW + itemGap;
        }

        /// <summary>
        /// アイコン凡例のキャップ幅 (IconItem と同じ規則: pad + アイコン +  gap)。
        /// 幾何を先に見積もる側 (凡例全体の幅計算など) が同じ値を使うための公開版。
        /// </summary>
        public static float IconItemCapWidth(int iconCount, float capPadX = CapPadX)
        {
            if (iconCount <= 0)
            {
                return 0f;
            }

            return capPadX * 2f + iconCount * IconSize + (iconCount - 1) * IconGap;
        }

        /// <summary>
        /// 凡例 1 項目をアイコン (Resources/Icons のスプライト) で置く版 (Issue #733)。
        /// 「↑↓ 選択」「←→ 変更」のような方向キーの説明を、文字グリフではなく
        /// chevron/arrow アイコンで示す。戻り値は次の項目の x (Item と同じ規則)。
        /// </summary>
        public static float IconItem(GameObject parent, float x, float y, string[] iconNames, string label)
        {
            return IconItem(parent, x, y, iconNames, label, Height, CapPadX, InnerGap, ItemGap,
                FontSize, V6Tokens.Key4, V6Tokens.TextMuted);
        }

        /// <summary>
        /// <see cref="Item(GameObject, float, float, string, string, float, float, float, float, float, Color, Color)"/>
        /// と同じく寸法・色を明示する版 (Title 凡例や Result モーダルの小さい凡例向け)。
        /// </summary>
        public static float IconItem(GameObject parent, float x, float y, string[] iconNames, string label,
            float height, float capPadX, float innerGap, float itemGap, float fontSize,
            Color capFill, Color textColor)
        {
            if (iconNames == null || iconNames.Length == 0)
            {
                return x;
            }

            float iconPadY = (height - IconSize) * 0.5f;
            float capW = IconItemCapWidth(iconNames.Length, capPadX);

            var cap = V6Controls.Panel(parent, "Legend Icons " + label,
                new Vector2(x, y), new Vector2(capW, height), capFill, V6Tokens.RadiusSmall);

            for (int i = 0; i < iconNames.Length; i++)
            {
                var icon = V6Icons.Create(cap, iconNames[i], IconSize, textColor,
                    "Icon " + iconNames[i]);
                if (icon == null)
                {
                    continue;
                }

                var rt = icon.rectTransform;
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(
                    capPadX + i * (IconSize + IconGap), -iconPadY);
                rt.sizeDelta = new Vector2(IconSize, IconSize);
            }

            float labelEstimate = Mathf.Ceil(V6Ui.LabelAdvanceEm(label) * fontSize);
            float labelX = x + capW + innerGap;
            var labelTmp = V6Controls.Text(parent, "Legend Label " + label, label,
                new Vector2(labelX, y), new Vector2(labelEstimate, height), fontSize,
                textColor, TextAlignmentOptions.Left);
            float labelW = V6Controls.MeasureWidth(labelTmp, label, labelEstimate);
            labelTmp.rectTransform.sizeDelta = new Vector2(labelW, height);

            return labelX + labelW + itemGap;
        }
    }
}
