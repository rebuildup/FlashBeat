using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Issue #730: GameScene にシリアライズされた Judge の効果音参照を
/// シーンファイルから直接検査する回帰テスト。
///
/// 背景: かつて hitSound と clickSound が同一 GUID
/// (NoteEditor 同梱のトーン状サンプル "Click 2.wav") を指しており、
/// キー押下のクリック音が効果音ラボのクリック音源ではなく
/// 音程のあるピープ音になっていた。コード側の再生経路が正しくても
/// Scene の参照が誤っていれば製品は誤った音を出すため、
/// 「参照の同一性」と「指定音源へのバインド」をここで固定する。
///
/// 検証する要求 (実装定数の写しではなく出荷経路の契約):
///   - clickSound は指定の効果音ラボ製クリック音源
///     (Assets/Game/Audio/ 配下の音源) を指す。
///   - hitSound と clickSound は別クリップを指す (同一 GUID の再発を拒否)。
///   - 両 GUID が実在するアセットへ解決できる
///     (GUID の打ち間違い・削除済み参照を検出する)。
/// </summary>
public class GameSceneSoundBindingTests
{
    private const string ScenePath = "Assets/Game/Scenes/GameScene.unity";

    /// <summary>レーンキークリック音の指定音源ディレクトリ。</summary>
    private const string ClickSoundDir = "Assets/Game/Audio/";

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
    public void ClickSound_ResolvesToDedicatedLabClickAsset()
    {
        string guid = ClipGuid("clickSound");
        string path = AssetDatabase.GUIDToAssetPath(guid);

        Assert.IsNotEmpty(path, "clickSound の GUID がアセットへ解決できない");
        StringAssert.StartsWith(ClickSoundDir, path,
            "clickSound は Assets/Game/Audio/ の指定クリック音源を指すこと");
        StringAssert.EndsWith(".mp3", path);
    }

    [Test]
    public void HitSound_And_ClickSound_AreDifferentClips()
    {
        string hit = ClipGuid("hitSound");
        string click = ClipGuid("clickSound");

        Assert.AreNotEqual(hit, click,
            "hitSound と clickSound が同一 GUID を指している (Issue #672/#730 の再発)");
    }

    [Test]
    public void HitSound_ResolvesToExistingAsset()
    {
        string guid = ClipGuid("hitSound");
        string path = AssetDatabase.GUIDToAssetPath(guid);

        Assert.IsNotEmpty(path, "hitSound の GUID がアセットへ解決できない");
        StringAssert.StartsWith("Assets/", path);
    }
}
