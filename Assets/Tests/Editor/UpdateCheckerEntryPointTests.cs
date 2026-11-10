using FlashBeat.UI;
using FlashBeat.Update;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// issue #118「更新は明示式」の入口 (RequestDownload / RequestApply) の副作用検証。
/// EditMode では MonoBehaviour の Awake は走らない (通信も起動も起きない) ので、
/// 「入口が呼ばれても状態が動かない」ことをそのまま観測できる。
/// </summary>
public class UpdateCheckerEntryPointTests
{
    private GameObject go;
    private UpdateChecker checker;

    [SetUp]
    public void SetUp()
    {
        go = new GameObject("UpdateCheckerEntryPointTests");
        checker = go.AddComponent<UpdateChecker>();
    }

    [TearDown]
    public void TearDown()
    {
        if (go != null) { Object.DestroyImmediate(go); }
    }

    [Test]
    public void RequestDownload_BeforeCheck_DoesNothing()
    {
        // 起動直後 (確認前) にダウンロード要求が来ても何もしない。
        checker.RequestDownload();
        Assert.AreEqual(UpdateFlowPhase.Idle, checker.Model.Phase);
    }

    [Test]
    public void RequestDownload_WithoutZipAsset_DoesNothing()
    {
        // 「更新がある」只知道 (Available) でも、実際の ZIP アセットが無ければ
        // ダウンロードは始まらない (通信も起きない)。
        checker.Model.StartCheck();
        checker.Model.CheckFoundNewVersion("v0.1.3-win64-abc1234", "0.1.3");
        Assert.AreEqual(UpdateFlowPhase.Available, checker.Model.Phase);

        checker.RequestDownload();
        Assert.AreEqual(UpdateFlowPhase.Available, checker.Model.Phase,
                        "ZIP アセットが無いのにダウンロードが始まっている");
    }

    [Test]
    public void RequestApply_WithoutStagedUpdate_DoesNothing()
    {
        // staging 済み更新が無いのに適用を要求しても、状態もプロセスも動かない。
        checker.Model.StartCheck();
        checker.Model.CheckFoundNewVersion("v0.1.3-win64-abc1234", "0.1.3");

        checker.RequestApply();
        Assert.AreEqual(UpdateFlowPhase.Available, checker.Model.Phase);
        Assert.IsFalse(checker.Model.UserConfirmed);
    }

    [Test]
    public void ApplyStagedUpdateNow_WithoutChecker_DoesNotThrow()
    {
        Object.DestroyImmediate(go);
        go = null;
        Assert.DoesNotThrow(() => UpdateChecker.ApplyStagedUpdateNow());
    }

    [Test]
    public void UpdateChecker_HasNoQuitOnItsOwn_WhenNothingIsStaged()
    {
        // 「押さない = 更新しない」の最終確認: 何も staging されていない状態では
        // 適用に入れない (UserConfirmed が立たない)。
        checker.Model.StartCheck();
        checker.Model.CheckFoundNewVersion("v0.1.3-win64-abc1234", "0.1.3");
        checker.RequestApply();
        Assert.IsFalse(checker.Model.UserConfirmed);
    }

    // ── 確認失敗 3 経路 (issue #118 の本 PR における唯一の機能的変更) ──

    /// <summary>
    /// 確認に失敗した 3 経路 (ビルド版識別子が解釈不能 / 通信失敗 / 新版なし) では、
    /// 状態機械が <see cref="UpdateFlowPhase.Checking"/> のまま残らない。
    /// Checking に固まると <see cref="UpdateFlowModel.StartCheck"/> が no-op になり、
    /// 将来「再チェック」機構を追加したときに確認できなくなるため。
    /// </summary>
    [TestCase("ビルド版識別子を解釈できません: '0.1.0' — 更新確認を省略します (現行版を維持します)。")]
    [TestCase("Releases の確認に失敗しました (timeout) — 現行版のまま続行します。")]
    [TestCase("win64 の更新候補はありません — 現行版のまま続行します。")]
    public void FinishCheckWithoutUpdate_ReturnsToIdle_AndAllowsRecheck(string reason)
    {
        checker.Model.StartCheck();
        Assert.AreEqual(UpdateFlowPhase.Checking, checker.Model.Phase);

        checker.FinishCheckWithoutUpdate(reason);

        Assert.AreNotEqual(UpdateFlowPhase.Checking, checker.Model.Phase,
                            "確認に失敗したのに Checking のまま残っています (再チェック不能)");
        Assert.AreEqual(UpdateFlowPhase.Idle, checker.Model.Phase);

        // 再チェックできる (StartCheck は Idle からのみ許される)。
        checker.Model.StartCheck();
        Assert.AreEqual(UpdateFlowPhase.Checking, checker.Model.Phase);

        // 「新版あり」でも見える誤表示はしない (通知は出ない)。
        checker.FinishCheckWithoutUpdate(reason);
        Assert.IsFalse(UpdateNoticePresenter.ShouldShow("TitleScene", checker.Model),
                       "確認失敗後に更新の通知が出ています");
    }

    [Test]
    public void FinishCheckWithoutUpdate_DoesNotWipeTheAvailableCandidate()
    {
        // 確認失敗の処理は Checking からのみ効く (UpdateFlowModel.CheckFinishedWithoutUpdate の仕様)。
        // 候補がある状態で呼んでも候補情報 (LatestTag) は消さない (安全側)。
        checker.Model.StartCheck();
        checker.Model.CheckFoundNewVersion("v0.1.3-win64-abc1234", "0.1.3");
        checker.FinishCheckWithoutUpdate("テスト");
        Assert.AreEqual(UpdateFlowPhase.Available, checker.Model.Phase);
        Assert.AreEqual("v0.1.3-win64-abc1234", checker.Model.LatestTag);
    }
}
