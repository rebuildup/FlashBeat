using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

namespace FlashBeat.Gameplay
{
    /// <summary>
    /// 3D プレイフィールドの「レーン構造」を実行時に復元する (Issue #233)。
    ///
    /// GameScene には 3D レーンの部品があるのに、**見えるものが判定線だけ**だった。
    ///
    ///   - "Lane" (8 x 1 x 40 の板, layer 8) … <c>m_IsActive: 0</c>。
    ///     暗色の半透明マテリアル (Lane.mat) なので、有効化すると動画の下半分を覆う。
    ///     そのまま有効化する方法は使えない。
    ///   - "LaneLight1..8" (幅 1 のレーン帯) … LaneFlash が alpha 0 のままに
    ///      randomization していないので、キー入力時だけ一瞬光るだけ。
    ///     待機中のレーン構造としては何も描かない。
    ///   - "JudgeLine" … 判定線。これは見えていた。
    ///
    /// そこで **シーンアセット (GameScene.unity) を触らず**、実行時に
    /// レーン境界のガイド線 (8 レーン = 9 本) を生成してレーン構造を出す。
    /// 設計上は v6 遠近法プレイフィールドのレーン線 (白 #FFFFFF2E) と同じ
    /// 「线的で.video の上に乗る」表現にしてある。
    ///
    /// 起動は <see cref="V6PlayfieldRuntime"/> と同じ
    /// RuntimeInitializeOnLoad + sceneLoaded の方式 (シーンにコンポーネントを
    /// 足す必要がない)。
    /// </summary>
    public sealed class PlayfieldLaneGuides : MonoBehaviour
    {
        private const string GameSceneName = "GameScene";

        /// <summary>ガイド線の本数 (8 レーン = 9 本の境界線)。</summary>
        public const int GuideCount = Judge.LaneCount + 1;

        /// <summary>ガイド線の太さ (ワールド x 単位)。判定線付近で 8 px 程度。</summary>
        public const float GuideWidth = 0.04f;

        /// <summary>ガイド線の不透明度 (0-1)。</summary>
        public const float GuideAlpha = 0.35f;

        /// <summary>
        /// ガイド線のある y。ノーツ面 (0.55) より手前 (下) なので、
        /// ノーツに隠れない。LaneLight の帯 (0.475-0.525) の上、
        /// 判定線の上 (0.5) のあたりに置く。
        /// </summary>
        public const float GuideY = 0.53f;

        /// <summary>
        /// ガイド線の z 範囲 (判定線 z = 0 から)。
        /// カメラ可視範囲 (8.61) より少し先まで伸ばすのは、
        /// 先端が画面で切れて線が消えるのを避けるため
        /// (視錐台が clips する)。
        /// </summary>
        public const float GuideDepth = MediaTimeline.VisibleDepthZ + 0.2f;

        private static bool subscribed;

        private MeshRenderer guideRenderer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!subscribed)
            {
                subscribed = true;
                SceneManager.sceneLoaded += OnSceneLoaded;
            }
            if (SceneManager.GetActiveScene().name == GameSceneName)
            {
                Ensure();
            }
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == GameSceneName)
            {
                Ensure();
            }
        }

        private static void Ensure()
        {
            if (FindFirstObjectByType<PlayfieldLaneGuides>() != null) return;

            var go = new GameObject("PlayfieldLaneGuides");
            go.AddComponent<PlayfieldLaneGuides>();
        }

        /// <summary>レーン境界 index の x (0 = 左端, 8 = 右端)。</summary>
        public static float LaneBoundaryX(int index)
        {
            return Judge.LaneFirstCenterX - Judge.LaneSpacing * 0.5f + index * Judge.LaneSpacing;
        }

        private void Start()
        {
            Build();
        }

        private void Update()
        {
            // v6 の遠近法プレイフィールドが有効なときは 3D のガイドを出さない
            // (v6 側のレーン線と二重になる)。
            bool show = !FlashBeat.UI.V6.V6UiMode.Playfield;
            if (guideRenderer != null && guideRenderer.enabled != show)
            {
                guideRenderer.enabled = show;
            }
        }

        /// <summary>9 本のガイド線を 1 メッシュ・1 レンダラで生成する。</summary>
        private void Build()
        {
            var go = new GameObject("LaneGuides");
            go.transform.SetParent(transform, false);

            var vertices = new Vector3[GuideCount * 4];
            var triangles = new int[GuideCount * 6];
            for (int i = 0; i < GuideCount; i++)
            {
                float x = LaneBoundaryX(i);
                float half = GuideWidth * 0.5f;
                int v = i * 4;
                vertices[v + 0] = new Vector3(x - half, GuideY, 0f);
                vertices[v + 1] = new Vector3(x + half, GuideY, 0f);
                vertices[v + 2] = new Vector3(x - half, GuideY, GuideDepth);
                vertices[v + 3] = new Vector3(x + half, GuideY, GuideDepth);

                int t = i * 6;
                triangles[t + 0] = v + 0;
                triangles[t + 1] = v + 2;
                triangles[t + 2] = v + 1;
                triangles[t + 3] = v + 1;
                triangles[t + 4] = v + 2;
                triangles[t + 5] = v + 3;
            }

            var mesh = new Mesh { name = "LaneGuides" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();

            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            guideRenderer = go.AddComponent<MeshRenderer>();
            guideRenderer.sharedMaterial = CreateMaterial();
            guideRenderer.shadowCastingMode = ShadowCastingMode.Off;
            guideRenderer.receiveShadows = false;
        }

        /// <summary>
        /// 透明な無光 (unlit) マテリアルを生成する。
        /// シーンの FlatUnlit (LaneLight.mat と同じシェーダ) を使い、
        /// 動画の上に乗る半透明の線にする。
        /// </summary>
        private static Material CreateMaterial()
        {
            Shader shader = Shader.Find("FlashBeat/FlatUnlit");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            var material = new Material(shader) { name = "LaneGuides (runtime)" };
            material.SetColor("_Color", new Color(1f, 1f, 1f, GuideAlpha));
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            // 裏側からも見えるようにする (頂点の巻き順に依存しない)。
            if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)CullMode.Off);
            material.renderQueue = (int)RenderQueue.Transparent;
            return material;
        }
    }
}
