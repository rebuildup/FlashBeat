using NUnit.Framework;

/// <summary>
/// Issue #849: MusicManager が「どのシーンプレイヤー (YPlayer) を使うか」を
/// 決める <see cref="MusicManager.ResolveScenePlayerIndex"/> の契約テスト。
///
/// かつては `songID < YPlayer.Length` の位置だけで選んでいたため、
/// クラウドカタログが末尾に追記した曲 (index >= 29) はシーンに平面が
/// あっても選ばれず、未検証の CameraNearPlane 経路に落ちていた。
/// 現在は videoId 一致を優先し、取れない場合だけ index に倒す。
/// </summary>
public class SceneVideoPlayerSelectionTests
{
    /// <summary>テスト用のシーン videoId 一覧 (index -> videoId)。</summary>
    static System.Func<int, string> SceneIds(params string[] ids)
    {
        return i => (i >= 0 && i < ids.Length) ? ids[i] : null;
    }

    [Test]
    public void VideoIdMatchWinsOverSongIndex()
    {
        // 内蔵曲の index と videoId がずれていても、videoId が正しい平面を選ぶ
        // (クラウドカタログの overlay で videoId が差し替わった場合など)。
        var scene = SceneIds("aaa", "bbb", "ccc");
        Assert.AreEqual(2, MusicManager.ResolveScenePlayerIndex(0, 3, "ccc", scene));
    }

    [Test]
    public void AppendedSongFindsEditorAddedPlaneByVideoId()
    {
        // エディタ追加の平面は YPlayer の末尾 (index >= 内蔵曲数) にある。
        // カタログ上の songID はさらに後ろでも、videoId で正しく引ける。
        var scene = SceneIds("aaa", "bbb", "STAYvideo", "TESTvideo");
        Assert.AreEqual(2, MusicManager.ResolveScenePlayerIndex(30, 4, "STAYvideo", scene));
        Assert.AreEqual(3, MusicManager.ResolveScenePlayerIndex(31, 4, "TESTvideo", scene));
    }

    [Test]
    public void BuiltInSongUsesVideoIdMatchLikeBefore()
    {
        // 内蔵曲: videoId がシーンの並びと一致していれば従来と同じ index。
        var scene = SceneIds("VIjqWffacio", "DeKLpgzh-qQ", "9O2VyUM5MlQ");
        Assert.AreEqual(1, MusicManager.ResolveScenePlayerIndex(1, 3, "DeKLpgzh-qQ", scene));
    }

    [Test]
    public void NoVideoIdMatchFallsBackToSongIndex()
    {
        // videoId が取れない (null) / 一致しない場合は従来どおり index。
        var scene = SceneIds("aaa", "bbb", "ccc");
        Assert.AreEqual(2, MusicManager.ResolveScenePlayerIndex(2, 3, null, scene));
        Assert.AreEqual(2, MusicManager.ResolveScenePlayerIndex(2, 3, "", scene));
        Assert.AreEqual(1, MusicManager.ResolveScenePlayerIndex(1, 3, "unknown", scene));
    }

    [Test]
    public void AppendedIndexWithoutScenePlaneReturnsMinusOne()
    {
        // シーンに平面が無い曲 (index >= playerCount) は -1 → 実行時複製経路。
        var scene = SceneIds("aaa", "bbb");
        Assert.AreEqual(-1, MusicManager.ResolveScenePlayerIndex(29, 2, "remoteOnly", scene));
        Assert.AreEqual(-1, MusicManager.ResolveScenePlayerIndex(29, 2, null, scene));
    }

    [Test]
    public void DuplicateVideoIdPicksFirstPlane()
    {
        // 同じ videoId の平面が複数ある場合 (ニセモノ等) は先頭を選ぶ。
        // 同一映像なので表示結果は変わらない。
        var scene = SceneIds("dup", "dup", "other");
        Assert.AreEqual(0, MusicManager.ResolveScenePlayerIndex(5, 3, "dup", scene));
    }

    [Test]
    public void NullOrEmptySceneIdsNeverMatch()
    {
        var scene = SceneIds(null, "", "ccc");
        Assert.AreEqual(2, MusicManager.ResolveScenePlayerIndex(9, 3, "ccc", scene));
        // videoId が空なら index に倒れる (9 >= 3 なので -1)。
        Assert.AreEqual(-1, MusicManager.ResolveScenePlayerIndex(9, 3, "", scene));
    }
}
