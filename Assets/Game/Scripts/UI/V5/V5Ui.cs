using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace FlashBeat.UI.V5
{
    /// <summary>
    /// v5 UI 共通ビルダ (Issue #106)。コードから Components v5 相当の uGUI を構築する。
    /// サイズは <see cref="UISizeProfile.Current"/>、色/角丸は <see cref="V5Tokens"/> から。
    ///
    /// 公開 API 一覧 (後続の画面実装はこの上に載せること):
    /// - プロファイル/Canvas
    ///   UISizeProfile.Current / UISizeProfile.Select(w,h) / UISizeProfile.ClearCache()
    ///   CanvasConfigurator.Configure(Canvas) : CanvasConfigurator (Constant Pixel Size x1, 解像度変化で再適用)
    /// - レイアウト (全て RectTransform を返す)
    ///   V5Ui.Node(parent, name)                       : 素の RectTransform
    ///   V5Ui.Fill(rt, margin)                         : 親全体に広げる (margin は実ピクセル)
    ///   V5Ui.PinTop/PinBottom/PinLeft/PinRight(rt, size, margin) : エッジ相対アンカーで固定
    ///   V5Ui.Place(rt, anchor, pivot, pos, size)      : 汎用アンカー設定 (点アンカー)
    /// - 背景スプライト
    ///   V5Ui.RoundedSprite(radiusPx) : 角丸 9-slice スプライト (実行時生成, 伸縮しても角が潰れない, 半径ごとにキャッシュ)
    /// - 部品 (GameObject を返す。RectTransform は go.GetComponent&lt;RectTransform&gt;())
    ///   V5Ui.Panel(parent, name, size, color, radius)      : 角丸背景付きパネル (Image Sliced)
    ///   V5Ui.Text(...)        : TMP テキスト (既定フォント NotoSansJP-Medium SDF / TMP_Settings.defaultFontAsset)
    ///   V5Ui.KeyCap(parent, label, size)                   : Prefab/KeyCap 相当 (bg Key4, r=4, ラベル fs_s muted)
    ///   V5Ui.Tag(parent, label, size)                      : Prefab/Tag 相当 (bg Key4, r=4, ラベル fs_s)
    ///   V5Ui.ButtonPrimary(parent, label)                  : Prefab/Button Primary 相当 (bg #EDEDED, r=8, ラベル #1B1B1B fs_m, Button 付き)
    ///   V5Ui.ButtonSecondary(parent, label)                : Prefab/Button Secondary 相当 (bg Key4, r=8, ラベル text fs_m, Button 付き)
    ///   V5Ui.SongRow(parent, title, composer, level)       : Prefab/SongRow 相当 (row_h, Thumb + Title/Composer + Level)
    ///   V5Ui.RankRow(parent, pos, name, score)             : Prefab/RankRow 相当 (Pos / Name / 右寄せ Score)
    /// - 押下演出やサムネ画像の設定は各画面側で行う。
    /// </summary>
    public static class V5Ui
    {
        private static readonly Dictionary<int, Sprite> spriteCache = new Dictionary<int, Sprite>();

        // --- レイアウト ---

        /// <summary>素の RectTransform ノードを作る。</summary>
        public static GameObject Node(GameObject parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (parent != null)
            {
                go.GetComponent<RectTransform>().SetParent(parent.transform, false);
            }

            return go;
        }

        /// <summary>親いっぱいに広げる (margin は実ピクセル)。Constant Pixel Size x1 前提。</summary>
        public static RectTransform Fill(Component node, float margin)
        {
            var rt = GetRect(node);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(margin, margin);
            rt.offsetMax = new Vector2(-margin, -margin);
            return rt;
        }

        /// <summary>上端に帯として固定 (高さ size)。</summary>
        public static RectTransform PinTop(Component node, float size, float margin)
        {
            var rt = GetRect(node);
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(margin, -margin - size);
            rt.offsetMax = new Vector2(-margin, -margin);
            return rt;
        }

        /// <summary>下端に帯として固定 (高さ size)。</summary>
        public static RectTransform PinBottom(Component node, float size, float margin)
        {
            var rt = GetRect(node);
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.offsetMin = new Vector2(margin, margin);
            rt.offsetMax = new Vector2(-margin, margin + size);
            return rt;
        }

        /// <summary>左端に帯として固定 (幅 size)。</summary>
        public static RectTransform PinLeft(Component node, float size, float margin)
        {
            var rt = GetRect(node);
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.offsetMin = new Vector2(margin, margin);
            rt.offsetMax = new Vector2(margin + size, -margin);
            return rt;
        }

        /// <summary>右端に帯として固定 (幅 size)。</summary>
        public static RectTransform PinRight(Component node, float size, float margin)
        {
            var rt = GetRect(node);
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(-margin - size, margin);
            rt.offsetMax = new Vector2(-margin, -margin);
            return rt;
        }

        /// <summary>汎用アンカー設定 (anchor 0-1 の点アンカー, pos はアンカー基準オフセット)。</summary>
        public static RectTransform Place(Component node, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var rt = GetRect(node);
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        // --- 背景 ---

        /// <summary>
        /// 角丸スプライトを実行時生成する (半径 radiusPx, fhd 実ピクセル基準)。
        /// 9-slice border を radius+pad に設定するので、どんなサイズに伸縮しても角が潰れない。
        /// 半径ごとにキャッシュ。白で塗るので色は Image.color で変える。
        /// </summary>
        public static Sprite RoundedSprite(int radiusPx)
        {
            if (spriteCache.TryGetValue(radiusPx, out var cached) && cached != null)
            {
                return cached;
            }

            const int pad = 2; // アンチエイリアス用の余白
            int radius = Mathf.Max(1, radiusPx);
            // +1: 9-slice の中央ストレッチ領域を最低 1px 確保する
            // (border の合計 2*(radius+pad) がテクスチャ幅と一致すると中央が 0px になり伸縮しない)。
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
                    // 角の中心からの距離でアルファを決める (AA 付き角丸)。
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
            sprite.name = "V5Rounded" + radius;
            spriteCache[radiusPx] = sprite;
            return sprite;
        }

        /// <summary>角丸背景付きパネル。色は V5Tokens から選ぶ。</summary>
        public static GameObject Panel(GameObject parent, string name, Vector2 size, Color color, float radius)
        {
            var go = Node(parent, name);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.type = Image.Type.Sliced;
            img.sprite = RoundedSprite(Mathf.RoundToInt(radius));
            img.color = color;
            return go;
        }

        // --- テキスト ---

        /// <summary>
        /// TMP テキストを作る。フォントは TMP_Settings.defaultFontAsset
        /// (プロジェクト既定 = NotoSansJP-Medium SDF) を使い、無ければ Resources ロード、
        /// それも無ければ TMP 既定に任せる。
        /// </summary>
        public static TextMeshProUGUI Text(GameObject parent, string name, string content, float fontSize, Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.Left, FontStyles weight = FontStyles.Normal)
        {
            var go = Node(parent, name);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = content;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.fontStyle = weight;
            tmp.raycastTarget = false;
            var font = TMP_Settings.defaultFontAsset != null
                ? TMP_Settings.defaultFontAsset
                : Resources.Load<TMP_FontAsset>("NotoSansJP-Medium SDF");
            if (font != null)
            {
                tmp.font = font;
                // 行送りを詰めて 1 行の描画位置を fs に揃える (折り返し段落で行間を広げたい
                // 呼び出し側は後から lineSpacing を上書きする)。
                // 注意: lineSpacing は行間 (2 行目以降の間隔) にしか効かず、Ellipsis の
                // 「矩形高 < 行送りなら 0 行」判定は faceInfo の行送りのまま変わらない。
                // 固定矩形で Ellipsis を使う箇所は FitEllipsisToLineHeight を併用すること。
                if (font.faceInfo.pointSize > 0)
                {
                    tmp.lineSpacing = (1f - font.faceInfo.lineHeight / font.faceInfo.pointSize) * 100f;
                }
            }

            return tmp;
        }

        /// <summary>
        /// 固定矩形 + <see cref="TextOverflowModes.Ellipsis"/> の 1 行テキストが
        /// 「1 行も描画されない」TMP の挙動を打ち消す。
        ///
        /// TMP は矩形の実効高 (rect 高 − margin 上下) が行送り
        /// (NotoSansJP-Medium: lineHeight 69.504 / pointSize 48 = <b>fs × 1.448</b>) を
        /// 下回ると Ellipsis で 0 行にし、空白になる。実測 (EditMode):
        /// fs20×h29 (= 28.96、ちょうど) は描画 / fs23×h33 (33.35) / fs26×h36 (37.65) /
        /// fs30×h43 (43.44) / fs34×h36 (49.23) は非描画。lineSpacing を詰めても
        /// この判定には効かない (行間専用)。design 矩形は往々にしてこの境界を
        /// 数 px 下回るため、矩形を高くする回避 (Pause の 43→46 等) が必要だった。
        ///
        /// ここでは上下 margin を同じ負値にして実効高だけを広げる。垂直中央揃えなので
        /// 描画位置・矩形サイズは自然描画と完全に一致する (実測でベースライン同一)。
        /// 矩形の高さ自体を増やすと中央揃えで下へずれるので不可。
        /// <b>rect (design 座標) と fontSize を確定した後に呼ぶこと。</b>
        /// text の再設定は不要 (margin は再生成後も保持される)。
        /// </summary>
        public static void FitEllipsisToLineHeight(TextMeshProUGUI tmp)
        {
            FitEllipsisToLineHeight(tmp, 0f);
        }

        /// <summary>
        /// <see cref="FitEllipsisToLineHeight(TextMeshProUGUI)"/> の矩形高さ明示版。
        /// LayoutGroup 配下など、呼び出し時点で rect が確定していない (0 の) 場合に使う。
        /// rectHeight &lt;= 0 は rect から読む。
        /// </summary>
        public static void FitEllipsisToLineHeight(TextMeshProUGUI tmp, float rectHeight)
        {
            if (tmp == null)
            {
                return;
            }

            var font = tmp.font;
            if (font == null || font.faceInfo.pointSize <= 0f)
            {
                return;
            }

            float lineHeight = font.faceInfo.lineHeight / font.faceInfo.pointSize * tmp.fontSize;
            if (rectHeight <= 0f)
            {
                rectHeight = tmp.rectTransform.rect.height;
            }

            var m = tmp.margin;
            float effective = rectHeight - m.y - m.w;
            if (effective >= lineHeight)
            {
                return;
            }

            // 切り上げで必ず行送り以上にする (実測境界が一致値のため丸め負けを防ぐ)。
            float pad = Mathf.Ceil((lineHeight - effective) * 0.5f);
            tmp.margin = new Vector4(m.x, m.y - pad, m.z, m.w - pad);
        }

        // --- Components v5 相当部品 ---

        /// <summary>
        /// Prefab/KeyCap 相当: bg Key4, 角丸 4, ラベル fs_s muted。
        /// fontSize &gt; 0 のときはその値をラベル fs に使う (呼び出し側の座標空間で指定)。
        /// 既定 0 は従来どおり <see cref="UISizeProfile.Current"/>.FsS。
        /// </summary>
        public static GameObject KeyCap(GameObject parent, string label, Vector2 size, float fontSize = 0f)
        {
            var fs = fontSize > 0f ? fontSize : UISizeProfile.Current.FsS;
            var go = Panel(parent, "KeyCap " + label, size, V5Tokens.ControlBg, V5Tokens.RadiusSmall);
            StretchText(go, "Key", label, fs, V5Tokens.TextMuted);
            return go;
        }

        /// <summary>
        /// Prefab/Tag 相当: bg Key4, 角丸 4, ラベル fs_s。
        /// fontSize &gt; 0 のときはその値をラベル fs に使う。既定 0 は従来どおり FsS。
        /// </summary>
        public static GameObject Tag(GameObject parent, string label, Vector2 size, float fontSize = 0f)
        {
            var fs = fontSize > 0f ? fontSize : UISizeProfile.Current.FsS;
            var go = Panel(parent, "Tag " + label, size, V5Tokens.ControlBg, V5Tokens.RadiusSmall);
            StretchText(go, "Text", label, fs, V5Tokens.Text);
            return go;
        }

        /// <summary>Prefab/Button Primary 相当: bg #EDEDED, 角丸 8, ラベル #1B1B1B fs_m。</summary>
        public static GameObject ButtonPrimary(GameObject parent, string label, Vector2 size)
        {
            return MakeButton(parent, label, size, V5Tokens.ButtonPrimaryBg, V5Tokens.ButtonPrimaryLabel);
        }

        /// <summary>Prefab/Button Secondary 相当: bg Key4, 角丸 8, ラベル text fs_m。</summary>
        public static GameObject ButtonSecondary(GameObject parent, string label, Vector2 size)
        {
            return MakeButton(parent, label, size, V5Tokens.ControlBg, V5Tokens.Text);
        }

        /// <summary>
        /// Prefab/SongRow 相当: 幅 card_mid.x, 高さ row_h。
        /// サムネ枠 (96:54 比率) + Title (fs_m) / Composer (fs_s muted) の 2 段 + 右端 Level (fs_s muted)。
        /// </summary>
        public static GameObject SongRow(GameObject parent, string title, string composer, string level)
        {
            var p = UISizeProfile.Current;
            var go = Panel(parent, "SongRow " + title, new Vector2(p.CardMid.x, p.RowH), V5Tokens.Recessed2, V5Tokens.RadiusMedium);

            float thumbH = p.RowH - p.GapS * 2f;
            float thumbW = thumbH * (96f / 54f);
            var thumb = Panel(go, "Thumb", new Vector2(thumbW, thumbH), V5Tokens.Key2, V5Tokens.RadiusSmall);
            var thumbRt = thumb.GetComponent<RectTransform>();
            thumbRt.anchorMin = new Vector2(0f, 0.5f);
            thumbRt.anchorMax = new Vector2(0f, 0.5f);
            thumbRt.pivot = new Vector2(0f, 0.5f);
            thumbRt.anchoredPosition = new Vector2(p.GapM, 0f);

            var col = Node(go, "Text");
            var colRt = col.GetComponent<RectTransform>();
            colRt.anchorMin = new Vector2(0f, 0f);
            colRt.anchorMax = new Vector2(1f, 1f);
            colRt.offsetMin = new Vector2(p.GapM + thumbW + p.GapM, p.GapS);
            colRt.offsetMax = new Vector2(-p.GapL, -p.GapS);

            var titleTmp = Text(col, "Title", title, p.FsM, V5Tokens.Text, TextAlignmentOptions.Left, FontStyles.Bold);
            var titleRt = titleTmp.rectTransform;
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.offsetMin = new Vector2(0f, -(p.FsM + p.GapS));
            titleRt.offsetMax = new Vector2(0f, 0f);

            var composerTmp = Text(col, "Composer", composer, p.FsS, V5Tokens.TextMuted);
            var composerRt = composerTmp.rectTransform;
            composerRt.anchorMin = new Vector2(0f, 0f);
            composerRt.anchorMax = new Vector2(1f, 0f);
            composerRt.offsetMin = new Vector2(0f, 0f);
            composerRt.offsetMax = new Vector2(0f, p.FsS + p.GapS);

            var levelTmp = Text(go, "Level", level, p.FsS, V5Tokens.TextMuted, TextAlignmentOptions.Right);
            var levelRt = levelTmp.rectTransform;
            levelRt.anchorMin = new Vector2(1f, 0f);
            levelRt.anchorMax = new Vector2(1f, 1f);
            levelRt.offsetMin = new Vector2(-(p.GapL + p.GapM), p.GapS);
            levelRt.offsetMax = new Vector2(-p.GapM, -p.GapS);
            return go;
        }

        /// <summary>
        /// Prefab/RankRow 相当: 順位 / 名前 / 右寄せスコア。
        /// 高さは row_h の 56/72 比 (Components v5 の SongRow:RankRow 比)。
        /// </summary>
        public static GameObject RankRow(GameObject parent, string pos, string name, string score)
        {
            var p = UISizeProfile.Current;
            float h = p.RowH * (56f / 72f);
            var go = Panel(parent, "RankRow " + pos, new Vector2(p.CardMid.x, h), V5Tokens.Recessed2, V5Tokens.RadiusMedium);

            var posTmp = Text(go, "Pos", pos, p.FsM, V5Tokens.Text, TextAlignmentOptions.Left, FontStyles.Bold);
            Place(posTmp.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(p.GapM, 0f), new Vector2(p.GapL, h));

            var nameTmp = Text(go, "Name", name, p.FsM, V5Tokens.Text);
            Place(nameTmp.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(p.GapM + p.GapL + p.GapM, 0f), new Vector2(300f, h));

            var scoreTmp = Text(go, "Score", score, p.FsM, V5Tokens.Text, TextAlignmentOptions.Right);
            Place(scoreTmp.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-p.GapM, 0f), new Vector2(p.GapL * 4f, h));
            return go;
        }

        // --- 内部 ---

        private static GameObject MakeButton(GameObject parent, string label, Vector2 size, Color bg, Color labelColor)
        {
            var go = Panel(parent, "Button " + label, size, bg, V5Tokens.RadiusMedium);
            StretchText(go, "Label", label, UISizeProfile.Current.FsM, labelColor, FontStyles.Bold);
            go.AddComponent<Button>();
            return go;
        }

        private static void StretchText(GameObject parent, string name, string content, float fontSize, Color color,
            FontStyles weight = FontStyles.Normal)
        {
            var tmp = Text(parent, name, content, fontSize, color, TextAlignmentOptions.Center, weight);
            var rt = tmp.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static RectTransform GetRect(Component node)
        {
            if (node is RectTransform rt)
            {
                return rt;
            }

            return node.GetComponent<RectTransform>();
        }
    }
}
