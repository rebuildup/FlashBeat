using System.Collections.Generic;
using System.Reflection;
using FlashBeat.Gameplay;
using FlashBeat.UI.V6;
using FlashBeat.UI.V6.Game.Hud;
using FlashBeat.UI.V6.Game.Playfield;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace FlashBeat.V6CanvasTests
{
    /// <summary>
    /// Issue #675 再検証: 実際のノーツ描画が「暗くない」ことを
    /// <b>実レンダリング結果の画素</b>で検証する。
    ///
    /// これまでの検証 (FlatRenderCheck.MeasureComposedNotes) は Note.prefab と
    /// 同形の代替キューブを置いていただけで、実ノーツの MeshRenderer /
    /// マテリアル / 実シェーダも、uGUI 経路の Head/Body Image も通っていなかった。
    /// ここでは出荷経路の部品をそのまま合成する:
    ///
    /// - 3D 経路 (既定, V6UiMode.Playfield=false): Resources/Prefabs/Note.prefab の
    ///   実体を Judge.LaneWorldX / MediaTimeline.NotePlaneY / NotePositionZ と
    ///   同じ式で配置し、GameScene と同じカメラ姿勢 (0,4.9,-0.8 / pitch 54.8 /
    ///   FOV 60) + PlayfieldCameraFit で RenderTexture へ描く。
    /// - v6 uGUI 経路 (Playfield=true): 実際の <see cref="Notes"/> が
    ///   <see cref="V6PlayfieldView.NoteLayer"/> に作る Head/Body Image を、
    ///   ScreenSpaceCamera Canvas (出荷は ScreenSpaceOverlay — 後述) 経由で描く。
    /// - その上に実物の <see cref="V6GameHudView"/> を載せる。上部シェードは
    ///   レーン帯の抜き (<see cref="V6TopShadeHole"/>) を焼き込んだ実スプライト、
    ///   判定行・キーガイド・歌詞も実物。V6GameRuntime.DisableInternalDim と
    ///   同じく HUD 内蔵の "Video Dim" は無効化する (出荷と同じ状態)。
    ///
    /// ノーツ自身の画素は「ノーツ有り / ノーツ無し」の 2 回描画の差分で同定する
    /// (位置の事前予測に依存しない)。差分画素はすべて明るさの下限を満たすこと、
    /// かつシェードが実際に描かれていること (コリドー外の上端が暗い) を確認する。
    ///
    /// RT は 1920x1080 (FHD プロファイルと一致) にする。batch の Screen は
    /// 640x480 だが、ScreenSpaceCamera Canvas の rect はカメラの描画先サイズに
    /// 追従し、HUD の V6HudMetrics も CanvasSize() (= canvas rect) を見るので、
    /// RT サイズを実解像度に合わせれば出荷と同じ寸法で検証できる。
    ///
    /// 再現していないもの (残りは物理環境で要確認):
    /// - ScreenSpaceOverlay の最終合成 (バッチでは camera RT にしか描けないため
    ///   ScreenSpaceCamera で代替。uGUI の Image/シェーダ/ブレンド経路は同一で、
    ///   sortingOrder の前後関係も overlay と同じ -3/-2/-1 を使う)。
    /// - GameScene 固有のオブジェクト (VideoPlayer 映像 quad, Directional Light,
    ///   旧 Canvas, BGFlash 実体, LaneLight の点灯状態)。映像は単色クリアで代替、
    ///   レーンガイドは PlayfieldLaneGuides.BuildGuideMesh/CreateGuideMaterial の
    ///   実メッシュ・実マテリアルを使う。シーン監査と実機撮影は
    ///   FlatRenderCheck / AutoShot が担う。
    /// - 実機の色域・ガンマ・リフレッシュレート (RT は sRGB 8bit)。
    /// </summary>
    public sealed class V6NoteRenderTests
    {
        private const int RtWidth = 1920;
        private const int RtHeight = 1080;

        /// <summary>ノーツ画素の輝度下限。Note 色 #EDEDED ≈ 0.93、
        /// 上部シェード最大 α≈0.65 が乗ると ≈0.33 まで落ちるので十分に分離できる。</summary>
        private const float MinNoteLuminance = 0.70f;

        /// <summary>シェードが効いていることの確認用: コリドー外の上端の輝度上限。</summary>
        private const float MaxShadedLuminance = 0.50f;

        private readonly List<UnityEngine.Object> spawned = new List<UnityEngine.Object>();
        private readonly List<Camera> disabledCameras = new List<Camera>();
        private bool savedPlayfield;
        private bool savedVideoDim;
        private bool savedStart;
        private float savedStepTime;
        private float savedNoteSpeed;

        [SetUp]
        public void SetUp()
        {
            savedPlayfield = V6UiMode.Playfield;
            savedVideoDim = V6UiMode.VideoDim;
            savedStart = GManager.Start;
            savedStepTime = GManager.step_time;
            savedNoteSpeed = GManager.noteSpeed;

            MediaTimeline.ResetSession();
            MediaTimeline.BeginSession(10f);
            PauseGate.HardReset();
            GManager.noteSpeed = 10f;
        }

        [TearDown]
        public void TearDown()
        {
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

            V6UiMode.Playfield = savedPlayfield;
            V6UiMode.VideoDim = savedVideoDim;
            GManager.Start = savedStart;
            GManager.step_time = savedStepTime;
            GManager.noteSpeed = savedNoteSpeed;
            MediaTimeline.ResetSession();
            PauseGate.HardReset();
        }

        private GameObject Track(GameObject go)
        {
            spawned.Add(go);
            return go;
        }

        // --- 構成 ---

        /// <summary>GameScene の Main Camera と同じ姿勢のカメラを作る。</summary>
        private Camera CreateCamera()
        {
            var go = Track(new GameObject("Main Camera", typeof(Camera)));
            go.tag = "MainCamera";
            var cam = go.GetComponent<Camera>();

            // CorridorRows / PlayfieldCameraFit は Camera.main を使う。
            // EditMode では他テストや残存シーンの MainCamera が先に拾われ、
            // コリドー抜きが実際の描画カメラとずれて全ノーツがシェード上に
            // 乗る誤検出になる (PlayfieldGeometryTests / V6TopShadeHoleTests
            // と同じ隔離手順)。
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
            // 明るい無地 (暗転検出のため、映像の平均より明るい背景)。
            cam.backgroundColor = new Color(0.6f, 0.6f, 0.6f, 1f);
            // GameScene / PlayfieldLaneGuides.Update と同じ水平 fit。
            PlayfieldCameraFit.Apply(cam, PlayfieldCameraFit.ScreenAspect());
            return cam;
        }

        /// <summary>ScreenSpaceCamera Canvas (出荷の overlay と同じ sortingOrder 系列)。</summary>
        private Canvas CreateCanvas(Camera cam, string name, int sortingOrder)
        {
            var go = Track(new GameObject(name, typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster)));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 1f;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            return canvas;
        }

        /// <summary>
        /// 出荷と同じ積層: 動画(=カメラ背景) → [dim canvas -3, VideoDim=false で省略]
        /// → 3D ワールド or v6 playfield canvas (-2) → HUD canvas (-1)。
        /// HUD は実物の V6GameHudView。内部 Video Dim は V6GameRuntime と同様に切る。
        /// </summary>
        private V6GameHudView CreateHud(Camera cam)
        {
            var hudCanvas = CreateCanvas(cam, "V6GameHudCanvas", -1);
            var frame = new GameObject("V6 HUD Frame", typeof(RectTransform));
            frame.transform.SetParent(hudCanvas.transform, false);
            var frt = frame.GetComponent<RectTransform>();
            frt.anchorMin = Vector2.zero;
            frt.anchorMax = Vector2.one;
            frt.offsetMin = Vector2.zero;
            frt.offsetMax = Vector2.zero;
            spawned.Add(frame);

            var hud = V6GameHudView.Create(frt);
            spawned.Add(hud.gameObject);

            // V6GameRuntime.DisableInternalDim と同じ処理 (private なので同等操作)。
            var dim = hud.Root.Find("Video Dim");
            if (dim != null) dim.gameObject.SetActive(false);

            Canvas.ForceUpdateCanvases();
            hud.RequestRefresh();
            Canvas.ForceUpdateCanvases();
            return hud;
        }

        /// <summary>3D 経路: 実 Note.prefab を実際の配置式で並べる。</summary>
        private List<Renderer> SpawnRealNotes3D(int laneCount)
        {
            var prefab = Resources.Load<GameObject>("Prefabs/Note");
            Assert.IsNotNull(prefab, "Resources/Prefabs/Note が見つからない");

            var renderers = new List<Renderer>();
            // 深い位置 (上部シェードの中) から判定線手前まで。全 8 レーンを浅く回す。
            float[] depths = { 0.6f, 2f, 4f, 6f, 8f };
            for (int lane = 0; lane < laneCount; lane++)
            {
                foreach (float z in depths)
                {
                    var go = Object.Instantiate(prefab);
                    go.name = "RealNote";
                    Track(go);
                    go.transform.position = new Vector3(
                        Judge.LaneWorldX(lane), MediaTimeline.NotePlaneY, z);
                    foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                    {
                        renderers.Add(r);
                    }
                }
            }
            return renderers;
        }

        /// <summary>uGUI 経路: 実 Notes が作る Head/Body Image を持つノーツ群。</summary>
        private List<Notes> SpawnRealNotesV6()
        {
            var notes = new List<Notes>();
            // t01 = 0.9 (最奥=上部シェード帯) / 0.55 / 0.2 をレーンを変えて。
            float[] t01s = { 0.9f, 0.55f, 0.2f };
            for (int i = 0; i < t01s.Length; i++)
            {
                int lane = i * 2; // 0, 2, 4 … 適度に散らす
                var go = Track(new GameObject("NoteV6 " + lane, typeof(Notes)));
                // Start が noteTime を z から復元するので生成位置は noteTime*zScale。
                go.transform.position = new Vector3(
                    lane + Judge.LaneFirstCenterX,
                    MediaTimeline.NotePlaneY,
                    5f * MediaTimeline.SessionZScale);
                var n = go.GetComponent<Notes>();
                InvokeLifecycle(n, "Start");
                notes.Add(n);

                // t01 を直接決める: remaining = t01 * approach (approach = 1.6s @ speed10)。
                GManager.Start = true;
                GManager.step_time = 5f - t01s[i] * 1.6f;
                InvokeLifecycle(n, "Update");
            }
            return notes;
        }

        private static void InvokeLifecycle(Notes notes, string method)
        {
            MethodInfo m = typeof(Notes).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(m, "Notes." + method + " が見つからない");
            m.Invoke(notes, null);
        }

        // --- 描画と差分 ---

        private sealed class Shot
        {
            public Texture2D Tex;
            public Color32[] Px;
        }

        private static Shot Render(Camera cam, int w, int h)
        {
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            var previous = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = previous;
            rt.Release();
            Object.DestroyImmediate(rt);
            return new Shot { Tex = tex, Px = tex.GetPixels32() };
        }

        private static float Luminance(Color32 c)
        {
            return (0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b) / 255f;
        }

        /// <summary>
        /// withNotes / withoutNotes の差分でノーツ画素を同定し、輝度を検査する。
        /// 戻り値はノーツ画素数。
        /// </summary>
        /// <summary>
        /// ノーツ差分画素を「背景が明るい場所」に限定して輝度を検査する。
        ///
        /// 絶対輝度だけで判定すると、上部シェードのコリドー外など
        /// 「背景自体が暗い場所」に重なったノーツを誤って暗いと判定してしまう
        /// (オーバーレイは背景も同じだけ暗くするので、ノーツのせいではない)。
        /// そこで withoutNotes の同画素輝度が <see cref="BrightBackgroundLuminance"/>
        /// 以上の画素 (= シェードの抜きや無地の上に乗る場所) だけを検査対象にし、
        /// そこではノーツが明るく描かれることを要求する。
        /// さらに上部帯のノーツのうち「背景が明るい」割合を見て、
        /// コリドー抜きが実際にレーンを覆っていることを確認する
        /// (抜きを無効化すると背景ごと暗くなり、この割合が崩れて失敗する)。
        /// </summary>
        private const float BrightBackgroundLuminance = 0.58f;

        private static int AssertNotePixelsBright(Shot withNotes, Shot withoutNotes, string label, bool requireTopZone = true)
        {
            Assert.AreEqual(withNotes.Px.Length, withoutNotes.Px.Length);
            int w = withNotes.Tex.width;
            int h = withNotes.Tex.height;

            int maskCount = 0;
            int checkedCount = 0;
            int dark = 0;
            int topZone = 0;
            int topZoneBright = 0;
            int excluded = 0;
            float darkest = float.MaxValue;
            int darkestX = -1, darkestY = -1;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    Color32 a = withNotes.Px[i];
                    Color32 b = withoutNotes.Px[i];
                    float dr = Mathf.Abs(a.r - b.r) / 255f;
                    float dg = Mathf.Abs(a.g - b.g) / 255f;
                    float db = Mathf.Abs(a.b - b.b) / 255f;
                    if (dr + dg + db < 0.08f) continue; // ノーツの寄与が無い画素

                    maskCount++;
                    // ReadPixels は下原点: 画面上端側 (上部シェード帯) は y が大きい側。
                    bool inTopZone = y > h * 0.7f;
                    if (inTopZone) topZone++;

                    float bgLum = Luminance(b);
                    if (bgLum < BrightBackgroundLuminance)
                    {
                        excluded++; // 背景自体が暗い場所 (コリドー外のシェード等) は対象外
                        continue;
                    }
                    if (inTopZone) topZoneBright++;

                    checkedCount++;
                    float lum = Luminance(a);
                    // AA の縁や半透明の寄与を考慮し、絶対輝度か背景からの明確な持ち上げを要求。
                    bool bright = lum >= MinNoteLuminance || (lum - bgLum) >= 0.20f;
                    if (lum < darkest) { darkest = lum; darkestX = x; darkestY = y; }
                    if (!bright)
                    {
                        dark++;
                        if (dark <= 8)
                        {
                            Debug.Log($"[V6NoteRender] {label}: dark note pixel at ({x},{y}) " +
                                $"with={a} without={b} lum={lum:F3} bg={bgLum:F3}");
                        }
                    }
                }
            }

            float brightRatio = topZone > 0 ? (float)topZoneBright / topZone : 0f;
            Debug.Log($"[V6NoteRender] {label}: note pixels={maskCount} checked={checkedCount} " +
                $"excluded(dark bg)={excluded} dark={dark}, topZone={topZone} " +
                $"topZoneBright={topZoneBright} ratio={brightRatio:F2}, darkest={darkest:F3} at ({darkestX},{darkestY})");

            Assert.Greater(maskCount, 200,
                label + ": ノーツ差分画素が少なすぎる (描画経路が繋がっていない?)");
            if (requireTopZone)
            {
                Assert.Greater(topZone, 0,
                    label + ": 上部 (シェード帯) を通るノーツが無い — シェード交差が未検証");
            }
            // 上部帯ノーツの過半数はコリドー抜き (=明るい背景) の上に乗るはず。
            if (requireTopZone) Assert.GreaterOrEqual(brightRatio, 0.5f,
                label + ": 上部帯ノーツの明るい背景率が低い ({brightRatio:F2}) — " +
                "シェードのレーン抜きがノーツを覆っていない可能性");
            Assert.AreEqual(0, dark,
                label + ": 明るい背景上のノーツ画素が暗い — " +
                "シェード/ガイド/BGFlash/マテリアルのどれかがノーツを暗くしている");
            return maskCount;
        }

        /// <summary>シェードが実際に描かれていること (テストが空でないこと) の確認。</summary>
        private static void AssertShadePresent(Camera cam, int w, int h, string label)
        {
            // 上端の左右 (レーンコリドーの外) はシェードのグラデーションが効く。
            var shot = Render(cam, w, h);
            spawnedSafeDestroy(shot.Tex);
            // 画面上端から 4% の行、左右 4% の列を採る (ReadPixels は下原点)。
            int y = Mathf.RoundToInt(h * 0.96f);
            var left = shot.Px[y * w + Mathf.RoundToInt(w * 0.04f)];
            var right = shot.Px[y * w + Mathf.RoundToInt(w * 0.96f)];
            float lum = Mathf.Min(Luminance(left), Luminance(right));
            Debug.Log($"[V6NoteRender] {label}: shade probe lum={lum:F3} (left={left} right={right})");
            Assert.Less(lum, MaxShadedLuminance,
                label + ": 上部シェードが描かれていない (輝度 " + lum + ") — " +
                "抜き検証が空振りしている可能性");
        }

        private static void spawnedSafeDestroy(UnityEngine.Object o)
        {
            if (o != null) Object.DestroyImmediate(o);
        }

        private static void SetRenderersEnabled(IEnumerable<Renderer> renderers, bool enabled)
        {
            foreach (var r in renderers) r.enabled = enabled;
        }

        // --- テスト ---

        /// <summary>
        /// 3D 出荷経路: 実 Note.prefab + 実レーンガイド + 実 HUD (上部シェード) を
        /// 同一カメラで合成し、ノーツ画素が明るいことを画素で証明する。
        /// </summary>
        [Test]
        public void Real3DNotes_StayBrightUnderLaneGuidesAndTopShade()
        {
            V6UiMode.Playfield = false;
            V6UiMode.VideoDim = false;

            Camera cam = CreateCamera();

            // 実シーンと同じガイドメッシュ/マテリアル (PlayfieldLaneGuides.Build と同等)。
            float frontZ = -PlayfieldLaneGuides.FrontDepth(cam);
            var guideGo = Track(new GameObject("Lane Guides"));
            guideGo.AddComponent<MeshFilter>().sharedMesh =
                PlayfieldLaneGuides.BuildGuideMesh(frontZ);
            var guideRenderer = guideGo.AddComponent<MeshRenderer>();
            guideRenderer.sharedMaterial = PlayfieldLaneGuides.CreateGuideMaterial();
            guideRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            guideRenderer.receiveShadows = false;

            var notes = SpawnRealNotes3D(Judge.LaneCount);
            CreateHud(cam);

            Canvas.ForceUpdateCanvases();

            var withNotes = Render(cam, RtWidth, RtHeight);
            spawned.Add(withNotes.Tex);

            SetRenderersEnabled(notes, false);
            Canvas.ForceUpdateCanvases();
            var withoutNotes = Render(cam, RtWidth, RtHeight);
            spawned.Add(withoutNotes.Tex);
            SetRenderersEnabled(notes, true);

            AssertNotePixelsBright(withNotes, withoutNotes, "3D path");
            AssertShadePresent(cam, RtWidth, RtHeight, "3D path");
        }

        /// <summary>
        /// v6 uGUI 経路 (Playfield=true): 実 Notes が作る Head/Body Image を
        /// ScreenSpaceCamera Canvas で描き、HUD (設計座標コリドーのシェード) と
        /// 合成してノーツが明るいことを証明する。
        /// </summary>
        [Test]
        public void V6UGuiNotes_StayBrightUnderTopShadeCorridor()
        {
            V6UiMode.Playfield = true;
            V6UiMode.VideoDim = false;

            Camera cam = CreateCamera();

            var playfieldCanvas = CreateCanvas(cam, "V6PlayfieldCanvas", -2);
            var view = V6PlayfieldView.Create(playfieldCanvas.GetComponent<RectTransform>());
            spawned.Add(view.gameObject);

            var notes = SpawnRealNotesV6();
            foreach (var n in notes)
            {
                Assert.IsNotNull(n.HeadImage, "v6 ノーツに HeadImage が無い");
            }

            CreateHud(cam);
            Canvas.ForceUpdateCanvases();

            // Head/Body Image の active 状態を差分の ON/OFF に使う。
            var noteObjects = new List<GameObject>();
            foreach (var n in notes)
            {
                noteObjects.Add(n.HeadImage.gameObject);
            }

            var withNotes = Render(cam, RtWidth, RtHeight);
            spawned.Add(withNotes.Tex);

            foreach (var go in noteObjects) go.SetActive(false);
            Canvas.ForceUpdateCanvases();
            var withoutNotes = Render(cam, RtWidth, RtHeight);
            spawned.Add(withoutNotes.Tex);
            foreach (var go in noteObjects) go.SetActive(true);

            AssertNotePixelsBright(withNotes, withoutNotes, "v6 uGUI path", requireTopZone: false);
            AssertShadePresent(cam, RtWidth, RtHeight, "v6 uGUI path");
        }

    }
}