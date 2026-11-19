using System.Collections.Generic;
using FlashBeat.Gameplay;
using FlashBeat.UI.V6.Game.Hud;
using UnityEngine;

namespace FlashBeat.UI.V6.Option
{
    /// <summary>
    /// Option 画面 Game Preview (Issue #579 / #607) の 3D シーンを保持するリグ。
    ///
    /// 静止した 2D 図形の代わりに、本編 (GameScene) と同じ幾何で
    /// レーンガイド・判定線・ノーツを描き、専用カメラの
    /// <see cref="RenderTexture"/> を uGUI の RawImage に流す。
    /// Issue #607 でゲーム体験プレビューへ拡張し、レーンフラッシュ・
    /// 判定ポップ・歌詞同期を追加した。
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
    /// - レーンフラッシュは GameScene の LaneLight1..8 (幅 1 のレーン帯、
    ///   LaneFlash が alpha 0.3 まで点灯・3/秒で減衰) と同じ領域・同じ
    ///   入力 (<see cref="KeyBindings.IsLaneKeyDown"/>)・同じ時定数で描く。
    /// - 判定は本編と同じ <see cref="JudgeQueue"/> + <see cref="LongNote"/>
    ///   の判定窓 (PERFECT/GREAT/BAD/MISS) を疑似チャートに対して回す。
    ///   スコア・コンボ・GManager への書き戻しは行わない (Judge.FlushScore
    ///   相当の処理は持たない)。ポップのラベル・色・寿命・立ち上がりは
    ///   <see cref="V6HudLogic"/> の共有関数を使う。
    /// - 歌詞は <see cref="V6OptionPreviewLyrics"/> が本編と同じ
    ///   読み込み経路・同じタイムライン判定で返す。
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

        /// <summary>
        /// レーンフラッシュの点灯アルファと減衰速度。
        /// 本編の LaneFlash (alpha = 0.3 / lightSpeed = 3) および
        /// V6PlayfieldView のレーンフラッシュと同じ値 (Issue #607:
        /// ゲーム本編と同等の視覚)。
        /// </summary>
        private const float LaneFlashPeakAlpha = 0.3f;
        private const float LaneFlashDecayPerSecond = 3f;

        /// <summary>
        /// レーンフラッシュ帯の y。GameScene の LaneLight は
        /// y 0.475..0.525 の帯 (天面 0.525)。ガイド線 (0.53) と
        /// ノーツ面 (0.55) の間、判定線 (0.5) の上に置く。
        /// </summary>
        private const float LaneFlashY = 0.525f;

        /// <summary>
        /// 判定スケジューリングの先読み量 (秒)。
        /// 現在のループ長 (<see cref="loopSeconds"/>) と同じだけ先に
        /// キューへ積む (1 ループ分を常に先読みする)。
        /// </summary>
        private float JudgementScheduleAhead
        {
            get { return loopSeconds; }
        }

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
        private Mesh laneFlashMesh;
        private Material guideMaterial;
        private Material judgeMaterial;
        private readonly Material[] laneFlashMaterials = new Material[Judge.LaneCount];
        private readonly float[] laneFlashAlpha = new float[Judge.LaneCount];
        private readonly bool[] laneFlashHasColor = new bool[Judge.LaneCount];

        private V6OptionPreviewChart.Note[] chartNotes;

        /// <summary>
        /// 疑似メディア時計のループ長 (秒)。実チャートでは
        /// 「最後のノーツ/歌詞 + 2 秒」、フォールバックの疑似チャートでは
        /// <see cref="V6OptionPreviewChart.LoopSeconds"/> (Issue #677)。
        /// </summary>
        private float loopSeconds = V6OptionPreviewChart.LoopSeconds;

        /// <summary>チャートを組んだときの曲 ID (選択曲の変更で組み直す)。</summary>
        private int chartSongId = int.MinValue;

        // --- BGFlash (拍フラッシュ) の疑似再現 (Issue #677) ---

        /// <summary>拍フラッシュの点灯アルファと減衰速度 (BGFlash.lightSpeed = 3)。</summary>
        private const float BeatFlashPeakAlpha = 0.3f;
        private const float BeatFlashDecayPerSecond = 3f;

        private Mesh bgFlashMesh;
        private Material bgFlashMaterial;
        private float bgFlashAlpha;
        private int bgFlashBeatIndex = int.MinValue;

        // --- ゲーム体験プレビュー (Issue #607) ---

        /// <summary>単調増加の疑似メディア時刻 (ループをまたいでも巻き戻らない)。</summary>
        private float previewTime;
        private float previousMediaTime = -1f;
        private bool clockInitialized;

        /// <summary>判定キュー (本編 Judge と同じ JudgeQueue)。GManager には触れない。</summary>
        private JudgeQueue judgementQueue;
        private int scheduledNoteTiming = int.MinValue;
        private float[] timedStart;
        private float[] timedEnd;
        /// <summary>次にキューへ積むループ番号 k (出現時刻 = timed + k * LoopSeconds)。</summary>
        private int scheduledLoop = int.MinValue;
        private int judgementCount;

        private readonly string[] verdictLabels = new string[Judge.LaneCount];
        private readonly float[] verdictStarts = new float[Judge.LaneCount];
        private readonly bool[] verdictActive = new bool[Judge.LaneCount];
        private string currentLyric;

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
            BuildLaneFlashes();
            BuildBeatFlash();
            BuildNotes();
            RebuildJudgement();

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
        /// (-4, 0.5, 0) → (4, 0.5, 0)、不透明白 JudgeLine.mat) と同じ
        /// 位置・視覚的な重さの平面 quad (Issue #706: 半透明 alpha 0.55
        /// では本編より薄かったため不透明へ合わせる)。
        /// マテリアルはガイドと同じ unlit を、不透明で使う。
        /// </summary>
        private void BuildJudgeLine()
        {
            float left = PlayfieldLaneGuides.LaneBoundaryX(0);
            float right = PlayfieldLaneGuides.LaneBoundaryX(PlayfieldLaneGuides.GuideCount - 1);
            const float y = 0.5f;
            // Issue #706: 本編 LineRenderer のリボン幅は widthMultiplier 0.2237
            // × widthCurve 0.116 ≈ 0.026 world。水平 quad の奥行きに直すと
            // 投影厚がほぼ一致する halfDepth ≈ 0.015 (旧 0.03 は約 2 倍太かった)。
            const float halfDepth = 0.015f;

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
                // 本編 JudgeLine.mat は不透明白 (_Color a=1)。
                // _Color が無い fallback シェーダでは何もしない
                // (HasProperty 保護は CreateGuideMaterial と同じ考え方)。
                if (judgeMaterial.HasProperty("_Color"))
                {
                    judgeMaterial.SetColor("_Color", new Color(1f, 1f, 1f, 1f));
                }
                renderer.sharedMaterial = judgeMaterial;
            }
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        /// <summary>
        /// レーンフラッシュ帯 (Issue #607)。GameScene の LaneLight1..8 は
        /// レーン幅 1・y 0.475..0.525・z 0..20 の帯で、LaneFlash が
        /// キー押下時に alpha 0.3 まで点灯し 3/秒で減衰させる。
        /// プレビューでは同じ領域を 8 レーン分の半透明 quad
        /// (サブメッシュごとに独立マテリアル) として敷き、
        /// <see cref="KeyBindings.IsLaneKeyDown"/> と同じ時定数で α を動かす。
        /// </summary>
        private void BuildLaneFlashes()
        {
            var vertices = new Vector3[Judge.LaneCount * 4];
            var uv = new Vector2[Judge.LaneCount * 4];

            for (int lane = 0; lane < Judge.LaneCount; lane++)
            {
                float x0 = Judge.LaneWorldX(lane) - Judge.LaneSpacing * 0.5f;
                float x1 = Judge.LaneWorldX(lane) + Judge.LaneSpacing * 0.5f;
                int v = lane * 4;
                vertices[v + 0] = new Vector3(x0, LaneFlashY, 0f);
                vertices[v + 1] = new Vector3(x1, LaneFlashY, 0f);
                vertices[v + 2] = new Vector3(x0, LaneFlashY, PlayfieldLaneGuides.GuideDepth);
                vertices[v + 3] = new Vector3(x1, LaneFlashY, PlayfieldLaneGuides.GuideDepth);
                uv[v + 0] = new Vector2(0f, 0f);
                uv[v + 1] = new Vector2(1f, 0f);
                uv[v + 2] = new Vector2(0f, 1f);
                uv[v + 3] = new Vector2(1f, 1f);
            }

            laneFlashMesh = new Mesh { name = "Preview Lane Flashes" };
            laneFlashMesh.vertices = vertices;
            laneFlashMesh.uv = uv;
            laneFlashMesh.subMeshCount = Judge.LaneCount;
            for (int lane = 0; lane < Judge.LaneCount; lane++)
            {
                // 判定線と同じ巻き順 (上向き quad)。頂点 index は
                // メッシュ全体の頂点配列を指すので lane*4 を足す。
                int b = lane * 4;
                laneFlashMesh.SetTriangles(new[] { b, b + 2, b + 1, b + 1, b + 2, b + 3 }, lane);
            }
            laneFlashMesh.RecalculateBounds();

            var go = new GameObject("Lane Flashes");
            go.transform.SetParent(root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = laneFlashMesh;

            var renderer = go.AddComponent<MeshRenderer>();
            var materials = new Material[Judge.LaneCount];
            for (int lane = 0; lane < Judge.LaneCount; lane++)
            {
                // レーンごとに α が違うのでマテリアルは 8 個に分ける
                // (CreateGuideMaterial は呼ぶたび新しい Material を返す)。
                var material = PlayfieldLaneGuides.CreateGuideMaterial();
                laneFlashMaterials[lane] = material;
                materials[lane] = material;
                laneFlashHasColor[lane] = material != null && material.HasProperty("_Color");
                if (laneFlashHasColor[lane])
                {
                    material.SetColor("_Color", new Color(1f, 1f, 1f, 0f));
                }
            }
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        /// <summary>
        /// BGFlash (拍フラッシュ) の背景 quad (Issue #677)。
        /// 本編の BGFlash は選択曲の BPM から拍間隔を求め、拍ごとに
        /// α=0.3 へ点灯して 3/秒で減衰させる。プレビューでは同じ規則を
        /// 疑似メディア時計へ適用し、ガイド線より奥 (奥行き方向の奥)
        /// に敷く半透明 quad で再現する。描画順は queue 3000 の透過で、
        /// 距離ソートによりガイド・レーンフラッシュより先 (奥) に描かれる。
        /// </summary>
        private void BuildBeatFlash()
        {
            // ガイドの最奥 (GuideDepth) よりさらに奥。視界を十分に覆う大きさ。
            const float z = PlayfieldLaneGuides.GuideDepth + 1.5f;
            var vertices = new[]
            {
                new Vector3(-6f, -1f, z),
                new Vector3(6f, -1f, z),
                new Vector3(-6f, 4f, z),
                new Vector3(6f, 4f, z),
            };
            var triangles = new[] { 0, 2, 1, 1, 2, 3 };
            var uv = new[]
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0f, 1f), new Vector2(1f, 1f),
            };

            bgFlashMesh = new Mesh { name = "Preview Beat Flash" };
            bgFlashMesh.vertices = vertices;
            bgFlashMesh.uv = uv;
            bgFlashMesh.triangles = triangles;
            bgFlashMesh.RecalculateBounds();

            var go = new GameObject("Beat Flash");
            go.transform.SetParent(root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = bgFlashMesh;

            var renderer = go.AddComponent<MeshRenderer>();
            bgFlashMaterial = PlayfieldLaneGuides.CreateGuideMaterial();
            if (bgFlashMaterial != null && bgFlashMaterial.HasProperty("_Color"))
            {
                bgFlashMaterial.SetColor("_Color", new Color(1f, 1f, 1f, 0f));
                renderer.sharedMaterial = bgFlashMaterial;
            }
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        /// <summary>
        /// 疑似チャート全ノーツ分の Note.prefab 実体をプールとして生成する。
        /// <see cref="Notes"/> は無効化し、移動は <see cref="TickSimulated"/> が担う
        /// (本編の step_time / SessionZScale 経路を通らない)。
        /// Issue #677: チャートは選択曲の実譜面 (<see cref="V6OptionPreviewChart.BuildForCurrentSong"/>)
        /// で、無い曲だけ疑似チャートへフォールバックする。
        /// 実際の組み立ては <see cref="RebuildNotePool"/> (Issue #695: 曲変更時の
        /// 組み直しと同じ経路)。
        /// </summary>
        private void BuildNotes()
        {
            RebuildNotePool();
        }

        /// <summary>
        /// ノーツプールを組み直す (Issue #695: 初回構築と曲変更で共有する
        /// 唯一の経路)。既存ノーツ実体を破棄し、選択曲のチャート分の
        /// Note.prefab を生成する。prefab が無いときは <see cref="chartNotes"/>
        /// を null にして songID だけ記録する (Tick ごとの Resources.Load
        /// 再試行を防ぐ)。
        /// </summary>
        private void RebuildNotePool()
        {
            // 既存のノーツ実体を破棄してから組み直す (プールの作り直し)。
            for (int i = 0; i < notes.Count; i++)
            {
                if (notes[i].Go != null)
                {
                    DestroyObject(notes[i].Go);
                }
            }
            notes.Clear();

            chartSongId = GManager.songID;
            GameObject prefab = Resources.Load<GameObject>("Prefabs/Note");
            if (prefab == null)
            {
                chartNotes = null;
                Debug.LogWarning("[V6OptionPreviewRig] Resources/Prefabs/Note が見つからない - ノーツ無しでガイド線だけ描く。");
                return;
            }

            chartNotes = V6OptionPreviewChart.BuildForCurrentSong(out loopSeconds);
            for (int i = 0; i < chartNotes.Length; i++)
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
                    Lane = chartNotes[i].Lane,
                    Time = chartNotes[i].Time,
                    HoldSeconds = chartNotes[i].HoldSeconds,
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
            TickSimulated(Mathf.Repeat(Time.time, loopSeconds), Time.deltaTime);
        }

        /// <summary>
        /// 疑似メディア時刻とフレーム時間を外から与える版 (EditMode 検証用)。
        /// 実機の <see cref="Tick"/> と同じ経路を通る。
        /// </summary>
        internal void TickSimulated(float mediaTime, float deltaTime)
        {
            if (previewCamera == null)
            {
                return;
            }

            EnsureChartForCurrentSong();
            AdvanceClock(mediaTime);
            UpdateNotes(mediaTime);
            UpdateLaneFlashes(deltaTime);
            UpdateBeatFlash(deltaTime);
            UpdateJudgement();
            currentLyric = V6OptionPreviewLyrics.CurrentLine(mediaTime);
        }

        /// <summary>
        /// 選択曲が変わっていたらチャートを組み直す (Issue #677)。
        /// Option 画面を開いたまま別の曲が選ばれる経路は通常無いが、
        /// 曲 ID の読み取りだけで判定するので副作用は無い。
        /// 組み直しは初回構築と同じ <see cref="RebuildNotePool"/> を通る
        /// (Issue #695)。prefab 不在でチャートが無いときは判定キューも
        /// 空のままにする (前の曲のノーツを判定し続けない)。
        /// </summary>
        private void EnsureChartForCurrentSong()
        {
            if (GManager.songID == chartSongId)
            {
                return;
            }

            RebuildNotePool();
            if (chartNotes != null)
            {
                RebuildJudgement();
            }
        }

        /// <summary>
        /// ループする疑似メディア時刻を単調増加のプレビュー時刻へ畳む。
        /// 判定キューはループをまたいでも時刻が巻き戻らないよう、
        /// 折り返しを 1 周分として積算する (これが無いとループ末尾の
        /// ノーツが折り返し直後に一斉 Miss になる)。
        /// </summary>
        private void AdvanceClock(float mediaTime)
        {
            if (!clockInitialized)
            {
                clockInitialized = true;
                previewTime = mediaTime;
                previousMediaTime = mediaTime;
                return;
            }

            float delta = mediaTime - previousMediaTime;
            if (delta < -loopSeconds * 0.5f)
            {
                delta += loopSeconds;
            }
            previewTime += delta;
            previousMediaTime = mediaTime;
        }

        /// <summary>ノーツ位置の更新 (Issue #579 の既存経路)。</summary>
        private void UpdateNotes(float mediaTime)
        {
            if (notes.Count == 0)
            {
                return;
            }

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
                // ループなので最も近い出現へ畳んだ時刻差を使う
                // (ループ長は実チャートでは可変。Issue #677)。
                float delta = V6OptionPreviewChart.DeltaWithTiming(
                    note.Time, GManager.noteTiming, mediaTime, loopSeconds);
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

        /// <summary>
        /// レーンフラッシュ (Issue #607)。本編 LaneFlash / V6PlayfieldView と
        /// 同じ規則: キーが押されたフレームは α = 0.3、離すと 3/秒で減衰、
        /// ポーズ中は消灯。入力は実キー + 検証ハーネス注入
        /// (<see cref="KeyBindings.IsLaneKeyDown"/>) をそのまま使う。
        /// </summary>
        private void UpdateLaneFlashes(float deltaTime)
        {
            bool paused = PauseGate.IsPaused;
            for (int lane = 0; lane < Judge.LaneCount; lane++)
            {
                float alpha = laneFlashAlpha[lane];
                if (paused)
                {
                    alpha = 0f;
                }
                else if (KeyBindings.IsLaneKeyDown(lane))
                {
                    alpha = LaneFlashPeakAlpha;
                }
                else if (alpha > 0f)
                {
                    alpha = Mathf.Max(0f, alpha - LaneFlashDecayPerSecond * deltaTime);
                }

                if (Mathf.Approximately(alpha, laneFlashAlpha[lane]))
                {
                    continue;
                }

                laneFlashAlpha[lane] = alpha;
                Material material = laneFlashMaterials[lane];
                if (material != null && laneFlashHasColor[lane])
                {
                    material.SetColor("_Color", new Color(1f, 1f, 1f, alpha));
                }
            }
        }

        /// <summary>
        /// BGFlash (拍フラッシュ) の疑似再現 (Issue #677)。本編 BGFlash と
        /// 同じ規則: 選択曲の BPM から拍間隔 (60/BPM × FlashBG の倍率) を求め、
        /// 拍ごとに α=0.3 へ点灯、lightSpeed=3 で減衰。位相は
        /// <see cref="GManager.FlashT"/> / 100 秒の初期オフセット +
        /// 疑似メディア時計。GManager.Flash (表示 On/Off) が Off または
        /// BPM が取れない曲では何も描かない。GManager / BGFlash の
        /// 実オブジェクトには触れない (読み取りのみ)。
        ///
        /// Issue #706: 本編 BGFlash は壁時計 (Time.deltaTime 蓄積、
        /// GManager.played 中のみ) で拍を刻むが、プレビューは意図的に
        /// 疑似メディア時計 (<see cref="previewTime"/>) を使う。
        /// Option 画面には「プレイ中」のセッションが無く (GManager.played
        /// は常に false で、本編の played ゲートを写すと一切点かない)、
        /// またノーツ・判定・歌詞がすべて同じ疑似時計で動くため、拍位相も
        /// それに揃えた方がループするチャートと拍がずれない。
        /// 壁時計との差は「メディア時刻に対する拍の位相が本編と独立」
        /// という点だけで、点灯条件・減衰・BPM/FlashBG の写像は本編と同一。
        /// </summary>
        private void UpdateBeatFlash(float deltaTime)
        {
            float alpha = bgFlashAlpha;
            if (PauseGate.IsPaused || !GManager.Flash || bgFlashMaterial == null)
            {
                alpha = 0f;
            }
            else
            {
                // Issue #677 review: プレビューは現在の選択曲を読む。
                // ResolveSong は pinned song を優先し得るため GetSong を使う。
                SongData song = GManager.GetSong(GManager.songID);
                float bpm = song != null ? song.bpm : 0f;
                if (bpm > 0f)
                {
                    // BGFlash.Start と同じ拍間隔: 60/BPM に FlashBG の倍率
                    // (写像は BGFlash.BeatIntervalMultiplier が単一の出所。
                    // Issue #695 でローカル再実装をやめた)。
                    float beat = 60f / bpm * BGFlash.BeatIntervalMultiplier(GManager.FlashBG);

                    // BPMCount = FlashT/100 からの経過で拍を数えるのと同じ位相。
                    int beatIndex = Mathf.FloorToInt(
                        (previewTime + GManager.FlashT / 100f) / beat);
                    if (bgFlashBeatIndex == int.MinValue)
                    {
                        bgFlashBeatIndex = beatIndex;
                    }
                    else if (beatIndex != bgFlashBeatIndex)
                    {
                        bgFlashBeatIndex = beatIndex;
                        alpha = BeatFlashPeakAlpha;
                    }
                    else if (alpha > 0f)
                    {
                        alpha = Mathf.Max(0f, alpha - BeatFlashDecayPerSecond * deltaTime);
                    }
                }
            }

            bgFlashAlpha = alpha;
            if (bgFlashMaterial != null && bgFlashMaterial.HasProperty("_Color"))
            {
                bgFlashMaterial.SetColor("_Color", new Color(1f, 1f, 1f, alpha));
            }
        }

        /// <summary>
        /// 疑似チャートに対する判定 (Issue #607)。本編 Judge.JudgeStep と
        /// 同じ処理順 (押下 → HOLD 保持 → 時刻切れ Miss) を回すが、
        /// 「どのノーツが・タップか保持か・ミスか」の決定手順は
        /// <see cref="Judge.TryJudgePress"/> / <see cref="Judge.UpdateHoldStep"/> /
        /// <see cref="Judge.TryJudgeMiss"/> の共有経路を通る (Issue #695)。
        /// スコア・コンボ・GManager への書き戻しは一切行わない。
        /// 判定結果は <see cref="ShowVerdict"/> でレーン別ポップへ流す。
        /// </summary>
        private void UpdateJudgement()
        {
            if (chartNotes == null || chartNotes.Length == 0)
            {
                return;
            }

            if (judgementQueue == null || GManager.noteTiming != scheduledNoteTiming)
            {
                RebuildJudgement();
            }
            ScheduleJudgement();

            // Issue #224 / #383 と同じゲート: ポーズ中は判定しない。
            if (PauseGate.IsPaused)
            {
                return;
            }

            // 1) 押下判定: レーンごとに独立 (Judge.JudgeStep と同じ共通経路)。
            for (int lane = 0; lane < Judge.LaneCount; lane++)
            {
                if (!KeyBindings.IsLaneKeyDown(lane))
                {
                    continue;
                }

                if (!Judge.TryJudgePress(judgementQueue, lane, previewTime,
                        out _, out LongNote.Judgement judgement))
                {
                    continue;
                }

                ShowVerdict(lane, judgement);
            }

            // 2) ロングノーツの保持。途中で離したら先頭判定を BAD へ
            //    差し替える (Judge.message(2) = Bad と同じ見た目)。
            for (int lane = 0; lane < Judge.LaneCount; lane++)
            {
                JudgeQueue.HoldResult result = Judge.UpdateHoldStep(
                    judgementQueue, lane, previewTime, KeyBindings.IsLaneKeyHeld(lane),
                    out _);
                if (result == JudgeQueue.HoldResult.Broken)
                {
                    ShowVerdict(lane, LongNote.Judgement.Bad);
                }
            }

            // 3) 時刻切れ (Miss)。
            for (int lane = 0; lane < Judge.LaneCount; lane++)
            {
                if (Judge.TryJudgeMiss(judgementQueue, lane, previewTime, out _))
                {
                    ShowVerdict(lane, LongNote.Judgement.Miss);
                }
            }
        }

        /// <summary>
        /// 判定キューを組み直す。NoteTiming (GManager.noteTiming、読み取りのみ)
        /// が変わると出現時刻がずれるので、時刻を再計算してキューを作り直す。
        /// ループ k の出現時刻は timedStart[i] + k * LoopSeconds。
        /// </summary>
        private void RebuildJudgement()
        {
            scheduledNoteTiming = GManager.noteTiming;
            int count = chartNotes == null ? 0 : chartNotes.Length;
            timedStart = new float[count];
            timedEnd = new float[count];
            for (int i = 0; i < count; i++)
            {
                // 本編 NotesManager.Load と同じ NoteTimeWithTiming。
                // HOLD の終端は開始 + 長さ (長さはタイミングで変わらない)。
                timedStart[i] = NotesManager.NoteTimeWithTiming(chartNotes[i].Time, scheduledNoteTiming);
                timedEnd[i] = timedStart[i] + chartNotes[i].HoldSeconds;
            }

            judgementQueue = new JudgeQueue(Judge.LaneCount);
            scheduledLoop = int.MinValue;
        }

        /// <summary>
        /// 判定に必要な分だけキューへ出現を積む。現在のプレビュー時刻より
        /// <see cref="JudgementScheduleAhead"/> 先までのループを追加するので、
        /// キューに常時あるのは高々 2〜3 ループ分 (約 500 件)。
        /// すでに判定窓を過ぎた出現は積まない (起動直後の一括 Miss 防止)。
        /// </summary>
        private void ScheduleJudgement()
        {
            if (scheduledLoop == int.MinValue)
            {
                // 現在時刻を含むループの 1 つ前から積み始める
                // (直前ループの末尾ノーツがまだ判定待ちのことがあるため)。
                scheduledLoop = Mathf.FloorToInt(
                    (previewTime - JudgementScheduleAhead) / loopSeconds);
            }

            float horizon = previewTime + JudgementScheduleAhead;
            while (scheduledLoop * loopSeconds <= horizon)
            {
                float loopBase = scheduledLoop * loopSeconds;
                for (int i = 0; i < chartNotes.Length; i++)
                {
                    float start = timedStart[i] + loopBase;
                    if (start + LongNote.BAD_WINDOW < previewTime)
                    {
                        continue; // 判定窓を過ぎた出現は Miss 済みと同じなので積まない
                    }

                    judgementQueue.Add(new JudgeQueue.NoteEntry
                    {
                        Time = start,
                        EndTime = timedEnd[i] + loopBase,
                        Type = chartNotes[i].HoldSeconds > 0f
                            ? LongNote.TYPE_HOLD
                            : LongNote.TYPE_TAP,
                        Lane = chartNotes[i].Lane,
                        SourceIndex = i,
                    });
                }
                scheduledLoop++;
            }
        }

        /// <summary>
        /// 判定ポップをレーンに出す。ラベル・色・寿命・立ち上がりの仕様は
        /// <see cref="V6HudLogic"/> (本編 V6HudVerdict と同じ関数) が持ち、
        /// ここでは「どのレーンに・いつ出たか」だけを記録する。
        /// GManager のスコア系には一切触れない。
        /// </summary>
        private void ShowVerdict(int lane, LongNote.Judgement judgement)
        {
            if (lane < 0 || lane >= Judge.LaneCount)
            {
                return;
            }

            string label = V6HudLogic.VerdictLabel(judgement);
            if (string.IsNullOrEmpty(label))
            {
                return;
            }

            verdictLabels[lane] = label;
            verdictStarts[lane] = Time.unscaledTime;
            verdictActive[lane] = true;
            judgementCount++;
        }

        // --- 検証ハーネス用 (V6OptionView の ForAudit API 経由で公開) ---

        /// <summary>いま表示中の歌詞本文 (無ければ null)。</summary>
        internal string CurrentLyric
        {
            get { return currentLyric; }
        }

        /// <summary>
        /// レーンの判定ポップ。表示中ならラベルと開始時刻
        /// (<see cref="Time.unscaledTime"/>) を返す。寿命切れ・未発火は false。
        /// </summary>
        internal bool TryGetVerdict(int lane, out string label, out float startTime)
        {
            label = null;
            startTime = 0f;
            if (lane < 0 || lane >= Judge.LaneCount || !verdictActive[lane] || verdictLabels[lane] == null)
            {
                return false;
            }

            if (Time.unscaledTime - verdictStarts[lane] >= V6HudLogic.VerdictTotalSeconds)
            {
                return false;
            }

            label = verdictLabels[lane];
            startTime = verdictStarts[lane];
            return true;
        }

        /// <summary>これまでに出した判定ポップの累計数 (状態分離の検証用)。</summary>
        internal int JudgementCount
        {
            get { return judgementCount; }
        }

        /// <summary>レーンフラッシュの現在 α (検証用)。</summary>
        internal float LaneFlashAlphaForAudit(int lane)
        {
            return lane >= 0 && lane < Judge.LaneCount ? laneFlashAlpha[lane] : 0f;
        }

        /// <summary>拍フラッシュ quad の現在 α (BGFlash.alfa 相当。検証用)。</summary>
        internal float BeatFlashAlphaForAudit
        {
            get { return bgFlashAlpha; }
        }

        /// <summary>プレビューカメラ (判定ポップのワールド→ビューポート写像用)。</summary>
        internal Camera PreviewCamera
        {
            get { return previewCamera; }
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
            laneFlashMesh = DestroyAsset(laneFlashMesh);
            bgFlashMesh = DestroyAsset(bgFlashMesh);
            guideMaterial = DestroyAsset(guideMaterial);
            judgeMaterial = DestroyAsset(judgeMaterial);
            bgFlashMaterial = DestroyAsset(bgFlashMaterial);
            for (int i = 0; i < laneFlashMaterials.Length; i++)
            {
                laneFlashMaterials[i] = DestroyAsset(laneFlashMaterials[i]);
                laneFlashHasColor[i] = false;
            }
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
