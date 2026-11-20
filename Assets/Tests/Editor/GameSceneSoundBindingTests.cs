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
/// 音程のあるピープ音になっていた。その後 clickSound を効果音ラボ製
/// mp3 (決定ボタンを押す31) へ差し替えたが、ユーザーフィードバックで
/// 「以前使っていたクリック音源と違う」「押してからの遅れが音源自体に
/// ある」と差し戻された。mp3 はエンコーダのプライミングとデコード
/// レイテンシで WAV より鳴り出しが遅れ、先頭にも無音成分を持つため、
/// 以前出荷していた Click 2.wav (アタック即時・無圧縮) へ戻す。
///
/// 検証する要求 (実装定数の写しではなく出荷経路の契約):
///   - clickSound は以前出荷していたクリック音源
///     (Assets/NoteEditor/Audio/Click 2.wav) を指す。
///   - hitSound と clickSound は別クリップを指す (同一 GUID の再発を拒否)。
///   - 両 GUID が実在するアセットへ解決できる
///     (GUID の打ち間違い・削除済み参照を検出する)。
/// </summary>
public class GameSceneSoundBindingTests
{
    private const string ScenePath = "Assets/Game/Scenes/GameScene.unity";

    /// <summary>以前出荷していたレーンキークリック音のアセットパス。</summary>
    private const string ClickSoundPath = "Assets/NoteEditor/Audio/Click 2.wav";

    /// <summary><see cref="ClickSoundPath"/> の GUID (シーン参照の固定値)。</summary>
    private const string ClickSoundGuid = "aa667abe26ae3b041b2406bddc7d8c20";

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
    public void ClickSound_ResolvesToRestoredClickAsset()
    {
        string guid = ClipGuid("clickSound");
        string path = AssetDatabase.GUIDToAssetPath(guid);

        Assert.AreEqual(ClickSoundGuid, guid,
            "clickSound は以前出荷していたクリック音源の GUID を指すこと");
        Assert.AreEqual(ClickSoundPath, path,
            "clickSound は " + ClickSoundPath + " を指すこと");
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
