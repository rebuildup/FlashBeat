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
/// シーンファイル (GameScene.unity) は読み取り専用で、テストは変更しない。
/// </summary>
public class PlayfieldGeometryTests
{
    private const string SceneRelativePath = "Assets/Game/Scenes/GameScene.unity";

    /// <summary>ノーツの平面 (y)。NotesManager がノーツを置く高さ。</summary>
    private const float NotePlaneY = 0.55f;

    /// <summary>参照アスペクト比 (16:9 = 1920x1080 / 2560x1440)。</summary>
    private const float ReferenceAspect = 16f / 9f;

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
        float derived = VisibleDepth(camera, NotePlaneY);

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
    /// 8 レーン (と 9 本の境界線) が判定線の位置でカメラに収まる。
    /// </summary>
    [Test]
    public void AllEightLanesFitIntoTheCameraViewAtTheHitLine()
    {
        CameraSetup camera = ReadMainCamera();

        for (int lane = 0; lane < Judge.LaneCount; lane++)
        {
            Assert.IsTrue(IsVisible(camera, Judge.LaneWorldX(lane), NotePlaneY, 0f, ReferenceAspect),
                "Lane " + lane + " is not visible at the hit line (16:9).");
        }

        for (int i = 0; i < PlayfieldLaneGuides.GuideCount; i++)
        {
            Assert.IsTrue(IsVisible(camera, PlayfieldLaneGuides.LaneBoundaryX(i), NotePlaneY, 0f, ReferenceAspect),
                "The lane boundary " + i + " is not visible at the hit line (16:9).");
        }
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

    // --- シーンの読み取り ---

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
    /// 視錐台の上端がノーツ面 (y = notePlaneY) に当たる z。
    /// </summary>
    private static float VisibleDepth(CameraSetup camera, float notePlaneY)
    {
        Vector3 forward = camera.rotation * Vector3.forward;
        Vector3 up = camera.rotation * Vector3.up;
        float half = camera.fieldOfView * 0.5f * Mathf.Deg2Rad;

        Vector3 top = forward * Mathf.Cos(half) + up * Mathf.Sin(half);
        float t = (notePlaneY - camera.position.y) / top.y;
        return camera.position.z + top.z * t;
    }

    /// <summary>点が視錐台の内側か (指定アスペクト比で)。</summary>
    private static bool IsVisible(CameraSetup camera, float x, float y, float z, float aspect)
    {
        Vector3 forward = camera.rotation * Vector3.forward;
        Vector3 up = camera.rotation * Vector3.up;
        Vector3 right = camera.rotation * Vector3.right;

        Vector3 d = new Vector3(x, y, z) - camera.position;
        float depth = Vector3.Dot(d, forward);
        if (depth <= 0f) return false;

        float half = camera.fieldOfView * 0.5f * Mathf.Deg2Rad;
        float tanVertical = Mathf.Tan(half);
        float tanHorizontal = tanVertical * aspect;

        return Mathf.Abs(Vector3.Dot(d, right)) / depth <= tanHorizontal
            && Mathf.Abs(Vector3.Dot(d, up)) / depth <= tanVertical;
    }
}
