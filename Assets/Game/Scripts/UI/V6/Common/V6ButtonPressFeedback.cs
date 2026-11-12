using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FlashBeat.UI.V6.Common
{
    /// <summary>
    /// ボタンを押したときに、ボタンの輪郭が外へ広がって消える控えめな効果
    /// (2026-11-12 オーナー指摘: 押した手応え)。
    ///
    /// 設計の意図:
    ///  - **地味**であること。色を反転させたり、ボタン自体を拡大したりしない。
    ///  - ボタンの**後ろ**で輪郭が少し外へ広がりながら透明になる (120ms)。
    ///  - レイアウトは動かさない (押した瞬間に文字がずれない)。
    ///
    /// 実装は、ボタンの最初の子として 4 辺の細い帯 (輪郭) を置き、押下時に
    /// その scale と alpha を時間で進めるだけ。スプライトを要求しないので
    /// どのボタンにも同じように付けられる。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class V6ButtonPressFeedback : MonoBehaviour, IPointerDownHandler
    {
        /// <summary>広がりきるまでの時間 (秒)。</summary>
        public const float ExpandSeconds = 0.12f;

        /// <summary>広がりきったときの倍率 (1.0 = ボタンと同じ大きさ)。</summary>
        public const float ExpandScale = 1.12f;

        /// <summary>押した瞬間の枠の不透明度。</summary>
        public const float StartAlpha = 0.28f;

        /// <summary>輪郭の線幅 (px)。</summary>
        private const float EdgeThickness = 2f;

        private RectTransform ring;
        private readonly Image[] edges = new Image[4];
        private float startTime = -1f;

        /// <summary>
        /// ボタンに効果を取り付ける。押下の検出は自分で行う (Button.onClick とは独立)。
        /// 既に付いている場合は何もしない。
        /// </summary>
        public static void Attach(GameObject button)
        {
            if (button == null)
            {
                return;
            }

            if (button.GetComponent<V6ButtonPressFeedback>() != null)
            {
                return;
            }

            button.AddComponent<V6ButtonPressFeedback>();
        }

        private void Awake()
        {
            BuildRing();
        }

        private void BuildRing()
        {
            var go = new GameObject("Press Ring", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            // ボタンの背景より後ろに置く (輪郭だけが外へはみ出して見える)。
            go.transform.SetSiblingIndex(0);

            ring = (RectTransform)go.transform;
            ring.anchorMin = Vector2.zero;
            ring.anchorMax = Vector2.one;
            ring.offsetMin = Vector2.zero;
            ring.offsetMax = Vector2.zero;
            ring.pivot = new Vector2(0.5f, 0.5f);

            // 上 / 下 / 左 / 右 の 4 辺。中央は空ける (ボタンの中身を覆わない)。
            edges[0] = AddEdge("Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, EdgeThickness));
            edges[1] = AddEdge("Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, EdgeThickness));
            edges[2] = AddEdge("Left", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(EdgeThickness, 0f));
            edges[3] = AddEdge("Right", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(EdgeThickness, 0f));

            go.SetActive(false);
        }

        private Image AddEdge(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(ring, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = sizeDelta;

            var image = go.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0f);
            image.raycastTarget = false;
            return image;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (ring == null)
            {
                return;
            }

            startTime = Time.unscaledTime;
            ring.gameObject.SetActive(true);
            Apply(0f);
        }

        private void Update()
        {
            if (startTime < 0f || ring == null)
            {
                return;
            }

            float elapsed = Time.unscaledTime - startTime;
            if (elapsed >= ExpandSeconds)
            {
                startTime = -1f;
                ring.gameObject.SetActive(false);
                return;
            }

            Apply(elapsed / ExpandSeconds);
        }

        /// <summary>進行度 (0..1) を枠の拡大と不透明度へ反映する。</summary>
        private void Apply(float t)
        {
            float eased = EaseOut(t);
            float scale = Mathf.Lerp(1f, ExpandScale, eased);
            ring.localScale = new Vector3(scale, scale, 1f);

            // 押した瞬間が最も濃く、広がりながら消える。
            float alpha = Mathf.Lerp(StartAlpha, 0f, eased);
            for (int i = 0; i < edges.Length; i++)
            {
                if (edges[i] == null)
                {
                    continue;
                }

                var c = edges[i].color;
                c.a = alpha;
                edges[i].color = c;
            }
        }

        private static float EaseOut(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - (1f - t) * (1f - t);
        }

        // --- テスト用の観測点 ---

        /// <summary>いま枠を出しているか。</summary>
        public bool RingVisibleForAudit
        {
            get { return ring != null && ring.gameObject.activeSelf; }
        }

        /// <summary>枠の現在の倍率。</summary>
        public float RingScaleForAudit
        {
            get { return ring != null ? ring.localScale.x : -1f; }
        }

        /// <summary>枠の現在の不透明度。</summary>
        public float RingAlphaForAudit
        {
            get { return edges[0] != null ? edges[0].color.a : -1f; }
        }
    }
}
