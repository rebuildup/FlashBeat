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
    ///   旧 Canvas, BGFlash 実体, LaneLight の点灯状態)。映像は 3D 経路では
    ///   単色クリア、uGUI 経路では明暗のむらがある手続きテクスチャの
    ///   背景 quad で代替 (CreateVideoLikeBackground)。
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
        private readonly List<Renderer> disabledRenderers = new List<Renderer>();
        private bool savedPlayfield;
        private bool savedVideoDim;
        private bool savedStart;
        private bool savedPlayed;
        private bool savedJudgmentsComplete;
        private bool savedLyricsEnabled;
        private float savedStepTime;
        private float savedNoteSpeed;

        [SetUp]
        public void SetUp()
        {
            savedPlayfield = V6UiMode.Playfield;
            savedVideoDim = V6UiMode.VideoDim;
            savedStart = GManager.Start;
            savedPlayed = GManager.played;
            savedJudgmentsComplete = GManager.judgmentsComplete;
            savedLyricsEnabled = PlaySession.LyricsEnabled;
            savedStepTime = GManager.step_time;
            savedNoteSpeed = GManager.noteSpeed;

            MediaTimeline.ResetSession();
            MediaTimeline.BeginSession(10f);
            PauseGate.HardReset();
            GManager.noteSpeed = 10f;

            // シェード/パネルのコリドー抜きスプライトは静的キャッシュで、
            // 焼き込み時のカメラに依存する。先行テストがカメラ無しで
            // 同じ解像度キーのスプライトを作ると、ここの描画とずれるので
            // 毎回空にして自分のカメラで焼き直させる。
            V6GameHudView.ClearTopShadeSpriteCacheForTests();
            V6PanelCorridorHole.ClearSpriteCacheForTests();

            // EditMode ではテスト間でシーンが共有され、前のテストが破棄し忘れた
            // 全画面 quad 等の Renderer が残りうる。フルスイートではそれが
            // カメラとノーツの間に入って「背景が全部暗い」誤検出になる
            // (focused 実行では出ない)。このテストが使う描画物は全て自分で
            // 作るので、既存 Renderer を一時的に全て止める。
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

            V6UiMode.Playfield = savedPlayfield;
            V6UiMode.VideoDim = savedVideoDim;
            GManager.Start = savedStart;
            GManager.played = savedPlayed;
            GManager.judgmentsComplete = savedJudgmentsComplete;
            PlaySession.SetLyricsEnabled(savedLyricsEnabled);
            GManager.step_time = savedStepTime;
            GManager.noteSpeed = savedNoteSpeed;
            MediaTimeline.ResetSession();
            PauseGate.HardReset();
            // 自分のカメラで焼いたスプライトを後続テストへ残さない。
            V6GameHudView.ClearTopShadeSpriteCacheForTests();
            V6PanelCorridorHole.ClearSpriteCacheForTests();
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
            // HUD のメトリクス (TopShadeH 等) とシェードのレーンコリドー抜きは
            // 構築時の Canvas 寸法で決まる。batchmode の Screen 寸法は RT の
            // 1920×1080 と一致しないため、構築前に targetTexture を合わせて
            // Canvas を RT 寸法でレイアウトさせる。さもないとコリドーが
            // 実際のノーツ位置からずれ、シェードがノーツを暗くする誤検出になる
            // (出荷では Screen = 描画解像度なのでこのずれは起きない)。
            var sizingRt = new RenderTexture(RtWidth, RtHeight, 0);
            var previousTarget = cam.targetTexture;
            cam.targetTexture = sizingRt;
            Canvas.ForceUpdateCanvases();

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

            cam.targetTexture = previousTarget;
            sizingRt.Release();
            Object.DestroyImmediate(sizingRt);
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

        /// <summary>
        /// 「映像フレーム風」の背景 quad をカメラ奥に置く (uGUI 経路用)。
        /// 出荷では VideoPlayer の映像がワールドに描かれ、その上に
        /// ScreenSpaceOverlay の uGUI (playfield -2 / HUD -1) が乗る。
        /// フラットな単色クリアでは「明暗のむらがある実映像」の上で
        /// ノーツが沈まないかを検証できないので、縦グラデーション +
        /// ノイズ + 暗い被写体ブロブを持つ手続きテクスチャを
        /// FlashBeat/FlatUnlit (_MainTex 対応) で貼った quad を置く。
        /// ScreenSpaceOverlay 自体はバッチの camera RT には描けないため、
        /// 合成は ScreenSpaceCamera + 出荷と同じ sortingOrder (-2/-1) で
        /// 代替する (uGUI の Image/シェーダ/ブレンド経路と前後関係は同一)。
        /// </summary>
        /// <param name="lumFloor">輝度の下限 (既定 0.66 = 明るい映像)。
        /// 暗い映像フレームの再現には 0.03 程度を渡す (Issue #735)。</param>
        /// <param name="lumCeil">輝度の上限 (既定 0.95)。</param>
        /// <param name="variationScale">明暗むら・ノイズ・被写体ブロブの強さ
        /// (既定 1)。暗い背景では 0.5 程度に落として全体を暗く保つ。</param>
        private GameObject CreateVideoLikeBackground(Camera cam,
            float lumFloor = 0.66f, float lumCeil = 0.95f, float variationScale = 1f)
        {
            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                float v = y / (float)(size - 1);
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)(size - 1);
                    // 上が明るく下もそれなりに明るい映像の明暗差 + 左右のゆるいムラ。
                    // 既定の下限 0.66: ノーツの多くは画面下半分 (判定線側)
                    // に乗るので、ここが暗いと「明るい背景上のノーツ」検査が
                    // 画素不足で空振りする (実測 checked=35 → 要件 >100)。
                    // 暗い背景を試すときは lumFloor/lumCeil を下げて渡す (Issue #735)。
                    float lum = Mathf.Lerp(lumFloor, lumCeil, Mathf.SmoothStep(0f, 1f, v));
                    lum += 0.05f * variationScale * Mathf.Sin(u * 11f + v * 5f);
                    lum += (Hash01(x, y) - 0.5f) * 0.12f * variationScale;
                    // 中央に暗い「被写体」ブロブ (実映像らしい濃淡)。
                    float dx = (u - 0.42f) * 2f, dy = (v - 0.55f) * 2f;
                    lum -= 0.15f * variationScale * Mathf.Exp(-(dx * dx + dy * dy) * 4f);
                    lum = Mathf.Clamp01(lum);
                    // 色味は下が寒色・上が暖色 (ステージ照明っぽい)。
                    // tint は輝度重み付きでほぼ 1 になるよう選ぶ
                    // (輝度 ≈ lum を保ち、下限 0.66 がそのまま
                    //  BrightBackgroundLuminance=0.58 を満たすようにする)。
                    Color tint = Color.Lerp(
                        new Color(0.82f, 0.92f, 1.05f),
                        new Color(1.05f, 0.92f, 0.78f),
                        Mathf.Clamp01(v * 1.4f));
                    var c = new Color(
                        Mathf.Clamp01(tint.r * lum),
                        Mathf.Clamp01(tint.g * lum),
                        Mathf.Clamp01(tint.b * lum), 1f);
                    px[y * size + x] = c;
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            spawned.Add(tex);

            var shader = Shader.Find("FlashBeat/FlatUnlit");
            Assert.IsNotNull(shader, "FlashBeat/FlatUnlit が見つからない");
            var mat = new Material(shader);
            spawned.Add(mat);
            mat.SetTexture("_MainTex", tex);
            mat.SetColor("_Color", Color.white);

            var go = Track(GameObject.CreatePrimitive(PrimitiveType.Quad));
            go.name = "VideoLike Background";
            var col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
            var bgRenderer = go.GetComponent<Renderer>();
            bgRenderer.sharedMaterial = mat;
            bgRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            bgRenderer.receiveShadows = false;

            // カメラの視錐台を distance の平面で覆う quad。
            // Quad の可視面法線は -Z なので、+Z を「カメラ→奥」に向ける。
            const float distance = 80f;
            Vector3 bl = cam.ViewportToWorldPoint(new Vector3(0f, 0f, distance));
            Vector3 br = cam.ViewportToWorldPoint(new Vector3(1f, 0f, distance));
            Vector3 tl = cam.ViewportToWorldPoint(new Vector3(0f, 1f, distance));
            Vector3 tr = cam.ViewportToWorldPoint(new Vector3(1f, 1f, distance));
            Vector3 center = (bl + br + tl + tr) * 0.25f;
            float w = (Vector3.Distance(bl, br) + Vector3.Distance(tl, tr)) * 0.5f * 1.3f;
            float h = (Vector3.Distance(bl, tl) + Vector3.Distance(br, tr)) * 0.5f * 1.3f;
            go.transform.position = center;
            go.transform.rotation = Quaternion.LookRotation(
                center - cam.transform.position, Vector3.up);
            go.transform.localScale = new Vector3(w, h, 1f);
            return go;
        }

        /// <summary>決定論的な 0..1 の擬似乱数 (画素ノイズ用)。</summary>
        private static float Hash01(int x, int y)
        {
            float h = Mathf.Sin(x * 127.1f + y * 311.7f) * 43758.5453f;
            return h - Mathf.Floor(h);
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

        private static int AssertNotePixelsBright(Shot withNotes, Shot withoutNotes, string label,
            bool requireTopZone, out int checkedPixels)
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
            checkedPixels = checkedCount;
            // 「明るい背景上で明るい」検査が空振りしないことも要求する。
            // 背景がほぼ暗いと checkedCount が 0 に近づき、dark==0 が
            // 意味を持たなくなる (カウンター例としての検出力の維持)。
            Assert.Greater(checkedCount, 100,
                label + ": 明るい背景上のノーツ画素が少なすぎる (checked=" +
                checkedCount + ") — 背景が暗すぎて検証が空振りしている");
            Assert.AreEqual(0, dark,
                label + ": 明るい背景上のノーツ画素が暗い — " +
                "シェード/ガイド/BGFlash/マテリアルのどれかがノーツを暗くしている");
            return maskCount;
        }

        // --- Issue #735: 暗い背景でのノーツ白色 (最終合成画素) ---

        /// <summary>「白い」とみなすノーツ画素の輝度。Note.mat は純白 (1.0)、
        /// v6 uGUI は #EDEDED (≈0.93)。上部シェード最大 α≈0.65 が乗ると
        /// ≈0.35 まで落ちるので、0.80 ならシェード被りを確実に検出できる。</summary>
        private const float DarkBgWhiteLuminance = 0.80f;

        /// <summary>「暗い」とみなす上限。シェード α0.65 被りの ≈0.35 と、
        /// AA/縁の中間値を分ける境界 (0.60 未満は白とは言えない)。</summary>
        private const float DarkBgDarkLuminance = 0.60f;

        /// <summary>ノーツ差分画素のうち白い画素が占めるべき割合。
        /// MSAA なしの RT ではノーツ縁も二値なので、ほぼ全画素が白のはず。</summary>
        private const float DarkBgMinWhiteRatio = 0.90f;

        /// <summary>背景からの持ち上げ (コントラスト) の平均下限。
        /// 暗い背景 (≈0.1) 上の白ノーツなら 0.8 以上になる。</summary>
        private const float DarkBgMinMeanLift = 0.40f;

        /// <summary>
        /// 暗い映像背景の上でのノーツ白色を <b>絶対輝度</b>で検査する (Issue #735)。
        ///
        /// <see cref="AssertNotePixelsBright"/> は「背景が明るい画素」だけを
        /// 対象にするため、暗い動画フレーム上のノーツは検証対象外だった。
        /// ここでは背景輝度で除外せず、差分画素すべてについて
        /// 「白い」「背景より暗くならない」「背景から明確に持ち上がる」を
        /// 独立に計測する。背景が本当に暗いことも確認し、
        /// 「暗い背景の上で白い」という肝心の条件が空振りしないようにする。
        ///
        /// <paramref name="exclude"/> は画面座標 (左下原点 px) の除外矩形。
        /// 検証対象から外す領域を指定する (パネル帯の外側を別途検証するために
        /// 内側を除く等)。
        ///
        /// <paramref name="include"/> は画面座標 (左下原点 px) の包含矩形。
        /// 指定時はこの矩形の内側だけを検証する (Issue #760: レーン回廊
        /// カットアウトが入った歌詞パネル帯を、外側と同じ厳しい基準で
        /// 検証するために使う)。exclude と併用した場合は
        /// 「include の内側かつ exclude の外側」だけが対象になる。
        /// </summary>
        private static int AssertNotePixelsWhiteOnDark(Shot withNotes, Shot withoutNotes,
            string label, Rect? exclude = null, Rect? include = null)
        {
            Assert.AreEqual(withNotes.Px.Length, withoutNotes.Px.Length);
            int w = withNotes.Tex.width;
            int h = withNotes.Tex.height;

            // ノーツ差分マスク (exclude/include 適用前の全画面)。
            // シルエット縁の判定に全画面のマスクが要るため先に作る。
            var mask = new bool[w * h];
            int maskCount = 0;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    Color32 a = withNotes.Px[i];
                    Color32 b = withoutNotes.Px[i];
                    float d = (Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b)) / 255f;
                    if (d < 0.08f) continue; // ノーツの寄与が無い画素
                    mask[i] = true;
                    maskCount++;
                }
            }

            int regionCount = 0;
            int interiorCount = 0;
            int whiteCount = 0;
            int darkCount = 0;
            int darkerCount = 0;
            int darkBgCount = 0;
            float liftSum = 0f;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (!mask[i]) continue;

                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    if (exclude.HasValue && exclude.Value.Contains(p))
                    {
                        continue;
                    }
                    if (include.HasValue && !include.Value.Contains(p))
                    {
                        continue;
                    }

                    regionCount++;
                    // ノーツ輪郭の縁 (ラスタライズ/フィルタで背景と中間色に
                    // なる画素) は「暗い背景上で白い」の対象外。暗い映像の
                    // 上では縁が必ず中間輝度 (~0.5) になるのが正常な描画で、
                    // ここを数えると実装の良し悪しと無関係に必ず失敗する。
                    // 4 近傍すべてがノーツ画素の「内部」だけを評価する。
                    if (x == 0 || y == 0 || x == w - 1 || y == h - 1
                        || !mask[i - 1] || !mask[i + 1] || !mask[i - w] || !mask[i + w])
                    {
                        continue;
                    }
                    interiorCount++;

                    Color32 a = withNotes.Px[i];
                    Color32 b = withoutNotes.Px[i];
                    float lum = Luminance(a);
                    float bgLum = Luminance(b);
                    liftSum += lum - bgLum;
                    if (bgLum < 0.35f) darkBgCount++;
                    if (lum >= DarkBgWhiteLuminance) whiteCount++;
                    if (lum < DarkBgDarkLuminance) darkCount++;
                    if (lum < bgLum - 0.02f) darkerCount++;
                }
            }

            float whiteRatio = interiorCount > 0 ? (float)whiteCount / interiorCount : 0f;
            float darkBgRatio = interiorCount > 0 ? (float)darkBgCount / interiorCount : 0f;
            float meanLift = interiorCount > 0 ? liftSum / interiorCount : 0f;
            Debug.Log($"[V6NoteRender] {label} (dark bg): note pixels={maskCount} " +
                $"region={regionCount} interior={interiorCount} " +
                $"white={whiteCount} ({whiteRatio:F2}) dark={darkCount} darker={darkerCount} " +
                $"darkBg={darkBgCount} ({darkBgRatio:F2}) meanLift={meanLift:F3}");

            Assert.Greater(regionCount, 200,
                label + ": ノーツ差分画素が少なすぎる (描画経路が繋がっていない?)");
            // 背景が本当に暗いこと: 差分画素の 8 割以上が暗い背景上にあること。
            // これが崩れると「暗い背景での白さ」の検証が空振りする。
            Assert.GreaterOrEqual(darkBgRatio, 0.80f,
                label + ": 差分画素の大半が暗い背景上に無い (darkBg=" +
                darkBgRatio.ToString("F2") + ") — 暗背景検証が空振りしている");
            Assert.AreEqual(0, darkCount,
                label + ": 暗い背景上のノーツ内部画素が白くない (" + darkCount +
                " 画素) — シェード/パネル/マテリアルのどれかがノーツを暗くしている");
            Assert.AreEqual(0, darkerCount,
                label + ": ノーツが背景より暗い画素を作っている (" + darkerCount + " 画素)");
            Assert.GreaterOrEqual(whiteRatio, DarkBgMinWhiteRatio,
                label + ": 白いノーツ画素の割合が不足 ({whiteRatio:F2} < " +
                DarkBgMinWhiteRatio + ")");
            Assert.GreaterOrEqual(meanLift, DarkBgMinMeanLift,
                label + ": 背景からのコントラスト不足 (meanLift=" +
                meanLift.ToString("F3") + " < " + DarkBgMinMeanLift + ")");
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

            AssertNotePixelsBright(withNotes, withoutNotes, "3D path", true, out _);
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
            // 出荷の「映像の上に ScreenSpaceOverlay uGUI」の合成に近づけるため、
            // フラットな単色クリアではなく明暗のむらがある映像風背景を敷く。
            CreateVideoLikeBackground(cam);

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

            AssertNotePixelsBright(withNotes, withoutNotes, "v6 uGUI path", false, out int uGUIChecked);
            Debug.Log("[V6NoteRender] v6 uGUI path: checkedPixels=" + uGUIChecked +
                " (video-like textured background)");
            AssertShadePresent(cam, RtWidth, RtHeight, "v6 uGUI path");
        }

        /// <summary>
        /// Issue #735: 出荷経路 (3D ノーツ + 実 HUD) を <b>暗い映像背景</b>の上で
        /// 合成し、ノーツ画素が絶対輝度で白いことを検証する。
        ///
        /// 既存テストは「背景が明るい画素」だけを対象にするため、暗い動画
        /// フレーム上のノーツは検証対象外だった。ここでは背景輝度で除外せず、
        /// 差分画素すべての輝度・コントラスト・「暗くならない」を計測する。
        ///
        /// 歌詞パネルは <see cref="PlaySession.LyricsEnabled"/>
        /// = false で隠す: このテストの主眼は「パネルの無い構成で
        /// マテリアル + シェード + コリドー抜きが暗背景でも白を保証する」
        /// ことであり、歌詞パネル帯そのものの検証は
        /// <see cref="Real3DNotes_LyricPanelZone_NotesStayWhite"/>
        /// (Issue #760 でカットアウト済み・同じ厳しい基準) が担う。
        /// </summary>
        [Test]
        public void Real3DNotes_StayWhiteOnDarkVideoBackground()
        {
            V6UiMode.Playfield = false;
            V6UiMode.VideoDim = false;
            // 演奏中の出荷状態を再現する (played=false だと Start Prompt と
            // 歌詞枠が出てパネル被りの計測が本筋から外れる)。
            GManager.played = true;
            PlaySession.SetLyricsEnabled(false);

            Camera cam = CreateCamera();

            // 実シーンと同じガイドメッ/マテリアル。
            float frontZ = -PlayfieldLaneGuides.FrontDepth(cam);
            var guideGo = Track(new GameObject("Lane Guides"));
            guideGo.AddComponent<MeshFilter>().sharedMesh =
                PlayfieldLaneGuides.BuildGuideMesh(frontZ);
            var guideRenderer = guideGo.AddComponent<MeshRenderer>();
            guideRenderer.sharedMaterial = PlayfieldLaneGuides.CreateGuideMaterial();
            guideRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            guideRenderer.receiveShadows = false;

            // 暗い映像フレーム (輝度 0.03〜0.16 + 弱い明暗むら)。
            CreateVideoLikeBackground(cam, 0.03f, 0.16f, 0.5f);

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

            AssertNotePixelsWhiteOnDark(withNotes, withoutNotes, "3D path dark bg");
            AssertShadePresent(cam, RtWidth, RtHeight, "3D path dark bg");
        }

        /// <summary>
        /// Issue #735 / #760: 歌詞パネル (LyricsEnabled = true, 既定) が
        /// 表示される出荷構成で、パネルに覆われた領域のノーツ画素を計測する。
        ///
        /// 歌詞パネルは黒 α≈0.6 の半透明パネルだが、Issue #760 で上部シェードと
        /// 同じレーン回廊カットアウト (<see cref="V6TopShadeHole"/> →
        /// <see cref="V6PanelCorridorHole"/>) が入った。パネル矩形内のノーツは
        /// すべてレーン帯 (= カットアウト) の内側を通るため、パネル帯の
        /// ノーツも外側と同じ厳しい白さ基準で検証する。
        /// パネル矩形は LyricSlotOrigin/Size (実配置の正本) から取り、
        /// 「外側は exclude・内側は include」で画面全体を漏れなく覆う。
        /// </summary>
        [Test]
        public void Real3DNotes_LyricPanelZone_NotesStayWhite()
        {
            V6UiMode.Playfield = false;
            V6UiMode.VideoDim = false;
            GManager.played = true;
            // 演奏中 = ノーツが画面に存在する状態を明示する
            // (歌詞パネルの枠あり/回廊クリア切替の前提条件)。
            GManager.judgmentsComplete = false;
            PlaySession.SetLyricsEnabled(true);

            Camera cam = CreateCamera();

            float frontZ = -PlayfieldLaneGuides.FrontDepth(cam);
            var guideGo = Track(new GameObject("Lane Guides"));
            guideGo.AddComponent<MeshFilter>().sharedMesh =
                PlayfieldLaneGuides.BuildGuideMesh(frontZ);
            var guideRenderer = guideGo.AddComponent<MeshRenderer>();
            guideRenderer.sharedMaterial = PlayfieldLaneGuides.CreateGuideMaterial();
            guideRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            guideRenderer.receiveShadows = false;

            CreateVideoLikeBackground(cam, 0.03f, 0.16f, 0.5f);

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

            // 歌詞スロットの矩形 (画面左上原点 → 左下原点 px へ変換)。
            // V6HudLogic.LyricSlotOrigin/Size がパネルの実配置の正本。
            // lyricFs は FHD プロファイルの実値 (ScaledLyricFs = m.LyricFs)。
            float lyricFs = FlashBeat.UI.V6.Game.Hud.V6HudMetrics
                .ForResolution(RtWidth, RtHeight).LyricFs;
            Vector2 slotOrigin = FlashBeat.UI.V6.Game.Hud.V6HudLogic.LyricSlotOrigin(
                RtWidth, RtHeight);
            Vector2 slotSize = FlashBeat.UI.V6.Game.Hud.V6HudLogic.LyricSlotSize(
                lyricFs, RtWidth, RtHeight);
            var panelRect = new Rect(
                slotOrigin.x,
                RtHeight - slotOrigin.y - slotSize.y,
                slotSize.x,
                slotSize.y);

            // パネル領域の外は暗背景テストと同じ白さを要求する。
            AssertNotePixelsWhiteOnDark(withNotes, withoutNotes,
                "3D path dark bg (lyrics on, outside lyric panel)", panelRect);

            // パネル領域の内側 (Issue #760): レーン回廊カットアウトにより
            // パネル帯のノーツも外側と同じ白さになるはずなので、
            // 同じ厳しい基準 (白・コントラスト・暗くならない) を要求する。
            // パネル矩形内のノーツ差分画素はすべてレーン帯 (= カットアウト
            // の内側) を通るため、パネルの残る暗幕に掛かるノーツ画素は無い。
            AssertNotePixelsWhiteOnDark(withNotes, withoutNotes,
                "3D path dark bg (lyrics on, lyric panel corridor)",
                null, panelRect);
        }

    }
}