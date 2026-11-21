using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

/// <summary>
/// GameScene の動画プレイヤー (MusicManager.YPlayer) の並びが、カタログの曲順と
/// 一致していることを検査する (#154 §3)。
///
/// MusicManager は `YPlayer[GManager.songID]` で動画を選ぶ — つまり **位置** で
/// 結線されている。カタログ (GManager.SongURL) の並びとシーンの並びがずれると、
/// 別の曲の動画が流れる。クラウドカタログは GManager.Merge で「ローカル曲は index を
/// 保ったまま上書き・未知の.cloud 曲は末尾に追記」的方式でマージされるため順序は
/// 保たれるが、シーン側の結線を新增画で作り忘れると静かにずれる。
///
/// ここでは scene ファイルをテキストとして読み、YPlayer 配列順の
/// InvidiousVideoPlayer.VideoId が GManager.SongURL の並びと完全に一致することを
/// 確認する (scene を開かずに読むので EditMode テストが editor 状態を壊さない)。
/// </summary>
public class SceneVideoCatalogOrderTests
{
    const string ScenePath = "Assets/Game/Scenes/GameScene.unity";

    static string ReadSceneText()
    {
        Assert.IsTrue(File.Exists(ScenePath), "the game scene is missing: " + ScenePath);
        return File.ReadAllText(ScenePath);
    }

    /// <summary>MusicManager.YPlayer 配列の要素 (GameObject fileID) を取り出す。</summary>
    static List<string> ReadYPlayerGameObjectIds(string text)
    {
        int start = text.IndexOf("\n  YPlayer:");
        Assert.IsTrue(start >= 0, "MusicManager.YPlayer is missing in " + ScenePath);
        string rest = text.Substring(start);
        int end = rest.IndexOf("\n  VPlayer:");
        string array = end > 0 ? rest.Substring(0, end) : rest;

        var ids = new List<string>();
        foreach (Match m in Regex.Matches(array, @"\{fileID: (\d+)\}"))
        {
            ids.Add(m.Groups[1].Value);
        }
        return ids;
    }

    /// <summary>シーンから (YPlayer 順の VideoId 一覧, YPlayer の要素数) を取り出す。</summary>
    static List<string> ReadSceneVideoIds()
    {
        string text = ReadSceneText();

        // GameObject fileID -> VideoId (InvidiousVideoPlayer を持つオブジェクト)
        var videoIdByGameObject = new Dictionary<string, string>();
        var blocks = Regex.Matches(
            text,
            @"--- !u!\d+ &(\d+)\r?\nMonoBehaviour:(.*?)(?=--- !u!|\z)",
            RegexOptions.Singleline);
        foreach (Match block in blocks)
        {
            string body = block.Groups[2].Value;
            if (!body.Contains("InvidiousVideoPlayer")) continue;

            Match owner = Regex.Match(body, @"m_GameObject: \{fileID: (\d+)\}");
            Match id = Regex.Match(body, @"\n\s*VideoId: (\S*)");
            if (!owner.Success || !id.Success) continue;
            videoIdByGameObject[owner.Groups[1].Value] = id.Groups[1].Value;
        }

        var ids = new List<string>();
        foreach (string go in ReadYPlayerGameObjectIds(text))
        {
            Assert.IsTrue(videoIdByGameObject.ContainsKey(go),
                "YPlayer[" + ids.Count + "] (" + go + ") has no InvidiousVideoPlayer with a VideoId");
            ids.Add(videoIdByGameObject[go]);
        }
        return ids;
    }

    [Test]
    public void ScenePlayerOrderMatchesCatalogOrder()
    {
        List<string> sceneIds = ReadSceneVideoIds();
        string[] catalog = GManager.SongURL;

        Assert.AreEqual(catalog.Length, sceneIds.Count,
            "the scene has " + sceneIds.Count + " video players but the catalog has " +
            catalog.Length + " songs - the scene wiring and GManager.SongURL are out of sync");

        var wrong = new List<string>();
        for (int i = 0; i < catalog.Length; i++)
        {
            if (sceneIds[i] == catalog[i]) continue;
            wrong.Add("[" + i + "] scene=" + sceneIds[i] + " catalog=" + catalog[i]);
        }

        Assert.IsEmpty(wrong,
            "the video of a song is not the video of that song (index = song id): " +
            string.Join(", ", wrong.ToArray()));
    }

    // --- Issue #849: プレイヤー平面の親子関係 ---------------------------------
    //
    // YouTubeSceneWiring がテンプレートを無親で clone したため、
    // YoutubePlayer_ニセモノ 2 体が scene root に残り、local TRS が
    // world TRS として解釈されて quad がカメラの後ろに置かれていた。
    // すべての YPlayer 平面は "YoutubePlayer" 親の子でなければならない。

    static readonly Regex TransformBlockPattern = new Regex(
        @"--- !u!(?:4|224) &(\d+)\r?\n(?:Transform|RectTransform):(.*?)(?=--- !u!|\z)",
        RegexOptions.Singleline);

    static string TransformBody(string text, string anchor)
    {
        var m = Regex.Match(text,
            @"--- !u!(?:4|224) &" + anchor + @"\r?\n(?:Transform|RectTransform):(.*?)(?=--- !u!|\z)",
            RegexOptions.Singleline);
        return m.Success ? m.Groups[1].Value : "";
    }

    /// <summary>GameObject fileID に対応する Transform/RectTransform の anchor fileID。</summary>
    static string FindTransformAnchor(string text, string gameObjectId)
    {
        foreach (Match block in TransformBlockPattern.Matches(text))
        {
            if (block.Groups[2].Value.Contains("m_GameObject: {fileID: " + gameObjectId + "}"))
                return block.Groups[1].Value;
        }
        return null;
    }

    /// <summary>m_Name が完全一致する GameObject の anchor fileID。</summary>
    static string FindGameObjectAnchor(string text, string name)
    {
        var blocks = Regex.Matches(
            text,
            @"--- !u!1 &(\d+)\r?\nGameObject:(.*?)(?=--- !u!|\z)",
            RegexOptions.Singleline);
        foreach (Match block in blocks)
        {
            if (Regex.IsMatch(block.Groups[2].Value, @"\n  m_Name: " + Regex.Escape(name) + @"\r?\n"))
                return block.Groups[1].Value;
        }
        return null;
    }

    [Test]
    public void ScenePlayersAreChildrenOfTheYoutubePlayerParent()
    {
        string text = ReadSceneText();

        string parentGo = FindGameObjectAnchor(text, "YoutubePlayer");
        Assert.NotNull(parentGo, "the 'YoutubePlayer' parent object is missing in " + ScenePath);
        string parentTransform = FindTransformAnchor(text, parentGo);
        Assert.NotNull(parentTransform, "the 'YoutubePlayer' parent has no transform");

        var playerTransforms = new List<string>();
        foreach (string go in ReadYPlayerGameObjectIds(text))
        {
            string t = FindTransformAnchor(text, go);
            Assert.NotNull(t, "YPlayer GameObject " + go + " has no transform");
            playerTransforms.Add(t);

            string body = TransformBody(text, t);
            StringAssert.Contains("m_Father: {fileID: " + parentTransform + "}", body,
                "YPlayer GameObject " + go + " (transform " + t + ") is not a child of " +
                "'YoutubePlayer' - a root player interprets its local TRS as world TRS " +
                "and the quad lands behind the camera (issue #849)");
        }

        // scene root (SceneRoots.m_Roots) にプレイヤー平面が残っていないこと。
        int rootsStart = text.IndexOf("SceneRoots:");
        Assert.GreaterOrEqual(rootsStart, 0, "SceneRoots block is missing in " + ScenePath);
        string roots = text.Substring(rootsStart);
        foreach (string t in playerTransforms)
        {
            Assert.IsFalse(roots.Contains("- {fileID: " + t + "}"),
                "YPlayer transform " + t + " is still a scene root");
        }
    }
}
