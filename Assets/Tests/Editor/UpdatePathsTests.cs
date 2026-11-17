using System;
using System.IO;
using NUnit.Framework;
using FlashBeat.Update;
using UnityEngine;

/// <summary>
/// issue #574: 更新データ (staging / last-result / ログ) が persistentDataPath 配下の
/// 責務別ディレクトリに集約されること、および旧レイアウトからの引き継ぎ
/// (所有が確認できる既知ファイルだけを移し、無関係ファイルは触らない) を固定する。
/// </summary>
public class UpdatePathsTests
{
    private string tempRoot;

    [SetUp]
    public void SetUp()
    {
        tempRoot = Path.Combine(Path.GetTempPath(), "FlashBeatUpdatePathsTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(tempRoot)) { Directory.Delete(tempRoot, true); }
    }

    // ── パス契約 ─────────────────────────────────────────────────

    [Test]
    public void BaseDir_IsUnderPersistentDataPath()
    {
        Assert.IsNotNull(UpdatePaths.PersistentDataRoot);
        Assert.IsNotNull(UpdatePaths.BaseDir);
        Assert.AreEqual(Path.Combine(UpdatePaths.PersistentDataRoot, "update"), UpdatePaths.BaseDir);
    }

    [Test]
    public void ResponsibilityDirs_AreSeparatedUnderBaseDir()
    {
        var baseDir = UpdatePaths.BaseDir;
        Assert.IsNotNull(baseDir);
        Assert.AreEqual(Path.Combine(baseDir, "staging"), UpdatePaths.StagingDir);
        Assert.AreEqual(Path.Combine(baseDir, "results"), UpdatePaths.ResultDir);
        Assert.AreEqual(Path.Combine(baseDir, "logs"), UpdatePaths.LogsDir);
        Assert.AreEqual(Path.Combine(baseDir, "updater"), UpdatePaths.UpdaterTmpDir);

        // 責務別: すべて別ディレクトリ
        var dirs = new[] { UpdatePaths.StagingDir, UpdatePaths.ResultDir, UpdatePaths.LogsDir, UpdatePaths.UpdaterTmpDir };
        for (int i = 0; i < dirs.Length; i++)
        {
            for (int j = i + 1; j < dirs.Length; j++)
            {
                Assert.AreNotEqual(dirs[i], dirs[j], "責務別ディレクトリが重複しています");
            }
        }
    }

    [Test]
    public void ResultFile_UsesResultDir()
    {
        Assert.AreEqual(UpdatePaths.ResultDir, UpdateResultFile.ResultDir);
        Assert.AreEqual(Path.Combine(UpdatePaths.ResultDir, UpdateResultFile.FileName), UpdateResultFile.ResultPath);
    }

    [Test]
    public void LegacyBaseDir_IsThePreIssue574Location()
    {
        var legacy = UpdatePaths.LegacyBaseDir;
        Assert.IsNotNull(legacy);
        Assert.AreNotEqual(UpdatePaths.BaseDir, legacy);
        StringAssert.EndsWith("update", legacy);
        // 旧レイアウトは persistentDataPath の外 (LOCALAPPDATA / Application Support 直下の FlashBeat)。
        Assert.IsFalse(legacy.StartsWith(UpdatePaths.PersistentDataRoot + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase));
    }

    [Test]
    public void LegacyBaseDir_MacVariant_UsesApplicationSupport()
    {
        var legacy = UpdatePaths.GetLegacyBaseDir(true);
        Assert.IsNotNull(legacy);
        StringAssert.Contains("Library", legacy);
        StringAssert.Contains("Application Support", legacy);
    }

    [Test]
    public void LegacyBaseDir_WindowsVariant_UsesLocalApplicationData()
    {
        var legacy = UpdatePaths.GetLegacyBaseDir(false);
        if (legacy == null) { Assert.Ignore("LocalApplicationData が解決できない環境"); }
        StringAssert.Contains("FlashBeat", legacy);
    }

    // ── 旧レイアウトからの引き継ぎ ────────────────────────────────

    [Test]
    public void MigrateLegacyData_MovesKnownEntriesOnly()
    {
        var legacy = Path.Combine(tempRoot, "legacy");
        var current = Path.Combine(tempRoot, "current");
        var legacyStaging = Path.Combine(legacy, "staging");
        Directory.CreateDirectory(legacyStaging);
        File.WriteAllText(Path.Combine(legacyStaging, "pack.zip"), "zip");
        File.WriteAllText(Path.Combine(legacy, "last-result.json"), "{}");
        File.WriteAllText(Path.Combine(legacy, "last-result-shown.json"), "{}");
        File.WriteAllText(Path.Combine(legacy, "unknown.bin"), "keep me");

        UpdatePaths.MigrateLegacyData(legacy, current);

        // 既知エントリは新しい責務別ディレクトリへ
        Assert.IsTrue(File.Exists(Path.Combine(current, "staging", "pack.zip")));
        Assert.IsTrue(File.Exists(Path.Combine(current, "results", "last-result.json")));
        Assert.IsTrue(File.Exists(Path.Combine(current, "results", "last-result-shown.json")));
        // 未知ファイルは触らない → 旧ルートは残る
        Assert.IsTrue(File.Exists(Path.Combine(legacy, "unknown.bin")));
        Assert.IsTrue(Directory.Exists(legacy));
        Assert.IsFalse(Directory.Exists(legacyStaging));
    }

    [Test]
    public void MigrateLegacyData_RemovesLegacyRootWhenFullyMigrated()
    {
        var legacy = Path.Combine(tempRoot, "legacy");
        var current = Path.Combine(tempRoot, "current");
        Directory.CreateDirectory(Path.Combine(legacy, "staging"));
        File.WriteAllText(Path.Combine(legacy, "last-result.json"), "{}");

        UpdatePaths.MigrateLegacyData(legacy, current);

        Assert.IsFalse(Directory.Exists(legacy), "既知エントリだけなら旧ルートは削除される");
        Assert.IsTrue(File.Exists(Path.Combine(current, "results", "last-result.json")));
    }

    [Test]
    public void MigrateLegacyData_KeepsNewerTarget()
    {
        var legacy = Path.Combine(tempRoot, "legacy");
        var current = Path.Combine(tempRoot, "current");
        Directory.CreateDirectory(Path.Combine(legacy, "staging"));
        File.WriteAllText(Path.Combine(legacy, "staging", "old.zip"), "old");
        var newStaging = Path.Combine(current, "staging");
        Directory.CreateDirectory(newStaging);
        File.WriteAllText(Path.Combine(newStaging, "new.zip"), "new");
        File.WriteAllText(Path.Combine(legacy, "last-result.json"), "old-result");
        var newResults = Path.Combine(current, "results");
        Directory.CreateDirectory(newResults);
        File.WriteAllText(Path.Combine(newResults, "last-result.json"), "new-result");

        UpdatePaths.MigrateLegacyData(legacy, current);

        // 移動先が既にあるものは旧側を残す (新しい方を優先・上書きしない)
        Assert.IsTrue(File.Exists(Path.Combine(newStaging, "new.zip")));
        Assert.IsTrue(File.Exists(Path.Combine(legacy, "staging", "old.zip")));
        Assert.AreEqual("new-result", File.ReadAllText(Path.Combine(newResults, "last-result.json")));
        Assert.AreEqual("old-result", File.ReadAllText(Path.Combine(legacy, "last-result.json")));
    }

    [Test]
    public void MigrateLegacyData_NoLegacyDir_IsNoOp()
    {
        var legacy = Path.Combine(tempRoot, "does-not-exist");
        var current = Path.Combine(tempRoot, "current");
        Assert.DoesNotThrow(() => UpdatePaths.MigrateLegacyData(legacy, current));
        Assert.IsFalse(Directory.Exists(current));
    }
}
