using System;
using System.IO;
using FlashBeat.Update;
using NUnit.Framework;
using UnityEngine;

public class UpdateVersionTests
{
    [Test]
    public void ParseTag_ValidTag_ReturnsVersionAndSha()
    {
        var id = UpdateVersion.ParseTag("v0.1.0-win64-743c88d");
        Assert.IsTrue(id.Valid);
        Assert.AreEqual("0.1.0", id.Version);
        Assert.AreEqual("743c88d", id.ShortSha);
    }

    [Test]
    public void ParseTag_WithoutVPrefix_Parses()
    {
        var id = UpdateVersion.ParseTag("0.2.0-win64-abcdef1");
        Assert.IsTrue(id.Valid);
        Assert.AreEqual("0.2.0", id.Version);
        Assert.AreEqual("abcdef1", id.ShortSha);
    }

    [Test]
    public void ParseTag_MacTag_Parses()
    {
        var id = UpdateVersion.ParseTag("v0.1.14-macos-abc1234");
        Assert.IsTrue(id.Valid);
        Assert.AreEqual("0.1.14", id.Version);
        Assert.AreEqual("abc1234", id.ShortSha);
        Assert.AreEqual(UpdatePlatform.MacOS, id.Platform);
    }

    [Test]
    public void ParseTag_PlatformFilter_RejectsOtherPlatform()
    {
        Assert.IsFalse(UpdateVersion.ParseTag("v0.1.14-win64-abc1234", UpdatePlatform.MacOS).Valid);
        Assert.IsFalse(UpdateVersion.ParseTag("v0.1.14-macos-abc1234", UpdatePlatform.Windows64).Valid);
        Assert.IsTrue(UpdateVersion.ParseTag("v0.1.14-win64-abc1234", UpdatePlatform.Windows64).Valid);
        Assert.IsTrue(UpdateVersion.ParseTag("v0.1.14-macos-abc1234", UpdatePlatform.MacOS).Valid);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("v0.1.0")]
    [TestCase("0.1.0+743c88d")]       // ビルド識別子をタグとして渡した場合
    [TestCase("v0.1.0-win64-")]       // SHA 空欄
    [TestCase("v0.1.0-win64-nothex!")]
    [TestCase("v0.1.0-win64-abc")]    // SHA が短すぎる (7 桁未満)
    [TestCase("vX.Y.0-win64-743c88d")] // バージョン不正
    [TestCase("v0.1.0-linux-743c88d")] // 未知のプラットフォーム
    public void ParseTag_Invalid_ReturnsInvalid(string tag)
    {
        Assert.IsFalse(UpdateVersion.ParseTag(tag).Valid);
    }

    [Test]
    public void ParseBuildId_Valid_ReturnsVersionAndSha()
    {
        var id = UpdateVersion.ParseBuildId("0.1.0+743c88d");
        Assert.IsTrue(id.Valid);
        Assert.AreEqual("0.1.0", id.Version);
        Assert.AreEqual("743c88d", id.ShortSha);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("0.1.0")]        // +<sha> 欠落 (版識別子未埋め込みのビルド)
    [TestCase("0.1.0+")]       // SHA 空欄
    [TestCase("unknown+743c88d")] // バージョン部が不正
    public void ParseBuildId_Invalid_ReturnsInvalid(string buildId)
    {
        Assert.IsFalse(UpdateVersion.ParseBuildId(buildId).Valid);
    }

    [Test]
    public void IsNewerThan_HigherVersion_True()
    {
        Assert.IsTrue(UpdateVersion.IsNewerThan(
            UpdateVersion.ParseTag("v0.2.0-win64-abc1234"),
            UpdateVersion.ParseBuildId("0.1.0+743c88d")));
    }

    [Test]
    public void IsNewerThan_SameVersionDifferentSha_False()
    {
        // 同じバージョンで別のビルドは「新旧」を判定できない (SHA に大小関係がない)。
        // 自動更新すると古いビルドへ戻すので、自動更新の対象にしない。
        Assert.IsFalse(UpdateVersion.IsNewerThan(
            UpdateVersion.ParseTag("v0.1.0-win64-abc1234"),
            UpdateVersion.ParseBuildId("0.1.0+743c88d")));
    }

    [Test]
    public void IsSameVersionDifferentBuild_SameVersionOtherSha_True()
    {
        Assert.IsTrue(UpdateVersion.IsSameVersionDifferentBuild(
            UpdateVersion.ParseTag("v0.1.0-win64-abc1234"),
            UpdateVersion.ParseBuildId("0.1.0+743c88d")));
    }

    [Test]
    public void IsSameVersionDifferentBuild_SameBuild_False()
    {
        Assert.IsFalse(UpdateVersion.IsSameVersionDifferentBuild(
            UpdateVersion.ParseTag("v0.1.0-win64-743c88d"),
            UpdateVersion.ParseBuildId("0.1.0+743c88d")));
    }

    [Test]
    public void IsSameVersionDifferentBuild_OtherVersion_False()
    {
        Assert.IsFalse(UpdateVersion.IsSameVersionDifferentBuild(
            UpdateVersion.ParseTag("v0.1.1-win64-abc1234"),
            UpdateVersion.ParseBuildId("0.1.0+743c88d")));
    }

    [Test]
    public void IsNewerThan_SameBuild_False()
    {
        Assert.IsFalse(UpdateVersion.IsNewerThan(
            UpdateVersion.ParseTag("v0.1.0-win64-743c88d"),
            UpdateVersion.ParseBuildId("0.1.0+743c88d")));
    }

    [Test]
    public void IsNewerThan_OlderVersion_False()
    {
        // ダウングレードさせない
        Assert.IsFalse(UpdateVersion.IsNewerThan(
            UpdateVersion.ParseTag("v0.0.9-win64-abc1234"),
            UpdateVersion.ParseBuildId("0.1.0+743c88d")));
    }

    [Test]
    public void IsNewerThan_InvalidCandidate_False()
    {
        Assert.IsFalse(UpdateVersion.IsNewerThan(
            UpdateVersion.ParseTag("not-a-tag"),
            UpdateVersion.ParseBuildId("0.1.0+743c88d")));
    }

    [Test]
    public void IsNewerThan_InvalidCurrent_False()
    {
        // 現行版識別子が解釈できない場合は更新しない (安全側)
        Assert.IsFalse(UpdateVersion.IsNewerThan(
            UpdateVersion.ParseTag("v0.2.0-win64-abc1234"),
            UpdateVersion.ParseBuildId("0.1.0")));
    }

    [Test]
    public void IsSame_SameBuild_True()
    {
        Assert.IsTrue(UpdateVersion.IsSame(
            UpdateVersion.ParseTag("v0.1.0-win64-743c88d"),
            UpdateVersion.ParseBuildId("0.1.0+743c88d")));
    }

    [Test]
    public void IsSame_Different_False()
    {
        Assert.IsFalse(UpdateVersion.IsSame(
            UpdateVersion.ParseTag("v0.1.1-win64-743c88d"),
            UpdateVersion.ParseBuildId("0.1.0+743c88d")));
    }
}

public class UpdateStagingTests
{
    private string tempDir;

    [SetUp]
    public void SetUp()
    {
        tempDir = Path.Combine(Path.GetTempPath(), "FlashBeatUpdateStagingTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(tempDir))
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public void ComputeSha256_KnownVector_Matches()
    {
        var path = Path.Combine(tempDir, "abc.txt");
        File.WriteAllText(path, "abc");
        // SHA-256("abc") の既知ベクトル
        Assert.AreEqual(
            "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad",
            UpdateStaging.ComputeSha256(path));
    }

    [Test]
    public void WriteAndReadManifest_RoundTrips()
    {
        var staged = new UpdateStaging.StagedUpdate
        {
            tag = "v0.1.1-win64-abc1234",
            zipName = "FlashBeat-0.1.1-win64-abc1234.zip",
            sha256 = new string('a', 64),
            stagedAtUtc = "2026-11-07T00:00:00.0000000Z",
        };
        Assert.IsTrue(UpdateStaging.WriteManifest(staged, tempDir));

        var read = UpdateStaging.ReadManifest(tempDir);
        Assert.IsNotNull(read);
        Assert.AreEqual(staged.tag, read.tag);
        Assert.AreEqual(staged.zipName, read.zipName);
        Assert.AreEqual(staged.sha256, read.sha256);
    }

    [Test]
    public void ReadManifest_Missing_ReturnsNull()
    {
        Assert.IsNull(UpdateStaging.ReadManifest(tempDir));
    }

    [Test]
    public void ReadManifest_BrokenJson_ReturnsNull()
    {
        File.WriteAllText(Path.Combine(tempDir, UpdateStaging.ManifestFileName), "{ not json");
        Assert.IsNull(UpdateStaging.ReadManifest(tempDir));
    }

    [Test]
    public void VerifyStaged_MatchingSha_True()
    {
        var zipPath = Path.Combine(tempDir, "update.zip");
        File.WriteAllText(zipPath, "payload");
        var staged = new UpdateStaging.StagedUpdate
        {
            tag = "v0.1.1-win64-abc1234",
            zipName = "update.zip",
            sha256 = UpdateStaging.ComputeSha256(zipPath),
        };
        string problem;
        Assert.IsTrue(UpdateStaging.VerifyStaged(staged, tempDir, out problem));
        Assert.IsNull(problem);
    }

    [Test]
    public void VerifyStaged_MismatchedSha_False()
    {
        var zipPath = Path.Combine(tempDir, "update.zip");
        File.WriteAllText(zipPath, "payload");
        var staged = new UpdateStaging.StagedUpdate
        {
            tag = "v0.1.1-win64-abc1234",
            zipName = "update.zip",
            sha256 = new string('0', 64),
        };
        string problem;
        Assert.IsFalse(UpdateStaging.VerifyStaged(staged, tempDir, out problem));
        Assert.IsTrue(problem.Contains("SHA-256"));
    }

    [Test]
    public void VerifyStaged_MissingZip_False()
    {
        var staged = new UpdateStaging.StagedUpdate
        {
            tag = "v0.1.1-win64-abc1234",
            zipName = "missing.zip",
            sha256 = new string('0', 64),
        };
        string problem;
        Assert.IsFalse(UpdateStaging.VerifyStaged(staged, tempDir, out problem));
        Assert.IsTrue(problem.Contains("ありません"));
    }

    [Test]
    public void VerifyStaged_BadManifest_False()
    {
        string problem;
        Assert.IsFalse(UpdateStaging.VerifyStaged(null, tempDir, out problem));
        Assert.IsFalse(UpdateStaging.VerifyStaged(new UpdateStaging.StagedUpdate
        {
            tag = "v0.1.1-win64-abc1234",
            zipName = "",
            sha256 = "",
        }, tempDir, out problem));
    }
}

public class UpdateReleaseSelectionTests
{
    private const string TwoReleasesOutOfOrder = @"
[{""tag_name"":""v0.1.0-win64-743c88d"",""draft"":false,
  ""published_at"":""2026-11-06T13:41:01Z"",""created_at"":""2026-11-06T13:18:29Z"",
  ""assets"":[{""name"":""FlashBeat-0.1.0-win64-743c88d.zip"",""browser_download_url"":""https://example.com/a.zip""}]},
 {""tag_name"":""v0.1.1-win64-abc1234"",""draft"":false,
  ""published_at"":""2026-11-07T00:00:00Z"",""created_at"":""2026-11-06T23:00:00Z"",
  ""assets"":[{""name"":""FlashBeat-0.1.1-win64-abc1234.zip"",""browser_download_url"":""https://example.com/b.zip""}]}]";

    [Test]
    public void ParseLatestRelease_ListOrderUnreliable_PicksHighestVersion()
    {
        // 実測: GitHub API の返却順は新しい Release が先とは限らない。
        // リストの先頭 (v0.1.0) ではなく version 最大 (v0.1.1) を選ぶこと。
        var latest = UpdateChecker.ParseLatestRelease(TwoReleasesOutOfOrder, includePrerelease: false);
        Assert.IsNotNull(latest);
        Assert.AreEqual("v0.1.1-win64-abc1234", latest.tag_name);
    }

    [Test]
    public void ParseLatestRelease_SameVersion_PicksNewerPublished()
    {
        var json = @"
[{""tag_name"":""v0.1.0-win64-aaaaaaa"",""draft"":false,
  ""published_at"":""2026-11-06T13:41:01Z"",""created_at"":""2026-11-06T13:18:29Z"", ""assets"":[]},
 {""tag_name"":""v0.1.0-win64-bbbbbbb"",""draft"":false,
  ""published_at"":""2026-11-06T16:25:27Z"",""created_at"":""2026-11-06T15:15:04Z"", ""assets"":[]}]";
        var latest = UpdateChecker.ParseLatestRelease(json, includePrerelease: false);
        Assert.IsNotNull(latest);
        Assert.AreEqual("v0.1.0-win64-bbbbbbb", latest.tag_name);
    }

    [Test]
    public void ParseLatestRelease_DraftAndInvalidTags_Skipped()
    {
        var json = @"
[{""tag_name"":""v0.2.0-win64-ccccccc"",""draft"":true, ""assets"":[]},
 {""tag_name"":""not-a-tag"",""draft"":false, ""assets"":[]},
 {""tag_name"":""v0.1.0-win64-ddddddd"",""draft"":false,
  ""published_at"":""2026-11-06T13:41:01Z"",""created_at"":""2026-11-06T13:18:29Z"", ""assets"":[]}]";
        var latest = UpdateChecker.ParseLatestRelease(json, includePrerelease: false);
        Assert.IsNotNull(latest);
        Assert.AreEqual("v0.1.0-win64-ddddddd", latest.tag_name);
    }

    [Test]
    public void ParseLatestRelease_NoValidRelease_ReturnsNull()
    {
        Assert.IsNull(UpdateChecker.ParseLatestRelease(@"[{""tag_name"":""x"",""draft"":false,""assets"":[]}]", includePrerelease: false));
        Assert.IsNull(UpdateChecker.ParseLatestRelease(@"[]", includePrerelease: false));
        Assert.IsNull(UpdateChecker.ParseLatestRelease("not json", includePrerelease: false));
    }

    private const string MixedPlatformReleases = @"
[{""tag_name"":""v0.1.14-win64-aaaaaaa"",""draft"":false,
  ""published_at"":""2026-11-08T00:00:00Z"",""created_at"":""2026-11-08T00:00:00Z"",
  ""assets"":[{""name"":""FlashBeat-0.1.14-win64-aaaaaaa.zip"",""browser_download_url"":""https://example.com/win.zip""}]},
 {""tag_name"":""v0.1.13-macos-bbbbbbb"",""draft"":false,
  ""published_at"":""2026-11-07T00:00:00Z"",""created_at"":""2026-11-07T00:00:00Z"",
  ""assets"":[{""name"":""FlashBeat-0.1.13-macos-bbbbbbb.zip"",""browser_download_url"":""https://example.com/mac.zip""}]}]";

    [Test]
    public void ParseLatestRelease_MacOSPlatform_IgnoresWindowsTags()
    {
        // Windows の v0.1.14 が新しくても、macOS の更新候補は macOS タグだけ。
        var latest = UpdateChecker.ParseLatestRelease(
            MixedPlatformReleases, includePrerelease: false, UpdatePlatform.MacOS);
        Assert.IsNotNull(latest);
        Assert.AreEqual("v0.1.13-macos-bbbbbbb", latest.tag_name);
    }

    [Test]
    public void ParseLatestRelease_WindowsPlatform_IgnoresMacTags()
    {
        var latest = UpdateChecker.ParseLatestRelease(
            MixedPlatformReleases, includePrerelease: false, UpdatePlatform.Windows64);
        Assert.IsNotNull(latest);
        Assert.AreEqual("v0.1.14-win64-aaaaaaa", latest.tag_name);
    }

    [Test]
    public void FindPlatformZip_MixedAssets_PicksMatchingPlatform()
    {
        var release = new UpdateChecker.ReleaseInfo
        {
            assets = new[]
            {
                new UpdateChecker.ReleaseAsset
                {
                    name = "FlashBeat-0.1.14-win64-aaaaaaa.zip",
                    browser_download_url = "https://example.com/win.zip",
                },
                new UpdateChecker.ReleaseAsset
                {
                    name = "FlashBeat-0.1.14-macos-aaaaaaa.zip",
                    browser_download_url = "https://example.com/mac.zip",
                },
                new UpdateChecker.ReleaseAsset
                {
                    name = "FlashBeat-0.1.14-macos-aaaaaaa.zip.sha256",
                    browser_download_url = "https://example.com/mac.zip.sha256",
                },
            },
        };

        Assert.AreEqual("https://example.com/mac.zip",
            UpdateChecker.FindPlatformZip(release, UpdatePlatform.MacOS).browser_download_url);
        Assert.AreEqual("https://example.com/win.zip",
            UpdateChecker.FindPlatformZip(release, UpdatePlatform.Windows64).browser_download_url);
    }
}

/// <summary>更新チャンネルの分離 (issue #41): 展示用 (stable) ビルドが
/// テスト用 prerelease を自動適用しないことを保証する。</summary>
public class UpdateChannelTests
{
    // 安定版 v0.1.0 と、より新しいテスト用 prerelease v0.1.1 (prerelease=true)。
    private const string StableAndPrerelease = @"
[{""tag_name"":""v0.1.0-win64-743c88d"",""draft"":false,""prerelease"":false,
  ""published_at"":""2026-11-06T13:41:01Z"",""created_at"":""2026-11-06T13:18:29Z"",""assets"":[]},
 {""tag_name"":""v0.1.1-win64-abc1234"",""draft"":false,""prerelease"":true,
  ""published_at"":""2026-11-07T00:00:00Z"",""created_at"":""2026-11-06T23:00:00Z"",""assets"":[]}]";

    [Test]
    public void StableChannel_PrereleaseIsNotAnUpdateCandidate()
    {
        // 展示用ビルド: テスト用 prerelease があっても更新候補にならない。
        var latest = UpdateChecker.ParseLatestRelease(StableAndPrerelease, includePrerelease: false);
        Assert.IsNotNull(latest);
        Assert.AreEqual("v0.1.0-win64-743c88d", latest.tag_name);
        Assert.IsFalse(latest.prerelease);
    }

    [Test]
    public void PreviewChannel_PrereleaseIsAnUpdateCandidate()
    {
        // 検証用ビルド: prerelease も更新候補になる。
        var latest = UpdateChecker.ParseLatestRelease(StableAndPrerelease, includePrerelease: true);
        Assert.IsNotNull(latest);
        Assert.AreEqual("v0.1.1-win64-abc1234", latest.tag_name);
        Assert.IsTrue(latest.prerelease);
    }

    [Test]
    public void StableChannel_OnlyPrereleaseExists_ReturnsNull()
    {
        // 安定版が 1 つも無いとき、stable ビルドは「更新なし」と判断する。
        var latest = UpdateChecker.ParseLatestRelease(StableAndPrerelease, includePrerelease: false);
        Assert.IsNotNull(latest);
        Assert.IsFalse(latest.prerelease);

        var onlyPrerelease = UpdateChecker.ParseLatestRelease(
            @"[{""tag_name"":""v0.1.1-win64-abc1234"",""draft"":false,""prerelease"":true,""assets"":[]}]",
            includePrerelease: false);
        Assert.IsNull(onlyPrerelease);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("garbage")]
    [TestCase("stable")]
    public void Normalize_UnknownOrStable_IsStable(string raw)
    {
        Assert.AreEqual(UpdateChannel.Stable, UpdateChannel.Normalize(raw));
        Assert.IsFalse(UpdateChannel.IncludesPrerelease(raw));
    }

    [TestCase("preview")]
    [TestCase("PREVIEW")]
    [TestCase(" Preview ")]
    public void Normalize_Preview_IsPreview(string raw)
    {
        Assert.AreEqual(UpdateChannel.Preview, UpdateChannel.Normalize(raw));
        Assert.IsTrue(UpdateChannel.IncludesPrerelease(raw));
    }
}
