using UnityEngine;
using UnityEngine.EventSystems;
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
        /// 生成した v5 の Canvas (Issue #396)。
        ///
        /// 旧 v5 UI を隠す処理は、以前シーン全体の GameObject/Transform を毎フレーム
        /// 列挙していた。v5 の Canvas はすべて <see cref="NewCanvas"/> を通るので、
        /// 生成時に参照を登録しておけば、隠す側は登録済みの数個を見るだけで済む
        /// (全走査も配列確保も起きない)。
        ///
        /// **v6 の Canvas は登録しない。** v6 も同じファクトリを使う
        /// (`V6GameHudCanvas` / `V6 Result Canvas` など) が、それらは v6 自身なので
        /// 隠してはいけない。名前が "V6" で始まるものは対象外にする。
        /// これを間違えると、v6 の HUD Canvas を v5 の隠す処理が無効化して
        /// 画面が極端に重くなる (2026-11-12 に実際に踏んだ)。
        /// </summary>
        private static readonly System.Collections.Generic.List<Canvas> createdCanvases =
            new System.Collections.Generic.List<Canvas>();

        /// <summary>
        /// v5 が生成した Canvas の一覧。隠す側はこれを読むだけ (コピーしない)。
        /// 毎フレーム読まれるので、配列を作り直さない。
        /// </summary>
        public static System.Collections.Generic.IReadOnlyList<Canvas> CreatedCanvases
        {
            get { return createdCanvases; }
        }

        /// <summary>
        /// v5 の Canvas を生成した回数 (Issue #396)。
        ///
        /// 旧 v5 UI が新しく現れるのは Canvas 生成のときだけなので、隠す側は
        /// この番号が変わったときだけシーンを走査すればよい。
        /// </summary>
        private static int canvasGeneration;

        public static int CanvasGeneration
        {
            get { return canvasGeneration; }
        }

        /// <summary>テスト用: 登録を空に戻す。</summary>
        public static void ResetCreatedCanvasesForAudit()
        {
            createdCanvases.Clear();
            canvasGeneration = 0;
        }

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
            // v6 自身の Canvas は「旧 v5 UI を隠す」対象にしない (登録しない)。
            if (name != null && !name.StartsWith("V6"))
            {
                createdCanvases.Add(canvas);
            }
            canvasGeneration++;
            // 監査で同じ名前の Canvas が二重に見えたとき、どの生成かを追えるようにする。
            Debug.Log("[V5GameUi] NewCanvas " + name + " id=" + canvas.GetEntityId()
                + " scene=" + go.scene.name + " children=" + go.transform.childCount);
            return canvas;
        }

        /// <summary>
        /// v5 の画面 (Title / Select / Option / Result) 用の Canvas を作る (Issue #167)。
        /// これらの画面はコードで Canvas を組み立てており、GraphicRaycaster が無かったため
        /// **マウスクリックが Button に届かず**「押せない」状態になっていた。
        /// </summary>
        public static Canvas NewScreenCanvas(string name)
        {
            return NewCanvas(name, 500);
        }

        /// <summary>
        /// オブジェクトを破棄する (Issue #167)。
        ///
        /// Edit モード (テスト) では Destroy が使えない ("Destroy may not be called
        /// from edit mode") ので DestroyImmediate に落とす。Play モードでは通常の
        /// Destroy (フレーム末に破棄) を使う。
        /// </summary>
        public static void DestroyNow(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(target);
                return;
            }

            Object.DestroyImmediate(target);
        }

        /// <summary>
        /// 入力の二重処理を防ぐ (Issue #167)。
        /// v5 の各 View は矢印キー等を自前で処理しているため、Unity の Selectable 自動
        /// ナビゲーションも入ると選択が二重に動く。ナビゲーションを切って自前処理に一本化する。
        /// また targetGraphic が無い Button には自分の Image を設定する (ホバー/押下表示)。
        /// </summary>
        public static void ApplySingleInputModel(Canvas canvas)
        {
            if (canvas == null) { return; }
            var selectables = canvas.GetComponentsInChildren<Selectable>(true);
            for (int i = 0; i < selectables.Length; i++)
            {
                var selectable = selectables[i];
                var navigation = selectable.navigation;
                navigation.mode = Navigation.Mode.None;
                selectable.navigation = navigation;
                if (selectable.targetGraphic == null)
                {
                    var image = selectable.GetComponent<Image>();
                    if (image != null) { selectable.targetGraphic = image; }
                }
            }
        }

        /// <summary>
        /// ポインタで選んだ Button の選択を解除する (Issue #167)。
        /// 解除しないと Enter キーで「押した Button」と「いま強調している行」の
        /// 両方が発火して二重実行になる。
        /// </summary>
        public static void ClearPointerSelection()
        {
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
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
            // Issue #401: 同じ Resources 画像から毎回 Sprite.Create していた。
            // 原 Texture と生成条件が同じなら同じ実体を返す (所有者は V6Sprites)。
            return FlashBeat.UI.V6.V6Sprites.FromTexture(tex, resourcePath);
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
