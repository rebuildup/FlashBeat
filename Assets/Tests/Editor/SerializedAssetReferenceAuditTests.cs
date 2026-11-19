using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Issue #744: 出荷 Scene / Prefab にシリアライズされた参照の
/// 包括監査。#730 の GameScene 音源バインド検査を一般化したもの。
///
/// 背景: GameScene.unity の hitSound/clickSound が同一 GUID を指す
/// 誤配線は、コード経路のテストが全て緑でも製品が誤った音を出す
/// 「serialized reference の欠陥」だった。GUID が打ち間違い・
/// アセット削除で解決不能になった場合も同じ経路で壊れる。
///
/// 検証する要求 (実装の写しではなく出荷経路の契約):
///   - Assets/Game/Scenes の全 Scene と Assets/Game 配下の全 Prefab が
///     監査対象に含まれる (対象の取りこぼしを防ぐ)。
///   - 各ファイル内の全 `guid:` 参照が実在アセットへ解決できる
///     (Unity 組み込み GUID のみ例外として許可リストに載せる)。
///   - 出荷 Scene ごとに必須コンポーネントのスクリプト GUID が
///     存在する (Scene から Manager/Judge が外れた退行を検出する)。
/// </summary>
public class SerializedAssetReferenceAuditTests
{
    /// <summary>
    /// Unity 組み込みアセットの疑似 GUID。AssetDatabase.GUIDToAssetPath
    /// が空を返すが正当な参照なので許可する。
    ///   - 0000000000000000e000000000000000: unity_builtin_extra
    ///   - 0000000000000000f000000000000000: unity default resources
    /// </summary>
    private static readonly HashSet<string> BuiltinGuids = new HashSet<string>
    {
        "0000000000000000e000000000000000",
        "0000000000000000f000000000000000",
    };

    private static readonly Regex GuidPattern =
        new Regex(@"guid: ([0-9a-f]{32})", RegexOptions.Compiled);

    /// <summary>監査対象の Scene ルート。出荷シーンは全てここに置く契約。</summary>
    private const string SceneFolder = "Assets/Game/Scenes";

    /// <summary>監査対象の Prefab ルート。</summary>
    private const string PrefabFolder = "Assets/Game";

    /// <summary>
    /// 出荷 Scene が必ず持つべきスクリプト GUID (Scene ファイル内の
    /// m_Script 参照として現れる)。値は各 .cs.meta の guid。
    /// 「その Scene の中核コンポーネントが消えた/差し替わった」退行を
    /// GUID 解決とは別に検出するための最小契約。
    /// </summary>
    private static readonly (string Scene, string Label, string Guid)[] RequiredSceneBindings =
    {
        ("GameScene.unity",   "Judge",                 "9e2cb74c4d9f4c5418b1103bbd000218"),
        ("GameScene.unity",   "GameSceneManager",      "2a1e91fa15c76504ba869b61ccb5d8ac"),
        ("SelectScene.unity", "SelectSceneManager",    "d9fac61016af35046af77d8a398ed4a1"),
        ("ResultScene.unity", "ResultSceneManager",    "528657884f4233e438778c1c35e5292b"),
        ("OptionScene.unity", "OptionSceneManager",    "aed571f7f1b42ba48a6dc4462d98dab4"),
        ("TitleScene.unity",  "TitleSceneManager",     "12de893f624acda448859bf43a9f8b6d"),
        ("Opening.unity",     "OpeningSceneManager",   "4add1aeaa1aa1c445ad363e41bbffcec"),
        ("MakeFileScene.unity", "MakeFileSceneManager", "0e02786fd96e8d845a6c4b72b65726c9"),
    };

    private static IEnumerable<string> FindAssetPaths(string filter, string[] folders)
    {
        foreach (string guid in AssetDatabase.FindAssets(filter, folders))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.IsNullOrEmpty(path))
                yield return path;
        }
    }

    private static List<string> SerializedGuids(string assetPath)
    {
        // 対象はテキストシリアライズの Scene / Prefab のみ。
        // assetPath は "Assets/..." 形式なので Application.dataPath 基準で解決する
        // (カレントディレクトリ依存にしない)。
        string full = Path.Combine(
            Application.dataPath, assetPath.Substring("Assets/".Length));
        string text = File.ReadAllText(full);
        var guids = new List<string>();
        foreach (Match m in GuidPattern.Matches(text))
            guids.Add(m.Groups[1].Value);
        return guids;
    }

    [Test]
    public void AuditScope_CoversEveryBuildScene()
    {
        // EditorBuildSettings の有効 Scene が全て監査フォルダ配下にあること。
        // 出荷 Scene を別フォルダへ置いて監査から漏らす退行を防ぐ。
        int checkedCount = 0;
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (!scene.enabled) continue;
            checkedCount++;
            StringAssert.StartsWith(SceneFolder + "/", scene.path,
                $"ビルド対象 Scene が監査フォルダ外: {scene.path}");
        }
        Assert.Greater(checkedCount, 0, "EditorBuildSettings に有効 Scene が無い");
    }

    [Test]
    public void AuditScope_FindsScenesAndPrefabs()
    {
        int sceneCount = 0;
        foreach (string _ in FindAssetPaths("t:Scene", new[] { SceneFolder }))
            sceneCount++;
        Assert.GreaterOrEqual(sceneCount, 7,
            "Game/Scenes の Scene が監査対象として見つからない");

        int prefabCount = 0;
        foreach (string _ in FindAssetPaths("t:Prefab", new[] { PrefabFolder }))
            prefabCount++;
        Assert.Greater(prefabCount, 0,
            "Assets/Game 配下の Prefab が監査対象として見つからない");
    }

    [Test]
    public void SerializedGuids_AllResolveToAssets()
    {
        var targets = new List<string>();
        targets.AddRange(FindAssetPaths("t:Scene", new[] { SceneFolder }));
        targets.AddRange(FindAssetPaths("t:Prefab", new[] { PrefabFolder }));
        Assert.Greater(targets.Count, 0, "監査対象が空");

        var failures = new List<string>();
        int checkedRefs = 0;
        foreach (string path in targets)
        {
            foreach (string guid in SerializedGuids(path))
            {
                if (BuiltinGuids.Contains(guid)) continue;
                checkedRefs++;
                string resolved = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(resolved))
                    failures.Add($"{path}: guid {guid} がアセットへ解決できない");
            }
        }

        Assert.Greater(checkedRefs, 0, "GUID 参照が 1 件も検出されなかった (検査自体の疑い)");
        Assert.IsEmpty(failures,
            "解決不能なシリアライズ参照があります:\n" + string.Join("\n", failures));
    }

    [Test]
    public void RequiredSceneBindings_EachSceneKeepsItsCoreComponents()
    {
        var failures = new List<string>();
        foreach (var (scene, label, guid) in RequiredSceneBindings)
        {
            string scenePath = SceneFolder + "/" + scene;
            string sceneFull = Path.Combine(
                Application.dataPath, scenePath.Substring("Assets/".Length));
            if (!File.Exists(sceneFull))
            {
                failures.Add($"{scene}: Scene ファイルが存在しない");
                continue;
            }
            string resolved = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(resolved))
            {
                failures.Add($"{scene}: {label} のスクリプト GUID {guid} が解決できない");
                continue;
            }
            if (!SerializedGuids(scenePath).Contains(guid))
                failures.Add($"{scene}: {label} ({resolved}) の参照が Scene 内に無い");
        }
        Assert.IsEmpty(failures,
            "必須シーンバインディングの欠落:\n" + string.Join("\n", failures));
    }
}
