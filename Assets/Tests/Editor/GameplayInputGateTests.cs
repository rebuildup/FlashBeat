using FlashBeat.Gameplay;
using NUnit.Framework;

/// <summary>
/// Gameplay input gate (Issue #604) の EditMode テスト。
/// クリック音と判定が共有する「プレイ中だけ受け付ける」条件を検証する:
/// 曲開始前 (GManager.Start == false)、ポーズ中、曲終了表示後は false。
/// </summary>
public class GameplayInputGateTests
{
    [Test]
    public void AcceptsInputWhilePlaying()
    {
        Assert.IsTrue(GameplayInputGate.CanAcceptInput(
            gameStarted: true, isPaused: false, songEndShown: false));
    }

    [Test]
    public void RejectsInputBeforeTheSongStarts()
    {
        Assert.IsFalse(GameplayInputGate.CanAcceptInput(
            gameStarted: false, isPaused: false, songEndShown: false));
    }

    [Test]
    public void RejectsInputWhilePaused()
    {
        Assert.IsFalse(GameplayInputGate.CanAcceptInput(
            gameStarted: true, isPaused: true, songEndShown: false));
    }

    [Test]
    public void RejectsInputAfterTheSongEndWasShown()
    {
        Assert.IsFalse(GameplayInputGate.CanAcceptInput(
            gameStarted: true, isPaused: false, songEndShown: true));
    }

    [Test]
    public void RejectsInputWhenPausedAndSongEnded()
    {
        Assert.IsFalse(GameplayInputGate.CanAcceptInput(
            gameStarted: true, isPaused: true, songEndShown: true));
    }

    [Test]
    public void RejectsInputWhenNotStartedEvenIfOtherFlagsAreClear()
    {
        // GManager.Start が false の間は他のフラグに関係なく受け付けない
        // (リザルト・選曲画面など非プレイ状態)。
        Assert.IsFalse(GameplayInputGate.CanAcceptInput(
            gameStarted: false, isPaused: true, songEndShown: true));
    }
}
