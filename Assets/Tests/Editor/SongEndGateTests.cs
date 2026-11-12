using FlashBeat.Gameplay;
using NUnit.Framework;

/// <summary>
/// 曲終了のゲート (design 決定 2026-11-11 §6.2、issue #383)。フェードと
/// 「終了」の表示中は判定キーを受け付けない。判定を止めても、残ったノーツは
/// SongEndMachine の期限で確定するので合計は変わらない (issue #226 の回帰)。
/// </summary>
public class SongEndGateTests
{
    [TearDown]
    public void TearDown()
    {
        SongEndGate.Reset();
    }

    [Test]
    public void JudgesWhilePlaying()
    {
        Assert.IsTrue(SongEndGate.CanJudge(isPaused: false, endShown: false));
    }

    [Test]
    public void DoesNotJudgeWhilePaused()
    {
        Assert.IsFalse(SongEndGate.CanJudge(isPaused: true, endShown: false));
    }

    [Test]
    public void DoesNotJudgeAfterTheSongEndWasShown()
    {
        Assert.IsFalse(SongEndGate.CanJudge(isPaused: false, endShown: true));
    }

    [Test]
    public void EnterClosesTheGateUntilTheNextPlayStarts()
    {
        SongEndGate.Enter();
        Assert.IsTrue(SongEndGate.BlocksJudging);
        Assert.IsFalse(SongEndGate.CanJudge(isPaused: false, endShown: SongEndGate.BlocksJudging));

        SongEndGate.Reset();
        Assert.IsFalse(SongEndGate.BlocksJudging);
        Assert.IsTrue(SongEndGate.CanJudge(isPaused: false, endShown: SongEndGate.BlocksJudging));
    }
}
