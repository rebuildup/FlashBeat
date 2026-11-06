using System;
using System.IO;
using NUnit.Framework;
using FlashBeat.Update;

/// <summary>
/// issue #125: Update.ps1 が書く last-result.json の解析、Title 通知文言の選択、
/// 一度だけ表示するための mark-as-shown (リネーム)。IO は一時ディレクトリに注入して検証する。
/// </summary>
public class UpdateResultFileTests
{
    private string dir;
    private string path;

    [SetUp]
    public void SetUp()
    {
        dir = Path.Combine(Path.GetTempPath(), "FlashBeatUpdateResultTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        path = Path.Combine(dir, UpdateResultFile.FileName);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(dir)) { Directory.Delete(dir, true); }
    }

    private void WriteFile(string content)
    {
        File.WriteAllText(path, content);
    }

    // ── 解析 (Read) ─────────────────────────────────────────────

    [Test]
    public void Read_FailedRecord_ParsesAllFields()
    {
        WriteFile("{\"result\":\"failed\",\"exitCode\":1," +
                  "\"fromVersion\":\"0.1.2\",\"toVersion\":\"0.1.3\"," +
                  "\"tag\":\"v0.1.3-win64-abc1234\"," +
                  "\"message\":\"作業フォルダがインストール先フォルダ内にあります: C:\\\\x\"," +
                  "\"timeUtc\":\"2026-11-07T00:00:00Z\"}");
        var record = UpdateResultFile.Read(path);
        Assert.IsNotNull(record);
        Assert.AreEqual(UpdateResultKind.Failed, record.Kind);
        Assert.AreEqual(1, record.exitCode);
        Assert.AreEqual("0.1.2", record.fromVersion);
        Assert.AreEqual("0.1.3", record.toVersion);
        Assert.AreEqual("v0.1.3-win64-abc1234", record.tag);
        Assert.IsTrue(record.message.Contains("作業フォルダ"));
        Assert.AreEqual("2026-11-07T00:00:00Z", record.timeUtc);
    }

    [Test]
    public void Read_MapsKinds()
    {
        WriteFile("{\"result\":\"success\",\"exitCode\":0,\"message\":\"更新が完了しました。\"}");
        Assert.AreEqual(UpdateResultKind.Success, UpdateResultFile.Read(path).Kind);

        WriteFile("{\"result\":\"aborted\",\"exitCode\":6,\"message\":\"実行中\"}");
        Assert.AreEqual(UpdateResultKind.Aborted, UpdateResultFile.Read(path).Kind);

        WriteFile("{\"result\":\"unknown-value\",\"exitCode\":0,\"message\":\"\"}");
        Assert.AreEqual(UpdateResultKind.None, UpdateResultFile.Read(path).Kind);

        WriteFile("{\"exitCode\":0,\"message\":\"result フィールド無し\"}");
        Assert.IsNull(UpdateResultFile.Read(path));
    }

    [Test]
    public void Read_MissingFile_ReturnsNull()
    {
        Assert.IsNull(UpdateResultFile.Read(path));
        Assert.IsNull(UpdateResultFile.Read(null));
        Assert.IsNull(UpdateResultFile.Read(""));
    }

    [Test]
    public void Read_CorruptJson_ReturnsNull()
    {
        WriteFile("{ これは JSON ではない");
        Assert.IsNull(UpdateResultFile.Read(path));
    }

    // ── 通知文言の選択 (BuildNoticeText) ────────────────────────

    [Test]
    public void BuildNoticeText_Failed_ShowsReasonCurrentVersionAndRetryHint()
    {
        var record = new UpdateResultRecord
        {
            result = "failed",
            exitCode = 1,
            message = "作業フォルダがインストール先フォルダ内にあります",
        };
        var text = UpdateResultFile.BuildNoticeText(record, "0.1.2");
        Assert.AreEqual("前回の更新に失敗しました: 作業フォルダがインストール先フォルダ内にあります" +
                        " - 現在の版 v0.1.2 を使用中 / F3 で再試行", text);
    }

    [Test]
    public void BuildNoticeText_Aborted_UsesFailureWording()
    {
        var record = new UpdateResultRecord { result = "aborted", exitCode = 6, message = "FlashBeat が実行中のため更新を中止しました" };
        var text = UpdateResultFile.BuildNoticeText(record, "0.1.2");
        Assert.IsTrue(text.StartsWith("前回の更新に失敗しました: "));
        Assert.IsTrue(text.Contains("現在の版 v0.1.2 を使用中"));
    }

    [Test]
    public void BuildNoticeText_FailedWithoutMessage_ShowsFallbackReason()
    {
        var record = new UpdateResultRecord { result = "failed", exitCode = 7, message = null };
        var text = UpdateResultFile.BuildNoticeText(record, "0.1.2");
        Assert.IsTrue(text.Contains("理由不明"));
    }

    [Test]
    public void BuildNoticeText_Success_ShowsNewVersionOnce()
    {
        var record = new UpdateResultRecord { result = "success", exitCode = 0, toVersion = "0.1.4" };
        Assert.AreEqual("更新しました v0.1.4", UpdateResultFile.BuildNoticeText(record, "0.1.2"));
    }

    [Test]
    public void BuildNoticeText_SuccessWithoutVersion_ShowsPlaceholder()
    {
        var record = new UpdateResultRecord { result = "success", exitCode = 0, toVersion = "" };
        Assert.AreEqual("更新しました v?", UpdateResultFile.BuildNoticeText(record, "0.1.2"));
    }

    [Test]
    public void BuildNoticeText_NoneOrNull_ReturnsNull()
    {
        Assert.IsNull(UpdateResultFile.BuildNoticeText(null, "0.1.2"));
        Assert.IsNull(UpdateResultFile.BuildNoticeText(new UpdateResultRecord { result = "weird" }, "0.1.2"));
        Assert.IsNull(UpdateResultFile.BuildNoticeText(new UpdateResultRecord(), "0.1.2"));
    }

    // ── 一度だけ表示 (MarkShown) ────────────────────────────────

    [Test]
    public void MarkShown_RenamesFile_AndNextReadReturnsNull()
    {
        WriteFile("{\"result\":\"failed\",\"exitCode\":1,\"message\":\"x\"}");
        Assert.IsNotNull(UpdateResultFile.Read(path));

        UpdateResultFile.MarkShown(path);

        Assert.IsFalse(File.Exists(path));
        Assert.IsTrue(File.Exists(Path.Combine(dir, UpdateResultFile.ShownFileName)));
        // 次回起動相当: もう読めないので通知は出ない
        Assert.IsNull(UpdateResultFile.Read(path));
    }

    [Test]
    public void MarkShown_ExistingShownFile_IsReplaced()
    {
        WriteFile("{\"result\":\"success\",\"exitCode\":0,\"toVersion\":\"0.1.4\"}");
        var shownPath = Path.Combine(dir, UpdateResultFile.ShownFileName);
        File.WriteAllText(shownPath, "{\"result\":\"failed\",\"exitCode\":1,\"message\":\"old\"}");
        UpdateResultFile.MarkShown(path);
        Assert.IsFalse(File.Exists(path));
        Assert.IsTrue(File.Exists(shownPath));
        Assert.AreEqual(UpdateResultKind.Success, UpdateResultFile.Read(shownPath).Kind);
    }

    [Test]
    public void MarkShown_MissingFile_DoesNotThrow()
    {
        Assert.DoesNotThrow(() => UpdateResultFile.MarkShown(path));
        Assert.DoesNotThrow(() => UpdateResultFile.MarkShown(null));
    }

    // ── Model への反映 ──────────────────────────────────────────

    [Test]
    public void SetLastResultNotice_IsSetOnceAndNotOverwritten()
    {
        var model = new UpdateFlowModel();
        Assert.IsNull(model.LastResultNotice);
        model.SetLastResultNotice("前回の更新に失敗しました: x - 現在の版 v0.1.2 を使用中 / F3 で再試行");
        model.SetLastResultNotice("上書きされない");
        Assert.IsTrue(model.LastResultNotice.StartsWith("前回の更新に失敗しました"));
    }

    [Test]
    public void Dismiss_WithOnlyResultNotice_HidesNotice()
    {
        var model = new UpdateFlowModel();
        model.SetLastResultNotice("更新しました v0.1.4");
        Assert.IsFalse(model.DismissedForSession);
        model.Dismiss();
        Assert.IsTrue(model.DismissedForSession);
    }
}

/// <summary>issue #125: staging / 適用対象の ZIP が現行より新しい版かの防護 (UpdateChecker 純粋関数)。</summary>
public class UpdateStagedVersionGuardTests
{
    [Test]
    public void NewerTag_IsAccepted()
    {
        Assert.IsTrue(UpdateChecker.IsStagedTagNewerThanCurrent("v0.1.3-win64-abc1234", "0.1.2+def5678"));
    }

    [Test]
    public void OlderTag_IsRejected()
    {
        Assert.IsFalse(UpdateChecker.IsStagedTagNewerThanCurrent("v0.1.1-win64-abc1234", "0.1.2+def5678"));
    }

    [Test]
    public void SameVersionDifferentBuild_IsRejected()
    {
        // 同一バージョンの別ビルドは新旧判定できないので適用しない
        Assert.IsFalse(UpdateChecker.IsStagedTagNewerThanCurrent("v0.1.2-win64-abc1234", "0.1.2+def5678"));
    }

    [Test]
    public void InvalidTagOrBuildId_IsRejected()
    {
        Assert.IsFalse(UpdateChecker.IsStagedTagNewerThanCurrent(null, "0.1.2+def5678"));
        Assert.IsFalse(UpdateChecker.IsStagedTagNewerThanCurrent("", "0.1.2+def5678"));
        Assert.IsFalse(UpdateChecker.IsStagedTagNewerThanCurrent("not-a-tag", "0.1.2+def5678"));
        Assert.IsFalse(UpdateChecker.IsStagedTagNewerThanCurrent("v0.1.3-win64-abc1234", null));
        Assert.IsFalse(UpdateChecker.IsStagedTagNewerThanCurrent("v0.1.3-win64-abc1234", "0.1.3"));
    }
}
