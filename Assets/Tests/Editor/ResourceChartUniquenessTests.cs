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
    /// <summary>譜面 JSON が置いてある唯一の Resources フォルダ。</summary>
    const string CanonicalRoot = "Assets/Game/Resources";

    /// <summary>
    /// Assets 配下の Resources フォルダをすべて見つける。
    /// フォルダをハードコードすると、3 つ目・4 つ目の.Resources を見落とすので、
    /// 実際に存在するものだけを列挙する。
    /// </summary>
    static List<string> ResourceRoots()
    {
        var roots = new List<string>();
        if (!Directory.Exists("Assets")) { return roots; }

        string[] found = Directory.GetDirectories("Assets", "Resources", SearchOption.AllDirectories);
        Array.Sort(found, StringComparer.Ordinal);
        roots.AddRange(found);
        return roots;
    }

    /// <summary>
    /// 譜面ファイルかどうか。
    /// Unity Performance Testing パッケージがテスト実行中に Assets/Resources/ へ
    /// 書き込む一時ファイル (PerformanceTestRunInfo / PerformanceTestRunSettings)
    /// は譜面ではないので対象外にする。
    /// </summary>
    static bool IsChartFile(string path)
    {
        string name = Path.GetFileNameWithoutExtension(path);
        return !name.StartsWith("PerformanceTestRun", StringComparison.Ordinal);
    }

    [Test]
    public void NoChartExistsInTwoResourcesFolders()
    {
        var seen = new Dictionary<string, List<string>>();
        var roots = ResourceRoots();
        for (int r = 0; r < roots.Count; r++)
        {
            string root = roots[r];
            var files = Directory.GetFiles(root, "*.json", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < files.Length; i++)
            {
                if (!IsChartFile(files[i])) { continue; }
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
    public void ChartFilesExistOnlyInTheCanonicalResourcesFolder()
    {
        // 譜面 JSON は 1 つの Resources フォルダ (Assets/Game/Resources) にだけ置く。
        // 別の Resources フォルダに譜面が入ると Resources.Load が不定になるため。
        var strays = new List<string>();
        var roots = ResourceRoots();
        for (int r = 0; r < roots.Count; r++)
        {
            string root = roots[r].Replace('\\', '/');
            if (root == CanonicalRoot) { continue; }

            var files = Directory.GetFiles(roots[r], "*.json", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < files.Length; i++)
            {
                if (!IsChartFile(files[i])) { continue; }
                strays.Add(files[i]);
            }
        }

        Assert.IsEmpty(strays, "chart files outside " + CanonicalRoot + ": " + string.Join(", ", strays.ToArray()));
    }

    [Test]
    public void TheLoadedChartHasTheMergedLyrics()
    {
        // 実際に Resources.Load が返すのはどちらか 1 つ。歌詞つき (マージ済み) であるべき。
        var missing = new List<string>();
        var seen = new HashSet<string>();
        var roots = ResourceRoots();
        for (int r = 0; r < roots.Count; r++)
        {
            string root = roots[r];
            var files = Directory.GetFiles(root, "*.json", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < files.Length; i++)
            {
                string name = Path.GetFileNameWithoutExtension(files[i]);
                if (name == "CloudCatalogConfig" || name == "E2EProbeSong") { continue; }
                // Unity Performance Testing パッケージがテスト実行中に
                // Assets/Resources/ へ書き込む一時ファイル (PerformanceTestRunInfo /
                // PerformanceTestRunSettings)。譜面ではないので対象外にする。
                if (!IsChartFile(files[i])) { continue; }
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
