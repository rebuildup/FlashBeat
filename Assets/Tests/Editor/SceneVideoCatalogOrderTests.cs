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

    /// <summary>シーンから (YPlayer 順の VideoId 一覧, YPlayer の要素数) を取り出す。</summary>
    static List<string> ReadSceneVideoIds()
    {
        Assert.IsTrue(File.Exists(ScenePath), "the game scene is missing: " + ScenePath);
        string text = File.ReadAllText(ScenePath);

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

        // MusicManager の YPlayer 配列 (GameObject の fileID 列)
        int start = text.IndexOf("\n  YPlayer:");
        Assert.IsTrue(start >= 0, "MusicManager.YPlayer is missing in " + ScenePath);
        string rest = text.Substring(start);
        int end = rest.IndexOf("\n  VPlayer:");
        string array = end > 0 ? rest.Substring(0, end) : rest;

        var ids = new List<string>();
        foreach (Match m in Regex.Matches(array, @"\{fileID: (\d+)\}"))
        {
            string go = m.Groups[1].Value;
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
}
