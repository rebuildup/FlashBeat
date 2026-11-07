using FlashBeat.UI;
using NUnit.Framework;

/// <summary>
/// HUD の状態判定 (Issue #30 §4): 準備中 / 失敗 / 非表示の切り分けと、
/// それに対応する表示文言・導線フラグ。
/// </summary>
public class HudPlayStateTests
{
    [Test]
    public void PlaybackStartedHidesStatus()
    {
        Assert.AreEqual(HudPlayPhase.Hidden, HudPlayState.Evaluate(false, true, false, 0f));
    }

    [Test]
    public void ReadyToStartHidesStatus()
    {
        // 準備完了で Space 待ちの画面は、既存の SpacetoStart 案内に任せる
        Assert.AreEqual(HudPlayPhase.Hidden, HudPlayState.Evaluate(false, false, false, 0f));
    }

    [Test]
    public void LoadingShowsPreparing()
    {
        Assert.AreEqual(HudPlayPhase.Preparing, HudPlayState.Evaluate(false, false, true, 0f));
        Assert.AreEqual(HudPlayPhase.Preparing, HudPlayState.Evaluate(false, false, true, 44.9f));
    }

    [Test]
    public void LoadingBeyondTimeoutShowsFailure()
    {
        Assert.AreEqual(HudPlayPhase.Failed, HudPlayState.Evaluate(false, false, true, HudPlayState.FailureTimeoutSeconds));
        Assert.AreEqual(HudPlayPhase.Failed, HudPlayState.Evaluate(false, false, true, 120f));
    }

    [Test]
    public void VideoErrorShowsFailureEvenDuringPlayback()
    {
        // URL 失効などで再生中にエラーが出たら、開始済みでも失敗として扱う
        Assert.AreEqual(HudPlayPhase.Failed, HudPlayState.Evaluate(true, false, true, 3f));
        Assert.AreEqual(HudPlayPhase.Failed, HudPlayState.Evaluate(true, true, false, 0f));
    }

    [Test]
    public void FailureMessageNamesTheWayBackToSelect()
    {
        string message = HudPlayState.Message(HudPlayPhase.Failed);
        Assert.IsTrue(message.Contains("失敗"), $"failure message should mention the failure: {message}");
        Assert.IsTrue(message.Contains("選曲"), $"failure message should name the way back: {message}");
    }

    [Test]
    public void PreparingMessageSaysItIsStillLoading()
    {
        string message = HudPlayState.Message(HudPlayPhase.Preparing);
        Assert.IsTrue(message.Contains("準備中"), $"preparing message should say so: {message}");
    }

    [Test]
    public void HiddenPhaseHasNoMessage()
    {
        Assert.AreEqual("", HudPlayState.Message(HudPlayPhase.Hidden));
    }

    [Test]
    public void BackEscapeIsOfferedWhilePreparingOrFailed()
    {
        Assert.IsTrue(HudPlayState.ShowBackEscape(HudPlayPhase.Preparing));
        Assert.IsTrue(HudPlayState.ShowBackEscape(HudPlayPhase.Failed));
        Assert.IsFalse(HudPlayState.ShowBackEscape(HudPlayPhase.Hidden));
    }
}
