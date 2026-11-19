using System;
using System.IO;
using NUnit.Framework;
using FlashBeat.Update;
using UnityEngine;

/// <summary>
/// issue #670: 更新データ (staging / last-result / ログ / updater 一時領域) が
/// <b>インストール先フォルダ内</b> の &lt;install&gt;/.flashbeat-update/ 配下に
/// 限定されること、persistentDataPath / AppData / Application Support を
/// 更新関連アーティファクトの置き場にしないこと、および旧レイアウトからの
/// 引き継ぎが「コピーのみ (旧側は移動・削除しない)」であることを固定する。
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
    public void InstallRoot_WindowsPlayer_IsDataPathParent()
    {
        var install = Path.Combine(tempRoot, "FlashBeat");
        var dataPath = Path.Combine(install, "FlashBeat_Data");
        Assert.AreEqual(install, UpdatePaths.GetInstallRoot(RuntimePlatform.WindowsPlayer, dataPath));
    }

    [Test]
    public void InstallRoot_MacPlayer_IsNull()
    {
        // .app バンドル内には安全に書き込めないため更新領域を持たない (issue #670)。
        var dataPath = "/Applications/FlashBeat.app/Contents/Resources/Data";
        Assert.IsNull(UpdatePaths.GetInstallRoot(RuntimePlatform.OSXPlayer, dataPath));
    }

    [Test]
    public void InstallRoot_Editor_IsNull()
    {
        // Editor の dataPath はプロジェクトフォルダを指すため更新領域を持たない。
        Assert.IsNull(UpdatePaths.GetInstallRoot(RuntimePlatform.WindowsEditor,
            Path.Combine(tempRoot, "Assets")));
        Assert.IsNull(UpdatePaths.GetInstallRoot(RuntimePlatform.OSXEditor,
            Path.Combine(tempRoot, "Assets")));
    }

    [Test]
    public void InstallRoot_WindowsPlayer_EmptyDataPath_IsNull()
    {
        Assert.IsNull(UpdatePaths.GetInstallRoot(RuntimePlatform.WindowsPlayer, null));
        Assert.IsNull(UpdatePaths.GetInstallRoot(RuntimePlatform.WindowsPlayer, ""));
    }

    [Test]
    public void ResponsibilityDirs_AreUnderInstallUpdateRoot()
    {
        var install = Path.Combine(tempRoot, "FlashBeat");
        var baseDir = UpdatePaths.GetBaseDir(install);
        Assert.AreEqual(Path.Combine(install, ".flashbeat-update"), baseDir);
        Assert.AreEqual(Path.Combine(baseDir, "staging"), UpdatePaths.GetStagingDir(install));
        Assert.AreEqual(Path.Combine(baseDir, "results"), UpdatePaths.GetResultDir(install));
        Assert.AreEqual(Path.Combine(baseDir, "logs"), UpdatePaths.GetLogsDir(install));
        Assert.AreEqual(Path.Combine(baseDir, "updater"), UpdatePaths.GetUpdaterTmpDir(install));

        // すべての責務別ディレクトリがインストール先の内側にあること (境界検証)。
        var dirs = new[]
        {
            UpdatePaths.GetStagingDir(install),
            UpdatePaths.GetResultDir(install),
            UpdatePaths.GetLogsDir(install),
            UpdatePaths.GetUpdaterTmpDir(install),
        };
        foreach (var dir in dirs)
        {
            Assert.IsTrue(dir.StartsWith(install + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase),
                "更新ディレクトリがインストール先外です: " + dir);
        }
        for (int i = 0; i < dirs.Length; i++)
        {
            for (int j = i + 1; j < dirs.Length; j++)
            {
                Assert.AreNotEqual(dirs[i], dirs[j], "責務別ディレクトリが重複しています");
            }
        }
    }

    [Test]
    public void UpdateDirs_NullInstallRoot_ReturnNull()
    {
        // インストール先を解決できない環境 (macOS .app / Editor) では
        // 更新領域のパスを一切提供しない → 書き込み自体が起きない。
        Assert.IsNull(UpdatePaths.GetBaseDir(null));
        Assert.IsNull(UpdatePaths.GetStagingDir(null));
        Assert.IsNull(UpdatePaths.GetResultDir(null));
        Assert.IsNull(UpdatePaths.GetLogsDir(null));
        Assert.IsNull(UpdatePaths.GetUpdaterTmpDir(null));
    }

    [Test]
    public void ResultFile_UsesResultDir()
    {
        // Editor では InstallRoot を解決できない (Windows Player 以外は null)
        // ため ResultDir/ResultPath は null。契約は「UpdateResultFile が
        // UpdatePaths.ResultDir をそのまま使う」ことで、null もそのまま伝播する。
        Assert.AreEqual(UpdatePaths.ResultDir, UpdateResultFile.ResultDir);
        Assert.AreEqual(UpdatePaths.ResultDir == null ? null
            : Path.Combine(UpdatePaths.ResultDir, UpdateResultFile.FileName),
            UpdateResultFile.ResultPath);

        // 明示したインストールルートでは .flashbeat-update/results に解決される。
        var install = Path.Combine(Path.GetTempPath(), "fb-update-paths-test");
        var resultDir = UpdatePaths.GetResultDir(install);
        Assert.AreEqual(Path.Combine(UpdatePaths.GetBaseDir(install), "results"), resultDir);
        Assert.IsTrue(resultDir.StartsWith(install + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase));
    }

    [Test]
    public void LegacyBaseDir_IsThePreIssue574Location()
    {
        var legacy = UpdatePaths.LegacyBaseDir;
        Assert.IsNotNull(legacy);
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

    // ── 旧レイアウトからの引き継ぎ (コピーのみ・旧側は保全) ─────────

    [Test]
    public void MigrateLegacyData_CopiesKnownEntriesAndKeepsLegacy()
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

        // 既知エントリは新しい責務別ディレクトリへコピーされる
        Assert.IsTrue(File.Exists(Path.Combine(current, "staging", "pack.zip")));
        Assert.IsTrue(File.Exists(Path.Combine(current, "results", "last-result.json")));
        Assert.IsTrue(File.Exists(Path.Combine(current, "results", "last-result-shown.json")));
        // issue #670: 旧側は移動も削除もされず、そのまま残る
        Assert.IsTrue(File.Exists(Path.Combine(legacy, "staging", "pack.zip")));
        Assert.IsTrue(File.Exists(Path.Combine(legacy, "last-result.json")));
        Assert.IsTrue(File.Exists(Path.Combine(legacy, "last-result-shown.json")));
        Assert.IsTrue(File.Exists(Path.Combine(legacy, "unknown.bin")));
        Assert.IsTrue(Directory.Exists(legacy));
        Assert.IsTrue(Directory.Exists(legacyStaging));
    }

    [Test]
    public void MigrateLegacyData_KeepsLegacyRootEvenWhenFullyCopied()
    {
        var legacy = Path.Combine(tempRoot, "legacy");
        var current = Path.Combine(tempRoot, "current");
        Directory.CreateDirectory(Path.Combine(legacy, "staging"));
        File.WriteAllText(Path.Combine(legacy, "last-result.json"), "{}");

        UpdatePaths.MigrateLegacyData(legacy, current);

        // issue #670: 旧ルートは削除されない (境界外ファイルの保全)
        Assert.IsTrue(Directory.Exists(legacy), "旧ルートは残る");
        Assert.IsTrue(File.Exists(Path.Combine(legacy, "last-result.json")));
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
