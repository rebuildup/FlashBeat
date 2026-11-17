using System.Collections.Generic;
using FlashBeat.Gameplay;
using UnityEngine;

namespace FlashBeat.UI.V6.Option
{
    /// <summary>
    /// Option 画面 Game Preview (Issue #579) の 3D シーンを保持するリグ。
    ///
    /// 静止した 2D 図形の代わりに、本編 (GameScene) と同じ幾何で
    /// レーンガイド・判定線・ノーツを描き、専用カメラの
    /// <see cref="RenderTexture"/> を uGUI の RawImage に流す。
    ///
    /// 副作用を持たない独立セッションとして作る:
    ///
    /// - rig 全体を本編カメラの far clip (1000) より十分遠く
    ///   (z = -<see cref="RigDistance"/>) に置くので、Option 画面の
    ///   Main Camera には決して映らない。プレビュー用カメラの far も
    ///   100 に絞り、シーン側のオブジェクトは視界に入らない。
    /// - ノーツは <c>Assets/Game/Resources/Prefabs/Note.prefab</c> の
    ///   実体を使うが、<see cref="Notes"/> コンポーネントは無効化する。
    ///   位置は毎フレームこのクラスが
    ///   <see cref="MediaTimeline.NotePositionZ"/> と同じ式
    ///   ((noteTime - mediaTime) * zScale、zScale は
    ///   <see cref="MediaTimeline.CurrentZScale"/>) で書き込むので、
    ///   <c>GManager.step_time</c>・スコア・セッション状態には触れない。
    /// - 疑似メディア時刻は <see cref="V6OptionPreviewChart.LoopSeconds"/>
    ///   秒周期のループ時計 (<see cref="Time.time"/>)。NoteTiming は
    ///   <see cref="NotesManager.NoteTimeWithTiming"/>、NoteSpeed は
    ///   <see cref="MediaTimeline.CurrentZScale"/> 経由で
    ///   オプション変更が即座に反映される。
    /// </summary>
    internal sealed class V6OptionPreviewRig
    {
        /// <summary>rig を置く z (本編カメラの far clip 1000 より遠い)。</summary>
        private const float RigDistance = 3000f;

        /// <summary>RenderTexture の解像度 (well 464x280 の約 2 倍)。</summary>
        private const int TextureWidth = 928;
        private const int TextureHeight = 560;

        /// <summary>プレビューカメラの far clip (rig 内だけが収まればよい)。</summary>
        private const float CameraFarClip = 100f;

        /// <summary>
        /// ノーツを表示し続ける z の余白 (ガイドの奥行き・手前端の外側)。
        /// フラスタムカリングに任せてもよいが、SetActive の往復を減らすため
        /// 余裕を持たせて範囲外は非表示にする。
        /// </summary>
        private const float ShowMargin = 0.5f;

        // GameScene / OptionScene の Main Camera と同じ姿勢
        // (位置 (0, 4.9, -0.8)、pitch 54.8°、FOV 60)。
        private static readonly Vector3 CameraPosition = new Vector3(0f, 4.9f, -0.8f);
        private static readonly Quaternion CameraRotation = new Quaternion(0.46019977f, 0f, 0f, 0.8878155f);
        private const float CameraFov = 60f;

        // flat rendering (docs/architecture/flat-rendering.md): the playfield
        // is unlit, so the preview deliberately has NO light. Every material
        // used here (Note.mat, the runtime guide material) is FlashBeat/FlatUnlit
        // and ignores lighting anyway; a Directional Light would be dead weight
        // and could silently relight anything lit added later.

        private readonly List<PreviewNote> notes = new List<PreviewNote>();
        private readonly float frontZ;

        private GameObject root;
        private Camera previewCamera;
        private RenderTexture texture;
        private Mesh guideMesh;
        private Mesh judgeMesh;
        private Material guideMaterial;
        private Material judgeMaterial;

        /// <summary>RawImage に貼る描画結果。</summary>
        public RenderTexture Texture
        {
            get { return texture; }
        }

        private sealed class PreviewNote
        {
            public GameObject Go;
            public int Lane;
            public float Time;
            public float HoldSeconds;
            public float BaseScaleZ;
        }

        public V6OptionPreviewRig()
        {
            texture = new RenderTexture(TextureWidth, TextureHeight, 24);

            // まず原点に組み立てる。FrontDepth / VisibleFrontDepth 系の
            // ヘルパーは「判定線がワールド z = 0」を前提にカメラの
            // ワールド座標から手前端を求めるので、遠くへ置いた状態では
            // 値が壊れる。組み立て後に rig ごと平行移動すれば、
            // カメラと判定線の相対幾何は本編と完全に同じまま保てる。
            root = new GameObject("V6 Option Preview Rig");

            BuildCamera();
            frontZ = -PlayfieldLaneGuides.FrontDepth(previewCamera);
            BuildGuides();
            BuildJudgeLine();
            BuildNotes();

            // 本編カメラからは far clip の外。回転は掛けない
            // (rig 内のローカル座標 = 本編のワールド座標として扱う)。
            root.transform.position = new Vector3(0f, 0f, -RigDistance);
        }

        private void BuildCamera()
        {
            var go = new GameObject("Preview Camera");
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = CameraPosition;
            go.transform.localRotation = CameraRotation;

            previewCamera = go.AddComponent<Camera>();
            previewCamera.fieldOfView = CameraFov;
            previewCamera.nearClipPlane = 0.3f;
            previewCamera.farClipPlane = CameraFarClip;
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            // 透明クリア: well ($k0) の下地色をそのまま見せる。
            previewCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            previewCamera.targetTexture = texture;

            // 本編と同じ水平 fit (PlayfieldCameraFit) をプレビューの
            // アスペクトに適用する。well は 464x280 ≒ 1.66 で 16:9 より
            // 縦長なので、DesignAspect (16:9) にピンされ本編の
            // 縦長画面と同じ横圧縮になる。
            PlayfieldCameraFit.Apply(previewCamera, (float)TextureWidth / TextureHeight);
        }

        /// <summary>本編と同じガイドメッシュ (PlayfieldLaneGuides.BuildGuideMesh)。</summary>
        private void BuildGuides()
        {
            var go = new GameObject("Lane Guides");
            go.transform.SetParent(root.transform, false);

            guideMesh = PlayfieldLaneGuides.BuildGuideMesh(frontZ);
            go.AddComponent<MeshFilter>().sharedMesh = guideMesh;

            var renderer = go.AddComponent<MeshRenderer>();
            guideMaterial = PlayfieldLaneGuides.CreateGuideMaterial();
            if (guideMaterial != null)
            {
                renderer.sharedMaterial = guideMaterial;
            }
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        /// <summary>
        /// 判定線 (z = 0) の帯。GameScene の JudgeLine (LineRenderer、
        /// (-4, 0.5, 0) → (4, 0.5, 0)) と同じ位置・幅感の平面 quad。
        /// マテリアルはガイドと同じ unlit を、もう少し濃くして使う。
        /// </summary>
        private void BuildJudgeLine()
        {
            float left = PlayfieldLaneGuides.LaneBoundaryX(0);
            float right = PlayfieldLaneGuides.LaneBoundaryX(PlayfieldLaneGuides.GuideCount - 1);
            const float y = 0.5f;
            const float halfDepth = 0.03f;

            var vertices = new[]
            {
                new Vector3(left, y, -halfDepth),
                new Vector3(right, y, -halfDepth),
                new Vector3(left, y, halfDepth),
                new Vector3(right, y, halfDepth),
            };
            var triangles = new[] { 0, 2, 1, 1, 2, 3 };
            var uv = new[]
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0f, 1f), new Vector2(1f, 1f),
            };

            judgeMesh = new Mesh { name = "Preview Judge Line" };
            judgeMesh.vertices = vertices;
            judgeMesh.uv = uv;
            judgeMesh.triangles = triangles;
            judgeMesh.RecalculateBounds();

            var go = new GameObject("Judge Line");
            go.transform.SetParent(root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = judgeMesh;

            var renderer = go.AddComponent<MeshRenderer>();
            judgeMaterial = PlayfieldLaneGuides.CreateGuideMaterial();
            if (judgeMaterial != null)
            {
                // ガイド線 (alpha 0.18) より目立たせる。_Color が無い
                // fallback シェーダでは何もしない (HasProperty 保護は
                // CreateGuideMaterial と同じ考え方)。
                if (judgeMaterial.HasProperty("_Color"))
                {
                    judgeMaterial.SetColor("_Color", new Color(1f, 1f, 1f, 0.55f));
                }
                renderer.sharedMaterial = judgeMaterial;
            }
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        /// <summary>
        /// 疑似チャート全ノーツ分の Note.prefab 実体をプールとして生成する。
        /// <see cref="Notes"/> は無効化し、移動は <see cref="Tick"/> が担う
        /// (本編の step_time / SessionZScale 経路を通らない)。
        /// </summary>
        private void BuildNotes()
        {
            GameObject prefab = Resources.Load<GameObject>("Prefabs/Note");
            if (prefab == null)
            {
                Debug.LogWarning("[V6OptionPreviewRig] Resources/Prefabs/Note が見つからない - ノーツ無しでガイド線だけ描く。");
                return;
            }

            V6OptionPreviewChart.Note[] chart = V6OptionPreviewChart.Build();
            for (int i = 0; i < chart.Length; i++)
            {
                var go = Object.Instantiate(prefab, root.transform, false);
                go.name = "Preview Note";

                // 本編の移動・v6 接続ロジックを止める (Start/Update を走らせない)。
                var notesComponent = go.GetComponent<Notes>();
                if (notesComponent != null)
                {
                    notesComponent.enabled = false;
                }

                // 初回 Tick まで画面に出さない (prefab 既定位置 (0.5,0.5,6.04)
                // のまま 1 フレーム見えるのを防ぐ)。
                go.SetActive(false);

                notes.Add(new PreviewNote
                {
                    Go = go,
                    Lane = chart[i].Lane,
                    Time = chart[i].Time,
                    HoldSeconds = chart[i].HoldSeconds,
                    BaseScaleZ = go.transform.localScale.z,
                });
            }
        }

        /// <summary>
        /// 疑似メディア時刻を進めてノーツ位置を更新する。
        /// V6OptionViewDriver の Update (= V6OptionView.Tick) から毎フレーム呼ぶ。
        /// NoteSpeed / NoteTiming の変更は GManager の値をそのまま読むので
        /// 次のフレームに反映される (Rebuild 不要)。
        /// </summary>
        public void Tick()
        {
            if (previewCamera == null || notes.Count == 0)
            {
                return;
            }

            float mediaTime = Mathf.Repeat(Time.time, V6OptionPreviewChart.LoopSeconds);
            // セッション固定の SessionZScale は本編専用。プレビューは
            // CurrentZScale (= GManager.noteSpeed から毎回計算) を使うので
            // オプション変更が即座に効く (Issue #579 仕様)。
            float zScale = MediaTimeline.CurrentZScale;

            for (int i = 0; i < notes.Count; i++)
            {
                PreviewNote note = notes[i];
                GameObject go = note.Go;
                if (go == null)
                {
                    continue;
                }

                // 本編の NoteTimeWithTiming + NotePositionZ と同じ式。
                // ループなので最も近い出現へ畳んだ時刻差を使う。
                float delta = V6OptionPreviewChart.DeltaWithTiming(note.Time, GManager.noteTiming, mediaTime);
                float z = delta * zScale;

                bool show = z <= PlayfieldLaneGuides.GuideDepth + ShowMargin
                    && z >= frontZ - ShowMargin;
                if (go.activeSelf != show)
                {
                    go.SetActive(show);
                }
                if (!show)
                {
                    continue;
                }

                Transform t = go.transform;
                t.localPosition = new Vector3(Judge.LaneWorldX(note.Lane), MediaTimeline.NotePlaneY, z);

                // ロングノーツは体を z 方向へ引き伸ばす (Notes.ApplyHoldLength
                // と同じ scale.z = holdSeconds * zScale。位置オフセットは
                // 本編の Update が毎フレーム上書きするので、見た目も中央揃え)。
                float scaleZ = note.HoldSeconds > 0f
                    ? note.HoldSeconds * zScale
                    : note.BaseScaleZ;
                if (!Mathf.Approximately(t.localScale.z, scaleZ))
                {
                    Vector3 scale = t.localScale;
                    scale.z = scaleZ;
                    t.localScale = scale;
                }
            }
        }

        /// <summary>画面非表示中はカメラを止める (RT への描画を省く)。</summary>
        public void SetRenderingEnabled(bool enabled)
        {
            if (previewCamera != null)
            {
                previewCamera.enabled = enabled;
            }
        }

        /// <summary>rig・カメラ・RT・メッシュ・マテリアルをすべて破棄する。</summary>
        public void Dispose()
        {
            if (previewCamera != null)
            {
                previewCamera.targetTexture = null;
            }

            if (texture != null)
            {
                if (texture.IsCreated())
                {
                    texture.Release();
                }
                DestroyObject(texture);
                texture = null;
            }

            if (root != null)
            {
                DestroyObject(root);
                root = null;
            }

            // root 配下の renderer 参照を外してから Mesh / Material を破棄する
            // (PlayfieldLaneGuides.OnDestroy と同じ順序。破棄保留中に
            // 使われていると Unity が拒否することがある)。
            guideMesh = DestroyAsset(guideMesh);
            judgeMesh = DestroyAsset(judgeMesh);
            guideMaterial = DestroyAsset(guideMaterial);
            judgeMaterial = DestroyAsset(judgeMaterial);
            previewCamera = null;
        }

        private static Mesh DestroyAsset(Mesh mesh)
        {
            if (mesh != null)
            {
                DestroyObject(mesh);
            }
            return null;
        }

        private static Material DestroyAsset(Material material)
        {
            if (material != null)
            {
                DestroyObject(material);
            }
            return null;
        }

        private static void DestroyObject(Object target)
        {
            if (target == null)
            {
                return;
            }
            // EditMode (テスト) では Destroy が使えない。
            if (Application.isPlaying)
            {
                Object.Destroy(target);
            }
            else
            {
                Object.DestroyImmediate(target);
            }
        }
    }
}
