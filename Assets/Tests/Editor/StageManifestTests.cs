using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// release/check/stage-manifest.json (#22) のスキーマが約束することを検証する EditMode テスト。
/// ネットワーク・Windows 実機に依存しない。実機側の再計算は release/check/verify-stage.ps1 が行い、
/// ここでは「manifest が壊れていないか」「manifest が参照するリポジ内パスが存在するか」
/// 「manifest 自身が宣言する不変値 (exe SHA-256 は全ステージで同一 / fingerprint は
/// docs/evidence/index.md に記載済み) が崩れていないか」だけを見る。
/// </summary>
public class StageManifestTests
{
    private const string ManifestRelPath = "release/check/stage-manifest.json";
    private const string VerifyScriptRelPath = "release/check/verify-stage.ps1";
    private const string EvidenceIndexRelPath = "docs/evidence/index.md";

    [Serializable]
    private class FingerprintInfo
    {
        public string algorithm;
        public string definition;
        public string command;
        public string note;
    }

    [Serializable]
    private class ArtifactInfo
    {
        public string archivePath;
        public string worktreeBuildPath;
        public string exeSha256;
        public string fingerprintSha256;
        public int fileCount;
    }

    [Serializable]
    private class EvidenceFile
    {
        public string gitRef;
        public string localPath;
        public string sha256;
    }

    [Serializable]
    private class PlayEvidence
    {
        public string state;
        public string[] proven;
        public string[] unproven;
        public EvidenceFile[] evidenceFiles;
    }

    [Serializable]
    private class Stage
    {
        public string id;
        public string title;
        public string status;
        public ArtifactInfo artifact;
        public PlayEvidence playEvidence;
    }

    [Serializable]
    private class Manifest
    {
        public int manifestVersion;
        public FingerprintInfo fingerprint;
        public Stage[] stages;
    }

    private static string RepoRoot
    {
        get { return Path.GetFullPath(Path.Combine(Application.dataPath, "..")); }
    }

    private static string ReadRepoFile(string relativePath)
    {
        var full = Path.Combine(RepoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.IsTrue(File.Exists(full), "リポジ内ファイルが見つかりません: " + relativePath);
        return File.ReadAllText(full, Encoding.UTF8);
    }

    private static Manifest LoadManifest()
    {
        string raw = ReadRepoFile(ManifestRelPath);
        var manifest = JsonUtility.FromJson<Manifest>(raw);
        Assert.IsNotNull(manifest, "stage-manifest.json が JSON として解釈できません。");
        return manifest;
    }

    [Test]
    public void ManifestParsesWithSchemaVersion1()
    {
        var manifest = LoadManifest();
        Assert.AreEqual(1, manifest.manifestVersion, "manifestVersion は 1 固定。");
    }

    [Test]
    public void RequiredTopLevelKeysExist()
    {
        string raw = ReadRepoFile(ManifestRelPath);
        Assert.That(raw, Does.Contain("\"fingerprint\""), "fingerprint ブロックが必要。");
        Assert.That(raw, Does.Contain("\"stages\""), "stages 配列が必要。");
        Assert.That(raw, Does.Contain("\"artifact\""), "各 stage の artifact ブロックが必要。");
        Assert.That(raw, Does.Contain("\"playEvidence\""), "各 stage の playEvidence ブロックが必要。");
    }

    [Test]
    public void StagesArePresentWithUniqueIds()
    {
        var manifest = LoadManifest();
        Assert.IsNotNull(manifest.stages, "stages 配列が解釈できません。");
        Assert.Greater(manifest.stages.Length, 0, "stages が空。");

        var seen = new HashSet<string>();
        foreach (var st in manifest.stages)
        {
            Assert.IsFalse(string.IsNullOrEmpty(st.id), "stage に id が無い。");
            Assert.IsTrue(seen.Add(st.id), "stage id が重複しています: " + st.id);
            Assert.IsFalse(string.IsNullOrEmpty(st.title), "stage " + st.id + " に title が無い。");
            Assert.IsFalse(string.IsNullOrEmpty(st.status), "stage " + st.id + " に status が無い。");
        }
    }

    [Test]
    public void FingerprintCommandPointsAtExistingScript()
    {
        var manifest = LoadManifest();
        Assert.IsNotNull(manifest.fingerprint, "fingerprint ブロックが解釈できません。");
        Assert.AreEqual("sha256", manifest.fingerprint.algorithm, "fingerprint.algorithm は sha256 固定。");
        Assert.That(
            manifest.fingerprint.command,
            Does.Contain(VerifyScriptRelPath),
            "fingerprint.command が verify-stage.ps1 を指していること。");
        // command が参照するスクリプトがリポジ内に実在する。
        ReadRepoFile(VerifyScriptRelPath);
    }

    [Test]
    public void ReferencedRepoRelativePathsExist()
    {
        string raw = ReadRepoFile(ManifestRelPath);
        // gitRef (origin/<branch>:docs/...) と proof フィールドが参照するリポジ相対パス。
        var matches = Regex.Matches(raw, @"docs/[A-Za-z0-9_./\-]+");
        Assert.Greater(matches.Count, 0, "manifest が docs/ 配下の参照を 1 件も持っていない (想定と不一致)。");

        var seen = new HashSet<string>();
        foreach (Match m in matches)
        {
            if (!seen.Add(m.Value)) { continue; }
            var full = Path.Combine(RepoRoot, m.Value.Replace('/', Path.DirectorySeparatorChar));
            Assert.IsTrue(File.Exists(full), "manifest が参照するパスがリポジに存在しません: " + m.Value);
        }
    }

    [Test]
    public void ExeSha256IsIdenticalAcrossStages()
    {
        var manifest = LoadManifest();
        string expected = null;
        foreach (var st in manifest.stages)
        {
            Assert.IsNotNull(st.artifact, "stage " + st.id + " の artifact ブロックが解釈できません。");
            string exe = st.artifact.exeSha256;
            if (string.IsNullOrEmpty(exe)) { continue; }
            if (expected == null) { expected = exe; }
            Assert.AreEqual(
                expected, exe,
                "manifest の note のとおり FlashBeat.exe は全ビルドで同一 SHA-256 (Unity launcher stub) だが、stage " + st.id + " が崩れている。");
        }
        Assert.IsNotNull(expected, "exeSha256 を持つ stage が 1 つも無い。");
    }

    [Test]
    public void EveryRecordedFingerprintIsListedInEvidenceIndex()
    {
        var manifest = LoadManifest();
        string index = ReadRepoFile(EvidenceIndexRelPath);
        int checkedCount = 0;
        foreach (var st in manifest.stages)
        {
            string fp = st.artifact == null ? null : st.artifact.fingerprintSha256;
            if (string.IsNullOrEmpty(fp)) { continue; }
            Assert.That(
                index, Does.Contain(fp),
                "verify-stage.ps1 が保証する契約 (docs/evidence/index.md が全 fingerprint に言及) が崩れています: stage " + st.id);
            checkedCount++;
        }
        Assert.Greater(checkedCount, 0, "fingerprintSha256 を持つ stage が 1 つも無い。");
    }
}
