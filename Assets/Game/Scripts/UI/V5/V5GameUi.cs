using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace FlashBeat.UI.V5
{
    /// <summary>
    /// V5 ゲーム画面専用の小さな uGUI ヘルパ。
    /// V5Ui (foundation, Issue #106) には手を入れないための別ファイル。
    /// </summary>
    internal static class V5GameUi
    {
        /// <summary>
        /// ScreenSpaceOverlay の Canvas を作る (Constant Pixel Size x1, Raycaster 付き)。
        /// CanvasConfigurator は解像度変化の再適用だけ担当する。
        /// </summary>
        public static Canvas NewCanvas(string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            CanvasConfigurator.Configure(canvas);
            canvas.sortingOrder = sortingOrder; // Configure は 500 を上書きするので戻す
            return canvas;
        }

        /// <summary>TMP テキスト。フォントは TMP 既定 → Resources の NotoSansJP SDF。</summary>
        public static TextMeshProUGUI Text(GameObject parent, string name, string content, float fontSize, Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.Left, FontStyles weight = FontStyles.Normal)
        {
            var tmp = V5Ui.Text(parent, name, content, fontSize, color, alignment, weight);
            var font = TMP_Settings.defaultFontAsset != null
                ? TMP_Settings.defaultFontAsset
                : Resources.Load<TMP_FontAsset>(V5Tokens.FontAssetPath);
            if (font != null) tmp.font = font;
            return tmp;
        }

        /// <summary>角丸パネル (V5Ui への委譲、読みやすさのため)。</summary>
        public static GameObject Panel(GameObject parent, string name, Vector2 size, Color color, float radius)
        {
            return V5Ui.Panel(parent, name, size, color, radius);
        }

        /// <summary>LegendItem 相当: KeyCap + 説明ラベルを横並びに。</summary>
        public static GameObject LegendItem(GameObject parent, string key, string label)
        {
            var p = UISizeProfile.Current;
            var row = V5Ui.Node(parent, "Legend " + label);
            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = p.GapM;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            V5Ui.KeyCap(row, key, new Vector2(p.FsS * 3.4f, p.FsS * 1.64f));
            Text(row, "Label", label, p.FsS, V5Tokens.TextMuted);
            return row;
        }

        /// <summary>横並びレイアウト (gap 指定, 子のサイズ維持)。</summary>
        public static HorizontalLayoutGroup Row(GameObject parent, string name, float gap, TextAnchor align)
        {
            var go = V5Ui.Node(parent, name);
            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = gap;
            layout.childAlignment = align;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return layout;
        }

        /// <summary>縦並びレイアウト (gap 指定, 子のサイズ維持)。</summary>
        public static VerticalLayoutGroup Column(GameObject parent, string name, float gap, TextAnchor align)
        {
            var go = V5Ui.Node(parent, name);
            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.spacing = gap;
            layout.childAlignment = align;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return layout;
        }

        /// <summary>
        /// design/assets の PNG を Resources/V5/&lt;screen&gt;/ から読んで Sprite 化する。
        /// (LFS 実体はビルド時に Resources へ取り込まれている前提。無ければ null。)
        /// </summary>
        public static Sprite LoadSprite(string resourcePath)
        {
            var tex = Resources.Load<Texture2D>(resourcePath);
            if (tex == null) return null;
            return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>Image にスプライトを設定する (スプライトが null のときは単色の角丸にフォールバック)。</summary>
        public static Image SetOrFallback(GameObject panel, Sprite sprite, Color fallbackColor, float radius)
        {
            var img = panel.GetComponent<Image>();
            if (sprite != null)
            {
                img.type = Image.Type.Simple;
                img.sprite = sprite;
                img.color = Color.white;
                img.preserveAspect = true;
            }
            else
            {
                img.type = Image.Type.Sliced;
                img.sprite = V5Ui.RoundedSprite(Mathf.RoundToInt(radius));
                img.color = fallbackColor;
            }
            return img;
        }
    }
}
