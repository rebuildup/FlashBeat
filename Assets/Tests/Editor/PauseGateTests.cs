using FlashBeat.Gameplay;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// ポーズゲート (Issue #224) の EditMode テスト。
/// 純ロジック (PauseGateLogic) の時間計算と、PauseGate 状態機械の
/// GManager への書き込みを検証する。
/// </summary>
public class PauseGateTests
{
    float savedStepTime;
    float savedStartTime;

    [SetUp]
    public void SaveClock()
    {
        savedStepTime = GManager.step_time;
        savedStartTime = GManager.StartTime;
        PauseGate.HardReset();
    }

    [TearDown]
    public void RestoreClock()
    {
        PauseGate.HardReset();
        GManager.step_time = savedStepTime;
        GManager.StartTime = savedStartTime;
    }

    // --- PauseGateLogic (純ロジック) ---

    [Test]
    public void CanJudge_BlocksJudgementWhilePaused()
    {
        Assert.IsTrue(PauseGateLogic.CanJudge(false));
        Assert.IsFalse(PauseGateLogic.CanJudge(true));
    }

    [Test]
    public void FrozenMediaTime_KeepsTheCurrentChartTime()
    {
        Assert.AreEqual(42.5f, PauseGateLogic.FrozenMediaTime(42.5f));
        Assert.AreEqual(0f, PauseGateLogic.FrozenMediaTime(0f));
    }

    [Test]
    public void ResumeJudgeStartTime_RealignsTheWallClockToTheFrozenChartTime()
    {
        // ポーズ開始: 譜面時刻 42.5 で固定 (壁時計 100)。
        // 10 秒ポーズして壁時計が 110 になったとき、StartTime は 110 - 42.5。
        float startTime = PauseGateLogic.ResumeJudgeStartTime(110f, 42.5f);
        Assert.AreEqual(67.5f, startTime);

        // 再開直後の判定時刻 (Time.time - StartTime) は固定中の譜面時刻に一致する。
        // これが一致しないと再開の瞬間にノーツ位置が飛ぶ。
        Assert.AreEqual(42.5f, 110f - startTime);
    }

    [Test]
    public void ResumeJudgeStartTime_WithZeroPauseDuration_KeepsTheClock()
    {
        // ポーズ時間 0 なら StartTime は不変 (既存の判定タイミングを壊さない)。
        float startTime = PauseGateLogic.ResumeJudgeStartTime(100f, 100f - 30f);
        Assert.AreEqual(30f, startTime);
        Assert.AreEqual(70f, 100f - startTime);
    }

    // --- PauseGate (状態機械) ---

    [Test]
    public void Enter_FreezesTheChartTime()
    {
        GManager.step_time = 12.5f;

        PauseGate.Enter();

        Assert.IsTrue(PauseGate.IsPaused);
        Assert.AreEqual(12.5f, PauseGate.FrozenMediaTime);
        Assert.AreEqual(12.5f, GManager.step_time);
    }

    [Test]
    public void Enter_IsIdempotent_WhilePaused()
    {
        GManager.step_time = 12.5f;
        PauseGate.Enter();

        // 2 回目の Enter で譜面時刻を再取得しない (最初の固定値を維持)。
        GManager.step_time = 99f;
        PauseGate.Enter();

        Assert.AreEqual(12.5f, PauseGate.FrozenMediaTime);
    }

    [Test]
    public void Exit_RestoresTheJudgeClockWithoutANoteJump()
    {
        GManager.step_time = 42.5f;

        PauseGate.Enter();
        PauseGate.Exit();

        Assert.IsFalse(PauseGate.IsPaused);
        Assert.AreEqual(42.5f, GManager.step_time);
        // 再開直後の判定時刻 (Time.time - StartTime) が固定中の譜面時刻に一致する。
        Assert.AreEqual(42.5f, Time.time - GManager.StartTime, 0.001f);
    }

    [Test]
    public void Exit_IsNoOp_WhenNotPaused()
    {
        GManager.StartTime = 1234f;

        PauseGate.Exit();

        Assert.IsFalse(PauseGate.IsPaused);
        Assert.AreEqual(1234f, GManager.StartTime);
    }

    [Test]
    public void HardReset_ClearsThePauseState()
    {
        PauseGate.Enter();
        Assert.IsTrue(PauseGate.IsPaused);

        PauseGate.HardReset();

        Assert.IsFalse(PauseGate.IsPaused);
        Assert.AreEqual(0f, PauseGate.FrozenMediaTime);
    }
}
