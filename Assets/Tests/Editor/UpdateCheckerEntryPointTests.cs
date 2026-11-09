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
}
