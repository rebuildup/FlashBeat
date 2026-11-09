using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using FlashBeat.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// Issue #233 (-fact 2): 映像プレーンの有効化。
///
/// GameScene.unity の映像プレーン ("1".."29") は **すべて**
/// <c>m_IsActive: 0</c> で置かれ、MusicManager が有効化していたのは
/// YoutubePlayer の親だけだった。そのため映像は 1 フレームも描画されなかった。
///
/// シーンファイルは編集しないので、実行時の有効化が必須になる。
/// ここでは「シーンが非アクティブであること」を固定し、実行時の
/// 有効化 (板の出し分け) を検証する。
/// </summary>
public class VideoPictureTests
{
    const string ScenePath = "Game/Scenes/GameScene.unity";

    [TearDown]
    public void TearDown()
    {
        foreach (var go in new List<GameObject>(created))
        {
            if (go != null) Object.DestroyImmediate(go);
        }
        created.Clear();
    }

    readonly List<GameObject> created = new List<GameObject>();

    GameObject CreatePicturePlane()
    {
        var go = new GameObject("PicturePlane");
        go.AddComponent<MeshFilter>();
        go.AddComponent<MeshRenderer>();
        go.AddComponent<VideoPlayer>();
        created.Add(go);
        return go;
    }

    /// <summary>
    /// シーンの映像プレーンが非アクティブであることを固定する。
    /// これが崩れると「実行時に有効化する」理由がなくなるので、
    /// 修正が不要になったことを検出できる。
    /// </summary>
    [Test]
    public void EveryPicturePlaneInTheSceneIsInactive()
    {
        string scene = File.ReadAllText(SourcePath("Assets/" + ScenePath));
        var videoFileIds = ReadVideoPlayerFileIds(scene);
        Assert.GreaterOrEqual(videoFileIds.Count, 20, "The scene should carry one picture plane per song.");

        int inactive = 0;
        foreach (string fileId in videoFileIds)
        {
            var block = FindBlock(scene, fileId);
            if (block == null) continue; // a prefab instance, not a scene object
            string goFileId = Regex.Match(block, @"m_GameObject: \{fileID: (-?\d+)\}").Groups[1].Value;
            var goBlock = FindBlock(scene, goFileId);
            if (goBlock == null) continue;

            string active = Regex.Match(goBlock, @"m_IsActive: (\d)").Groups[1].Value;
            Assert.AreEqual("0", active,
                "The picture planes are expected to be inactive in the scene (the runtime enables them).");
            inactive++;
        }

        Assert.GreaterOrEqual(inactive, 20, "No inactive picture plane was found in the scene.");
    }

    /// <summary>
    /// 有効化するとプレーンは動くが、映像のフレームが 1 枚も出ていない
    /// 間は板 (白) を隠す。
    /// </summary>
    [Test]
    public void TheBoardStaysHiddenUntilThePictureHasAFrame()
    {
        var go = CreatePicturePlane();
        go.SetActive(false);
        var video = go.GetComponent<VideoPlayer>();

        Assert.IsTrue(VideoPicture.Activate(video), "The picture plane should be activated.");
        Assert.IsTrue(go.activeSelf, "The picture plane must be active after the activation.");
        Assert.IsFalse(go.GetComponent<Renderer>().enabled,
            "The board must stay hidden while there is no video frame (it would be a white quad).");
        Assert.IsFalse(VideoPicture.IsVisible(video));

        // フレームがまだ無い: 板は出さない。
        VideoPicture.ShowWhenFrameReady(video, false);
        Assert.IsFalse(go.GetComponent<Renderer>().enabled);

        // フレームが出た: 板を出す。
        VideoPicture.ShowWhenFrameReady(video, true);
        Assert.IsTrue(go.GetComponent<Renderer>().enabled, "The board must appear once a frame is ready.");
        Assert.IsTrue(VideoPicture.IsVisible(video));
    }

    /// <summary>
    /// 有効化は冪等 (2 回呼んでも壊れない)。
    /// </summary>
    [Test]
    public void ActivatingTwiceKeepsThePlaneUsable()
    {
        var go = CreatePicturePlane();
        go.SetActive(false);
        var video = go.GetComponent<VideoPlayer>();

        Assert.IsTrue(VideoPicture.Activate(video));
        Assert.IsTrue(VideoPicture.Activate(video));
        Assert.IsTrue(go.activeSelf);

        VideoPicture.ShowWhenFrameReady(video, true);
        Assert.IsTrue(VideoPicture.IsVisible(video));
    }

    /// <summary>
    /// 死んだ Invidious 経路 (機能しない公開インスタンス) が
    /// プレーンに残らないことをソースで固定する。
    /// </summary>
    [Test]
    public void TheDeadInvidiousPlayerIsRemovedFromThePicture()
    {
        string source = File.ReadAllText(SourcePath("Assets/Game/Scripts/Gameplay/VideoPicture.cs"));
        StringAssert.Contains("GetComponent<InvidiousVideoPlayer>", source,
            "The picture plane must drop the Invidious player component.");
        StringAssert.Contains("Destroy", source,
            "The Invidious player component must be destroyed, not only disabled.");

        // MusicManager が実際にこの補助を呼んでいること
        // (呼ばないと補助が死んだコードになる)。
        string manager = File.ReadAllText(SourcePath("Assets/Game/Scripts/Gameplay/MusicManager.cs"));
        StringAssert.Contains("VideoPicture.Activate(songVideo)", manager,
            "MusicManager must activate the picture plane at runtime.");
        StringAssert.Contains("VideoPicture.ShowWhenFrameReady(songVideo", manager,
            "MusicManager must reveal the board once the video has frames.");
    }

    // --- シーンのパース補助 -------------------------------------------------

    static List<string> ReadVideoPlayerFileIds(string scene)
    {
        var result = new List<string>();
        Match manager = Regex.Match(scene, @"VPlayer:\n((?:\s*- \{fileID: -?\d+\}\n)+)");
        if (!manager.Success) return result;

        foreach (Match item in Regex.Matches(manager.Groups[1].Value, @"fileID: (-?\d+)"))
        {
            result.Add(item.Groups[1].Value);
        }
        return result;
    }

    static string FindBlock(string scene, string fileId)
    {
        Match m = Regex.Match(scene, @"--- !u!\d+ &" + Regex.Escape(fileId) + @"\n(.*?)(?=\n--- !u!|\z)",
            RegexOptions.Singleline);
        return m.Success ? m.Groups[1].Value : null;
    }

    static string SourcePath(string relative)
    {
        string absolute = Path.Combine(Application.dataPath, relative.Substring("Assets/".Length));
        if (File.Exists(absolute)) return absolute;
        return relative;
    }
}
