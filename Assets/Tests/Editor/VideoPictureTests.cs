using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using FlashBeat.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// Issue #233: 映像プレーンの制御。
///
/// 事実の訂正 (PR #263 レビュー B2):
///   * シーンファイル (GameScene.unity) の映像プレーン ("1".."29") は
///     **すべて** <c>m_IsActive: 0</c> で置かれているが、これは
///     「シーンが映像表示をランタイムに委ねている」ためである。
///   * 実行時に <c>MusicManager.Start</c> が
///     <c>YPlayer[GManager.songID].SetActive(true)</c> で **選択した平面を
///     有効化する** ので、<c>VideoPicture.Activate</c> 内の SetActive は
///     実行時には no-op である。
///   * 映像が 1 フレームも描画されなかった **本当の原因** は、シーンの
///     VideoPlayer に target renderer も render texture も設定されて
///     いなかったこと (実行時に <c>MusicManager.WireVideoPicture</c> が
///     配線する) である。
///
/// 板 (プレーンの renderer) は prepare 完了とは独立に隠す (レビュー B1):
/// prepare が成功しない環境 (Issue #13) では prepare が終わるまで板が
/// 白いまま残るので、「prepare 完了を待つ」方式では直らない。
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
        var video = go.AddComponent<VideoPlayer>();
        // テスト中に VideoPlayer 自身が再生を始めないようにする。
        video.playOnAwake = false;
        created.Add(go);
        return go;
    }

    /// <summary>
    /// シーンは映像プレーンを非アクティブなまま置き、**実行時に選択した
    /// 平面だけを有効化する** という運用であることを固定する。
    ///
    /// (レビュー B2: 「平面は一度も有効化されなかった」という誤ったモデルを
    /// 固定していたテストを、市场に即した言い換えに置き換える。)
    /// </summary>
    [Test]
    public void TheSceneDelegatesThePictureDisplayToTheRuntime()
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

        // ランタイムが「選択した平面」を有効化している (ここが誤った原因
        // 説明の訂正点)。
        string manager = File.ReadAllText(SourcePath("Assets/Game/Scripts/Gameplay/MusicManager.cs"));
        StringAssert.Contains("YPlayer[GManager.songID].SetActive(true)", manager,
            "The runtime has to enable the picture plane of the selected song.");
    }

    /// <summary>
    /// レビュー B1: **prepare の前** から板 (白い quad) が見えないこと。
    ///
    /// 板は「その曲の平面が選択された時点」で隠され、フレームが出てきた
    /// ときだけ <see cref="VideoPicture.ShowWhenFrameReady"/> が出す。
    /// </summary>
    [Test]
    public void TheBoardIsHiddenBeforeTheVideoIsPrepared()
    {
        var go = CreatePicturePlane();
        var video = go.GetComponent<VideoPlayer>();
        var renderer = go.GetComponent<Renderer>();

        // シーンのプレーンと同じく、レンダラは有効な状態から始まる。
        Assert.IsTrue(renderer.enabled, "The board starts visible in the scene.");

        Assert.IsTrue(VideoPicture.HideBoard(video), "The board should be hideable.");
        Assert.IsFalse(renderer.enabled, "The board must be hidden before the video is prepared.");
        Assert.IsFalse(VideoPicture.IsVisible(video));

        // prepare が失敗しても、フレームは出ない → 板は出ない。
        VideoPicture.ShowWhenFrameReady(video, false);
        Assert.IsFalse(renderer.enabled, "The board must stay hidden while there is no frame.");
    }

    /// <summary>
    /// 板を隠すのは「prepare 完了を待つ」ためではなく、
    /// **平面が選択された時点** である (prepare 失敗環境でも効くため)。
    /// </summary>
    [Test]
    public void TheBoardIsHiddenWhenThePlaneIsSelectedAndNotWhenTheVideoIsPrepared()
    {
        string manager = File.ReadAllText(SourcePath("Assets/Game/Scripts/Gameplay/MusicManager.cs"));

        // 平面の選択 (Start) と同じメソッドの中で板を隠す。
        int startIndex = manager.IndexOf("void Start()", System.StringComparison.Ordinal);
        Assert.GreaterOrEqual(startIndex, 0, "MusicManager.Start is gone.");
        int nextMethod = manager.IndexOf("\n    void ", startIndex + 5, System.StringComparison.Ordinal);
        if (nextMethod < 0) nextMethod = manager.Length;
        string startBody = manager.Substring(startIndex, nextMethod - startIndex);

        Assert.IsTrue(startBody.Contains("YPlayer[GManager.songID].SetActive(true)"),
            "The runtime has to select the picture plane of the song.");
        Assert.IsTrue(startBody.Contains("VideoPicture.HideBoard(songVideo)"),
            "The board has to be hidden when the picture plane of the song is selected.");

        // 「prepare できた時だけ隠す」ではないこと (prepare が失敗する環境では
        // 白い板が残ってしまう)。
        int hide = startBody.IndexOf("VideoPicture.HideBoard(songVideo)", System.StringComparison.Ordinal);
        int prepared = startBody.IndexOf("isPrepared", System.StringComparison.Ordinal);
        Assert.IsTrue(prepared < 0 || prepared > hide,
            "The board must not wait for the video to be prepared - a failed prepare would keep the white board.");
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
    /// プレーンから **外される** ことを **挙動** で固定する (レビュー S6:
    /// ソース grep ではなく挙動で検証する)。
    /// </summary>
    [Test]
    public void TheDeadInvidiousPlayerIsRemovedFromThePicture()
    {
        var go = CreatePicturePlane();
        // 非 active のままコンポーネントを足す: Awake が走らないので
        // InvidiousVideoPlayer の Awake / OnDestroy が壊れずに済む。
        go.SetActive(false);

        var video = go.GetComponent<VideoPlayer>();
        System.Type invidiousType = FindInvidiousType();
        Assert.IsNotNull(invidiousType, "The youtube-player package (InvidiousVideoPlayer) is gone.");

        var invidious = go.AddComponent(invidiousType);
        Assert.IsNotNull(invidious, "The Invidious player component could not be added.");

        // Awake は EditMode で走らないので、OnDestroy が購読解除できる
        // ように VideoPlayer を差し込んでおく。
        SetInvidiousVideoPlayer(invidious, video);

        Assert.IsTrue(VideoPicture.Activate(video), "The picture plane should be activated.");
        Assert.IsNull(go.GetComponent(invidiousType),
            "The dead Invidious player must be removed from the picture plane.");
    }

    // --- InvidiousVideoPlayer (リフレクションで触る) ------------------------

    static System.Type FindInvidiousType()
    {
        foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            var type = assembly.GetType("YoutubePlayer.Components.InvidiousVideoPlayer");
            if (type != null) return type;
        }
        return null;
    }

    static void SetInvidiousVideoPlayer(Component invidious, VideoPlayer video)
    {
        var field = invidious.GetType().GetField("<VideoPlayer>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, "The VideoPlayer property of InvidiousVideoPlayer is gone.");
        field.SetValue(invidious, video);
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
