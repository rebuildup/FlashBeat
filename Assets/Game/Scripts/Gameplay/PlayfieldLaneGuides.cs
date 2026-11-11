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
    /// 「線として video の上に乗る」表現にしてある。
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

        /// <summary>
        /// ガイド線の太さ (ワールド x 単位)。design "Lane 0..7" (stroke 2 at
        /// 1728 wide) を 1920x1080 で約 2 px に写す値。判定線付近で 2.3 px。
        /// </summary>
        public const float GuideWidth = 0.011f;

        /// <summary>
        /// ガイド線の不透明度 (0-1)。design の #FFFFFF2E (alpha 46/255)。
        /// レーンの境界は判別できるが、明るい縁取りにはしない (Issue #380)。
        /// </summary>
        public const float GuideAlpha = 46f / 255f;

        /// <summary>
        /// ガイド線を画面下端より少し先まで伸ばす余白 (z 単位)。
        /// 視錐台の下端がレーン面を切る位置の外側まで描くので、
        /// 線は画面下端で切れずに消える。
        /// </summary>
        public const float GuideFrontMargin = 0.25f;

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

        /// <summary>
        /// 判定線より手前 (負の z) に伸ばす長さ。カメラの下端がレーン面
        /// (<see cref="GuideY"/>) を切る位置 + <see cref="GuideFrontMargin"/>。
        /// design ではレーンは画面下端 (y = 972) まで続き、判定線 (792) で
        /// 途切れない。
        /// </summary>
        public static float FrontDepth(Camera camera)
        {
            float front = camera != null
                ? MediaTimeline.VisibleFrontDepth(camera, GuideY)
                : 0f;
            // カメラが無い (テスト等) ときも判定線の下に必ず線を出す。
            if (front <= 0f) front = 0.4f;
            return front + GuideFrontMargin;
        }

        private static bool subscribed;

        private MeshRenderer guideRenderer;
        private Mesh guideMesh;
        private Material guideMaterial;
        private Camera fittedCamera;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            // "Enter Play Mode without domain reload" では static が残るため、
            // sceneLoaded の購読が二重にならないように戻す (PR #263 レビュー N4)。
            subscribed = false;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

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
            ApplyCameraFit();
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

            if (show) ApplyCameraFit();
        }

        /// <summary>
        /// 3D レーンを使うときは、カメラの水平 fit を設計アスペクト比に
        /// 固定する (Issue #233, PR #263 レビュー S1)。
        ///
        /// 縦長画面 (2560x1664 = 1.538) では GateFit = Vertical のままでは
        /// 判定線での可視半幅が 3.567 しかなく、外側レーンのノーツ
        /// (x -4.0..-3.0) の 43% が画面外になる。固定すると垂直方向の幾何
        /// (可視深度 8.6143) は変わらず、判定線 (z = 0) と 1.6 秒の
        /// アプローチ時間の不変条件は保たれる。
        /// </summary>
        private void ApplyCameraFit()
        {
            // v6 の 2D プレイフィールド (画面座標で描画する) を有効にしている
            // ときは、カメラの水平 fit をしてはいけない。映像だけが水平に
            // 圧縮され、v6 のレーンとの位置関係がずれる (PR #263 レビュー)。
            if (FlashBeat.UI.V6.V6UiMode.Playfield) return;

            if (fittedCamera == null) fittedCamera = Camera.main;
            if (fittedCamera == null) return;

            PlayfieldCameraFit.Apply(fittedCamera, PlayfieldCameraFit.ScreenAspect());
        }

        /// <summary>9 本のガイド線を 1 メッシュ・1 レンダラで生成する。</summary>
        private void Build()
        {
            var go = new GameObject("LaneGuides");
            go.transform.SetParent(transform, false);

            // 判定線 (z = 0) より手前にも伸ばし、画面下端まで続ける (Issue #380)。
            float front = -FrontDepth(fittedCamera != null ? fittedCamera : Camera.main);

            var vertices = new Vector3[GuideCount * 4];
            var triangles = new int[GuideCount * 6];
            var uv = new Vector2[GuideCount * 4];
            for (int i = 0; i < GuideCount; i++)
            {
                float x = LaneBoundaryX(i);
                float half = GuideWidth * 0.5f;
                int v = i * 4;
                vertices[v + 0] = new Vector3(x - half, GuideY, front);
                vertices[v + 1] = new Vector3(x + half, GuideY, front);
                vertices[v + 2] = new Vector3(x - half, GuideY, GuideDepth);
                vertices[v + 3] = new Vector3(x + half, GuideY, GuideDepth);

                // UV を明示する (PR #263 レビュー N3: UV が無いと texel (0,0) を
                // サンプルするので、テクスチャのあるシェーダだと 1 画素の色で
                // guide 全体が塗られる)。
                uv[v + 0] = new Vector2(0f, 0f);
                uv[v + 1] = new Vector2(1f, 0f);
                uv[v + 2] = new Vector2(0f, 1f);
                uv[v + 3] = new Vector2(1f, 1f);

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
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            guideMesh = mesh;

            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            guideRenderer = go.AddComponent<MeshRenderer>();
            guideMaterial = CreateMaterial();
            if (guideMaterial != null)
            {
                guideRenderer.sharedMaterial = guideMaterial;
            }
            guideRenderer.shadowCastingMode = ShadowCastingMode.Off;
            guideRenderer.receiveShadows = false;
        }

        /// <summary>
        /// 実行時に生成した Mesh と Material を破棄する (PR #263 レビュー S2)。
        ///
        /// GameObject はシーン依存で消えるが、Mesh / Material は native
        /// オブジェクトなので破棄しないとアプリ終了まで残り、GameScene に
        /// 入るたびに 1 個ずつリークする (リトライ / 再プレイ / 毎曲)。
        /// </summary>
        private void OnDestroy()
        {
            if (guideRenderer != null) guideRenderer.sharedMaterial = null;

            // 参照を先に外してから破棄する (破棄対象がまだ使われていると
            // Unity が破棄を拒否することがある)。
            var filter = GetComponentInChildren<MeshFilter>();
            if (filter != null) filter.sharedMesh = null;

            if (guideMesh != null)
            {
                SafeDestroy(guideMesh);
                guideMesh = null;
            }
            if (guideMaterial != null)
            {
                SafeDestroy(guideMaterial);
                guideMaterial = null;
            }
        }

        static void SafeDestroy(Object target)
        {
            if (target == null) return;
            // EditMode (テスト) では Destroy が使えない。
            if (Application.isPlaying) Object.Destroy(target);
            else Object.DestroyImmediate(target);
        }

        /// <summary>
        /// 透明な無光 (unlit) マテリアルを生成する。
        /// シーンの FlatUnlit (LaneLight.mat と同じシェーダ) を使い、
        /// 動画の上に乗る半透明の線にする。
        ///
        /// シェーダが見つからない場合は例外を投げずに null を返す
        /// (PR #263 レビュー S3: `new Material(null)` は
        /// ArgumentNullException を投げる)。
        /// </summary>
        private static Material CreateMaterial()
        {
            Shader shader = Shader.Find("FlashBeat/FlatUnlit");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                Debug.LogWarning("[PlayfieldLaneGuides] no unlit shader found - the lane guides stay invisible.");
                return null;
            }

            var material = new Material(shader) { name = "LaneGuides (runtime)" };
            // fallback シェーダには _Color が無い (HasProperty で保護する)。
            if (material.HasProperty("_Color")) material.SetColor("_Color", new Color(1f, 1f, 1f, GuideAlpha));
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
