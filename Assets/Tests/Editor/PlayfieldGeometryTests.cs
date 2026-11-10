using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using FlashBeat.Gameplay;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Issue #233: 3D プレイフィールドの幾何を **シーンファイルから再導出する**。
///
/// 判定線 (z = 0) から見られるのはカメラが z &lt;= 8.61 までしか見ないためで、
/// その 8.61 は GameScene の Main Camera (位置 / 向き / FOV) から決まる。
/// カメラを直すと「見える時間 (= 反応できる時間)」が変わるため、
/// 数値をコードに直書きするだけでなく **シーンから再導出できる** 状態で保つ。
///
/// 画面的比例 (1920x1080 = 1.778 と 2560x1664 = 1.538) の **両方** で、
/// 8 レーンと **ノーツ幅 (1.0)** が入ること、判定線 (z = 0) と
/// 1.6 秒のアプローチ時間の不変条件が変わらないことを固定する
/// (PR #263 レビュー S1)。
///
/// シーンファイル (GameScene.unity) は読み取り専用で、テストは変更しない。
/// </summary>
public class PlayfieldGeometryTests
{
    private const string SceneRelativePath = "Assets/Game/Scenes/GameScene.unity";

    /// <summary>ノーツの平面 (y)。NotesManager がノーツを置く高さ。</summary>
    private const float NotePlaneY = MediaTimeline.NotePlaneY;

    /// <summary>
    /// ノートの幅 (ワールド x 単位)。Note.prefab は builtin Quad (1x1) を
    /// localScale x = 1 で置くので幅 1.0。
    /// </summary>
    private const float NoteWidth = 1f;

    /// <summary>1920x1080 (16:9) と 2560x1664 (展示機 / 証拠採取時の表示)。</summary>
    private const int DesignWidth = 1920;
    private const int DesignHeight = 1080;
    private const int ExhibitWidth = 2560;
    private const int ExhibitHeight = 1664;

    [TearDown]
    public void TearDown()
    {
        foreach (var go in new List<GameObject>(created))
        {
            if (go != null) UnityEngine.Object.DestroyImmediate(go);
        }
        created.Clear();
    }

    readonly List<GameObject> created = new List<GameObject>();

    /// <summary>
    /// シーンから読み取ったカメラ (Main Camera)。
    /// </summary>
    private struct CameraSetup
    {
        public Vector3 position;
        public Quaternion rotation;
        public float fieldOfView;
    }

    /// <summary>
    /// 8.61 という数字がシーンファイルと整合している (実測 8.6143)。
    /// </summary>
    [Test]
    public void TheVisibleDepthMatchesTheCameraInTheSceneFile()
    {
        CameraSetup camera = ReadMainCamera();
        float derived = VisibleDepth(camera);

        Assert.Greater(derived, 0f, "The camera does not see the note plane at all.");
        Assert.AreEqual(MediaTimeline.VisibleDepthZ, derived, 0.05f,
            "MediaTimeline.VisibleDepthZ does not match the camera in " + SceneRelativePath
            + " (derived " + derived.ToString("F4") + "). Re-derive it.");
    }

    /// <summary>
    /// ノーツ速度 10 のとき、判定線に着く 1.6 秒前にノーツが画面奥の端に
    /// ちょうど見える (設計意図 = ApproachSecondsAtSpeed10)。
    /// </summary>
    [Test]
    public void TheVisibleWindowAtNoteSpeed10IsTheDesignedOnePointSixSeconds()
    {
        float zScale = MediaTimeline.ZScaleFor(10f);
        float window = MediaTimeline.VisibleDepthZ / zScale;

        Assert.AreEqual(MediaTimeline.ApproachSecondsAtSpeed10, window, 0.0001f,
            "The 3D lane no longer shows the designed approach time at note speed 10.");
        Assert.GreaterOrEqual(window, MediaTimeline.ApproachSecondsAtSpeed10);

        // 判定線に着く 1.6 秒前 = 画面奥の端 (z = VisibleDepthZ)。
        const float noteTime = 12.5f;
        float zAtEdge = MediaTimeline.NotePositionZ(noteTime, noteTime - MediaTimeline.ApproachSecondsAtSpeed10, zScale);
        Assert.AreEqual(MediaTimeline.VisibleDepthZ, zAtEdge, 0.0001f);
    }

    /// <summary>
    /// ノーツ速度を上げると「見える時間」が 1/speed に比例して短くなる
    /// (速度オプションの意味が変わっていないこと)。
    /// </summary>
    [Test]
    public void AFasterNoteSpeedShortensTheVisibleApproach()
    {
        float windowAt10 = MediaTimeline.VisibleDepthZ / MediaTimeline.ZScaleFor(10f);
        float windowAt20 = MediaTimeline.VisibleDepthZ / MediaTimeline.ZScaleFor(20f);
        float windowAt5 = MediaTimeline.VisibleDepthZ / MediaTimeline.ZScaleFor(5f);

        Assert.AreEqual(windowAt10 * 0.5f, windowAt20, 0.0001f);
        Assert.AreEqual(windowAt10 * 2f, windowAt5, 0.0001f);
    }

    /// <summary>
    /// 8 レーン (レーン中心 **と ノーツ幅 1.0**) と 9 本の境界線が、
    /// 判定線の位置でカメラに収まる。1920x1080 と 2560x1664 の両方で。
    /// </summary>
    [TestCase(DesignWidth, DesignHeight)]
    [TestCase(ExhibitWidth, ExhibitHeight)]
    public void EveryLaneWithTheNoteWidthFitsIntoTheCameraViewAtTheHitLine(int width, int height)
    {
        CameraSetup setup = ReadMainCamera();
        Camera camera = CreateCamera(setup);
        PlayfieldCameraFit.Apply(camera, (float)width / height);

        string aspect = width + "x" + height;

        float halfWidth = MediaTimeline.HalfWidthAtHitLine(camera);
        Assert.GreaterOrEqual(halfWidth, PlayfieldCameraFit.RequiredHalfWidth,
            "The visible half width at the judge line is too small for " + aspect + ".");

        for (int lane = 0; lane < Judge.LaneCount; lane++)
        {
            float center = Judge.LaneWorldX(lane);
            Assert.IsTrue(MediaTimeline.IsVisible(camera, new Vector3(center, NotePlaneY, 0f)),
                "Lane " + lane + " is not visible at the hit line (" + aspect + ").");

            // レーン中心ではなく **ノーツ全体** (幅 1.0) が入っていること。
            float noteEdge = center + (lane < Judge.LaneCount / 2 ? -NoteWidth * 0.5f : NoteWidth * 0.5f);
            Assert.IsTrue(MediaTimeline.IsVisible(camera, new Vector3(noteEdge, NotePlaneY, 0f)),
                "The note of lane " + lane + " is cut off at the hit line (" + aspect + ").");
            Assert.LessOrEqual(Mathf.Abs(noteEdge), halfWidth,
                "The note of lane " + lane + " reaches outside of the view (" + aspect + ").");
        }

        for (int i = 0; i < PlayfieldLaneGuides.GuideCount; i++)
        {
            Assert.IsTrue(MediaTimeline.IsVisible(camera, new Vector3(PlayfieldLaneGuides.LaneBoundaryX(i), NotePlaneY, 0f)),
                "The lane boundary " + i + " is not visible at the hit line (" + aspect + ").");
        }
    }

    /// <summary>
    /// 判定線 (z = 0) の幾何と時間 (1.6 秒のアプローチ) の不変条件は、
    /// 画面のアスペクト比が何であっても変わらない。
    ///
    /// (水平の fit だけを変えるので、垂直方向の幾何 = 可視深度 = z スケールは
    /// 変わらない。ノーツは自分の判定時刻ちょうどに z = 0 を通る。)
    /// </summary>
    [TestCase(DesignWidth, DesignHeight)]
    [TestCase(ExhibitWidth, ExhibitHeight)]
    public void TheJudgeLineGeometryAndTheTimingAreTheSameAtEveryAspect(int width, int height)
    {
        CameraSetup setup = ReadMainCamera();
        Camera camera = CreateCamera(setup);
        PlayfieldCameraFit.Apply(camera, (float)width / height);

        // 可視深度 (縦方向) は変わらない。
        float depth = MediaTimeline.VisibleDepth(camera);
        Assert.AreEqual(MediaTimeline.VisibleDepthZ, depth, 0.05f,
            "The camera fit must not change the vertical geometry.");

        // 1.6 秒の可視時間。z スケールは定数 (カメラから再導出する値) から
        // 決まるので.camera の深度は Scene の値と誤差の範囲で一致する。
        float zScale = MediaTimeline.ZScaleFor(10f);
        Assert.AreEqual(MediaTimeline.ApproachSecondsAtSpeed10, MediaTimeline.VisibleDepthZ / zScale, 0.0001f,
            "The approach time must stay the designed one at every aspect.");
        Assert.AreEqual(MediaTimeline.ApproachSecondsAtSpeed10, depth / zScale, 0.001f,
            "The approach time of the camera in the scene must stay the designed one at every aspect.");

        // ノーツは自分の判定時刻に判定線 (z = 0) を通る。
        const float noteTime = 9.25f;
        Assert.AreEqual(0f, MediaTimeline.NotePositionZ(noteTime, noteTime, zScale), 0.0001f);
        Assert.Greater(MediaTimeline.NotePositionZ(noteTime, noteTime - 1f, zScale), 0f);
    }

    /// <summary>
    /// 負の対照: fit を適用しないと、2560x1664 では外側レーンのノーツが
    /// 画面外に出る (これが S1 の症状)。
    /// </summary>
    [Test]
    public void WithoutTheFitTheOuterNoteWouldBeCutOffOnTheExhibitDisplay()
    {
        CameraSetup setup = ReadMainCamera();
        Camera camera = CreateCamera(setup);
        camera.aspect = (float)ExhibitWidth / ExhibitHeight;

        float halfWidth = MediaTimeline.HalfWidthAtHitLine(camera);
        float outerNoteEdge = Mathf.Abs(Judge.LaneWorldX(0)) + NoteWidth * 0.5f;

        Assert.Less(halfWidth, outerNoteEdge,
            "The 1.538 aspect is expected to cut the outer lane off without the camera fit "
            + "(half width " + halfWidth.ToString("F3") + " vs note edge " + outerNoteEdge.ToString("F3") + ").");

        // fit を適用すれば外側レーンが入る。
        PlayfieldCameraFit.Apply(camera, (float)ExhibitWidth / ExhibitHeight);
        Assert.GreaterOrEqual(MediaTimeline.HalfWidthAtHitLine(camera), outerNoteEdge,
            "With the camera fit the outer lane has to fit into the view.");
    }

    /// <summary>
    /// ゲーム画面のカメラには実際に fit が適用される (Main Camera は
    /// MainCamera タグなので Camera.main で拾える)。
    /// </summary>
    [Test]
    public void TheGameCameraIsFittedAtRuntime()
    {
        var go = new GameObject("FittedMainCamera", typeof(Camera));
        created.Add(go);
        go.tag = "MainCamera";
        Camera camera = go.GetComponent<Camera>();

        // 他の MainCamera カメラがあると Camera.main がそちらを拾うので、
        // 一時的に無効にする (テストの後で戻す)。
        var others = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
        var disabled = new List<Camera>();
        for (int i = 0; i < others.Length; i++)
        {
            if (others[i] == camera) continue;
            if (!others[i].CompareTag("MainCamera")) continue;
            // 元々無効だったカメラは有効化しない (後処理で「元に戻した」ことに
            // なるうえ、後続テストの Camera.main を壊す)。
            if (!others[i].enabled) continue;
            others[i].enabled = false;
            disabled.Add(others[i]);
        }

        try
        {
            PlayfieldCameraFit.EnsureApplied();

            Assert.AreEqual(PlayfieldCameraFit.FittedAspect(PlayfieldCameraFit.ScreenAspect()), camera.aspect, 0.0001f,
                "The game camera has to render with the fitted aspect.");
        }
        finally
        {
            for (int i = 0; i < disabled.Count; i++)
            {
                if (disabled[i] != null) disabled[i].enabled = true;
            }
        }
    }

    /// <summary>
    /// 3D プレイフィールドが起動時に fit を適用すること (実行時に
    /// 呼ばれることの固定)。
    /// </summary>
    [Test]
    public void ThePlayfieldAppliesTheCameraFitAtRuntime()
    {
        string source = File.ReadAllText(SourcePath("Assets/Game/Scripts/Gameplay/PlayfieldLaneGuides.cs"));
        StringAssert.Contains("PlayfieldCameraFit.Apply(", source,
            "The playfield has to apply the camera fit at runtime.");
    }

    /// <summary>
    /// 生成されたレーン境界線が、8 レーンを隙間なく覆う。
    /// </summary>
    [Test]
    public void TheLaneGuidesCoverEveryLaneWithoutLeavingAGap()
    {
        Assert.AreEqual(Judge.LaneCount + 1, PlayfieldLaneGuides.GuideCount);

        float left = PlayfieldLaneGuides.LaneBoundaryX(0);
        float right = PlayfieldLaneGuides.LaneBoundaryX(PlayfieldLaneGuides.GuideCount - 1);

        // 境界はレーン中心の左右 0.5 = レーン幅の半分。
        Assert.AreEqual(Judge.LaneWorldX(0) - Judge.LaneSpacing * 0.5f, left, 0.0001f);
        Assert.AreEqual(Judge.LaneWorldX(Judge.LaneCount - 1) + Judge.LaneSpacing * 0.5f, right, 0.0001f);

        for (int i = 1; i < PlayfieldLaneGuides.GuideCount; i++)
        {
            float previous = PlayfieldLaneGuides.LaneBoundaryX(i - 1);
            float current = PlayfieldLaneGuides.LaneBoundaryX(i);
            Assert.AreEqual(Judge.LaneSpacing, current - previous, 0.0001f,
                "The lane guides leave a gap or overlap at boundary " + i + ".");
        }

        // ガイドはカメラ可視範囲まで届く (先端が画面内で見える)。
        Assert.GreaterOrEqual(PlayfieldLaneGuides.GuideDepth, MediaTimeline.VisibleDepthZ);
    }

    /// <summary>
    /// ノーツは幾何を導出したのと同じ面 (y = 0.55) に置かれる。
    /// (NotesManager の生成高さ.EXPECTED が変わったら幾何がずれる)
    /// </summary>
    [Test]
    public void TheNotesAreCreatedOnThePlaneTheGeometryIsDerivedFrom()
    {
        string source = File.ReadAllText(SourcePath("Assets/Game/Scripts/Gameplay/NotesManager.cs"));
        StringAssert.Contains("0.55f", source,
            "NotesManager must place the notes on the note plane the geometry is derived from.");
        StringAssert.Contains("Instantiate(noteObj", source);
    }

    /// <summary>
    /// 旧 "Lane" の板は非アクティブなままにする。
    /// 暗い半透明のマテリアルなので、有効化すると動画の下半分を覆う。
    /// </summary>
    [Test]
    public void TheLegacyLaneBoardStaysDisabledBecauseItWouldCoverTheVideo()
    {
        string text = File.ReadAllText(ScenePath());
        string block = FindGameObjectBlock(text, "Lane");
        Assert.IsNotNull(block, "The 'Lane' object is gone from " + SceneRelativePath);
        StringAssert.Contains("m_IsActive: 0", block,
            "The 'Lane' board must stay disabled - its dark material covers the video.");
    }

    // --- シーンの読み取り -------------------------------------------------

    private static string ScenePath()
    {
        string absolute = Path.Combine(Application.dataPath, "Game/Scenes/GameScene.unity");
        if (File.Exists(absolute)) return absolute;
        return SceneRelativePath;
    }

    private static string SourcePath(string relative)
    {
        string absolute = Path.Combine(Application.dataPath, relative.Substring("Assets/".Length));
        if (File.Exists(absolute)) return absolute;
        return relative;
    }

    private static string FindGameObjectBlock(string text, string name)
    {
        foreach (string block in SplitBlocks(text))
        {
            // The split removes the "\n--- !u!" prefix, so a GameObject block
            // starts with its class id ("1 &<fileId>").
            if (!block.StartsWith("1 &")) continue;
            if (Regex.IsMatch(block, @"m_Name: " + Regex.Escape(name) + "\r?\n")) return block;
        }
        return null;
    }

    /// <summary>シーンファイルを "--- !u!class &amp;fileId" ごとに分ける。</summary>
    private static IEnumerable<string> SplitBlocks(string text)
    {
        string[] parts = text.Split(new[] { "\n--- !u!" }, StringSplitOptions.None);
        for (int i = 0; i < parts.Length; i++)
        {
            yield return parts[i];
        }
    }

    private static CameraSetup ReadMainCamera()
    {
        string text = File.ReadAllText(ScenePath());
        string gameObject = FindGameObjectBlock(text, "Main Camera");
        Assert.IsNotNull(gameObject, "The 'Main Camera' object is gone from " + SceneRelativePath);

        Match idMatch = Regex.Match(gameObject, @"^1 &(\d+)");
        Assert.IsTrue(idMatch.Success, "Could not read the file id of the 'Main Camera' object.");
        string goId = idMatch.Groups[1].Value;

        CameraSetup setup = new CameraSetup();
        bool gotTransform = false;
        bool gotCamera = false;

        foreach (string block in SplitBlocks(text))
        {
            if (!Regex.IsMatch(block, @"m_GameObject: \{fileID: " + goId + @"\}")) continue;

            Match position = Regex.Match(block, @"m_LocalPosition: \{x: ([-\d.eE]+), y: ([-\d.eE]+), z: ([-\d.eE]+)\}");
            if (position.Success)
            {
                setup.position = new Vector3(
                    float.Parse(position.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture),
                    float.Parse(position.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture),
                    float.Parse(position.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture));
            }

            Match rotation = Regex.Match(block, @"m_LocalRotation: \{x: ([-\d.eE]+), y: ([-\d.eE]+), z: ([-\d.eE]+), w: ([-\d.eE]+)\}");
            if (rotation.Success)
            {
                setup.rotation = new Quaternion(
                    float.Parse(rotation.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture),
                    float.Parse(rotation.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture),
                    float.Parse(rotation.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture),
                    float.Parse(rotation.Groups[4].Value, System.Globalization.CultureInfo.InvariantCulture));
                gotTransform = true;
            }

            Match fov = Regex.Match(block, @"field of view: ([-\d.eE]+)");
            if (fov.Success)
            {
                setup.fieldOfView = float.Parse(fov.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                gotCamera = true;
            }
        }

        Assert.IsTrue(gotTransform, "The transform of the 'Main Camera' is missing in " + SceneRelativePath);
        Assert.IsTrue(gotCamera, "The camera component of the 'Main Camera' is missing in " + SceneRelativePath);
        return setup;
    }

    /// <summary>
    /// シーンのカメラと同じ設定のカメラをテスト用に作る (本番の計算を
    /// そのまま使うため)。
    /// </summary>
    private Camera CreateCamera(CameraSetup setup)
    {
        var go = new GameObject("PlayfieldGeometryTestCamera");
        created.Add(go);

        var camera = go.AddComponent<Camera>();
        go.transform.position = setup.position;
        go.transform.rotation = setup.rotation;
        camera.fieldOfView = setup.fieldOfView;
        return camera;
    }

    /// <summary>
    /// 視錐台の上端がノーツ面 (y = notePlaneY) に当たる z (本番の計算)。
    /// </summary>
    private float VisibleDepth(CameraSetup setup)
    {
        Camera camera = CreateCamera(setup);
        return MediaTimeline.VisibleDepth(camera, NotePlaneY);
    }
}
