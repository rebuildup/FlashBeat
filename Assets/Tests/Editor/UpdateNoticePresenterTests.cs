using FlashBeat.UI;
using FlashBeat.Update;
using NUnit.Framework;

/// <summary>
/// issue #118「更新は明示式」の通知 UI の振る舞い。
/// Title 画面 (ホーム) にだけ『アップデートあり』が出ること、
/// 押さない限り更新しないこと、を presenters 経由で固定する。
/// UpdateNoticeUI 本体は MonoBehaviour なので、表示・文言・操作の判断は
/// 純粋クラス <see cref="UpdateNoticePresenter"/> に切り出して検証する。
/// </summary>
public class UpdateNoticePresenterTests
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
        model.CheckFoundNewVersion("v0.1.3-win64-abc1234", "0.1.3");
    }

    private void MakeReady()
    {
        MakeAvailable();
        model.BeginDownload();
        model.DownloadSucceeded();
    }

    // ── 表示する画面 ─────────────────────────────────────────────

    [Test]
    public void ShouldShow_OnlyOnTitleScene()
    {
        MakeAvailable();
        Assert.IsTrue(UpdateNoticePresenter.ShouldShow("TitleScene", model),
                      "タイトル画面には『アップデートあり』が出るはず");
        foreach (var scene in new[] { "GameScene", "SelectScene", "OptionScene", "ResultScene", "Opening" })
        {
            Assert.IsFalse(UpdateNoticePresenter.ShouldShow(scene, model),
                           "更新通知は " + scene + " では出ない (ゲームプレイを妨げない)");
        }
    }

    [Test]
    public void ShouldShow_FalseWhenNoUpdateAndNoResultNotice()
    {
        model.StartCheck();
        model.CheckFinishedWithoutUpdate();
        Assert.IsFalse(UpdateNoticePresenter.ShouldShow("TitleScene", model));
    }

    [Test]
    public void ShouldShow_TrueForAvailableDownloadingReadyAndApplying()
    {
        MakeAvailable();
        Assert.IsTrue(UpdateNoticePresenter.ShouldShow("TitleScene", model));

        model.BeginDownload();
        Assert.IsTrue(UpdateNoticePresenter.ShouldShow("TitleScene", model));

        model.DownloadSucceeded();
        Assert.IsTrue(UpdateNoticePresenter.ShouldShow("TitleScene", model));

        model.BeginApply();
        Assert.IsTrue(UpdateNoticePresenter.ShouldShow("TitleScene", model));
    }

    [Test]
    public void ShouldShow_FalseAfterDismiss()
    {
        MakeAvailable();
        model.Dismiss();
        Assert.IsFalse(UpdateNoticePresenter.ShouldShow("TitleScene", model),
                       "× / F4 で隠した通知は復帰しない (このセッションでは更新しない)");
    }

    [Test]
    public void ShouldShow_TrueWhenOnlyLastResultNoticeExists()
    {
        // 前回の更新結果 (issue #125) だけの通知も Title で出す。
        model.SetLastResultNotice("前回の更新に失敗しました: 展開エラー");
        Assert.IsTrue(UpdateNoticePresenter.ShouldShow("TitleScene", model));
        Assert.IsFalse(UpdateNoticePresenter.ShouldShow("GameScene", model));
    }

    // ── 操作 (押したときの動き) ───────────────────────────────────

    [Test]
    public void ResolveAction_Available_IsDownload()
    {
        MakeAvailable();
        Assert.AreEqual(UpdateNoticeAction.Download, UpdateNoticePresenter.ResolveAction(model));
    }

    [Test]
    public void ResolveAction_Ready_IsApply()
    {
        MakeReady();
        Assert.AreEqual(UpdateNoticeAction.Apply, UpdateNoticePresenter.ResolveAction(model));
    }

    [Test]
    public void ResolveAction_NothingElse()
    {
        // 起動直後 (Idle) / 確認中 (Checking) / ダウンロード中 (Downloading) /
        // 適用中 (Applying) では、何もしない (更新しない)。
        Assert.AreEqual(UpdateNoticeAction.None, UpdateNoticePresenter.ResolveAction(model));

        model.StartCheck();
        Assert.AreEqual(UpdateNoticeAction.None, UpdateNoticePresenter.ResolveAction(model));

        MakeAvailable();
        model.BeginDownload();
        Assert.AreEqual(UpdateNoticeAction.None, UpdateNoticePresenter.ResolveAction(model));

        model.DownloadSucceeded();
        model.BeginApply();
        Assert.AreEqual(UpdateNoticeAction.None, UpdateNoticePresenter.ResolveAction(model));
    }

    [Test]
    public void ResolveAction_NoneAfterDismiss()
    {
        MakeReady();
        model.Dismiss();
        Assert.AreEqual(UpdateNoticeAction.None, UpdateNoticePresenter.ResolveAction(model),
                       "dismiss した後は通知そのものを出さない (押さない = 更新しない)");
        // 状態 (staging) は保持される — 適用は別経路 (Option のボタン) から明示的に行える。
        Assert.AreEqual(UpdateFlowPhase.Ready, model.Phase);
    }

    // ── 明示操作 (v6 タイトルの「更新して再起動」ボタン) ─────────────
    //
    // dismiss (F4) は「このセッションは通知を出さない」だけで、更新を禁じる
    // ものではない。v6 タイトルでは通知バナーと操作ボタンが別要素なので、
    // dismiss 後にボタンを押しても効かなければ「押しても何も起きないボタン」
    // になる (Issue #118 / #167)。

    [Test]
    public void ResolveUserAction_Available_IsDownloadEvenAfterDismiss()
    {
        MakeAvailable();
        model.Dismiss();
        Assert.AreEqual(UpdateNoticeAction.Download, UpdateNoticePresenter.ResolveUserAction(model));
    }

    [Test]
    public void ResolveUserAction_Ready_IsApplyEvenAfterDismiss()
    {
        MakeReady();
        model.Dismiss();
        Assert.AreEqual(UpdateNoticeAction.Apply, UpdateNoticePresenter.ResolveUserAction(model));
    }

    [Test]
    public void ResolveUserAction_NoneWhileDownloadingOrIdle()
    {
        Assert.AreEqual(UpdateNoticeAction.None, UpdateNoticePresenter.ResolveUserAction(model),
                       "Idle では押しても何も起きない (ボタン自体が出ない)");
        Assert.AreEqual(UpdateNoticeAction.None, UpdateNoticePresenter.ResolveUserAction(null));

        MakeAvailable();
        model.BeginDownload();
        Assert.AreEqual(UpdateNoticeAction.None, UpdateNoticePresenter.ResolveUserAction(model),
                       "Downloading 中は進捗表示のみ (再押下で二重に開始しない)");
    }

    // ── 文言 ─────────────────────────────────────────────────────

    [Test]
    public void BuildText_Available_SaysUpdateIsAvailableAndOptional()
    {
        MakeAvailable();
        var text = UpdateNoticePresenter.BuildText(model);
        Assert.IsTrue(text.Contains("アップデートあり"), "『アップデートあり』表示が文言にない: " + text);
        Assert.IsTrue(text.Contains("v0.1.3"), "バージョンが文言にない: " + text);
        Assert.IsTrue(text.Contains("押さなければ更新しません"),
                      "『押さなければ更新しません』の明示がない: " + text);
    }

    [Test]
    public void BuildText_Ready_SaysApplyNeedsExplicitAction()
    {
        MakeReady();
        var text = UpdateNoticePresenter.BuildText(model);
        Assert.IsTrue(text.Contains("適用"), "適用に関する文言がない: " + text);
        Assert.IsTrue(text.Contains("F3"), "操作キー (F3) の案内がない: " + text);
    }

    [Test]
    public void BuildText_IncludesFailureMessage()
    {
        MakeAvailable();
        model.BeginDownload();
        model.DownloadFailed("SHA-256 検証に失敗しました。現行のまま使えます");
        var text = UpdateNoticePresenter.BuildText(model);
        Assert.IsTrue(text.Contains("SHA-256"), "失敗理由が文言にない: " + text);
    }

    [Test]
    public void BuildText_IncludesLastResultNoticeBeforePhaseText()
    {
        MakeAvailable();
        model.SetLastResultNotice("前回の更新に失敗しました: 展開エラー");
        var text = UpdateNoticePresenter.BuildText(model);
        Assert.IsTrue(text.Contains("前回の更新に失敗しました"), "前回の更新結果がない: " + text);
        Assert.IsTrue(text.Contains("アップデートあり"), "更新候補の文言が失われている: " + text);
        Assert.Less(text.IndexOf("前回の更新に失敗しました", System.StringComparison.Ordinal),
                    text.IndexOf("アップデートあり", System.StringComparison.Ordinal));
    }

    [Test]
    public void BuildText_EmptyWhenNothingToShow()
    {
        Assert.AreEqual(string.Empty, UpdateNoticePresenter.BuildText(model));
    }
}
