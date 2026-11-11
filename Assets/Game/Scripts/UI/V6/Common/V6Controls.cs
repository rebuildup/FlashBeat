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

        /// <summary>
        /// Button 内 KeyCap の寸法 (design px)。正本 Button Primary/Secondary の
        /// Cap は高さ 32・padding [0,8] (単体 KeyCap の 36 / [0,10] とは別)。
        /// </summary>
        public const float ButtonKeyCapHeight = 32f;
        public const float ButtonKeyCapPaddingX = 8f;

        // --- 契約 §4 の KeyCap / Tag 定数 (design px) ---
        public const float KeyCapFontSize = 22f;
        public const float KeyCapPaddingX = 10f;
        public const float TagFontSize = 22f;

        // --- ヘッダー左上の戻るボタン (design zUcsi/zCQBY) ---
        // 単体 KeyCap (36) より小さい専用部品: 56×56 の中に
        // arrow-left 24 @(16,4) と Key fs16 @(15,30) 26×23 を重ねる。
        // 中央寄せで積むとアイコンが下がり Key と重なる (Select で発生していた)。
        public const float BackButtonSize = 56f;
        public const float BackButtonIconSize = 24f;
        public static readonly Vector2 BackButtonIconPos = new Vector2(16f, 4f);
        public static readonly Vector2 BackButtonKeyPos = new Vector2(15f, 30f);
        public static readonly Vector2 BackButtonKeySize = new Vector2(26f, 23f);
        public const float BackButtonKeyFontSize = 16f;

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
            FontStyles weight = FontStyles.Normal, TextWrappingModes wrapping = TextWrappingModes.NoWrap)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            V6Layout.Place(go.GetComponent<RectTransform>(), designTopLeft, size);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = content;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = align;
            // design 空間の固定 Rect に収める前提なので既定は折り返さない
            // (幅が足りないラベル "FlashBeat" / "START" / "設定" が 2 行に折れて
            // 隣の要素と重なる事故を防ぐ。v5 / v6 各ビューと同じ NoWrap 方針)。
            // design 上で複数行になる説明文だけが wrapping: Normal を明示する
            // (Option の Desc: w464 h68 / lineHeight 1.4)。
            tmp.textWrappingMode = wrapping;
            tmp.fontStyle = weight;
            tmp.raycastTarget = false;
            var font = TMP_Settings.defaultFontAsset != null
                ? TMP_Settings.defaultFontAsset
                : Resources.Load<TMP_FontAsset>("NotoSansJP-Medium SDF");
            if (font != null)
            {
                tmp.font = font;
                // 行送りを詰めて 1 行の描画位置を fs に揃える。
                // 注意: lineSpacing は行間 (2 行目以降) にしか効かず、Ellipsis の
                // 「実効高 < 行送り (fs × 1.448) なら 0 行」判定は変わらない
                // (fs26×h36 で消えた実例の回避は V5Ui.FitEllipsisToLineHeight で行う)。
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
        /// Button Primary (契約 §4 / Prefab/Button Primary meK44):
        /// 高さ 56 / 背景 #EDEDED r8 / gap 12 / justify center / align center。
        /// 中身は icon 32×32 #1B1B1B + Label fs30 w500 #1B1B1B + Cap (h32 r4 pad[0,8]
        /// $cap-primary #00000026, Key fs22 #1B1B1B) の 3 要素を中央に並べる。
        /// </summary>
        public static GameObject ButtonPrimary(GameObject parent, string label, Vector2 designTopLeft, Vector2 size,
            string iconName = null, string keyLabel = null)
        {
            return MakeButton(parent, label, designTopLeft, size, V6Tokens.ButtonPrimaryBg,
                V6Tokens.ButtonPrimaryLabel, V6Tokens.ButtonPrimaryLabel, V6Tokens.ButtonPrimaryLabel,
                V6Tokens.CapPrimary, iconName, keyLabel);
        }

        /// <summary>
        /// Button Secondary (契約 §4 / Prefab/Button Secondary qWWcD):
        /// 背景 $k4 #3A3A3A / Label fs30 w500 $text / icon $text-muted /
        /// Cap (h32 r4 pad[0,8] $cap-secondary #00000040, Key fs22 $text-muted)。
        /// Primary と同じく中身は中央揃え。
        /// </summary>
        public static GameObject ButtonSecondary(GameObject parent, string label, Vector2 designTopLeft, Vector2 size,
            string iconName = null, string keyLabel = null)
        {
            return MakeButton(parent, label, designTopLeft, size, V6Tokens.Key4,
                V6Tokens.Text, V6Tokens.TextMuted, V6Tokens.TextMuted, V6Tokens.CapSecondary,
                iconName, keyLabel);
        }

        /// <summary>
        /// KeyCap (契約 §4): 高さ 36 / 背景 $k4 / 角丸 4 / padding [0,10] / Key fs22 $text-muted。
        /// フォントサイズは <see cref="V6Ui.KeyCapFontSize"/> で決める
        /// (v6 は design 空間のため v5 の実寸 fs を使ってはいけない)。
        /// </summary>
        public static GameObject KeyCap(GameObject parent, string label, Vector2 designTopLeft, Vector2 size)
        {
            return KeyCap(parent, label, designTopLeft, size, KeyCapPaddingX);
        }

        /// <summary>
        /// 既存の 56×56 戻るボタンに正本どおりの中身 (arrow-left 24 + Key fs16) を入れる。
        /// ボタン自体の生成 / クリック結線は各画面のヘルパに任せる (二重に Button を付けない)。
        /// </summary>
        public static void FillBackButton(GameObject back, string keyLabel)
        {
            if (back == null)
            {
                return;
            }

            var icon = V6Icons.Create(back, "arrow-left", BackButtonIconSize, V6Tokens.TextMuted, "Icon");
            if (icon != null)
            {
                var iconRect = icon.rectTransform;
                iconRect.anchorMin = new Vector2(0f, 1f);
                iconRect.anchorMax = new Vector2(0f, 1f);
                iconRect.pivot = new Vector2(0f, 1f);
                iconRect.anchoredPosition = new Vector2(BackButtonIconPos.x, -BackButtonIconPos.y);
                iconRect.sizeDelta = new Vector2(BackButtonIconSize, BackButtonIconSize);
            }

            if (!string.IsNullOrEmpty(keyLabel))
            {
                Text(back, "Key", keyLabel, BackButtonKeyPos, BackButtonKeySize,
                    BackButtonKeyFontSize, V6Tokens.TextMuted, TextAlignmentOptions.Center);
            }
        }

        /// <summary>
        /// KeyCap を左右パディング指定で作る。ボタン内 (padding 8) のように
        /// 正本の寸法が単体 KeyCap と異なる箇所で使う。背景・Key 色は既存の
        /// 「単体 Cap = $k4 / $text-muted」(Prefab/KeyCap gr4IM) のまま。
        /// </summary>
        public static GameObject KeyCap(GameObject parent, string label, Vector2 designTopLeft, Vector2 size,
            float paddingX)
        {
            return KeyCap(parent, label, designTopLeft, size, paddingX, V6Tokens.Key4, V6Tokens.TextMuted);
        }

        /// <summary>
        /// KeyCap を背景色・Key 色まで指定して作る。Primary ボタン内の Cap は
        /// 透過黒 (#00000026) + #1B1B1B の Key (Prefab/Button Primary meK44) と
        /// 単体 Cap で配色が異なるため、呼び出し側が明示できるようにする。
        /// </summary>
        public static GameObject KeyCap(GameObject parent, string label, Vector2 designTopLeft, Vector2 size,
            float paddingX, Color capFill, Color keyColor)
        {
            var go = Panel(parent, "KeyCap " + label, designTopLeft, size, capFill, V6Tokens.RadiusSmall);
            float fs = KeyFontSize(size.x, label, paddingX);
            StretchText(go, "Key", label, fs, keyColor, paddingX);
            return go;
        }

        /// <summary>
        /// KeyCap の Key fs (design 空間)。正本は高さ 36 の単体 Cap も、高さ 32 の
        /// Button 内 / フッター凡例 Cap も Key fs22 (Prefab/KeyCap, Prefab/Button Primary,
        /// 05b フッター Legend の実測値)。高さ比では縮めず、幅に収まらないときだけ縮小する。
        /// </summary>
        public static float KeyFontSize(float capWidth, string label, float paddingX)
        {
            float available = capWidth - paddingX * 2f;
            float em = V6Ui.LabelAdvanceEm(label);
            if (em <= 0f || available <= 0f)
            {
                return KeyCapFontSize;
            }

            return Mathf.Max(1f, Mathf.Floor(Mathf.Min(KeyCapFontSize, available / em)));
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
            Color bg, Color labelColor, Color iconColor, Color keyColor, Color capFill,
            string iconName, string keyLabel)
        {
            var go = Panel(parent, "Button " + label, designTopLeft, size, bg, ButtonRadius);
            var button = go.AddComponent<Button>();
            // targetGraphic が無いと ColorTint 遷移が走らず、押下しても見た目が変わらない
            // (IconButton は設定済み)。主要ボタンすべてに押下 / ホバーの手応えを付ける。
            button.targetGraphic = go.GetComponent<Image>();

            // 正本 (Prefab/Button Primary meK44 / Secondary qWWcD) は
            // gap 12 / justify center / align center。icon・Label・Cap は
            // 右寄せ・左寄せに固定せず、3 つをまとめて中央に並べる。
            // 右寄せ固定にすると、長い日本語ラベル (この曲へ) で Cap が
            // ボタン端に貼り付き、短いラテンラベル (OK) で余りすぎる
            // (design では この曲へ = 端から 9.5px / OK = 48px の差が出る)。
            Image icon = string.IsNullOrEmpty(iconName)
                ? null
                : V6Icons.Create(go, iconName, ButtonIconSize, iconColor, "Icon");
            float iconW = icon != null ? ButtonIconSize : 0f;

            var labelTmp = Text(go, "Label", label, Vector2.zero, new Vector2(size.x, size.y),
                ButtonFontSize, labelColor, TextAlignmentOptions.Center);
            var labelRt = labelTmp.rectTransform;
            labelRt.anchorMin = new Vector2(0f, 0.5f);
            labelRt.anchorMax = new Vector2(0f, 0.5f);
            labelRt.pivot = new Vector2(0f, 0.5f);

            // 送り幅は 0.62em の概算だと Latin で 3 割ほど広い ("Register" の
            // 実測 110.5px に対し概算 149px)。ボタンは中身を中央に寄せるため、
            // この誤差がそのまま要素の位置ずれ・Cap のはみ出しになる。
            // TMP の preferred width を実測し、取れないときだけ概算へ戻す。
            float labelW = PreferredWidth(labelTmp, label,
                Mathf.Ceil(V6Ui.LabelAdvanceEm(label) * ButtonFontSize));

            GameObject capGo = null;
            float keyW = 0f;
            if (!string.IsNullOrEmpty(keyLabel))
            {
                // 契約 §4: Button 内 KeyCap は Key fs22 (高さ 32, r4, [0,8])。
                // 幅はまず概算で作り、Key の実測幅で詰める。
                keyW = Mathf.Ceil(V6Ui.LabelAdvanceEm(keyLabel) * ButtonKeyFontSize)
                    + ButtonKeyCapPaddingX * 2f;
                capGo = KeyCap(go, keyLabel, Vector2.zero,
                    new Vector2(keyW, ButtonKeyCapHeight), ButtonKeyCapPaddingX, capFill, keyColor);
                var capKeyTmp = capGo.GetComponentInChildren<TextMeshProUGUI>();
                keyW = PreferredWidth(capKeyTmp, keyLabel, keyW - ButtonKeyCapPaddingX * 2f)
                    + ButtonKeyCapPaddingX * 2f;
            }

            int parts = (iconW > 0f ? 1 : 0) + 1 + (capGo != null ? 1 : 0);
            float contentW = iconW + labelW + keyW + ButtonGap * (parts - 1);
            float x = (size.x - contentW) * 0.5f;

            if (icon != null)
            {
                var iconRt = icon.GetComponent<RectTransform>();
                iconRt.anchorMin = new Vector2(0f, 0.5f);
                iconRt.anchorMax = new Vector2(0f, 0.5f);
                iconRt.pivot = new Vector2(0f, 0.5f);
                iconRt.anchoredPosition = new Vector2(x, 0f);
                x += iconW + ButtonGap;
            }

            labelRt.sizeDelta = new Vector2(labelW, size.y);
            labelRt.anchoredPosition = new Vector2(x, 0f);
            x += labelW;

            if (capGo != null)
            {
                x += ButtonGap;
                var capRt = capGo.GetComponent<RectTransform>();
                capRt.sizeDelta = new Vector2(keyW, ButtonKeyCapHeight);
                capRt.anchoredPosition = new Vector2(x, -(size.y - ButtonKeyCapHeight) * 0.5f);
            }

            return go;
        }

        /// <summary>
        /// TMP の preferred width を実測値として返す (design 空間 px)。
        /// 0 / NaN / 未レイアウトで取れないときは fallback (概算) を返す。
        /// 0.62em の ASCII 概算はラテン文字を過大評価する ("Register" 実測 110.5px に対し
        /// 概算 149px) ため、frame を実測で詰めたい呼び出し側 (凡例ラベル・ボタン) から使う。
        /// </summary>
        public static float MeasureWidth(TextMeshProUGUI tmp, string content, float fallback)
        {
            return PreferredWidth(tmp, content, fallback);
        }

        private static float PreferredWidth(TextMeshProUGUI tmp, string content, float fallback)
        {
            if (tmp == null)
            {
                return fallback;
            }

            float w = tmp.GetPreferredValues(content).x;
            if (float.IsNaN(w) || float.IsInfinity(w) || w <= 0f)
            {
                return fallback;
            }

            return Mathf.Ceil(w);
        }

        private static void StretchText(GameObject parent, string name, string content, float fontSize, Color color,
            float paddingX = KeyCapPaddingX)
        {
            var tmp = Text(parent, name, content, Vector2.zero, Vector2.zero, fontSize, color,
                TextAlignmentOptions.Center);
            var rt = tmp.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(paddingX, 0f);
            rt.offsetMax = new Vector2(-paddingX, 0f);
        }
    }
}
