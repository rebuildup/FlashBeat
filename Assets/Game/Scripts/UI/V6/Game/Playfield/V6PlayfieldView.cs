using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FlashBeat.UI.V6.Game.Playfield
{
    /// <summary>
    /// v6 プレイフィールドの uGUI 描画 (Issue #219)。
    ///
    /// design ノード <c>nrfw8</c> の部品をコードから構築する (v5 と同じ方式、Prefab は触らない):
    /// - レーン分離線 8 本 (中心線。stroke #FFFFFF2E / 2設計px、SVG パスが線幅固定なので
    ///   線幅も深度で変えない — 上ほど幅が狭くなるのは線の傾きが担う)
    /// - 判定ライン (y=792 h=4 #EDEDED、画面全幅) + 判定グロウ (y=796 h=2 #FFFFFF4D)
    /// - ノーツを載せる <see cref="NoteLayer"/> (<see cref="Notes"/> が Image を親にする)
    ///
    /// 動画背景の上に重なる Screen Space Overlay 前提。動画側の減光 (Video Dim) は
    /// HUD 側の責務なのでここでは描かない。
    /// </summary>
    public sealed class V6PlayfieldView : MonoBehaviour
    {
        /// <summary>現在のビュー (GameScene で 1 つ)。無い場合は <see cref="Notes"/> が 3D 描画にフォールバックする。</summary>
        public static V6PlayfieldView Current { get; private set; }

        private const float SeparatorStrokeDesignPx = 2f;
        private const float JudgeLineHeightDesignPx = 4f;
        private const float JudgeGlowHeightDesignPx = 2f;

        private readonly List<Image> separatorLines = new List<Image>();
        private readonly List<Image> laneFlashes = new List<Image>();
        private Image judgeLine;
        private Image judgeGlow;
        private Vector2 lastSize;

        /// <summary>プレイフィールド全体のルート (Canvas いっぱいに伸びる)。</summary>
        public RectTransform Root { get; private set; }

        /// <summary>ノーツ Image の親。兄弟順で最後 (線・判定線より手前)。</summary>
        public RectTransform NoteLayer { get; private set; }

        /// <summary>現在の 設計px → px 倍率 (高さ合わせ)。</summary>
        public float PixelsPerDesignPixel
        {
            get { return V6LaneGeometry.ScreenScale(Root.rect.size); }
        }

        /// <summary>
        /// 親 (通常は Canvas の RectTransform) の下に v6 プレイフィールドを構築する。
        /// 二重生成防止のため <see cref="Current"/> を設定する。
        /// </summary>
        public static V6PlayfieldView Create(RectTransform parent)
        {
            var go = new GameObject("V6PlayfieldRoot", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var view = go.AddComponent<V6PlayfieldView>();
            view.Root = rt;   // Build/ApplyLayout が Root を使うので先に設定する
            view.Build();
            Current = view;
            return view;
        }

        /// <summary>プレイフィールドの表示/非表示 (ノーツ Image も一緒に隠れる)。</summary>
        public void SetVisible(bool visible)
        {
            Root.gameObject.SetActive(visible);
        }

        /// <summary>設計座標 (y 下向き) → ルート基準のローカル座標 (y 上向き、中心原点)。</summary>
        public Vector2 DesignToLocal(Vector2 design)
        {
            Vector2 size = Root.rect.size;
            float s = V6LaneGeometry.ScreenScale(size);
            Vector2 screen = V6LaneGeometry.DesignToScreen(design, size);
            return new Vector2(screen.x - size.x * 0.5f, size.y * 0.5f - screen.y);
        }

        private void Build()
        {
            // レーンフラッシュ 8 本 (旧 LaneFlash と同等: 押している間 alpha 0.3、
            // 離すと 3/s で減衰)。分離線より奥 (兄弟順で先) に置く。
            // 旧 3D の LaneLight は V6GameRuntime が隠すので、入力フィードバックは
            // ここが担う (Issue #233: レーンフラッシュが消えた)。
            for (int lane = 0; lane < V6LaneGeometry.LaneCount; lane++)
            {
                var flashGo = new GameObject("LaneFlash " + lane, typeof(RectTransform));
                var flash = flashGo.AddComponent<Image>();
                flash.color = new Color(1f, 1f, 1f, 0f);
                flash.raycastTarget = false;
                flash.rectTransform.SetParent(Root, false);
                flash.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                flash.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                flash.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                laneFlashes.Add(flash);
            }

            // レーン分離線 8 本 (回転した細長 Image = 線。スプライト無しの Image は白矩形)
            for (int lane = 0; lane < V6LaneGeometry.LaneCount; lane++)
            {
                var lineGo = new GameObject("LaneSeparator " + lane, typeof(RectTransform));
                var img = lineGo.AddComponent<Image>();
                img.color = V6LaneGeometry.SeparatorColor;
                img.raycastTarget = false;
                img.rectTransform.SetParent(Root, false);
                img.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                img.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                img.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                separatorLines.Add(img);
            }

            judgeGlow = MakeBar("JudgeGlow", V6LaneGeometry.JudgeGlowColor);
            judgeLine = MakeBar("JudgeLine", V6LaneGeometry.JudgeLineColor);

            var layerGo = new GameObject("Notes", typeof(RectTransform));
            NoteLayer = layerGo.GetComponent<RectTransform>();
            NoteLayer.SetParent(Root, false);
            NoteLayer.anchorMin = Vector2.zero;
            NoteLayer.anchorMax = Vector2.one;
            NoteLayer.offsetMin = Vector2.zero;
            NoteLayer.offsetMax = Vector2.zero;

            ApplyLayout();
        }

        /// <summary>判定線/グロウは画面全幅 (design では x=0 w=1728 = フレーム全幅)。</summary>
        private Image MakeBar(string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.SetParent(Root, false);
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            return img;
        }

        private void Update()
        {
            // CanvasConfigurator が解像度変更で Canvas を再適用するので、
            // サイズ変化を検知して幾何を張り直す。
            Vector2 size = Root.rect.size;
            if (size != lastSize)
            {
                ApplyLayout();
            }

            // レーンフラッシュ (旧 LaneFlash と同一の挙動: 押下で alpha 0.3、3/s で減衰)。
            for (int lane = 0; lane < laneFlashes.Count; lane++)
            {
                var img = laneFlashes[lane];
                float alpha = img.color.a;
                if (KeyBindings.IsLaneKeyDown(lane))
                {
                    alpha = 0.3f;
                }
                else if (alpha > 0f)
                {
                    alpha = Mathf.Max(0f, alpha - 3f * Time.deltaTime);
                }
                if (img.color.a != alpha)
                {
                    img.color = new Color(1f, 1f, 1f, alpha);
                }
            }
        }

        private void ApplyLayout()
        {
            Vector2 size = Root.rect.size;
            lastSize = size;
            float px = V6LaneGeometry.ScreenScale(size);

            for (int lane = 0; lane < separatorLines.Count; lane++)
            {
                V6LaneGeometry.TryLanePoint(lane, 0f, out var bottom, out _);
                V6LaneGeometry.TryLanePoint(lane, 1f, out var top, out _);
                var a = DesignToLocal(bottom);
                var b = DesignToLocal(top);
                var dir = b - a;

                var rt = separatorLines[lane].rectTransform;
                rt.anchoredPosition = (a + b) * 0.5f;
                rt.sizeDelta = new Vector2(dir.magnitude, SeparatorStrokeDesignPx * px);
                rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            }

            // レーンフラッシュ: レーン中心線に沿った帯 (幅はレーン幅の実測)。
            // 旧 LaneFlash と同じ減衰 (押下 0.3 → 3/s) は Update で扱う。
            for (int lane = 0; lane < laneFlashes.Count; lane++)
            {
                V6LaneGeometry.TryLanePoint(lane, 0f, out var fBottom, out var fBottomScale);
                V6LaneGeometry.TryLanePoint(lane, 1f, out var fTop, out var fTopScale);
                var fa = DesignToLocal(fBottom);
                var fb = DesignToLocal(fTop);
                var fdir = fb - fa;

                var frt = laneFlashes[lane].rectTransform;
                frt.anchoredPosition = (fa + fb) * 0.5f;
                frt.sizeDelta = new Vector2(
                    V6LaneGeometry.LaneCentreBottomStep * (fBottomScale + fTopScale) * 0.5f * px,
                    fdir.magnitude);
                frt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(fdir.y, fdir.x) * Mathf.Rad2Deg);
            }

            // 判定ライン y=792..796、グロウ y=796..798 (設計座標、y 下向き)
            var lineRt = judgeLine.rectTransform;
            lineRt.offsetMin = new Vector2(0f, size.y * 0.5f - (V6LaneGeometry.JudgeLineY + JudgeLineHeightDesignPx) * px);
            lineRt.offsetMax = new Vector2(0f, size.y * 0.5f - V6LaneGeometry.JudgeLineY * px);
            var glowRt = judgeGlow.rectTransform;
            glowRt.offsetMin = new Vector2(0f, size.y * 0.5f - (V6LaneGeometry.JudgeLineY + JudgeLineHeightDesignPx + JudgeGlowHeightDesignPx) * px);
            glowRt.offsetMax = new Vector2(0f, size.y * 0.5f - (V6LaneGeometry.JudgeLineY + JudgeLineHeightDesignPx) * px);
        }

        private void OnDestroy()
        {
            if (Current == this)
            {
                Current = null;
            }
        }
    }
}
