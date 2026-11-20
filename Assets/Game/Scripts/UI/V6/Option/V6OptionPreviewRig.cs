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
    /// - Issue #732: 入力が無いと全ノーツが時刻切れで MISS になるだけなので、
    ///   デモ契約として決定的なオートプレイ (<see cref="AutoPlayScript"/>) が
    ///   譜面に沿って押下を注入し、PERFECT/GREAT/BAD/MISS がすべて観察できる
    ///   ようにする。実キー入力も同じ <see cref="Judge.TryJudgePress"/> 経路で
    ///   判定される (オートプレイと実入力は同じキューを共有する)。
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
        /// ノーツ面 (0.55) の間、判定線 (0.48) の上に置く。
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

        // --- デモ用オートプレイ (Issue #732) ---
        //
        // Option 画面には「プレイする人」がいないため、入力が無いと
        // 全ノーツが時刻切れで MISS になり、判定表示のデモとして機能しない
        // (オーナー報告の全 MISS の直接原因)。そこで譜面に沿った決定的な
        // 押下スクリプトを注入し、PERFECT/GREAT/BAD/MISS と
        // HOLD の Released/Broken/Expired がすべて観察できるようにする。
        // 押下は実キーと同じ Judge.TryJudgePress / UpdateHoldStep /
        // TryJudgeMiss の共有経路を通り、スコア・GManager には書き戻さない。
        // 実キー入力も同じ経路で毎フレーム評価されるので、ユーザーが
        // 演奏すれば実入力どおりの判定が出る (オートプレイと実入力は
        // 同じ JudgeQueue を共有し、先に確定した方がノーツを消費する)。

        /// <summary>オートプレイの有効フラグ。既定 true (デモ本番)。検証ハーネスが false にする。</summary>
        internal bool AutoPlayEnabled = true;

        /// <summary>オートプレイの予定押下 1 件 (時刻は previewTime ドメイン)。</summary>
        private struct AutoPress
        {
            /// <summary>押下の理想時刻。TryJudgePress へはこの値を渡すので、
            /// フレーム刻みのずれに関係なく決定的なラグになる。</summary>
            public float Time;
            public int Lane;
            public bool IsHold;
            /// <summary>HOLD の解放予定時刻 (endTime + オフセット)。</summary>
            public float ReleaseTime;
        }

        private readonly List<AutoPress> autoPresses = new List<AutoPress>();

        /// <summary>レーンごとのオート HOLD 解放予定時刻 (previewTime ドメイン)。</summary>
        private readonly float[] autoHoldUntil = new float[Judge.LaneCount];

        /// <summary>このフレームにオートプレイが押したレーン (レーンフラッシュ用)。</summary>
        private readonly bool[] autoKeyDown = new bool[Judge.LaneCount];

        /// <summary>判定種別ごとの累計数 (LongNote.Judgement の int 値で引く。検証用)。</summary>
        private readonly int[] verdictKindCounts = new int[4];

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
        /// 判定線 (z = 0) の帯。GameScene の JudgeLine (LineRenderer の
        /// ローカル座標 (-4, 0.5, 0) → (4, 0.5, 0)、Transform y=-0.02 なので
        /// ワールド y = <see cref="Judge.JudgeLineWorldY"/> = 0.48、不透明白
        /// JudgeLine.mat) と同じ位置・視覚的な重さの平面 quad
        /// (Issue #706: 半透明 alpha 0.55 では本編より薄かったため
        /// 不透明へ合わせる)。マテリアルはガイドと同じ unlit を、不透明で使う。
        ///
        /// 注意: 判定ポップのオーバーレイもこの線を基準にする。
        /// 本編 V6HudVerdict / V6HudLogic.LegacyJudgeLineAnchor が使う
        /// 投影点はこの quad と同じワールド座標 (Issue #715 で
        /// エフェクト位置 0.76/0.15 から描画判定線へ修正)。
        /// </summary>
        private void BuildJudgeLine()
        {
            float left = PlayfieldLaneGuides.LaneBoundaryX(0);
            float right = PlayfieldLaneGuides.LaneBoundaryX(PlayfieldLaneGuides.GuideCount - 1);
            // GameScene.unity 実測: JudgeLine transform y=-0.02 ×
            // LineRenderer ローカル y=0.5 → ワールド y=0.48 (Issue #715)。
            float y = Judge.JudgeLineWorldY;
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
        /// 疑似メディア時計へ適用する。
        ///
        /// Issue #706 (実レンダリング比較で検出): 旧実装はガイド最奥に
        /// 垂直の壁 quad (z≈21) を立てていたが、本編カメラ姿勢
        /// (y=4.9, pitch 54.8°) では壁のほぼ全域が視錐台の上外で、
        /// 画面には下端の微小な帯しか映らず拍フラッシュが実質見えなかった。
        /// 本編 GameScene の "Flash" は y≈0.12 の薄い水平スラブ
        /// (x ±4.44, z -0.94..20.96, LaneLight.mat) なので、プレビューも
        /// 同じ footprint の水平 quad に改める。巻き順はレーンフラッシュ・
        /// 判定線と同じ (+Y 法線) で、カメラ真上から見える。
        /// </summary>
        private void BuildBeatFlash()
        {
            // GameScene "Flash" 実測 footprint: x ±4.44, z -0.94..20.96, y≈0.12。
            // 本編は pos (0,0.12,10.01) × scale (8.89,0.01,21.9) の薄い cube
            // なので、カメラ真上から見える天面は x ±4.445・z -0.94..20.96・
            // y=0.125。四捨五入で広げると点灯面積が数% 大きく出るため
            // (実レンダリング比較で pv≈0.79 vs gp≈0.77 を観測)、
            // 実測値をそのまま使う。
            const float y = 0.125f;
            const float x0 = -4.445f;
            const float x1 = 4.445f;
            const float z0 = -0.94f;
            const float z1 = 20.96f;
            var vertices = new[]
            {
                new Vector3(x0, y, z0),
                new Vector3(x1, y, z0),
                new Vector3(x0, y, z1),
                new Vector3(x1, y, z1),
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
            // Issue #732: 曲変更でループ長 (loopSeconds) が変わり得るので、
            // Repeat の畳み込みはチャート確保の「後」に行う。旧ループ長で
            // 畳んだ mediaTime を AdvanceClock へ渡すと、位相の違う値どうしの
            // 差分が previewTime へ混入し、キュー済みノーツが一斉に時刻切れ
            // (MISS バースト) になる。
            EnsureChartForCurrentSong();
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
            // 判定キューの先読みとオートプレイ注入は時計確定の直後に行う
            // (オートプレイの押下がこのフレームのレーンフラッシュにも効く)。
            PrepareJudgement();
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
            // Issue #732: ループ長・チャート時刻系が変わったので疑似時計を
            // リベースする。旧ループ長の位相を持つ previousMediaTime のまま
            // AdvanceClock を回すと、ドメインの違う mediaTime との差分が
            // そのまま previewTime へ加算され、判定キューが一斉に時刻切れに
            // なる (全 MISS の一因)。キュー側は RebuildJudgement が
            // scheduledLoop を振り直すので、時計もここで揃える。
            ResetPreviewClock();
            if (chartNotes != null)
            {
                RebuildJudgement();
            }
        }

        /// <summary>
        /// 疑似時計を初期化直前の状態へ戻す (チャート/ループ長の変更時)。
        /// 次の <see cref="AdvanceClock"/> が現在の mediaTime をそのまま
        /// previewTime の原点にするので、ドメインの違う時刻どうしの差分が
        /// 混入しない。
        /// </summary>
        private void ResetPreviewClock()
        {
            clockInitialized = false;
            previewTime = 0f;
            previousMediaTime = -1f;
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
        /// 同じ規則: キーが押されたフレームは α = 0.3、離すと 3/秒で減衰。
        /// 入力は実キー + 検証ハーネス注入
        /// (<see cref="KeyBindings.IsLaneKeyDown"/>) をそのまま使う。
        /// v0.2.1 (#782): プレビューは独立した疑似セッションなので本編の
        /// <see cref="PauseGate"/> では止めない — ポーズメニューの設定画面でも
        /// フラッシュが点く (本編ゲームの判定は PauseGate が別途止める)。
        /// </summary>
        private void UpdateLaneFlashes(float deltaTime)
        {
            for (int lane = 0; lane < Judge.LaneCount; lane++)
            {
                float alpha = laneFlashAlpha[lane];
                if (KeyBindings.IsLaneKeyDown(lane) || autoKeyDown[lane])
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
            if (!GManager.Flash || bgFlashMaterial == null)
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
        /// 判定キューの組み立て・先読み・オートプレイ注入
        /// (<see cref="TickSimulated"/> から毎フレーム、時計確定の直後に呼ぶ)。
        /// 実キーの押下・保持・時刻切れの評価は <see cref="UpdateJudgement"/>。
        /// </summary>
        private void PrepareJudgement()
        {
            if (chartNotes == null || chartNotes.Length == 0)
            {
                for (int lane = 0; lane < Judge.LaneCount; lane++)
                {
                    autoKeyDown[lane] = false;
                }
                return;
            }

            if (judgementQueue == null || GManager.noteTiming != scheduledNoteTiming)
            {
                RebuildJudgement();
            }
            ScheduleJudgement();
            FireAutoPlay();
        }

        /// <summary>
        /// オートプレイの予定押下のうち時刻が来たものを発火する (Issue #732)。
        /// 押下は <see cref="Judge.TryJudgePress"/> を「イベントの理想時刻」
        /// で呼ぶので、フレーム刻みやモニタのリフレッシュに関係なく
        /// <see cref="AutoPlayScript"/> のラグがそのまま判定幅へ乗る。
        /// HOLD は押下が成立したときだけ解放予定時刻を記録し、
        /// <see cref="UpdateJudgement"/> の保持判定がそれを「キー保持」と
        /// して読む。オートプレイ OFF のときは発火せず予定だけ捨てる
        /// (再開時に過去時刻の押下がまとめて走るのを防ぐ)。
        /// </summary>
        private void FireAutoPlay()
        {
            for (int lane = 0; lane < Judge.LaneCount; lane++)
            {
                autoKeyDown[lane] = false;
            }
            if (autoPresses.Count == 0)
            {
                return;
            }

            float now = previewTime;
            // v0.2.1 (#782): プレビューは PauseGate の対象外 (独立した疑似
            // セッション)。ポーズメニュー表示中もオートプレイは発火し続ける。
            bool accept = AutoPlayEnabled;
            int write = 0;
            for (int i = 0; i < autoPresses.Count; i++)
            {
                AutoPress press = autoPresses[i];
                // 未来の予定は残す。1 ループ以上過去の予定は押しても
                // 判定窓に入らないので捨てる (滞留防止)。
                if (press.Time > now || press.Time < now - loopSeconds)
                {
                    if (press.Time > now)
                    {
                        autoPresses[write++] = press;
                    }
                    continue;
                }

                if (!accept)
                {
                    continue;
                }

                // オートプレイの押下もレーンフラッシュを点ける
                // (実キー押下と同じ見え方。UpdateLaneFlashes が読む)。
                autoKeyDown[press.Lane] = true;
                if (Judge.TryJudgePress(judgementQueue, press.Lane, press.Time,
                        out JudgeQueue.NoteEntry entry, out LongNote.Judgement judgement))
                {
                    ShowVerdict(press.Lane, judgement);
                    if (press.IsHold && entry.Type == LongNote.TYPE_HOLD)
                    {
                        autoHoldUntil[press.Lane] = press.ReleaseTime;
                    }
                }
            }
            autoPresses.RemoveRange(write, autoPresses.Count - write);
        }

        /// <summary>
        /// ノーツ index のオートプレイ・スクリプト (Issue #732)。
        /// index % 8 で判定種別を決定的に割り当て、PERFECT/GREAT/BAD/MISS と
        /// HOLD の Released/Broken/Expired が 1 ループの中ですべて出る。
        ///
        ///   - pressOffset: 押下時刻 = 判定時刻 + pressOffset (秒)。
        ///     TryJudgePress へ理想時刻を渡すので |pressOffset| がそのまま
        ///     判定ラグになる (0.10 以下 PERFECT / 0.15 以下 GREAT /
        ///     0.20 以下 BAD)。
        ///   - skip: 押さない → 時刻切れで MISS。
        ///   - releaseOffset: HOLD の解放時刻 = 終端 + releaseOffset。
        ///     +0.05 は GREAT_WINDOW 内 (Released)、-0.30 は早すぎる離し
        ///     (Broken → BAD ポップ)、+0.50 は終端を押し続けて Expired。
        /// </summary>
        private static void AutoPlayScript(int index, bool isHold,
            out float pressOffset, out bool skip, out float releaseOffset)
        {
            skip = false;
            pressOffset = 0f;
            releaseOffset = 0.05f;
            switch (index % 8)
            {
                case 0:
                case 4:
                    // PERFECT / HOLD は Released。
                    break;
                case 1:
                case 5:
                    // GREAT。
                    pressOffset = 0.12f;
                    break;
                case 2:
                    // BAD。HOLD は途中で離して Broken → BAD ポップ。
                    pressOffset = -0.18f;
                    releaseOffset = -0.30f;
                    break;
                case 3:
                    // 押さない → MISS。
                    skip = true;
                    break;
                case 6:
                    // PERFECT。HOLD は解放せず Expired (押し続け = 成功扱い)。
                    releaseOffset = 0.50f;
                    break;
                default:
                    // GREAT。HOLD は途中で離して Broken → BAD ポップ。
                    pressOffset = 0.12f;
                    releaseOffset = -0.30f;
                    break;
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
        /// オートプレイの押下自体は <see cref="PrepareJudgement"/> が済ませて
        /// いる (キューの先読みと同じ場所)。ここでは実キー入力と
        /// オート HOLD の保持状態・時刻切れだけを見る。
        /// </summary>
        private void UpdateJudgement()
        {
            if (chartNotes == null || chartNotes.Length == 0 || judgementQueue == null)
            {
                return;
            }

            // v0.2.1 (#782): 本編のポーズゲート (Issue #224 / #383) は
            // ゲーム本体の判定だけに適用する。プレビューは独立した疑似
            // セッションなので PauseGate.IsPaused では止めない。

            // 1) 押下判定: レーンごとに独立 (Judge.JudgeStep と同じ共通経路)。
            //    実キーの押下は実時間 (previewTime) で判定するので、
            //    ユーザーが演奏すれば実入力どおりの判定が出る (Issue #732)。
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

            // 2) ロングノーツの保持。キー保持は実キー + オートプレイの
            //    解放予定時刻の OR。途中で離したら先頭判定を BAD へ
            //    差し替える (Judge.message(2) = Bad と同じ見た目)。
            for (int lane = 0; lane < Judge.LaneCount; lane++)
            {
                bool held = KeyBindings.IsLaneKeyHeld(lane)
                    || previewTime < autoHoldUntil[lane];
                JudgeQueue.HoldResult result = Judge.UpdateHoldStep(
                    judgementQueue, lane, previewTime, held,
                    out _);
                if (result == JudgeQueue.HoldResult.Broken)
                {
                    ShowVerdict(lane, LongNote.Judgement.Bad);
                }
            }

            // 3) 時刻切れ (Miss)。オートプレイが押さなかったノーツと、
            //    実入力でもオートプレイでも拾われなかったノーツがここに来る。
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

            // オートプレイ状態もリセット (Issue #732): 予定押下は旧チャートの
            // 時刻系で作られているので、新しい timedStart/timedEnd で
            // ScheduleJudgement が積み直す。HOLD の解放予定も同じ。
            autoPresses.Clear();
            for (int lane = 0; lane < Judge.LaneCount; lane++)
            {
                autoHoldUntil[lane] = float.NegativeInfinity;
                autoKeyDown[lane] = false;
            }
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

                    bool isHold = chartNotes[i].HoldSeconds > 0f;
                    judgementQueue.Add(new JudgeQueue.NoteEntry
                    {
                        Time = start,
                        EndTime = timedEnd[i] + loopBase,
                        Type = isHold ? LongNote.TYPE_HOLD : LongNote.TYPE_TAP,
                        Lane = chartNotes[i].Lane,
                        SourceIndex = i,
                    });

                    // Issue #732: オートプレイの予定押下も同じ出現に紐付けて積む。
                    // skip (MISS 役) のノーツは押下を作らず、時刻切れに任せる。
                    AutoPlayScript(i, isHold, out float pressOffset, out bool skip,
                        out float releaseOffset);
                    if (!skip)
                    {
                        autoPresses.Add(new AutoPress
                        {
                            Time = start + pressOffset,
                            Lane = chartNotes[i].Lane,
                            IsHold = isHold,
                            ReleaseTime = timedEnd[i] + loopBase + releaseOffset,
                        });
                    }
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
            int kind = (int)judgement;
            if (kind >= 0 && kind < verdictKindCounts.Length)
            {
                verdictKindCounts[kind]++;
            }
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

        /// <summary>
        /// 単調増加の疑似プレビュー時刻 (previewTime ドメイン、秒)。
        /// ループ折り返し・チャート変更時のリベースを検証するための観測点
        /// (Issue #732)。
        /// </summary>
        internal float PreviewTimeForAudit
        {
            get { return previewTime; }
        }

        /// <summary>判定種別ごとの累計ポップ数 (オートプレイの網羅性検証用)。</summary>
        internal int VerdictCountForAudit(LongNote.Judgement judgement)
        {
            int kind = (int)judgement;
            return kind >= 0 && kind < verdictKindCounts.Length ? verdictKindCounts[kind] : 0;
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

        /// <summary>
        /// rig ローカル座標 (組み立て時は本編ワールド座標と同じ値) を
        /// 実際のワールド座標へ写す。root は原点で組み立てたあと
        /// (0,0,-<see cref="RigDistance"/>) へ平行移動するだけなので、
        /// 回転・スケールは掛からない。rig 外のワールド座標
        /// (例: z=0 の判定アンカー) をそのまま PreviewCamera へ投影すると
        /// 3000 単位ぶん奥行きが違ってビューポート位置が壊れる (Issue #706)。
        /// </summary>
        internal Vector3 RigLocalToWorld(Vector3 localPosition)
        {
            return root == null ? localPosition : root.transform.TransformPoint(localPosition);
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
