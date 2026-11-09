using FlashBeat.Update;
using FlashBeat.UI.V6.Common;
using FlashBeat.UI.V6.Title;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Issue #118「更新は明示式」の v6 タイトル表示。
///
/// v6 モードでは旧 (v5) の更新通知ピルが隠されるため (V6UiMode.HideLegacyUpdateNotice)、
/// v6 タイトルは「アップデートあり」を自前で描く必要がある。
/// ここはその **判断だけ** (pure) を固定する:
///
/// - 更新が実際に利用できるときだけ出す ( Idle / Checking では出ない)
/// - このセッションで dismiss (F4) したら出ない
/// - 「状態の表示」(『アップデートあり』) と「操作の表示」(『更新して再起動』ボタン) が区別できる
/// - 「押さなければ更新しません」を明示する
/// </summary>
public class V6TitleUpdateNoticeTests
{
    private UpdateFlowModel model;

    [SetUp]
    public void SetUp()
    {
        model = new UpdateFlowModel();
    }

    private void MakeAvailable()
    {
        model.StartCheck();
        model.CheckFoundNewVersion("v0.1.5-win64-abc1234", "0.1.5");
    }

    // --- 表示条件 ---

    [Test]
    public void ShowUpdateAvailableNotice_FalseWithoutUpdate()
    {
        Assert.IsFalse(V6TitleLogic.ShowUpdateAvailableNotice(null));
        Assert.IsFalse(V6TitleLogic.ShowUpdateAvailableNotice(model), "Idle では出ない");

        model.StartCheck();
        Assert.IsFalse(V6TitleLogic.ShowUpdateAvailableNotice(model), "確認中 (Checking) では出ない");
    }

    [Test]
    public void ShowUpdateAvailableNotice_TrueWhenUpdateIsAvailable()
    {
        MakeAvailable();
        Assert.IsTrue(V6TitleLogic.ShowUpdateAvailableNotice(model));

        model.BeginDownload();
        Assert.IsTrue(V6TitleLogic.ShowUpdateAvailableNotice(model));

        model.DownloadSucceeded();
        Assert.IsTrue(V6TitleLogic.ShowUpdateAvailableNotice(model));
    }

    [Test]
    public void ShowUpdateAvailableNotice_FalseAfterDismiss()
    {
        MakeAvailable();
        model.Dismiss();
        Assert.IsFalse(V6TitleLogic.ShowUpdateAvailableNotice(model),
            "F4 / × で消した通知は復帰しない (押さない = 更新しない)");
    }

    [Test]
    public void ShowUpdateAvailableNotice_FalseWhenCheckFinishedWithoutUpdate()
    {
        model.StartCheck();
        model.CheckFinishedWithoutUpdate();
        Assert.IsFalse(V6TitleLogic.ShowUpdateAvailableNotice(model));
    }

    // --- 文言 ---

    [Test]
    public void UpdateNoticeTitle_Available_SaysUpdateIsAvailableAndVersion()
    {
        MakeAvailable();
        string title = V6TitleLogic.UpdateNoticeTitle(model.Phase, model.LatestVersion, model.DownloadProgress);
        StringAssert.Contains("アップデートあり", title);
        StringAssert.Contains("v0.1.5", title);
    }

    [Test]
    public void UpdateNoticeTitle_WithoutVersion_StillSaysUpdateIsAvailable()
    {
        string title = V6TitleLogic.UpdateNoticeTitle(UpdateFlowPhase.Available, null, 0f);
        Assert.AreEqual("アップデートあり", title);
    }

    [Test]
    public void UpdateNoticeTitle_EmptyWhenNothingToShow()
    {
        Assert.AreEqual("", V6TitleLogic.UpdateNoticeTitle(UpdateFlowPhase.Idle, null, 0f));
        Assert.AreEqual("", V6TitleLogic.UpdateNoticeTitle(UpdateFlowPhase.Checking, "0.1.5", 0f));
    }

    [Test]
    public void UpdateNoticeTitle_Downloading_ShowsProgress()
    {
        string title = V6TitleLogic.UpdateNoticeTitle(UpdateFlowPhase.Downloading, "0.1.5", 0.42f);
        StringAssert.Contains("準備中", title);
        StringAssert.Contains("42%", title);
    }

    [Test]
    public void UpdateNoticeTitle_Ready_SaysPreparationIsDone()
    {
        string title = V6TitleLogic.UpdateNoticeTitle(UpdateFlowPhase.Ready, "0.1.5", 1f);
        StringAssert.Contains("準備ができました", title);
    }

    [Test]
    public void UpdateNoticeHint_Available_SaysNotUpdatingUnlessPressed()
    {
        string hint = V6TitleLogic.UpdateNoticeHint(UpdateFlowPhase.Available);
        StringAssert.Contains("押さなければ更新しません", hint);
        StringAssert.Contains("F3", hint);
    }

    [Test]
    public void UpdateNoticeHint_Ready_MentionsApply()
    {
        string hint = V6TitleLogic.UpdateNoticeHint(UpdateFlowPhase.Ready);
        StringAssert.Contains("適用", hint);
        StringAssert.Contains("F3", hint);
    }

    [Test]
    public void UpdateNoticeHint_EmptyWhenNothingToShow()
    {
        Assert.AreEqual("", V6TitleLogic.UpdateNoticeHint(UpdateFlowPhase.Idle));
        Assert.AreEqual("", V6TitleLogic.UpdateNoticeHint(UpdateFlowPhase.Checking));
    }

    [Test]
    public void NoticeLabel_IsDifferentFromTheUpdateActionLabel()
    {
        // 「状態の表示」と「操作の表示」は別の文言にする (混ぜない)。
        Assert.AreNotEqual(V6TitleLogic.UpdateButtonLabel(UpdateFlowPhase.Available, 0f),
            V6TitleLogic.UpdateAvailableLabel);
        Assert.AreEqual("更新して再起動", V6TitleLogic.UpdateButtonLabel(UpdateFlowPhase.Available, 0f));
        Assert.AreEqual("アップデートあり", V6TitleLogic.UpdateAvailableLabel);
    }

    // --- レイアウト (v6 design 空間) ---

    [Test]
    public void UpdateNoticeBanner_StaysInsideMainZone()
    {
        // Main ゾーンはフレーム内 (24,96) 1680×780。
        Vector2 mainTopLeft = new Vector2(24f, 96f);
        Vector2 mainSize = new Vector2(1680f, 780f);
        Assert.IsTrue(V6Layout.FitsIn(mainTopLeft, mainSize, V6TitleLogic.UpdateNoticePos,
            V6TitleLogic.UpdateNoticeSize), "更新通知バナーが Main ゾーンからはみ出している");
    }

    [Test]
    public void UpdateNoticeBanner_DoesNotOverlapTheLogo()
    {
        // ロゴ (Main ローカル (0,180) 1680×332) の上に重ならない。
        Assert.LessOrEqual(V6TitleLogic.UpdateNoticePos.y + V6TitleLogic.UpdateNoticeSize.y,
            V6TitleLogic.LogoPos.y);
    }

    [Test]
    public void UpdateNoticeBanner_AlignsWithTheTabRow()
    {
        // タブ列と同じ幅・左右位置 (タイトル画面の中央軸)。
        Assert.AreEqual(V6TitleLogic.TabsPos.x, V6TitleLogic.UpdateNoticePos.x);
        Assert.AreEqual(V6TitleLogic.TabsSize.x, V6TitleLogic.UpdateNoticeSize.x);
    }
}
