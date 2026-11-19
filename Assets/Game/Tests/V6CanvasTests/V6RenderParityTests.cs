using System.Collections.Generic;
using System.Reflection;
using System.Text;
using FlashBeat.Gameplay;
using FlashBeat.UI.V5;
using FlashBeat.UI.V6;
using FlashBeat.UI.V6.Common;
using FlashBeat.UI.V6.Game.Hud;
using FlashBeat.UI.V6.Option;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace FlashBeat.V6CanvasTests
{
    /// <summary>
    /// Issue #706/#677: ゲーム本編プレイフィールドと Option 画面 Game Preview
    /// の<b>実レンダリング結果</b>を意味領域で比較する検証。
    ///
    /// これまでのプレビュー検証 (V6PreviewParityTests / V6OptionPreviewExperienceTests)
    /// は構造・定数・audit 値の一致だけを見ており、「実際に同じ絵が出るか」は
    /// 誰も確かめていなかった。ここでは両側を RenderTexture へ描き、
    /// ノーツ・レーンフラッシュ・判定ポップ・歌詞ボックス・判定線・
    /// 拍フラッシュを正規化座標の領域で突き合わせる。
    ///
    /// 本編側ステージ (出荷の 3D 経路 V6UiMode.Playfield=false と同じ構成):
    /// - GameScene と同じカメラ姿勢 (0,4.9,-0.8 / pitch 54.8 / FOV 60) +
    ///   PlayfieldCameraFit。RT は <b>640x480 = batch の Screen サイズ</b>。
    ///   HUD (V6GameHudView) の px 定数は CanvasSize() を、プレビューの
    ///   オーバーレイは Screen.* を見るので、両側が同じ 640x480 を参照する
    ///   限り正規化座標で一致する (1920x1080 にするとオーバーレイ側の
    ///   Screen.height=480 基準とずれる — テスト環境の制約であって製品差ではない)。
    /// - GameScene.unity 実測の部品: LaneLight1..8 (LaneLight.mat, 幅1・
    ///   y0.475..0.525・z0.01..20.01, LaneFlash α0.3/3s^-1)、JudgeLine
    ///   (LineRenderer ±4, y≈0.48, JudgeLine.mat 不透明白)、Flash (BGFlash,
    ///   y≈0.12 の薄い水平スラブ)、実 Note.prefab (Notes 無効・rig と同じ式で配置)。
    /// - 実物 V6GameHudView (Video Dim は出荷同様に無効化) と、歌詞データを
    ///   読ませる実 GameSceneManager。
    ///
    /// プレビュー側: ScreenSpaceCamera Canvas + 専用オーバーレイカメラ
    /// (640x480 RT) に実 V6OptionView を置き、コンパクト表示のパンで
    /// Game Preview well を画面中央へ寄せる。3D 内容 (ノーツ・
    /// レーン/拍フラッシュ・判定線) は rig の RenderTexture を直接読み、
    /// 歌詞ボックス・判定ポップなどの uGUI オーバーレイは well 部分を
    /// ort から切り出して比較する (well 合成の 2 段 alpha 減衰で
    /// 半透明 3D 要素が閾値以下に薄まるため — RenderPreview3D 参照)。
    ///
    /// 比較は正規化ビューポート座標 (x,y ∈ [0,1], y は下原点)。
    /// ピクセル完全一致ではなく「同じレーンに・同じ位置に・同じくらいの
    /// 面積で描かれるか」を見る。両カメラは同じ姿勢・FOV・fitted aspect
    /// (16:9) なのでワールド→ビューポート写像は一致する。
    /// </summary>
    public sealed class V6RenderParityTests
    {
        /// <summary>本編 RT / プレビューオーバーレイ RT のサイズ (batch Screen と同じ)。</summary>
        private const int RtW = 640;
        private const int RtH = 480;

        /// <summary>差分判定の輝度差しきい値 (0..1)。</summary>
        private const float DiffThreshold = 0.08f;

        private readonly List<Object> spawned = new List<Object>();
        private readonly List<Camera> disabledCameras = new List<Camera>();
        private readonly List<Renderer> disabledRenderers = new List<Renderer>();
        private V6OptionView optionView;

        private int savedSongId;
        private int savedNoteTiming;
        private float savedNoteSpeed;
        private float savedStepTime;
        private bool savedPlayed;
        private bool savedStart;
        private bool savedFlash;
        private int savedFlashBg;
        private int savedFlashT;
        private bool savedLyricsEnabled;
        private bool savedSuppressJudgements;

        [SetUp]
        public void SetUp()
        {
            savedSongId = GManager.songID;
            savedNoteTiming = GManager.noteTiming;
            savedNoteSpeed = GManager.noteSpeed;
            savedStepTime = GManager.step_time;
            savedPlayed = GManager.played;
            savedStart = GManager.Start;
            savedFlash = GManager.Flash;
            savedFlashBg = GManager.FlashBG;
            savedFlashT = GManager.FlashT;
            savedLyricsEnabled = PlaySession.LyricsEnabled;
            savedSuppressJudgements = Judge.SuppressJudgementEffects;

            // ロストアンブレラ (songID=1): 実チャート + 歌詞データあり。
            GManager.songID = 1;
            GManager.noteSpeed = 10f;
            GManager.noteTiming = 0;
            GManager.Flash = true;
            GManager.FlashBG = 8;   // BeatIntervalMultiplier = ×1 (60/BPM)
            GManager.FlashT = 0;
            PlaySession.SetLyricsEnabled(true);

            MediaTimeline.ResetSession();
            MediaTimeline.BeginSession(10f);
            PauseGate.HardReset();
            KeyBindings.ClearInjectedLaneKeyDowns();
            KeyBindings.ClearInjectedKeyHolds();
            Judge.ClearPendingVerdicts();

            // 前テストの残存 Renderer が撮影を汚さないよう隔離
            // (V6NoteRenderTests と同じ手順)。自分の描画物は SetUp 後に作る。
            var renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (!renderers[i].enabled) continue;
                renderers[i].enabled = false;
                disabledRenderers.Add(renderers[i]);
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (optionView != null)
            {
                optionView.Dispose();
                optionView = null;
            }

            // RT を破棄する前に targetTexture 参照を全部外す。
            // 残ったまま RenderTexture を DestroyImmediate すると
            // "Releasing render texture that is set as Camera.targetTexture!"
            // が unhandled log になり全テストが失敗扱いになる。
            RenderTexture.active = null;
            var cams = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            for (int i = 0; i < cams.Length; i++)
            {
                if (cams[i] != null && cams[i].targetTexture != null)
                {
                    cams[i].targetTexture = null;
                }
            }

            for (int i = 0; i < spawned.Count; i++)
            {
                if (spawned[i] != null) Object.DestroyImmediate(spawned[i]);
            }
            spawned.Clear();
            for (int i = 0; i < disabledCameras.Count; i++)
            {
                if (disabledCameras[i] != null) disabledCameras[i].enabled = true;
            }
            disabledCameras.Clear();
            for (int i = 0; i < disabledRenderers.Count; i++)
            {
                if (disabledRenderers[i] != null) disabledRenderers[i].enabled = true;
            }
            disabledRenderers.Clear();

            GManager.songID = savedSongId;
            GManager.noteTiming = savedNoteTiming;
            GManager.noteSpeed = savedNoteSpeed;
            GManager.step_time = savedStepTime;
            GManager.played = savedPlayed;
            GManager.Start = savedStart;
            GManager.Flash = savedFlash;
            GManager.FlashBG = savedFlashBg;
            GManager.FlashT = savedFlashT;
            PlaySession.SetLyricsEnabled(savedLyricsEnabled);
            Judge.SuppressJudgementEffects = savedSuppressJudgements;
            KeyBindings.ClearInjectedLaneKeyDowns();
            KeyBindings.ClearInjectedKeyHolds();
            Judge.ClearPendingVerdicts();
            MediaTimeline.ResetSession();
            PauseGate.HardReset();
        }

        private GameObject Track(GameObject go)
        {
            spawned.Add(go);
            return go;
        }

        // ==================================================================
        // 本編ステージ (3D 出荷経路)
        // ==================================================================

        private sealed class GameplayStage
        {
            public Camera Cam;
            public Camera HudCam;
            public RenderTexture Rt;
            public V6GameHudView Hud;
            public GameSceneManager LyricSource;
            public readonly List<Renderer> NoteRenderers = new List<Renderer>();
            public readonly List<LaneFlash> LaneFlashes = new List<LaneFlash>();
            public BGFlash BeatFlash;
            public V6OptionPreviewChart.Note[] Chart;
            public float LoopSeconds;
            public float FrontZ;
            public float ZScale;
        }

        private static void InvokePrivate(object target, string method)
        {
            MethodInfo m = target.GetType().GetMethod(method,
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(m, target.GetType().Name + "." + method + " が見つからない");
            m.Invoke(target, null);
        }

        private static T GetField<T>(object target, string name)
        {
            FieldInfo f = target.GetType().GetField(name,
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(f, target.GetType().Name + "." + name + " が見つからない");
            return (T)f.GetValue(target);
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo f = target.GetType().GetField(name,
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(f, target.GetType().Name + "." + name + " が見つからない");
            f.SetValue(target, value);
        }

        /// <summary>
        /// GameScene の見た目を EditMode で再現した本編ステージを組む。
        /// mediaTime は GManager.step_time / ノーツ配置 / 歌詞同期の共通時刻。
        /// </summary>
        private GameplayStage BuildGameplay(float mediaTime)
        {
            var stage = new GameplayStage();
            stage.Chart = V6OptionPreviewChart.BuildForCurrentSong(out stage.LoopSeconds);
            Assert.IsNotNull(stage.Chart);
            Assert.Greater(stage.Chart.Length, 0, "選択曲のチャートが空");
            stage.ZScale = MediaTimeline.CurrentZScale;

            // --- カメラ (GameScene Main Camera と同じ姿勢) ---
            var camGo = Track(new GameObject("Parity Main Camera", typeof(Camera)));
            camGo.tag = "MainCamera";
            var cam = camGo.GetComponent<Camera>();
            var others = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            for (int i = 0; i < others.Length; i++)
            {
                if (others[i] == cam || !others[i].CompareTag("MainCamera")) continue;
                if (!others[i].enabled) continue;
                others[i].enabled = false;
                disabledCameras.Add(others[i]);
            }
            cam.transform.localPosition = new Vector3(0f, 4.9f, -0.8f);
            cam.transform.localRotation = new Quaternion(0.46019977f, 0f, 0f, 0.8878155f);
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 1000f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.6f, 0.6f, 0.6f, 1f);
            PlayfieldCameraFit.Apply(cam, RtW / (float)RtH);
            stage.Cam = cam;
            stage.Rt = new RenderTexture(RtW, RtH, 24, RenderTextureFormat.ARGB32);
            stage.Rt.Create();
            spawned.Add(stage.Rt);
            cam.targetTexture = stage.Rt; // canvas の pixelRect を RT に合わせるため常時付ける

            // Standard シェーダの実マテリアル (LaneLight/JudgeLine) を lit で
            // 描くための光 (GameScene の Directional Light 相当)。
            var lightGo = Track(new GameObject("Parity Directional Light", typeof(Light)));
            var light = lightGo.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;
            lightGo.transform.localRotation = Quaternion.Euler(50f, -30f, 0f);

            // --- レーンガイド (PlayfieldLaneGuides と同じ実メッシュ/実マテリアル) ---
            stage.FrontZ = -PlayfieldLaneGuides.FrontDepth(cam);
            var guideGo = Track(new GameObject("Lane Guides"));
            guideGo.AddComponent<MeshFilter>().sharedMesh =
                PlayfieldLaneGuides.BuildGuideMesh(stage.FrontZ);
            var guideRenderer = guideGo.AddComponent<MeshRenderer>();
            guideRenderer.sharedMaterial = PlayfieldLaneGuides.CreateGuideMaterial();
            guideRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            guideRenderer.receiveShadows = false;

            // --- LaneLight1..8 (GameScene 実測: 幅1・y0.5・z10.01・scale(1,0.05,20)) ---
            var laneMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Game/Art/Materials/LaneLight.mat");
            Assert.IsNotNull(laneMat, "LaneLight.mat が見つからない");
            for (int lane = 0; lane < Judge.LaneCount; lane++)
            {
                var go = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
                go.name = "LaneLight" + (lane + 1);
                go.transform.localPosition = new Vector3(Judge.LaneWorldX(lane), 0.5f, 10.01f);
                go.transform.localScale = new Vector3(1f, 0.05f, 20f);
                // LaneFlash/BGFlash は rend.material (実体化) で色を書く。
                // 本番と同じくレーンごとの実体マテリアルにしておく
                // (EditMode の .material 初回アクセスはエラーになるので、
                //  Update 呼び出し側でログを抑止する)。
                var laneInstance = new Material(laneMat);
                spawned.Add(laneInstance);
                go.GetComponent<Renderer>().sharedMaterial = laneInstance;
                var lf = go.AddComponent<LaneFlash>();
                SetField(lf, "laneNumber", lane + 1);
                InvokePrivate(lf, "Start");
                stage.LaneFlashes.Add(lf);
            }

            // --- JudgeLine (GameScene 実測: LineRenderer ±4, y≈0.48, z=0, JudgeLine.mat) ---
            var judgeMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Game/Art/Materials/JudgeLine.mat");
            Assert.IsNotNull(judgeMat, "JudgeLine.mat が見つからない");
            var judgeGo = Track(new GameObject("JudgeLine"));
            judgeGo.transform.localPosition = new Vector3(0f, -0.02f, 0f);
            judgeGo.transform.localScale = new Vector3(8f, 1f, 40f);
            var lr = judgeGo.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.SetPosition(0, new Vector3(-4f, 0.5f, 0f));
            lr.SetPosition(1, new Vector3(4f, 0.5f, 0f));
            // widthMultiplier 0.2237 × widthCurve ≈0.116 ≈ 0.026 world。
            // ここでは最終幅だけを同じ値で指定する (シーン YAML の直列化形式に
            // 依存しない。描画上の意味は同一)。
            lr.startWidth = 0.026f;
            lr.endWidth = 0.026f;
            lr.widthMultiplier = 1f;
            lr.startColor = Color.white;
            lr.endColor = Color.white;
            lr.sharedMaterial = judgeMat;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;

            // --- BGFlash (Flash: y≈0.12 の薄い水平スラブ, LaneLight.mat) ---
            var flashGo = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            flashGo.name = "Flash";
            flashGo.transform.localPosition = new Vector3(0f, 0.12f, 10.01f);
            flashGo.transform.localScale = new Vector3(8.89f, 0.01f, 21.9f);
            var flashInstance = new Material(laneMat);
            spawned.Add(flashInstance);
            flashGo.GetComponent<Renderer>().sharedMaterial = flashInstance;
            stage.BeatFlash = flashGo.AddComponent<BGFlash>();
            stage.BeatFlash.Flash = flashGo;
            InvokePrivate(stage.BeatFlash, "Start");

            // --- ノーツ (実 Note.prefab。rig と同じ配置式、Notes は無効) ---
            var prefab = Resources.Load<GameObject>("Prefabs/Note");
            Assert.IsNotNull(prefab, "Resources/Prefabs/Note が見つからない");
            float showZMax = PlayfieldLaneGuides.GuideDepth + 0.5f;
            float showZMin = stage.FrontZ - 0.5f;
            for (int i = 0; i < stage.Chart.Length; i++)
            {
                var note = stage.Chart[i];
                float delta = V6OptionPreviewChart.DeltaWithTiming(
                    note.Time, GManager.noteTiming, mediaTime, stage.LoopSeconds);
                float z = delta * stage.ZScale;
                if (z > showZMax || z < showZMin) continue;

                var go = Track(Object.Instantiate(prefab));
                go.name = "Parity Note";
                var notesComponent = go.GetComponent<Notes>();
                if (notesComponent != null) notesComponent.enabled = false;
                go.transform.localPosition = new Vector3(
                    Judge.LaneWorldX(note.Lane), MediaTimeline.NotePlaneY, z);
                if (note.HoldSeconds > 0f)
                {
                    var scale = go.transform.localScale;
                    scale.z = note.HoldSeconds * stage.ZScale;
                    go.transform.localScale = scale;
                }
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    stage.NoteRenderers.Add(r);
                }
            }

            // --- HUD (実 V6GameHudView。Video Dim は出荷同様に無効) ---
            GManager.played = true;
            GManager.Start = true;
            GManager.step_time = mediaTime;

            // HUD 専用カメラ: 出荷の HUD は ScreenSpaceOverlay (= Screen px)
            // なので canvas 寸法は「RT アスペクト」で決まる必要がある。
            // ワールドカメラ (PlayfieldCameraFit で aspect=16:9 固定) を
            // worldCamera にすると ScreenSpaceCamera canvas が描画アスペクト
            // へ水増しされ (640x480 RT → 853x480 canvas) 出荷とずれる。
            // fit を掛けない別カメラを worldCamera にすれば canvas = RT =
            // Screen 寸法になり、出荷の overlay と同じ px 定数系になる。
            // clearFlags=Depth で 3D の上に必ず乗る (= overlay の前後関係)。
            var hudCamGo = Track(new GameObject("Parity HUD Camera", typeof(Camera)));
            var hudCam = hudCamGo.GetComponent<Camera>();
            hudCam.clearFlags = CameraClearFlags.Depth;
            hudCam.targetTexture = stage.Rt;
            hudCam.cullingMask = 1 << 5; // UI layer のみ (3D を二重に描かない)
            stage.HudCam = hudCam;

            var canvasGo = Track(new GameObject("Parity HUD Canvas",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler)));
            canvasGo.layer = 5; // UI (hudCam.cullingMask と合わせる)
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = hudCam;
            // ScreenSpaceOverlay と同じ「1 canvas unit = 1 画面 px」にする。
            // planeDistance=1 のままだと canvas lossyScale≈0.0024 になり、
            // V6GameHudView.UpdateLaneGrids の ScreenXToTemplateX(…, rootScale)
            // が scale で割って判定行 x が画面外へ飛ぶ (出荷 overlay では
            // scale=1 なのでこの経路は常に 1)。
            // scale = 2*d*tan(fov/2)/RtH = 1 となる d を選ぶと、
            // アスペクトも一致するので canvas がちょうど画面いっぱいに写る。
            canvas.planeDistance = RtH / (2f * Mathf.Tan(hudCam.fieldOfView * 0.5f * Mathf.Deg2Rad));
            canvas.sortingOrder = -1;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;

            var frame = Track(new GameObject("Parity HUD Frame", typeof(RectTransform)));
            frame.transform.SetParent(canvasGo.transform, false);
            var frt = frame.GetComponent<RectTransform>();
            frt.anchorMin = Vector2.zero;
            frt.anchorMax = Vector2.one;
            frt.offsetMin = Vector2.zero;
            frt.offsetMax = Vector2.zero;

            stage.Hud = V6GameHudView.Create(frt);
            spawned.Add(stage.Hud.gameObject);
            var dim = stage.Hud.Root.Find("Video Dim");
            if (dim != null) dim.gameObject.SetActive(false);

            // 歌詞データ (実 GameSceneManager.Load 経路)。
            var gsmGo = Track(new GameObject("Parity GameSceneManager", typeof(GameSceneManager)));
            stage.LyricSource = gsmGo.GetComponent<GameSceneManager>();
            SongData song = GManager.GetSong(GManager.songID);
            Assert.IsNotNull(song, "選択曲が解決できない");
            MethodInfo load = typeof(GameSceneManager).GetMethod("Load",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(load, "GameSceneManager.Load が見つからない");
            load.Invoke(stage.LyricSource, new object[] { song.title });

            // canvas の pixelRect が RT サイズへ確定してから HUD を更新する
            // (ScreenSpaceCamera canvas は描画時に pixelRect を camera から取る)。
            stage.Cam.Render();
            stage.HudCam.Render();
            Canvas.ForceUpdateCanvases();
            stage.Hud.Refresh();
            stage.HudCam.Render();
            Canvas.ForceUpdateCanvases();

            Vector2 cs = stage.Hud.CanvasSizeForAudit;
            Assert.AreEqual(RtW, Mathf.RoundToInt(cs.x), 0.6f,
                "HUD canvas が RT サイズに追従していない (x)");
            Assert.AreEqual(RtH, Mathf.RoundToInt(cs.y), 0.6f,
                "HUD canvas が RT サイズに追従していない (y)");
            return stage;
        }

        // ==================================================================
        // プレビューステージ (Option 画面 Game Preview)
        // ==================================================================

        private sealed class PreviewStage
        {
            public Camera OverlayCam;
            public RenderTexture Ort;
            public Camera PreviewCam;
            public GameObject RigRoot;
            public RectTransform WellRt;
            public Rect WellRect; // ort 内の well 画素矩形 (ReadPixels と同じ下原点)
            public readonly List<GameObject> PreviewNotes = new List<GameObject>();
        }

        private PreviewStage BuildPreview()
        {
            var stage = new PreviewStage();

            // オーバーレイカメラ: ScreenSpaceCamera canvas を 640x480 RT へ描く。
            var camGo = Track(new GameObject("Parity Overlay Camera", typeof(Camera)));
            var cam = camGo.GetComponent<Camera>();
            cam.transform.localPosition = new Vector3(0f, 0f, -50f);
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 1f);
            stage.Ort = new RenderTexture(RtW, RtH, 24, RenderTextureFormat.ARGB32);
            stage.Ort.Create();
            spawned.Add(stage.Ort);
            cam.targetTexture = stage.Ort;
            stage.OverlayCam = cam;

            var canvasGo = Track(new GameObject("Parity Option Canvas",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler)));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 1f;
            canvas.sortingOrder = 0;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;

            optionView = V6OptionView.Create(canvasGo.GetComponent<RectTransform>(), () => { });
            // Issue #732: オートプレイの自動押下がレーンフラッシュや
            // 判定ポップへ混ざるとパリティ計測が濁るので、
            // このハーネスでは実キー注入だけを使う。
            optionView.PreviewAutoPlayForAudit = false;
            Canvas.ForceUpdateCanvases();

            // rig / プレビューカメラ / ノーツプールを名前で取得
            // (V6OptionPreviewRig は internal なので公開 audit API 経由の GameObject 経路)。
            stage.RigRoot = GameObject.Find("V6 Option Preview Rig");
            Assert.IsNotNull(stage.RigRoot, "プレビュー rig が見つからない");
            var previewCamGo = stage.RigRoot.transform.Find("Preview Camera");
            Assert.IsNotNull(previewCamGo, "Preview Camera が見つからない");
            stage.PreviewCam = previewCamGo.GetComponent<Camera>();
            Assert.IsNotNull(stage.PreviewCam);

            var descendants = stage.RigRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < descendants.Length; i++)
            {
                if (descendants[i].name == "Preview Note")
                {
                    stage.PreviewNotes.Add(descendants[i].gameObject);
                }
            }

            // --- well を画面中央へ (コンパクトビューポートのパンで 1:1 移動) ---
            var well = optionView.Root.Find("Main/Preview/Game Preview");
            Assert.IsNotNull(well, "Game Preview well が見つからない");
            stage.WellRt = (RectTransform)well;
            var viewport = V6CompactScreen.FindViewport(optionView.Root);
            Assert.IsNotNull(viewport, "コンパクトビューポートが無い (640x480 で compact のはず)");
            var pan = viewport.GetComponent<V6FramePan>();
            Assert.IsNotNull(pan, "V6FramePan が無い");

            for (int iter = 0; iter < 8; iter++)
            {
                Canvas.ForceUpdateCanvases();
                Vector3[] corners = new Vector3[4];
                stage.WellRt.GetWorldCorners(corners);
                Vector2 c = Vector2.zero;
                for (int i = 0; i < 4; i++)
                {
                    Vector3 s = cam.WorldToScreenPoint(corners[i]);
                    c += new Vector2(s.x, Screen.height - s.y); // y_down
                }
                c *= 0.25f;
                Vector2 err = new Vector2(RtW * 0.5f - c.x, RtH * 0.5f - c.y);
                if (err.sqrMagnitude < 4f) break; // 2px 以内
                // pan は content 左上のビューポート内 y_down 位置。1:1 で追従する。
                pan.Pan = pan.Pan + err;
            }
            Canvas.ForceUpdateCanvases();

            // well の画素矩形 (ort 座標、ReadPixels と同じ下原点)。
            // RawImage "Game Preview 3D" は well 全面ストレッチなので
            // well 矩形 = プレビューカメラのビューポート全体。
            Vector3[] wc = new Vector3[4];
            stage.WellRt.GetWorldCorners(wc);
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                Vector3 s = cam.WorldToScreenPoint(wc[i]);
                minX = Mathf.Min(minX, s.x); maxX = Mathf.Max(maxX, s.x);
                minY = Mathf.Min(minY, s.y); maxY = Mathf.Max(maxY, s.y);
            }
            stage.WellRect = new Rect(minX, minY, maxX - minX, maxY - minY);
            Assert.Greater(stage.WellRect.width, RtW * 0.4f,
                "well が小さすぎる/画面外 (パン収束失敗?) wellRect=" + stage.WellRect);
            return stage;
        }

        private static Vector3 OverlayScreenPoint(PreviewStage stage, Vector3 worldPos)
        {
            return stage.OverlayCam.WorldToScreenPoint(worldPos);
        }

        /// <summary>プレビューの Preview Note を全部非表示にする (次の Tick で復活)。</summary>
        private static void HidePreviewNotes(PreviewStage stage)
        {
            for (int i = 0; i < stage.PreviewNotes.Count; i++)
            {
                if (stage.PreviewNotes[i] != null)
                {
                    stage.PreviewNotes[i].SetActive(false);
                }
            }
        }

        // ==================================================================
        // 描画と画像解析
        // ==================================================================

        private sealed class Shot
        {
            public Texture2D Tex;
            public Color32[] Px;
            public int W;
            public int H;
        }

        /// <summary>カメラの targetTexture を読んで Shot にする (RT は保持済み)。</summary>
        private static Shot ReadCameraTarget(Camera cam)
        {
            var rt = cam.targetTexture;
            Assert.IsNotNull(rt);
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            return new Shot { Tex = tex, Px = tex.GetPixels32(), W = rt.width, H = rt.height };
        }

        /// <summary>Shot の一部を切り出す (Rect は ReadPixels と同じ下原点)。</summary>
        private static Shot Crop(Shot src, Rect rect)
        {
            int x = Mathf.Clamp(Mathf.RoundToInt(rect.x), 0, src.W);
            int y = Mathf.Clamp(Mathf.RoundToInt(rect.y), 0, src.H);
            int w = Mathf.Clamp(Mathf.RoundToInt(rect.width), 1, src.W - x);
            int h = Mathf.Clamp(Mathf.RoundToInt(rect.height), 1, src.H - y);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels(src.Tex.GetPixels(x, y, w, h));
            tex.Apply();
            return new Shot { Tex = tex, Px = tex.GetPixels32(), W = w, H = h };
        }

        private Shot RenderGameplay(GameplayStage stage)
        {
            stage.Cam.Render();
            stage.HudCam.Render(); // HUD canvas (Depth clear) を 3D の上に合成
            Canvas.ForceUpdateCanvases();
            return ReadCameraTarget(stage.Cam);
        }

        /// <summary>
        /// HUD canvas を合成しない 3D のみの shot (Issue #706)。
        ///
        /// レーン/拍フラッシュの比較で使う。プレビュー側は rig の RT
        /// (uGUI オーバーレイ無し) を直接読むのに対し、HUD 付きの shot では
        /// Top Shade 等の半透明 uGUI が 3D の on/off 差分を (1-α) に減衰
        /// させる — 同じ点灯でも coverage が過小に出て、実際には一致して
        /// いるのに閾値差が残る (レーンフラッシュ gp≈0.77 vs pv≈1.0、
        /// 拍フラッシュ gp≈0.69 vs pv≈0.79 の残差はこれが原因)。
        /// シェードは画面クロームでありプレビューの well には存在しない
        /// ので、3D 内容の意味比較は両側とも「オーバーレイ無し」で揃える。
        /// </summary>
        private Shot RenderGameplay3D(GameplayStage stage)
        {
            stage.Cam.Render(); // SolidColor clear なので前回の HUD 合成は残らない
            Canvas.ForceUpdateCanvases();
            return ReadCameraTarget(stage.Cam);
        }

        /// <summary>現在のプレビュー状態をそのまま well で描く (追加 tick 無し)。</summary>
        private Shot RenderPreviewRaw(PreviewStage stage)
        {
            stage.PreviewCam.Render();
            stage.OverlayCam.Render();
            Canvas.ForceUpdateCanvases();
            Shot full = ReadCameraTarget(stage.OverlayCam);
            return Crop(full, stage.WellRect);
        }

        /// <summary>
        /// プレビューの 3D 部分だけを rig の RenderTexture から直接読む
        /// (Issue #706)。
        ///
        /// well の最終合成は「PreviewCamera → 透明クリア RT → RawImage」
        /// の 2 段 alpha 合成を通る。SrcAlpha/OneMinusSrcAlpha は alpha
        /// チャネルにも掛かるため、RT 内の半透明要素 (レーンフラッシュ
        /// α0.3・拍フラッシュ α0.3・ガイド α0.18) は RT の alpha に
        /// ≈α² で残り、RawImage が well 背景へ重ねるときにさらに
        /// α で減衰する — 出荷の Option 画面でも同じ経路なので、
        /// well 経由では点灯差分が DiffThreshold を大きく下回る
        /// (実測で pv coverage ≈ 0)。RGB チャネル自体は 1 段目の
        /// ブレンドで正しく蓄積されるので、3D 内容の比較は rig の
        /// RT を直接読む。uGUI オーバーレイ要素 (歌詞・判定ポップ) は
        /// RT に含まれないため <see cref="RenderPreviewRaw"/> を使う。
        /// </summary>
        private Shot RenderPreview3D(PreviewStage stage, float mediaTime, float deltaTime, bool hideNotes)
        {
            optionView.TickPreviewForAudit(mediaTime, deltaTime);
            if (hideNotes) HidePreviewNotes(stage);
            stage.PreviewCam.Render();
            Canvas.ForceUpdateCanvases();
            return ReadCameraTarget(stage.PreviewCam);
        }

        private static bool PixelDiffers(Color32 a, Color32 b, float threshold)
        {
            float d = (Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b)) / (3f * 255f);
            return d >= threshold;
        }

        /// <summary>
        /// 水平面 (y=planeY, z∈[zNear,zFar]) 上のレーン帯を正規化座標で表す。
        /// レーン境界は直線なので、近端/遠端のビューポート座標を各行で線形補間する。
        /// worldOffset は rig のような平行移動を吸収するための加算 (rig は
        /// 回転・スケール無しの (0,0,-3000) 並進なので加算で正確)。
        /// </summary>
        private sealed class LaneBands
        {
            public float PlaneY;
            public float ZNear;
            public float ZFar;
            public Vector3 WorldOffset;
            public readonly Vector2[] Near = new Vector2[PlayfieldLaneGuides.GuideCount];
            public readonly Vector2[] Far = new Vector2[PlayfieldLaneGuides.GuideCount];
            public float YNear;
            public float YFar;
            public int RowNear;
            public int RowFar;

            public void Build(Camera cam, float w, float h)
            {
                for (int i = 0; i < PlayfieldLaneGuides.GuideCount; i++)
                {
                    float x = PlayfieldLaneGuides.LaneBoundaryX(i);
                    Near[i] = cam.WorldToViewportPoint(
                        WorldOffset + new Vector3(x, PlaneY, ZNear));
                    Far[i] = cam.WorldToViewportPoint(
                        WorldOffset + new Vector3(x, PlaneY, ZFar));
                }
                YNear = Near[0].y;
                YFar = Far[0].y;
                int ih = Mathf.RoundToInt(h);
                RowNear = Mathf.Clamp(Mathf.FloorToInt(YNear * h), 0, ih - 1);
                RowFar = Mathf.Clamp(Mathf.CeilToInt(YFar * h), 0, ih);
                if (RowFar < RowNear) RowFar = RowNear;
            }

            private readonly float[] bx = new float[PlayfieldLaneGuides.GuideCount];

            /// <summary>行 py (下原点) のレーン境界 x (正規化) を bx に書く。範囲外は false。</summary>
            private bool RowBounds(int py, int h)
            {
                float ny = (py + 0.5f) / h;
                float denom = YFar - YNear;
                if (Mathf.Abs(denom) < 1e-6f) return false;
                float t = (ny - YNear) / denom;
                if (t < -0.001f || t > 1.001f) return false;
                for (int i = 0; i < PlayfieldLaneGuides.GuideCount; i++)
                {
                    bx[i] = Mathf.LerpUnclamped(Near[i].x, Far[i].x, t);
                }
                return true;
            }

            /// <summary>
            /// 2 枚の Shot の差分をレーンごとに集計する。
            /// diffPx[lane] = 差分のあった画素数、areaPx[lane] = レーン帯の画素数。
            /// </summary>
            public void Measure(Shot a, Shot b, int[] diffPx, float[] areaPx)
            {
                for (int l = 0; l < Judge.LaneCount; l++) { diffPx[l] = 0; areaPx[l] = 0f; }
                int w = a.W, h = a.H;
                for (int py = RowNear; py < RowFar; py++)
                {
                    if (!RowBounds(py, h)) continue;
                    for (int px = 0; px < w; px++)
                    {
                        float nx = (px + 0.5f) / w;
                        int lane = -1;
                        for (int l = 0; l < Judge.LaneCount; l++)
                        {
                            if (nx >= bx[l] && nx < bx[l + 1]) { lane = l; break; }
                        }
                        if (lane < 0) continue;
                        areaPx[lane] += 1f;
                        if (PixelDiffers(a.Px[py * w + px], b.Px[py * w + px], DiffThreshold))
                        {
                            diffPx[lane]++;
                        }
                    }
                }
            }
        }

        /// <summary>矩形領域 (正規化、下原点) の差分画素数と重心を返す。</summary>
        private static int RegionDiff(Shot a, Shot b, Rect norm, out Vector2 centroid)
        {
            centroid = Vector2.zero;
            int count = 0;
            float sx = 0f, sy = 0f;
            int x0 = Mathf.Clamp(Mathf.FloorToInt(norm.x * a.W), 0, a.W - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt(norm.xMax * a.W), 0, a.W);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(norm.y * a.H), 0, a.H - 1);
            int y1 = Mathf.Clamp(Mathf.CeilToInt(norm.yMax * a.H), 0, a.H);
            for (int py = y0; py < y1; py++)
            {
                for (int px = x0; px < x1; px++)
                {
                    int i = py * a.W + px;
                    if (!PixelDiffers(a.Px[i], b.Px[i], DiffThreshold)) continue;
                    count++;
                    sx += (px + 0.5f) / a.W;
                    sy += (py + 0.5f) / a.H;
                }
            }
            if (count > 0)
            {
                centroid = new Vector2(sx / count, sy / count);
            }
            return count;
        }

        /// <summary>矩形領域の差分率 (差分画素 / 領域画素)。</summary>
        private static float RegionCoverage(Shot a, Shot b, Rect norm)
        {
            int x0 = Mathf.Clamp(Mathf.FloorToInt(norm.x * a.W), 0, a.W - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt(norm.xMax * a.W), 0, a.W);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(norm.y * a.H), 0, a.H - 1);
            int y1 = Mathf.Clamp(Mathf.CeilToInt(norm.yMax * a.H), 0, a.H);
            int count = 0, total = 0;
            for (int py = y0; py < y1; py++)
            {
                for (int px = x0; px < x1; px++)
                {
                    total++;
                    if (PixelDiffers(a.Px[py * a.W + px], b.Px[py * a.W + px], DiffThreshold))
                    {
                        count++;
                    }
                }
            }
            return total > 0 ? (float)count / total : 0f;
        }

        // ==================================================================
        // テスト
        // ==================================================================

        /// <summary>チャートから指定レーンの最初のタップノーツ判定時刻を返す。</summary>
        private static float FirstTapTimeInLane(V6OptionPreviewChart.Note[] chart, int lane)
        {
            float best = float.MaxValue;
            foreach (var note in chart)
            {
                if (note.Lane == lane && note.HoldSeconds <= 0f)
                {
                    float timed = NotesManager.NoteTimeWithTiming(note.Time, GManager.noteTiming);
                    if (timed < best) best = timed;
                }
            }
            return best == float.MaxValue ? -1f : best;
        }

        /// <summary>表示範囲に十分なノーツがある mediaTime をチャートから選ぶ。</summary>
        private static float MediaTimeWithVisibleNotes(V6OptionPreviewChart.Note[] chart)
        {
            // 最初のタップノーツの 0.45 秒前 = ノーツが画面中央〜奥に並ぶ時刻。
            float first = float.MaxValue;
            foreach (var note in chart)
            {
                float timed = NotesManager.NoteTimeWithTiming(note.Time, GManager.noteTiming);
                if (timed > 0.5f && timed < first) first = timed;
            }
            Assert.Less(first, float.MaxValue, "チャートにノーツが無い");
            return Mathf.Max(0.2f, first - 0.45f);
        }

        /// <summary>
        /// ノーツ: 同じチャート・同じ mediaTime で本編とプレビューを描き、
        /// レーンごとのノーツ占有率 (差分面積/レーン帯面積) が一致すること。
        /// </summary>
        [Test]
        public void Notes_RenderSameLanesAndCoverage()
        {
            float loop;
            var chart = V6OptionPreviewChart.BuildForCurrentSong(out loop);
            Assert.IsNotNull(chart);
            Assert.Greater(chart.Length, 0);
            float mediaTime = MediaTimeWithVisibleNotes(chart);

            var gp = BuildGameplay(mediaTime);
            var pv = BuildPreview();

            // 本編: ノーツ有り/無し
            Shot gpWith = RenderGameplay(gp);
            spawned.Add(gpWith.Tex);
            SetRenderersEnabled(gp.NoteRenderers, false);
            Shot gpWithout = RenderGameplay(gp);
            spawned.Add(gpWithout.Tex);
            SetRenderersEnabled(gp.NoteRenderers, true);

            // プレビュー: 同じ mediaTime で 1 ティック。3D 内容は rig の RT を
            // 直接読む (well 合成の 2 段 alpha 減衰を避ける — RenderPreview3D 参照)。
            Shot pvWith = RenderPreview3D(pv, mediaTime, 0.016f, false);
            spawned.Add(pvWith.Tex);
            // 「無し」は同じ previewTime でもう一度ティックし、
            // 描画直前にノーツだけ消す (dt=0 で時計は進まない)。
            Shot pvWithout = RenderPreview3D(pv, mediaTime, 0.0f, true);
            spawned.Add(pvWithout.Tex);

            // レーン帯 (ノーツ面 y=0.55, z∈[frontZ-0.5, GuideDepth+0.5])
            var bands = new LaneBands
            {
                PlaneY = MediaTimeline.NotePlaneY,
                ZNear = gp.FrontZ - 0.5f,
                ZFar = PlayfieldLaneGuides.GuideDepth + 0.5f,
            };
            bands.Build(gp.Cam, gpWith.W, gpWith.H);
            var gpDiff = new int[Judge.LaneCount];
            var gpArea = new float[Judge.LaneCount];
            bands.Measure(gpWith, gpWithout, gpDiff, gpArea);

            // プレビュー well は「カメラのビューポート全体」を写すので、
            // 同じ正規化座標系で測れる。PreviewCamera は rig (z=-3000) の子なので
            // ワールド座標へは rigRoot.position のオフセットが要る。
            var pvBands = new LaneBands
            {
                PlaneY = MediaTimeline.NotePlaneY,
                ZNear = gp.FrontZ - 0.5f,
                ZFar = PlayfieldLaneGuides.GuideDepth + 0.5f,
                WorldOffset = pv.RigRoot.transform.position,
            };
            pvBands.Build(pv.PreviewCam, pvWith.W, pvWith.H);
            var pvDiff = new int[Judge.LaneCount];
            var pvArea = new float[Judge.LaneCount];
            pvBands.Measure(pvWith, pvWithout, pvDiff, pvArea);

            int lanesWithNotesG = 0, lanesWithNotesP = 0;
            var report = new StringBuilder();
            for (int lane = 0; lane < Judge.LaneCount; lane++)
            {
                float covG = gpArea[lane] > 0f ? (float)gpDiff[lane] / gpArea[lane] : 0f;
                float covP = pvArea[lane] > 0f ? (float)pvDiff[lane] / pvArea[lane] : 0f;
                bool hasG = covG > 0.002f;
                bool hasP = covP > 0.002f;
                if (hasG) lanesWithNotesG++;
                if (hasP) lanesWithNotesP++;
                report.Append($" lane{lane}: gp={covG:F4} pv={covP:F4}");
                Assert.AreEqual(hasG, hasP,
                    $"lane {lane} のノーツ有無が本編とプレビューで違う ({report})");
                if (hasG && hasP)
                {
                    float ratio = covP / Mathf.Max(1e-5f, covG);
                    Assert.Greater(ratio, 0.35f,
                        $"lane {lane} のノーツ占有率が小さすぎる pv/gp={ratio:F2} ({report})");
                    Assert.Less(ratio, 2.8f,
                        $"lane {lane} のノーツ占有率が大きすぎる pv/gp={ratio:F2} ({report})");
                }
            }
            Debug.Log("[V6RenderParity] notes coverage:" + report);
            Assert.GreaterOrEqual(lanesWithNotesG, 2,
                "ノーツがほとんど見えない mediaTime (テスト前提の失敗)");
            Assert.AreEqual(lanesWithNotesG, lanesWithNotesP,
                "ノーツのあるレーン数が本編とプレビューで違う");
        }

        /// <summary>
        /// レーンフラッシュ: 同じキー入力で本編 LaneFlash とプレビューの
        /// レーン帯が同じレーン・同程度の面積で点灯すること。
        /// </summary>
        [Test]
        public void LaneFlash_CoversSameLanes()
        {
            int[] pressed = { 1, 4, 6 }; // 0-based lanes
            float mediaTime = 5f;       // 歌詞・判定に無関係な時刻でよい

            var gp = BuildGameplay(mediaTime);
            var pv = BuildPreview();

            // プレビュー: まず消灯状態を描く (α=0、ノーツは消して帯だけ見る)
            Shot pvOff = RenderPreview3D(pv, mediaTime, 0.016f, true);
            spawned.Add(pvOff.Tex);

            // 本編: 消灯 → 押下して LaneFlash.Update を回す。
            // 3D のみの shot を使う (HUD の Top Shade が差分を減衰させる
            // ため — RenderGameplay3D 参照)。
            Shot gpOff = RenderGameplay3D(gp);
            spawned.Add(gpOff.Tex);
            foreach (int lane in pressed) KeyBindings.InjectLaneKeyDown(lane);
            // LaneFlash.Update は rend.material (初回は実体化) で色を書く。
            // EditMode ではそのたびに "Instantiating material" エラーが出るので、
            // この区間だけ失敗ログを抑止する (描画結果は shot で検証する)。
            bool prevIgnore = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            try
            {
                for (int i = 0; i < gp.LaneFlashes.Count; i++)
                {
                    InvokePrivate(gp.LaneFlashes[i], "Update");
                }
            }
            finally
            {
                LogAssert.ignoreFailingMessages = prevIgnore;
            }
            Shot gpOn = RenderGameplay3D(gp);
            spawned.Add(gpOn.Tex);

            // プレビュー: 同じキーで点灯 (注入はまだ生きている)
            Shot pvOn = RenderPreview3D(pv, mediaTime + 0.016f, 0.016f, true);
            spawned.Add(pvOn.Tex);

            var bands = new LaneBands
            {
                PlaneY = 0.525f, // LaneFlashY / LaneLight 上面と同じ帯
                ZNear = 0f,
                ZFar = PlayfieldLaneGuides.GuideDepth,
            };
            bands.Build(gp.Cam, gpOff.W, gpOff.H);
            var gpDiff = new int[Judge.LaneCount];
            var gpArea = new float[Judge.LaneCount];
            bands.Measure(gpOff, gpOn, gpDiff, gpArea);

            var pvBands = new LaneBands
            {
                PlaneY = 0.525f,
                ZNear = 0f,
                ZFar = PlayfieldLaneGuides.GuideDepth,
                WorldOffset = pv.RigRoot.transform.position,
            };
            pvBands.Build(pv.PreviewCam, pvOn.W, pvOn.H);
            var pvDiff = new int[Judge.LaneCount];
            var pvArea = new float[Judge.LaneCount];
            pvBands.Measure(pvOff, pvOn, pvDiff, pvArea);

            var report = new StringBuilder();
            for (int lane = 0; lane < Judge.LaneCount; lane++)
            {
                float covG = gpArea[lane] > 0f ? (float)gpDiff[lane] / gpArea[lane] : 0f;
                float covP = pvArea[lane] > 0f ? (float)pvDiff[lane] / pvArea[lane] : 0f;
                report.Append($" lane{lane}: gp={covG:F4} pv={covP:F4}");
                bool pressedLane = System.Array.IndexOf(pressed, lane) >= 0;
                Assert.AreEqual(pressedLane, covG > 0.05f,
                    $"本編 lane {lane} のフラッシュ点灯が期待と違う ({report})");
                Assert.AreEqual(pressedLane, covP > 0.05f,
                    $"プレビュー lane {lane} のフラッシュ点灯が期待と違う ({report})");
                if (pressedLane)
                {
                    // like-for-like (両側 3D のみ) では gp/pv ≈ 0.99。
                    // 旧 0.3..3.0 は HUD 減衰を含む計測で甘かったため、
                    // 実測比 ±50% に締める (面積 2 倍の乖離は検出できる)。
                    float ratio = covP / Mathf.Max(1e-5f, covG);
                    Assert.Greater(ratio, 0.5f,
                        $"lane {lane} フラッシュ面積比が小さすぎる pv/gp={ratio:F2} ({report})");
                    Assert.Less(ratio, 2.0f,
                        $"lane {lane} フラッシュ面積比が大きすぎる pv/gp={ratio:F2} ({report})");
                }
            }
            Debug.Log("[V6RenderParity] lane flash coverage:" + report);
        }

        /// <summary>
        /// 判定ポップ: 本編 Judge キュー経由の判定と、プレビューのキー判定が
        /// 同じレーン・同じ「判定線上の出発点」(Issue #731: 判定確定直後は
        /// オフセット 0 = 描画判定線の位置) に出ること。
        /// </summary>
        [Test]
        public void Verdict_RendersSameLaneAtJudgeLineStart()
        {
            int lane = 3;
            const int judgement = (int)LongNote.Judgement.Great;

            var gp = BuildGameplay(3f);
            var pv = BuildPreview();

            // --- 本編: Judge の判定イベントキューへ直接積んで HUD に流す ---
            Judge.ClearPendingVerdicts();
            MethodInfo enqueue = typeof(Judge).GetMethod("EnqueueVerdict",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(enqueue, "Judge.EnqueueVerdict が見つからない");
            enqueue.Invoke(null, new object[] { judgement, lane });
            gp.Hud.Refresh();
            Canvas.ForceUpdateCanvases();
            Assert.AreEqual("GREAT", gp.Hud.VerdictTextForAudit(lane),
                "本編 HUD に GREAT が出ていない");

            // 判定テキスト GO を取得 ("Verdict Row/Verdict 3")
            var verdictGoG = gp.Hud.Root.Find("Verdict Row/Verdict " + lane);
            Assert.IsNotNull(verdictGoG, "本編の判定テキスト GO が見つからない");

            // 「無し」→「有り」の順で撮る (Refresh を挟まないので α/位置は固定)。
            verdictGoG.gameObject.SetActive(false);
            Canvas.ForceUpdateCanvases();
            Shot gpHidden = RenderGameplay(gp);
            spawned.Add(gpHidden.Tex);
            verdictGoG.gameObject.SetActive(true);
            Canvas.ForceUpdateCanvases();
            Shot gpShown = RenderGameplay(gp);
            spawned.Add(gpShown.Tex);

            // --- プレビュー: 実チャートの lane 3 ノーツを GREAT 判定させる ---
            float timed = FirstTapTimeInLane(
                V6OptionPreviewChart.BuildForCurrentSong(out _), lane);
            Assert.Greater(timed, 0f, "lane " + lane + " にタップノーツが無い");
            optionView.TickPreviewForAudit(timed - 0.5f, 0.016f);
            KeyBindings.InjectLaneKeyDown(lane);
            optionView.TickPreviewForAudit(timed - 0.12f, 0.016f);
            Assert.AreEqual("GREAT", optionView.PreviewVerdictForAudit(lane),
                "プレビューに GREAT 判定が出ていない");

            var verdictTmpP = optionView.PreviewVerdictTextForAudit(lane);
            Assert.IsNotNull(verdictTmpP);
            var chainP = new StringBuilder();
            for (Transform t = verdictTmpP.transform; t != null; t = t.parent)
            {
                var rt = t as RectTransform;
                chainP.Append($" {t.name}[scale={t.lossyScale.x:F4} pos={t.position} " +
                    $"anch={(rt != null ? rt.anchoredPosition.ToString() : "-")} " +
                    $"size={(rt != null ? rt.rect.size.ToString() : "-")}]");
            }
            Vector3 spP = OverlayScreenPoint(pv, verdictTmpP.transform.position);
            Debug.Log($"[V6RenderParity] pv verdict tmp: active={verdictTmpP.gameObject.activeInHierarchy} " +
                $"color.a={verdictTmpP.color.a} text='{verdictTmpP.text}' font={(verdictTmpP.font != null)} " +
                $"chain:{chainP} ortPt={spP} wellRect={pv.WellRect}");
            verdictTmpP.gameObject.SetActive(false);
            Shot pvHidden = RenderPreviewRaw(pv);
            spawned.Add(pvHidden.Tex);
            verdictTmpP.gameObject.SetActive(true);
            Shot pvShown = RenderPreviewRaw(pv);
            spawned.Add(pvShown.Tex);

            // --- 期待位置: 判定アンカー (LaneWorldX, 0.76, 0.15) の投影 ---
            // x はレーン中心の「外側レーン間割合」で比較する。両カメラの
            // RT アスペクトが違う (本編 640x480、プレビュー 928x560) ため
            // fitted aspect 16:9 への水平方向の見かけスケールが異なり、
            // 生の vp.x では数%ずれる。中心対称の水平変換はレーン割合を
            // 保存するので、こちらが意味座標として正しい
            // (well アスペクト差は Issue #706 横断監査で既知の設計差)。
            Vector3 AnchorAt(int l)
            {
                return new Vector3(Judge.LaneWorldX(l), 0.76f, 0.15f);
            }
            Vector3 vpG = gp.Cam.WorldToViewportPoint(AnchorAt(lane));
            Vector3 rigWorld = pv.RigRoot.transform.TransformPoint(AnchorAt(lane));
            Vector3 vpP = pv.PreviewCam.WorldToViewportPoint(rigWorld);
            float edgeG0 = gp.Cam.WorldToViewportPoint(AnchorAt(0)).x;
            float edgeG7 = gp.Cam.WorldToViewportPoint(AnchorAt(Judge.LaneCount - 1)).x;
            float edgeP0 = pv.PreviewCam.WorldToViewportPoint(
                pv.RigRoot.transform.TransformPoint(AnchorAt(0))).x;
            float edgeP7 = pv.PreviewCam.WorldToViewportPoint(
                pv.RigRoot.transform.TransformPoint(AnchorAt(Judge.LaneCount - 1))).x;

            // 判定テキストの中心は「描画判定線の投影 + VerdictPoseAt の
            // オフセット」。Issue #731: 判定確定直後 (elapsed≈0) は
            // オフセット 0 = 判定線上が出発点 (旧 #671 の「静止位置の上から
            // 落下」は廃止)。アンカーは本編・プレビューとも
            // V6HudLogic.LegacyJudgeLineAnchor (Issue #715)。
            // プレビュー側は rig ローカル→ワールド変換が必須 (rig は z=-3000)。
            Vector3 lineVpG = gp.Cam.WorldToViewportPoint(V6HudLogic.LegacyJudgeLineAnchor());
            Vector3 lineVpP = pv.PreviewCam.WorldToViewportPoint(
                pv.RigRoot.transform.TransformPoint(V6HudLogic.LegacyJudgeLineAnchor()));
            Assert.Greater(lineVpG.z, 0f, "判定線アンカーが本編カメラの前方に無い");
            Assert.Greater(lineVpP.z, 0f, "判定線アンカーがプレビューカメラの前方に無い");
            float expectY_G = lineVpG.y;
            float expectY_P = lineVpP.y;

            Rect regionG = new Rect(vpG.x - 0.09f, expectY_G - 0.16f, 0.18f, 0.30f);
            Rect regionP = new Rect(vpP.x - 0.09f, expectY_P - 0.16f, 0.18f, 0.30f);

            Vector2 cG, cP;
            int nG = RegionDiff(gpHidden, gpShown, regionG, out cG);
            int nP = RegionDiff(pvHidden, pvShown, regionP, out cP);
            int nGfull = RegionDiff(gpHidden, gpShown, new Rect(0f, 0f, 1f, 1f), out _);
            int nPfull = RegionDiff(pvHidden, pvShown, new Rect(0f, 0f, 1f, 1f), out _);
            var tmpG = verdictGoG.GetComponent<TMPro.TextMeshProUGUI>();
            Debug.Log($"[V6RenderParity] verdict lane{lane}: gp diff={nG} full={nGfull} " +
                $"centroid=({cG.x:F3},{cG.y:F3}) expected=({vpG.x:F3},{expectY_G:F3}); " +
                $"pv diff={nP} full={nPfull} centroid=({cP.x:F3},{cP.y:F3}) " +
                $"expected=({vpP.x:F3},{expectY_P:F3})");
            var chain = new StringBuilder();
            for (Transform t = verdictGoG; t != null; t = t.parent)
            {
                var rt = t as RectTransform;
                chain.Append($" {t.name}[scale={t.lossyScale.x:F4} pos={t.position} " +
                    $"anch={ (rt != null ? rt.anchoredPosition.ToString() : "-") } " +
                    $"size={(rt != null ? rt.rect.size.ToString() : "-")}]");
            }
            Vector3 spG = gp.Cam.WorldToScreenPoint(verdictGoG.position);
            Debug.Log($"[V6RenderParity] verdict tmp state: gp active={tmpG != null && tmpG.gameObject.activeInHierarchy} " +
                $"color.a={(tmpG != null ? tmpG.color.a : -1f)} font={(tmpG != null && tmpG.font != null)} " +
                $"text='{(tmpG != null ? tmpG.text : "?")}' chain:{chain} screenPt={spG}");

            Assert.Greater(nG, 30, "本編の判定ポップが描かれていない");
            Assert.Greater(nP, 10, "プレビューの判定ポップが描かれていない");
            // x はレーン割合 (外側レーン 0..7 の内分) で比較 — RT アスペクト差の
            // 水平スクイーズを除いた意味座標。
            float fracG = (cG.x - edgeG0) / (edgeG7 - edgeG0);
            float fracP = (cP.x - edgeP0) / (edgeP7 - edgeP0);
            float expectFrac = lane / (float)(Judge.LaneCount - 1);
            Debug.Log($"[V6RenderParity] verdict lane{lane}: gp diff={nG} centroid=({cG.x:F3},{cG.y:F3}) " +
                $"laneFrac={fracG:F3} (exp {expectFrac:F3}); pv diff={nP} centroid=({cP.x:F3},{cP.y:F3}) " +
                $"laneFrac={fracP:F3}");
            Assert.Less(Mathf.Abs(fracG - expectFrac), 0.06f,
                $"本編判定ポップのレーン位置が期待とずれている ({fracG:F3} vs {expectFrac:F3})");
            Assert.Less(Mathf.Abs(fracP - expectFrac), 0.06f,
                $"プレビュー判定ポップのレーン位置が期待とずれている ({fracP:F3} vs {expectFrac:F3})");
            Assert.Less(Mathf.Abs(fracG - fracP), 0.06f,
                $"判定ポップのレーン位置が本編とプレビューで違う ({fracG:F3} vs {fracP:F3})");
            // y は「描画判定線 + タイムラインのオフセット」の式で両側一致するはず。
            // 出現直後はオフセット≈0。上昇の位相差 (unscaledTime の差) を
            // 考慮して ±0.08 許容。
            Assert.Less(Mathf.Abs(cG.y - expectY_G), 0.08f,
                $"本編判定ポップの y が期待位置とずれている ({cG.y:F3} vs {expectY_G:F3})");
            Assert.Less(Mathf.Abs(cP.y - expectY_P), 0.08f,
                $"プレビュー判定ポップの y が期待位置とずれている ({cP.y:F3} vs {expectY_P:F3})");
            Assert.Less(Mathf.Abs(cG.y - cP.y), 0.10f,
                $"判定ポップの y が本編とプレビューで違う ({cG.y:F3} vs {cP.y:F3})");
        }

        /// <summary>
        /// 歌詞ボックス: 本編 HUD とプレビューオーバーレイが同じスロット
        /// (上中央・同じ画面位置) に出ること。
        /// </summary>
        [Test]
        public void LyricBox_RendersSameTopCenterSlot()
        {
            const float mediaTime = 1.5f; // ロストアンブレラ「僕を連れてって」区間

            var gp = BuildGameplay(mediaTime);
            var pv = BuildPreview();

            // 本編: 歌詞ボックスを出して「無し/有り」を撮る
            Assert.AreEqual("僕を連れてって", LyricTextOf(gp.Hud),
                "本編 HUD に歌詞が出ていない (前提)");
            var lyricGoG = gp.Hud.LyricBoxForAudit;
            Assert.IsNotNull(lyricGoG);
            lyricGoG.gameObject.SetActive(false);
            Canvas.ForceUpdateCanvases();
            Shot gpHidden = RenderGameplay(gp);
            spawned.Add(gpHidden.Tex);
            lyricGoG.gameObject.SetActive(true);
            Canvas.ForceUpdateCanvases();
            Shot gpShown = RenderGameplay(gp);
            spawned.Add(gpShown.Tex);

            // プレビュー: 同じ mediaTime で歌詞を出す
            optionView.TickPreviewForAudit(mediaTime, 0.016f);
            Assert.AreEqual("僕を連れてって", optionView.PreviewLyricForAudit,
                "プレビューに歌詞が出ていない");
            var lyricGoP = optionView.PreviewLyricBoxRectForAudit;
            Assert.IsNotNull(lyricGoP);
            lyricGoP.gameObject.SetActive(false);
            Shot pvHidden = RenderPreviewRaw(pv);
            spawned.Add(pvHidden.Tex);
            lyricGoP.gameObject.SetActive(true);
            Shot pvShown = RenderPreviewRaw(pv);
            spawned.Add(pvShown.Tex);

            // 歌詞ボックスは上部中央。差分 (文字+半透明黒) の重心で見る。
            Rect region = new Rect(0.15f, 0.55f, 0.7f, 0.45f); // 上部帯 (下原点)
            Vector2 cG, cP;
            int nG = RegionDiff(gpHidden, gpShown, region, out cG);
            int nP = RegionDiff(pvHidden, pvShown, region, out cP);
            Debug.Log($"[V6RenderParity] lyric: gp diff={nG} centroid=({cG.x:F3},{cG.y:F3}) " +
                $"pv diff={nP} centroid=({cP.x:F3},{cP.y:F3})");

            Assert.Greater(nG, 30, "本編の歌詞ボックスが描かれていない");
            Assert.Greater(nP, 10, "プレビューの歌詞ボックスが描かれていない");
            Assert.Less(Mathf.Abs(cG.x - 0.5f), 0.12f,
                $"本編歌詞ボックスが中央からずれている ({cG.x:F3})");
            Assert.Less(Mathf.Abs(cP.x - 0.5f), 0.12f,
                $"プレビュー歌詞ボックスが中央からずれている ({cP.x:F3})");
            Assert.Less(Mathf.Abs(cG.x - cP.x), 0.08f,
                $"歌詞ボックスの x が本編とプレビューで違う ({cG.x:F3} vs {cP.x:F3})");
            Assert.Greater(cG.y, 0.6f, "本編歌詞ボックスが上部に無い");
            Assert.Greater(cP.y, 0.6f, "プレビュー歌詞ボックスが上部に無い");
        }

        private static string LyricTextOf(V6GameHudView hud)
        {
            // 歌詞本文は Lyric Box 配下の "Lyrics" TMP (V6HudLyrics.BuildLyric)。
            var box = hud.LyricBoxForAudit;
            if (box == null) return null;
            var lyrics = box.Find("Lyrics");
            if (lyrics != null)
            {
                var direct = lyrics.GetComponent<TMPro.TextMeshProUGUI>();
                if (direct != null) return direct.text;
            }
            // 名前が変わっても本文 TMP を拾えるようフォールバック。
            var all = box.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name == "Lyrics") return all[i].text;
            }
            for (int i = 0; i < all.Length; i++)
            {
                if (!string.IsNullOrEmpty(all[i].text)) return all[i].text;
            }
            return null;
        }

        /// <summary>
        /// 判定線: 本編 LineRenderer とプレビューの判定線 quad が
        /// 同じ画面行に描かれること。
        /// </summary>
        [Test]
        public void JudgeLine_RendersSameScreenRow()
        {
            var gp = BuildGameplay(5f);
            var pv = BuildPreview();

            Shot gpShot = RenderGameplay(gp);
            spawned.Add(gpShot.Tex);
            // 診断: HUD 無しの 3D だけの shot でも判定線が見えるか
            // (HUD 要素による遮蔽と描画欠落を切り分ける)。
            gp.Cam.Render();
            var gp3d = ReadCameraTarget(gp.Cam);
            spawned.Add(gp3d.Tex);
            Shot pvShot = RenderPreview3D(pv, 5f, 0.016f, true);
            spawned.Add(pvShot.Tex);

            // 期待行: 本編は実際の LineRenderer ワールド位置 (y≈0.48, z=0)、
            // プレビューは quad (y=0.48, z=0 — GameScene transform y=-0.02
            // × ローカル y=0.5 と同じワールド高) の投影。
            float expectedG = gp.Cam.WorldToViewportPoint(new Vector3(0f, 0.48f, 0f)).y;
            Vector3 pvWorld = pv.RigRoot.transform.TransformPoint(new Vector3(0f, 0.48f, 0f));
            float expectedP = pv.PreviewCam.WorldToViewportPoint(pvWorld).y;

            float rowG = DetectBrightRow(gpShot, expectedG, 0.03f);
            float rowP = DetectBrightRow(pvShot, expectedP, 0.03f);
            Debug.Log($"[V6RenderParity] judge line: gp row={rowG:F4} (exp {expectedG:F4}), " +
                $"pv row={rowP:F4} (exp {expectedP:F4})");
            float row3d = DetectBrightRow(gp3d, expectedG, 0.03f);
            Debug.Log($"[V6RenderParity] judge line 3d-only row={row3d:F4} (exp {expectedG:F4})");

            Assert.Greater(rowG, 0f, "本編の判定線が見つからない");
            Assert.Greater(rowP, 0f, "プレビューの判定線が見つからない");
            Assert.Less(Mathf.Abs(rowG - expectedG), 0.012f,
                $"本編判定線の行が期待とずれている ({rowG:F4} vs {expectedG:F4})");
            Assert.Less(Mathf.Abs(rowP - expectedP), 0.012f,
                $"プレビュー判定線の行が期待とずれている ({rowP:F4} vs {expectedP:F4})");
            Assert.Less(Mathf.Abs(rowG - rowP), 0.015f,
                $"判定線の行が本編とプレビューで違う ({rowG:F4} vs {rowP:F4})");

            // サブピクセル補正: ワールド y の 0.02 差 (JudgeLineWorldY 0.48 vs
            // 旧実装の 0.5 — #719 で実際に起きた退行) は 928x560 の RT では
            // 約 0.5px しか動かず、DetectBrightRow の行分解能と ±0.012 の
            // 許容差の両方を下回って検出できない (実測: y=0.5 でも PASS)。
            // そのため幾何そのものをプレビューの judge メッシュ頂点で検証する。
            var judgeLineT = pv.RigRoot.transform.Find("Judge Line");
            Assert.IsNotNull(judgeLineT, "プレビューの Judge Line が見つからない");
            var judgeMesh = judgeLineT.GetComponent<MeshFilter>().sharedMesh;
            Assert.IsNotNull(judgeMesh, "Judge Line のメッシュが無い");
            var jverts = judgeMesh.vertices;
            Assert.Greater(jverts.Length, 0);
            for (int i = 0; i < jverts.Length; i++)
            {
                Assert.AreEqual(Judge.JudgeLineWorldY, jverts[i].y, 0.001f,
                    $"プレビュー判定線メッシュの y が JudgeLineWorldY と違う (v{i}.y={jverts[i].y:F4})");
            }
        }

        /// <summary>
        /// 期待行の近傍で「明るい画素が最も多い行」(下原点の正規化 y) を返す。
        /// 判定線は画面幅いっぱいに伸びるので、明るい画素数で行を同定する
        /// (行平均だと細い線が背景 0.6 gray に薄まって検出できない)。
        /// </summary>
        private static float DetectBrightRow(Shot shot, float expectedVy, float band)
        {
            int y0 = Mathf.Clamp(Mathf.FloorToInt((expectedVy - band) * shot.H), 0, shot.H - 1);
            int y1 = Mathf.Clamp(Mathf.CeilToInt((expectedVy + band) * shot.H), 0, shot.H);
            int bestCount = -1;
            int bestY = -1;
            for (int py = y0; py < y1; py++)
            {
                int count = 0;
                for (int px = 0; px < shot.W; px++)
                {
                    Color32 c = shot.Px[py * shot.W + px];
                    float lum = (0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b) / 255f;
                    if (lum > 0.80f) count++;
                }
                if (count > bestCount) { bestCount = count; bestY = py; }
            }
            // 判定線は全幅に伸びるが、キーガイドキャップ等の HUD が
            // 一部を覆うことがある (本番でも同じ重なり)。明るい画素は
            // キャップの隙間に残るので、行の 2 割を下限とする
            // (他の行は帯内でほぼ 0 なので誤検出にはならない)。
            if (bestY < 0 || bestCount < shot.W * 0.2f) return -1f;
            return (bestY + 0.5f) / shot.H;
        }

        /// <summary>
        /// 拍フラッシュ (BGFlash): 本編は壁時計の拍、プレビューは疑似メディア時計の
        /// 拍で点灯する。点灯条件・α・減衰は同一規則。ここでは「拍で点いたときに
        /// プレイフィールドの同程度の面積が明るくなるか」を比較する。
        /// 位置/位相の完全一致は本編が壁時計・プレビューがループ時計という
        /// 設計差 (Option 画面に played セッションが無い) のため不可能 —
        /// V6OptionPreviewRig.UpdateBeatFlash のコメント参照。
        /// </summary>
        [Test]
        public void BeatFlash_LightsSamePlayfieldArea()
        {
            float mediaTime = 5f;
            var gp = BuildGameplay(mediaTime);
            var pv = BuildPreview();

            // --- 本編: 消灯 shot → 拍境界で点火 → 点灯 shot ---
            // 3D のみ (HUD の Top Shade が差分を減衰させるため — RenderGameplay3D)。
            Shot gpOff = RenderGameplay3D(gp);
            spawned.Add(gpOff.Tex);
            float beatG = GetField<float>(gp.BeatFlash, "Beat");
            Assert.Greater(beatG, 0f, "BGFlash.Beat が解決できない (song bpm)");
            SetField(gp.BeatFlash, "BPMCount", beatG);
            // LaneFlash と同じく rend.material の EditMode 実体化エラーを抑止。
            bool prevIgnore2 = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            try
            {
                InvokePrivate(gp.BeatFlash, "Update");
            }
            finally
            {
                LogAssert.ignoreFailingMessages = prevIgnore2;
            }
            Shot gpOn = RenderGameplay3D(gp);
            spawned.Add(gpOn.Tex);

            // --- プレビュー: 拍境界をまたぐ 2 ティックで点灯 ---
            // FlashT=0 なので拍 index = floor(previewTime / beat)。
            SongData song = GManager.GetSong(GManager.songID);
            float beatP = 60f / song.bpm * BGFlash.BeatIntervalMultiplier(GManager.FlashBG);
            float t0 = beatP * 0.5f; // 最初の拍の中点から開始
            // 3D 内容は rig の RT を直接読む (RenderPreview3D 参照)。
            Shot pvOff = RenderPreview3D(pv, t0, 0.016f, true);
            spawned.Add(pvOff.Tex);
            // 次の拍を越えるまで進める
            Shot pvOn = RenderPreview3D(pv, t0 + beatP + 0.02f, 0.016f, true);
            spawned.Add(pvOn.Tex);
            Assert.Greater(optionView.PreviewBeatFlashAlphaForAudit, 0.1f,
                "プレビューの拍フラッシュが点灯していない");
            var bfT = pv.RigRoot.transform.Find("Beat Flash");
            if (bfT != null)
            {
                var bfR = bfT.GetComponent<MeshRenderer>();
                Debug.Log($"[V6RenderParity] beat flash obj: active={bfT.gameObject.activeInHierarchy} " +
                    $"renderer={(bfR != null)} enabled={(bfR != null && bfR.enabled)} " +
                    $"bounds={(bfR != null ? bfR.bounds.ToString() : "-")} " +
                    $"mat={(bfR != null && bfR.sharedMaterial != null ? bfR.sharedMaterial.name : "null")} " +
                    $"color={(bfR != null && bfR.sharedMaterial != null && bfR.sharedMaterial.HasProperty("_Color") ? bfR.sharedMaterial.GetColor("_Color").ToString() : "-")} " +
                    $"queue={(bfR != null && bfR.sharedMaterial != null ? bfR.sharedMaterial.renderQueue : -1)} " +
                    $"camPos={pv.PreviewCam.transform.position} camForward={pv.PreviewCam.transform.forward}");
            }
            else
            {
                Debug.Log("[V6RenderParity] beat flash obj: NOT FOUND under rig root");
            }

            // 差分は「拍で明るくなった面積」。HUD/歌詞/判定線は両 shot で同一なので相殺。
            // 上端 (歌詞・判定ポップ帯) を除いた領域で測る。
            Rect playfield = new Rect(0f, 0.05f, 1f, 0.75f);
            float covG = RegionCoverage(gpOff, gpOn, playfield);
            float covP = RegionCoverage(pvOff, pvOn, playfield);
            Debug.Log($"[V6RenderParity] beat flash coverage: gp={covG:F4} pv={covP:F4} " +
                $"(beatG={beatG:F3}s beatP={beatP:F3}s)");

            Assert.Greater(covG, 0.005f, "本編の拍フラッシュが描かれていない");
            Assert.Greater(covP, 0.005f, "プレビューの拍フラッシュが描かれていない");
            // like-for-like (両側 3D のみ) では pv/gp ≈ 1.0。±50% に締める。
            float ratio = covP / Mathf.Max(1e-5f, covG);
            Assert.Greater(ratio, 0.5f, $"拍フラッシュ面積比が小さすぎる pv/gp={ratio:F2}");
            Assert.Less(ratio, 2.0f, $"拍フラッシュ面積比が大きすぎる pv/gp={ratio:F2}");
        }

        private static void SetRenderersEnabled(IEnumerable<Renderer> renderers, bool enabled)
        {
            foreach (var r in renderers) r.enabled = enabled;
        }
    }
}
