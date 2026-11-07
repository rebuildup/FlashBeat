using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Resources.Load("曲名") は同じ名前のアセットが複数の Resources フォルダにあると
/// どれか 1 つしか返らない。Resources フォルダが二重にあると曲の読み込みが壊れるので、
/// 重複がないことと、読み込まれるのは曲情報 (lyrics 入り) の方であることを確かめる。
/// </summary>
public class ResourceChartUniquenessTests
{
    static readonly string[] ResourceRoots =
    {
        "Assets/Resources",
        "Assets/Game/Resources",
    };

    [Test]
    public void NoChartExistsInTwoResourcesFolders()
    {
        var seen = new Dictionary<string, List<string>>();
        for (int r = 0; r < ResourceRoots.Length; r++)
        {
            string root = ResourceRoots[r];
            if (!Directory.Exists(root)) { continue; }
            var files = Directory.GetFiles(root, "*.json", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < files.Length; i++)
            {
                string name = Path.GetFileNameWithoutExtension(files[i]);
                if (!seen.ContainsKey(name)) { seen[name] = new List<string>(); }
                seen[name].Add(files[i]);
            }
        }

        var duplicates = new List<string>();
        foreach (var pair in seen)
        {
            if (pair.Value.Count > 1) { duplicates.Add(pair.Key + " -> " + string.Join(", ", pair.Value.ToArray())); }
        }
        Assert.IsEmpty(duplicates, "the same chart must not exist in two Resources folders: " + string.Join(" | ", duplicates.ToArray()));
    }

    [Test]
    public void TheLoadedChartHasTheMergedLyrics()
    {
        // 実際に Resources.Load が返すのはどちらか 1 つ。歌詞つき (マージ済み) であるべき。
        var missing = new List<string>();
        var seen = new HashSet<string>();
        for (int r = 0; r < ResourceRoots.Length; r++)
        {
            string root = ResourceRoots[r];
            if (!Directory.Exists(root)) { continue; }
            var files = Directory.GetFiles(root, "*.json", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < files.Length; i++)
            {
                string name = Path.GetFileNameWithoutExtension(files[i]);
                if (name == "CloudCatalogConfig" || name == "E2EProbeSong") { continue; }
                // Unity Performance Testing パッケージがテスト実行中に
                // Assets/Resources/ へ書き込む一時ファイル (PerformanceTestRunInfo /
                // PerformanceTestRunSettings)。譜面ではないので対象外にする。
                if (name.StartsWith("PerformanceTestRun", StringComparison.Ordinal)) { continue; }
                seen.Add(name);
            }
        }

        foreach (string name in seen)
        {
            var asset = Resources.Load<TextAsset>(name);
            if (asset == null) { missing.Add(name + " (not loadable)"); continue; }
            if (!asset.text.Contains("\"lyrics\"")) { missing.Add(name + " (no lyrics block)"); }
        }

        Assert.IsEmpty(missing, "Resources.Load must return the merged chart: " + string.Join(", ", missing.ToArray()));
    }
}
