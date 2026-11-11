using System.IO;
using FlashBeat.Update;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 配布ビルドの基準バージョンの解決 (2026-11-11 の版数事故の回帰)。
///
/// 以前は未設定時に BuildScript が 0.1.0 へ暗黙フォールバックし、配布済みの
/// 0.1.22 より古い `0.1.0+e2f6a3f9` が Release 候補として作られた。
/// 現在は「環境変数 FLASHBEAT_VERSION_BASE → リポジトリ直下の VERSION」の順で解決し、
/// どちらも無い / X.Y.Z でない場合は null を返してビルドを中止する。
/// </summary>
public class BuildVersionSourceTests
{
    /// <summary>このガードを入れた時点の配布版。VERSION はこれより真に大きいこと。</summary>
    private const string LastDistributedVersionAtGuard = "0.1.22";

    [Test]
    public void ResolveVersionBase_UsesEnvironmentVariableWhenSet()
    {
        Assert.AreEqual("0.2.0", BuildScript.ResolveVersionBase("0.2.0", "0.1.99"));
    }

    [Test]
    public void ResolveVersionBase_FallsBackToVersionFile()
    {
        Assert.AreEqual("0.1.23", BuildScript.ResolveVersionBase("", "0.1.23\n"));
    }

    [Test]
    public void ResolveVersionBase_DoesNotInventADefault()
    {
        Assert.IsNull(BuildScript.ResolveVersionBase(null, null),
            "未設定時に暗黙の既定値 (0.1.0) へ倒してはいけない");
        Assert.IsNull(BuildScript.ResolveVersionBase("  ", "\n"));
    }

    [Test]
    public void ResolveVersionBase_RejectsMalformedEnvironmentValue()
    {
        Assert.IsNull(BuildScript.ResolveVersionBase("0.1", "0.1.23"),
            "環境変数が不正なら VERSION へ黙って切り替えず、解決失敗にする");
        Assert.IsNull(BuildScript.ResolveVersionBase("v0.1.23", "0.1.23"));
        Assert.IsNull(BuildScript.ResolveVersionBase("0.1.23+e2f6a3f9", "0.1.23"));
    }

    [Test]
    public void ResolveVersionBase_RejectsMalformedVersionFile()
    {
        Assert.IsNull(BuildScript.ResolveVersionBase(null, "0.1"));
        Assert.IsNull(BuildScript.ResolveVersionBase(null, "1.2.3.4"));
    }

    [TestCase("0.1.23", true)]
    [TestCase("1.0.0", true)]
    [TestCase("0.1", false)]
    [TestCase("0.1.23+e2f6a3f9", false)]
    [TestCase("v0.1.23", false)]
    [TestCase("", false)]
    public void IsVersionTriplet_MatchesReleaseTagFormat(string value, bool expected)
    {
        Assert.AreEqual(expected, BuildScript.IsVersionTriplet(value));
    }

    [Test]
    public void RepositoryVersionFile_IsAValidTriplet()
    {
        string version = ReadRepositoryVersion();

        Assert.IsNotNull(version, "リポジトリ直下に VERSION が無い");
        Assert.IsTrue(BuildScript.IsVersionTriplet(version), "VERSION が X.Y.Z ではない: '" + version + "'");
    }

    [Test]
    public void RepositoryVersionFile_IsNewerThanTheDistributedVersion()
    {
        string version = ReadRepositoryVersion();
        Assume.That(version, Is.Not.Null);

        Assert.Greater(UpdateVersion.CompareVersions(version.Trim(), LastDistributedVersionAtGuard), 0,
            "VERSION (" + version + ") は配布済み " + LastDistributedVersionAtGuard + " より大きくする (自動更新は真に大きい版だけを適用する)");
    }

    private static string ReadRepositoryVersion()
    {
        // Application.dataPath = <repo>/Assets
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string path = Path.Combine(root, BuildScript.VersionFileRelativePath);
        return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
    }
}
