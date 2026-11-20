using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Issue #730: GameScene にシリアライズされた Judge の効果音参照を
/// シーンファイルから直接検査する回帰テスト。
///
/// 背景と確定した結論 (git 履歴 + 音響解析で検証済み):
///   - 出荷済みの効果音は初版 (ee11d764) からずっと
///     カーソル移動4.mp3 (GUID ac29...) 1 本だけで、UI の `sound` と
///     Judge.hitSound の両方にバインドされていた。クリックス音源
///     という独立アセットは存在しなかった。
///   - Issue #604 で追加された clickSound は NoteEditor 同梱サンプル
///     "Click 2.wav" (GUID aa66...) だったが、実測で支配周波数約 1 kHz の
///     ピッチ音 (約 100 ms) + 1.5 s の無音パディングと判明し、
///     「音程のある音」「遅れ」というユーザー指摘に合致する。
///     Click 2.wav がゲームのシーンへバインドされたのは #604 が初めてで、
///     「以前出荷していたクリック音源」ではない (誤情報に基づく
///     d8cbf0f0 の差し戻しを訂正)。
///   - clickSound の正解は効果音ラボ「決定ボタンを押す31(カチッ)」を
///     WAV 化・末尾無音トリムした Assets/Game/Audio/レーンキークリック.wav
///     (GUID 00cf...)。実測で高域ノイズ主体・約 110 ms・アタック即時の
///     非ピッチのボタン音。元 mp3 はエンコーダプライミング約 23 ms が
///     残り得るため、WAV 派生で遅延要因を除去した。
///   - hitSound は歴史的正解のカーソル移動4.mp3 (GUID ac29...) のまま
///     (実測で約 25 ms の高域ティック — 非ピッチの判定音として適切)。
///
/// 検証する要求 (実装定数の写しではなく出荷経路の契約):
///   - clickSound は上記 WAV クリップを指す。
///   - hitSound はカーソル移動4.mp3 を指す (歴史的バインドの固定)。
///   - 両 GUID が実在するアセットへ解決できる
///     (GUID の打ち間違い・削除済み参照を検出する)。
/// </summary>
public class GameSceneSoundBindingTests
{
    private const string ScenePath = "Assets/Game/Scenes/GameScene.unity";

    /// <summary>レーンキークリック音のアセットパス (効果音ラボ音源の WAV 派生)。</summary>
    private const string ClickSoundPath = "Assets/Game/Audio/レーンキークリック.wav";

    /// <summary><see cref="ClickSoundPath"/> の GUID (シーン参照の固定値)。</summary>
    private const string ClickSoundGuid = "00cf8062f3d34ae9af330210bc82e43a";

    /// <summary>判定ヒット音のアセットパス (初版から出荷されている音源)。</summary>
    private const string HitSoundPath = "Assets/Game/Resources/Musics/カーソル移動4.mp3";

    /// <summary><see cref="HitSoundPath"/> の GUID (シーン参照の固定値)。</summary>
    private const string HitSoundGuid = "ac2961a545ba50e40bfbbccacb3fe8d7";

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
    public void ClickSound_ResolvesToLaneClickWav()
    {
        string guid = ClipGuid("clickSound");
        string path = AssetDatabase.GUIDToAssetPath(guid);

        Assert.AreEqual(ClickSoundGuid, guid,
            "clickSound はレーンキークリック WAV の GUID を指すこと (Issue #730)");
        Assert.AreEqual(ClickSoundPath, path,
            "clickSound は " + ClickSoundPath + " を指すこと");
    }

    [Test]
    public void HitSound_ResolvesToHistoricalCursorTick()
    {
        string guid = ClipGuid("hitSound");
        string path = AssetDatabase.GUIDToAssetPath(guid);

        Assert.AreEqual(HitSoundGuid, guid,
            "hitSound は初版から出荷されているカーソル移動4.mp3 の GUID を指すこと");
        Assert.AreEqual(HitSoundPath, path,
            "hitSound は " + HitSoundPath + " を指すこと");
    }

    [Test]
    public void HitSound_And_ClickSound_AreDifferentClips()
    {
        string hit = ClipGuid("hitSound");
        string click = ClipGuid("clickSound");

        Assert.AreNotEqual(hit, click,
            "hitSound と clickSound が同一 GUID を指している (Issue #672/#730 の再発)");
    }
}
