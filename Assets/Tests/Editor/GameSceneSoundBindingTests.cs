using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Issue #791: GameScene にシリアライズされた Judge の効果音参照を
/// シーンファイルから直接検査する回帰テスト。
///
/// 背景と確定した結論 (v0.2.1 実機受入 + git 履歴 + 音源の出荷経路で検証済み):
///   - レーンキーのクリック音の正本は カーソル移動4.mp3 (GUID ac29...)。
///     出荷済みの UI `sound` (全シーン) と Judge.hitSound が昔から
///     この 1 本を指しており、ユーザー確認でもこの音源が正しいと確定した。
///   - かつてこのファイルは Assets/Game/Resources/Musics/ に置かれていたが、
///     Resources/Musics は BuildScript.MediaExcludePaths でビルド時に
///     プロジェクト外へ退避される (song-free media policy)。そのため
///     リリースビルドでは AudioClip が欠落し、クリック音も UI の決定音も
///     鳴らなかった (Issue #791 の実機症状)。
///   - 対応としてファイルごと Assets/Game/Audio/ へ移動した (GUID は .meta
///     ごと維持 = ac29...)。ビルド除外の対象外なので、シーン側の参照は
///     すべてそのまま解決される。
///   - clickSound は hitSound と同じクリップを指す。押下 1 回につき
///     クリック音が 1 回鳴り (空押しを含む)、ヒット時は Judge.PressLane の
///     「hitSound == clickSound なら重ねて鳴らさない」ガード (Issue #672、
///     JudgeClickSoundTests で固定) により二重発音しない。
///
/// 検証する要求 (実装定数の写しではなく出荷経路の契約):
///   - clickSound / hitSound はどちらも カーソル移動4.mp3 の GUID を指す。
///   - その GUID が実在するアセットへ解決でき、かつパスが
///     Resources/Musics の外 (ビルド除外の対象外) にあること。
/// </summary>
public class GameSceneSoundBindingTests
{
    private const string ScenePath = "Assets/Game/Scenes/GameScene.unity";

    /// <summary>クリック音 / ヒット音のアセットパス (ビルド同梱される場所)。</summary>
    private const string ClickSoundPath = "Assets/Game/Audio/カーソル移動4.mp3";

    /// <summary><see cref="ClickSoundPath"/> の GUID (シーン参照の固定値)。</summary>
    private const string ClickSoundGuid = "ac2961a545ba50e40bfbbccacb3fe8d7";

    /// <summary>
    /// ビルド時にプロジェクト外へ退避されるディレクトリ
    /// (BuildScript.MediaExcludePaths の "Assets/Game/Resources/Musics")。
    /// 効果音をここに置くとリリースビルドで欠落する (Issue #791)。
    /// </summary>
    private const string BuildExcludedPrefix = "Assets/Game/Resources/Musics/";

    private static string SceneText =>
        File.ReadAllText(Path.Combine(Application.dataPath, "Game/Scenes/GameScene.unity"));

    /// <summary>
    /// Judge MonoBehaviour ブロック内の hitSound / clickSound 行から GUID を取る。
    /// 行は `hitSound: {fileID: 8300000, guid: &lt;32hex&gt;, type: 3}` 形式。
    /// </summary>
    private static string ClipGuid(string field)
    {
        var m = Regex.Match(SceneText, $@"^\s*{field}: \{{fileID: \d+, guid: ([0-9a-f]{{32}})",
            RegexOptions.Multiline);
        Assert.IsTrue(m.Success, $"GameScene.unity に {field} の GUID 参照が見つからない");
        return m.Groups[1].Value;
    }

    [Test]
    public void ClickSound_ResolvesToCursorMove4()
    {
        string guid = ClipGuid("clickSound");
        string path = AssetDatabase.GUIDToAssetPath(guid);

        Assert.AreEqual(ClickSoundGuid, guid,
            "clickSound は カーソル移動4.mp3 の GUID を指すこと (Issue #791)");
        Assert.AreEqual(ClickSoundPath, path,
            "clickSound は " + ClickSoundPath + " を指すこと");
    }

    [Test]
    public void HitSound_ResolvesToCursorMove4()
    {
        string guid = ClipGuid("hitSound");
        string path = AssetDatabase.GUIDToAssetPath(guid);

        Assert.AreEqual(ClickSoundGuid, guid,
            "hitSound は初版から出荷されている カーソル移動4.mp3 の GUID を指すこと");
        Assert.AreEqual(ClickSoundPath, path,
            "hitSound は " + ClickSoundPath + " を指すこと");
    }

    [Test]
    public void ClickSound_And_HitSound_ShareTheSameShippedClip()
    {
        // Issue #791: クリック音とヒット音は同一クリップでよい。
        // 押下ごとのクリックは PlayLaneClickSound が鳴らし、ヒット時の
        // 重複再生は Judge.PressLane の同一クリップガードが防ぐ
        // (JudgeClickSoundTests.Update_SameClipForHitAndClick_DoesNotDoublePlay)。
        Assert.AreEqual(ClipGuid("hitSound"), ClipGuid("clickSound"),
            "clickSound と hitSound は同じ カーソル移動4.mp3 を指すこと (Issue #791)");
    }

    [Test]
    public void EffectSounds_AreNotUnderTheBuildExcludedDirectory()
    {
        // Issue #791 の根本原因: Resources/Musics はリリースビルドから
        // 退避されるため、そこに置いた効果音は出荷物に存在しない。
        foreach (string field in new[] { "hitSound", "clickSound" })
        {
            string path = AssetDatabase.GUIDToAssetPath(ClipGuid(field));
            Assert.IsFalse(path.StartsWith(BuildExcludedPrefix, System.StringComparison.Ordinal),
                field + " がビルド除外ディレクトリ (" + BuildExcludedPrefix + ") 配下にある: " + path);
        }
    }
}
