using UnityEngine;
using UnityEngine.UI;

namespace FlashBeat.UI.V5
{
    /// <summary>
    /// v5 共有デコ (Issue #120)。Title/Opening などで使う背景・エッジ装飾・ロゴを
    /// Assets/Game/Resources/V5/Common/ の画像から構築する。
    /// 他画面もこのクラス (または直接 Resources.Load("V5/Common/...")) を使ってよい。
    /// エッジ装飾は「高さに連動した幅」で左右端に固定するので、どの解像度・
    /// アスペクト比でも元画像の比率が崩れない (design 01 Title の Edge Deco 実測:
    /// 1920x1080 で 380x1080, stretch)。
    /// </summary>
    public static class V5Decor
    {
        /// <summary>Resources 直下の共通デコのパス接頭辞。</summary>
        public const string ResourcePrefix = "V5/Common/";

        /// <summary>edge-left/right.png の設計比率 (幅/高さ, 380x1080)。</summary>
        public const float EdgeAspect = 380f / 1080f;

        /// <summary>Resources から Texture2D を読み、pivot 中央の Sprite を作る (毎回新規)。</summary>
        public static Sprite LoadSprite(string name)
        {
            var tex = Resources.Load<Texture2D>(ResourcePrefix + name);
            if (tex == null)
            {
                Debug.LogWarning("[V5Decor] texture not found: " + ResourcePrefix + name);
                return null;
            }

            var sprite = Sprite.Create(
                tex,
                new Rect(0f, 0f, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect);
            sprite.name = "V5Decor " + name;
            return sprite;
        }

        /// <summary>画面全体を塗りつぶす不透明背景 (V5Tokens.Bg)。レガシー UI を覆い隠す。</summary>
        public static GameObject BuildBackdrop(GameObject parent)
        {
            var go = V5Ui.Node(parent, "Backdrop");
            V5Ui.Fill(go.GetComponent<RectTransform>(), 0f);
            var img = go.AddComponent<Image>();
            img.color = V5Tokens.Bg;
            img.raycastTarget = false;
            return go;
        }

        /// <summary>左右のエッジ装飾 (01 Title 実測: 380x1080 stretch)。</summary>
        public static void BuildEdgeDeco(GameObject parent)
        {
            BuildEdge(parent, "Edge Deco Left", "edge-left", false);
            BuildEdge(parent, "Edge Deco Right", "edge-right", true);
        }

        private static void BuildEdge(GameObject parent, string name, string asset, bool right)
        {
            var go = V5Ui.Node(parent, name);
            var rt = go.GetComponent<RectTransform>();
            // 高さに連動した幅: 親の高さに対し 380/1080 の比率。歪まない。
            rt.anchorMin = new Vector2(right ? 1f : 0f, 0f);
            rt.anchorMax = new Vector2(right ? 1f : 0f, 1f);
            rt.pivot = new Vector2(right ? 1f : 0f, 0.5f);
            var img = go.AddComponent<Image>();
            img.raycastTarget = false;
            var sprite = LoadSprite(asset);
            if (sprite != null)
            {
                img.sprite = sprite;
            }

            go.AddComponent<EdgeSizer>().Init(EdgeAspect);
        }

        /// <summary>FlashBeat ロゴ (13.png, 5007x990) を contain で置く。
        /// 01 Title 実測: (280, 108.5) に 1280x253。</summary>
        public static Image BuildLogo(GameObject parent, Vector2 anchoredPos, Vector2 size)
        {
            var go = V5Ui.Node(parent, "Logo");
            V5Ui.Place(go.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 0f),
                anchoredPos, size);
            var img = go.AddComponent<Image>();
            img.raycastTarget = false;
            var sprite = LoadSprite("13");
            if (sprite != null)
            {
                img.sprite = sprite;
                img.preserveAspect = true;
            }

            return img;
        }

        /// <summary>ロゴの設計アスペクト (5007x990)。</summary>
        public const float LogoAspect = 5007f / 990f;

        /// <summary>
        /// KeyCap 凡例行 (01 Title / 06 Option の Legend)。anchoredPos はスケール済みのこと。
        /// items は (KeyCap ラベル, KeyCap 幅 [スケール済み], 後ろに置くラベル or null)。
        /// </summary>
        public static void BuildLegend(GameObject parent, Vector2 anchoredPos,
            (string key, float width, string label)[] items)
        {
            var legend = V5Ui.Node(parent, "Legend");
            V5Ui.Place(legend.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 0f),
                anchoredPos, new Vector2(1920f, 36f));

            float cursor = 0f;
            var p = UISizeProfile.Current;
            foreach (var item in items)
            {
                var cap = V5Ui.KeyCap(legend, item.key, new Vector2(item.width, p.FsM * 1.2f));
                var capRt = cap.GetComponent<RectTransform>();
                capRt.anchorMin = new Vector2(0f, 0.5f);
                capRt.anchorMax = new Vector2(0f, 0.5f);
                capRt.pivot = new Vector2(0f, 0.5f);
                capRt.anchoredPosition = new Vector2(cursor, 0f);
                cursor += item.width + p.GapS;

                if (item.label == null)
                {
                    continue;
                }

                var labelTmp = V5Ui.Text(legend, "Legend " + item.label, item.label, S(22), V5Tokens.TextMuted);
                var labelRt = labelTmp.rectTransform;
                labelRt.anchorMin = new Vector2(0f, 0.5f);
                labelRt.anchorMax = new Vector2(0f, 0.5f);
                labelRt.pivot = new Vector2(0f, 0.5f);
                labelRt.sizeDelta = new Vector2(labelTmp.preferredWidth + 4f, p.FsM * 1.4f);
                labelRt.anchoredPosition = new Vector2(cursor, 0f);
                cursor += labelRt.sizeDelta.x + p.GapM;
            }
        }

        /// <summary>fhd 実測 px をプロファイル倍率に合わせる (各 View の S() と同じ式)。</summary>
        public static float S(float fhdPx)
        {
            return fhdPx * (UISizeProfile.Current.FsM / 30f);
        }
    }

    /// <summary>
    /// 親の高さ (または幅) から自分の幅を毎フレーム再計算する最小ヘルパ。
    /// 解像度変更 (CanvasConfigurator が検知) 後もエッジ装飾の比率を保つ。
    /// </summary>
    public sealed class EdgeSizer : MonoBehaviour
    {
        private float aspect;
        private RectTransform rect;

        /// <summary>aspect = 幅/高さ の設計比率。</summary>
        public void Init(float aspectRatio)
        {
            aspect = aspectRatio;
            rect = GetComponent<RectTransform>();
            Apply();
        }

        private void Update()
        {
            Apply();
        }

        private void Apply()
        {
            if (rect == null || aspect <= 0f)
            {
                return;
            }

            var parent = rect.parent as RectTransform;
            if (parent == null)
            {
                return;
            }

            float h = parent.rect.height;
            if (h <= 0f)
            {
                return;
            }

            float w = h * aspect;
            // anchor が左右端のどちらでも sizeDelta.x に実幅を入れ、offsetMin/Max を張り直す。
            if (Mathf.Abs(rect.sizeDelta.x - w) > 0.5f)
            {
                rect.sizeDelta = new Vector2(w, 0f);
            }
        }
    }
}
