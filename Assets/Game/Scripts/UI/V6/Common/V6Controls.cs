using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace FlashBeat.UI.V6.Common
{
    /// <summary>
    /// v6 契約 (§4/§5/§7) の共通部品ビルダ。すべて design 空間 (1728×972、左上原点・y_down)
    /// の座標・寸法で指定する。実画面ピクセル (UISizeProfile) は混ぜない。
    ///
    /// - 位置指定は design 左上座標 (<see cref="V6Layout.Place"/> 経由、変換は 1 箇所に集約)
    /// - フォントサイズ・角丸・高さは契約の数値のみ (捏造しない)
    /// - 角丸スプライトは v6 版をこのクラス内で生成 (v5 には依存しない)
    /// </summary>
    public static class V6Controls
    {
        // --- 契約 §4 の共通ボタン定数 (design px) ---
        public const float ButtonFontSize = 30f;
        public const float ButtonPaddingX = 24f;
        public const float ButtonGap = 12f;
        public const float ButtonIconSize = 32f;
        public const float ButtonRadius = V6Tokens.RadiusMedium;
        /// <summary>Button 内 KeyCap の Key fs (契約 §4)。</summary>
        public const float ButtonKeyFontSize = 22f;

        // --- 契約 §4 の KeyCap / Tag 定数 (design px) ---
        public const float KeyCapFontSize = 22f;
        public const float KeyCapPaddingX = 10f;
        public const float TagFontSize = 22f;

        private static readonly Dictionary<int, Sprite> SpriteCache = new Dictionary<int, Sprite>();

        /// <summary>
        /// 角丸背景パネル (9-slice)。スプライトは v6 版の実装をこのクラス内で
        /// 実行時生成する (v5 の V5Ui.RoundedSprite と同アルゴリズム、キャッシュのみ独立)。
        /// </summary>
        public static GameObject Panel(GameObject parent, string name, Vector2 designTopLeft, Vector2 size,
            Color color, float radius)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent.transform, false);
            var image = go.GetComponent<Image>();
            image.type = Image.Type.Sliced;
            image.sprite = RoundedSprite(Mathf.RoundToInt(radius));
            image.color = color;
            V6Layout.Place(go.GetComponent<RectTransform>(), designTopLeft, size);
            return go;
        }

        /// <summary>
        /// TMP テキスト。位置・サイズ・fs は design 空間。
        /// フォントは TMP_Settings.defaultFontAsset (NotoSansJP-Medium = w500)。
        /// 契約の「w500」はこのフォントasset自身のウェイトなので weight は既定 Normal、
        /// 強調したい場面だけ呼び出し側が Bold を渡す。
        /// </summary>
        public static TextMeshProUGUI Text(GameObject parent, string name, string content, Vector2 designTopLeft,
            Vector2 size, float fontSize, Color color, TextAlignmentOptions align = TextAlignmentOptions.Left,
            FontStyles weight = FontStyles.Normal)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            V6Layout.Place(go.GetComponent<RectTransform>(), designTopLeft, size);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = content;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = align;
            // design 空間の固定 Rect に収める前提なので折り返さない
            // (幅が足りないラベル "FlashBeat" / "START" / "設定" が 2 行に折れて
            // 隣の要素と重なる事故を防ぐ。v5 / v6 各ビューと同じ NoWrap 方針)。
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.fontStyle = weight;
            tmp.raycastTarget = false;
            var font = TMP_Settings.defaultFontAsset != null
                ? TMP_Settings.defaultFontAsset
                : Resources.Load<TMP_FontAsset>("NotoSansJP-Medium SDF");
            if (font != null)
            {
                tmp.font = font;
                // NotoSansJP の行送りは fs の約 1.45 倍 (lineHeight 69.504 / pointSize 48) で、
                // 矩形の縦高が行送り未満だと Ellipsis で 1 行も描画されない
                // (v6-select の fs26×矩形28 消滅事故)。行送りを ≈ fs に詰め、
                // 「矩形高 ≥ fs なら必ず 1 行描画される」ようにする。
                // 折り返し段落で行間を広げたい呼び出し側は後から lineSpacing を上書きする
                // (lineSpacing は fs への追加率なので、上書き値がそのまま fs 比の行間になる)。
                if (font.faceInfo.pointSize > 0)
                {
                    tmp.lineSpacing = (1f - font.faceInfo.lineHeight / font.faceInfo.pointSize) * 100f;
                }
            }

            return tmp;
        }

        /// <summary>
        /// Button Primary (契約 §4): 高さ 56 / 背景 #EDEDED r8 / Label fs30 w500 #1B1B1B /
        /// icon 32×32 / Button コンポーネント付き。keyLabel を指定すると右端に KeyCap を載せる。
        /// </summary>
        public static GameObject ButtonPrimary(GameObject parent, string label, Vector2 designTopLeft, Vector2 size,
            string iconName = null, string keyLabel = null)
        {
            return MakeButton(parent, label, designTopLeft, size, V6Tokens.ButtonPrimaryBg,
                V6Tokens.ButtonPrimaryLabel, V6Tokens.ButtonPrimaryLabel, iconName, keyLabel);
        }

        /// <summary>
        /// Button Secondary (契約 §4): 背景 $k4 #3A3A3A / Label fs30 w500 $text / icon・Key $text-muted。
        /// </summary>
        public static GameObject ButtonSecondary(GameObject parent, string label, Vector2 designTopLeft, Vector2 size,
            string iconName = null, string keyLabel = null)
        {
            return MakeButton(parent, label, designTopLeft, size, V6Tokens.Key4,
                V6Tokens.Text, V6Tokens.TextMuted, iconName, keyLabel);
        }

        /// <summary>
        /// KeyCap (契約 §4): 高さ 36 / 背景 $k4 / 角丸 4 / padding [0,10] / Key fs22 $text-muted。
        /// フォントサイズは <see cref="V6Ui.KeyCapFontSize"/> で決める
        /// (v6 は design 空間のため v5 の実寸 fs を使ってはいけない)。
        /// </summary>
        public static GameObject KeyCap(GameObject parent, string label, Vector2 designTopLeft, Vector2 size)
        {
            var go = Panel(parent, "KeyCap " + label, designTopLeft, size, V6Tokens.Key4, V6Tokens.RadiusSmall);
            float fs = V6Ui.KeyCapFontSize(size.x, size.y, label);
            StretchText(go, "Key", label, fs, V6Tokens.TextMuted);
            return go;
        }

        /// <summary>Tag (契約 §4): fs22 (v6 規定)。背景 $k4 r4。</summary>
        public static GameObject Tag(GameObject parent, string label, Vector2 designTopLeft, Vector2 size)
        {
            var go = Panel(parent, "Tag " + label, designTopLeft, size, V6Tokens.Key4, V6Tokens.RadiusSmall);
            float fs = Mathf.Min(TagFontSize, V6Ui.KeyCapFontSize(size.x, size.y, label));
            StretchText(go, "Text", label, fs, V6Tokens.Text);
            return go;
        }

        /// <summary>
        /// アイコンのみの丸ボタン (契約 §7): Settings 48×48 r8 / skip 56×56 r28 (icon 32) /
        /// play 72×72 r36 (icon 36 #1B1B1B) など。背景色・角丸・アイコン寸法は
        /// 契約値を呼び出し側から渡す。アイコンは <see cref="V6Icons"/> 経由
        /// (既存実装・変更禁止)。スプライトが見つからない場合は背景のみ。
        /// </summary>
        public static GameObject IconButton(GameObject parent, string iconName, Vector2 designTopLeft, Vector2 size,
            Color bg, float radius, float iconSize, Color iconColor)
        {
            var go = Panel(parent, "IconButton " + iconName, designTopLeft, size, bg, radius);
            V6Icons.Create(go, iconName, iconSize, iconColor, "Icon");
            var button = go.AddComponent<Button>();
            button.targetGraphic = go.GetComponent<Image>();
            return go;
        }

        /// <summary>角丸スプライト (v6 版)。半径ごとにキャッシュ。白で塗るので色は Image.color で変える。</summary>
        public static Sprite RoundedSprite(int radiusPx)
        {
            if (SpriteCache.TryGetValue(radiusPx, out var cached) && cached != null)
            {
                return cached;
            }

            const int pad = 2; // アンチエイリアス用の余白
            int radius = Mathf.Max(1, radiusPx);
            // +1: 9-slice の中央ストレッチ領域を最低 1px 確保する。
            int size = radius * 2 + pad * 2 + 1;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color32[size * size];
            var opaque = new Color32(255, 255, 255, 255);
            var clear = new Color32(0, 0, 0, 0);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float cx = Mathf.Min(x, size - 1 - x);
                    float cy = Mathf.Min(y, size - 1 - y);
                    float dx = Mathf.Max(0f, radius - 0.5f - cx);
                    float dy = Mathf.Max(0f, radius - 0.5f - cy);
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(radius - dist);
                    pixels[y * size + x] = alpha <= 0f ? clear : Color32.Lerp(clear, opaque, alpha);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            float border = radius + pad;
            var sprite = Sprite.Create(
                tex,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(border, border, border, border));
            sprite.name = "V6Rounded" + radius;
            SpriteCache[radiusPx] = sprite;
            return sprite;
        }

        // --- 内部 ---

        private static GameObject MakeButton(GameObject parent, string label, Vector2 designTopLeft, Vector2 size,
            Color bg, Color labelColor, Color iconColor, string iconName, string keyLabel)
        {
            var go = Panel(parent, "Button " + label, designTopLeft, size, bg, ButtonRadius);
            go.AddComponent<Button>();

            float contentRight = size.x - ButtonPaddingX;
            if (!string.IsNullOrEmpty(keyLabel))
            {
                // 契約 §4: Button 内 KeyCap は Key fs22 (高さ 36, r4, [0,8])。
                float keyW = Mathf.Ceil(V6Ui.LabelAdvanceEm(keyLabel) * ButtonKeyFontSize) + KeyCapPaddingX * 2f;
                var keyGo = KeyCap(go, keyLabel,
                    new Vector2(contentRight - keyW, (size.y - V6Tokens.KeyCapHeight) * 0.5f),
                    new Vector2(keyW, V6Tokens.KeyCapHeight));
                contentRight = keyGo.GetComponent<RectTransform>().anchoredPosition.x - ButtonGap;
            }

            float contentLeft = ButtonPaddingX;
            if (!string.IsNullOrEmpty(iconName))
            {
                var icon = V6Icons.Create(go, iconName, ButtonIconSize, iconColor, "Icon");
                if (icon != null)
                {
                    var iconRt = icon.GetComponent<RectTransform>();
                    iconRt.anchorMin = new Vector2(0f, 0.5f);
                    iconRt.anchorMax = new Vector2(0f, 0.5f);
                    iconRt.pivot = new Vector2(0.5f, 0.5f);
                    iconRt.anchoredPosition = new Vector2(contentLeft + ButtonIconSize * 0.5f, 0f);
                    contentLeft += ButtonIconSize + ButtonGap;
                }
            }

            var labelTmp = Text(go, "Label", label, Vector2.zero, size, ButtonFontSize, labelColor,
                TextAlignmentOptions.Center);
            var labelRt = labelTmp.rectTransform;
            labelRt.anchorMin = new Vector2(0f, 0f);
            labelRt.anchorMax = new Vector2(1f, 1f);
            labelRt.offsetMin = new Vector2(contentLeft, 0f);
            labelRt.offsetMax = new Vector2(-(size.x - contentRight), 0f);
            return go;
        }

        private static void StretchText(GameObject parent, string name, string content, float fontSize, Color color)
        {
            var tmp = Text(parent, name, content, Vector2.zero, Vector2.zero, fontSize, color,
                TextAlignmentOptions.Center);
            var rt = tmp.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(KeyCapPaddingX, 0f);
            rt.offsetMax = new Vector2(-KeyCapPaddingX, 0f);
        }
    }
}
