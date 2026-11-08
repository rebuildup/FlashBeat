using NUnit.Framework;
using FlashBeat.Gameplay;

/// <summary>
/// 曲終了状態機械 (SongEndMachine, Issue #226) の EditMode テスト。
///
/// 旧実装は duration だけを見て Result へ遷移していたため、
///   (a) 末尾ノーツの判定窓が流れる前に遷移し、判定合計がノーツ数に届かず、
///   (b) 動画がメタデータより先に終わると時計が止まり Result に進めない
/// という問題があった。ここで状態遷移の契約を固定する:
///
///   - 終端条件が成立しても未確定ノーツがあれば Completed にしない
///   - 全ノーツ確定 + 終端条件成立で Completed
///   - 期限 (SettleDeadline + 猶予) を過ぎても未確定なら強制完了 (印付き)
///   - duration が不正でも破綻しない (MediaTimeline の契約と合わせて検証)
///   - Failed は Completed にならない
///
/// SongEndMachine は Input / Time / MonoBehaviour に依存しないため、
/// 時刻を明示して直接検証する。
/// </summary>
public class SongEndStateMachineTests
{
    static SongEndTick Tick(bool endReached, float judgeTime, float settleDeadline, int pending, int total)
    {
        return new SongEndTick
        {
            EndReached = endReached,
            JudgeTime = judgeTime,
            SettleDeadline = settleDeadline,
            PendingNotes = pending,
            TotalNotes = total
        };
    }

    // ---- 終端条件とノーツの確定 ----

    [Test]
    public void StaysPlayingWhileEndNotReached()
    {
        var machine = new SongEndMachine();

        bool done = machine.Update(Tick(false, 5f, 10.2f, 0, 10));

        Assert.IsFalse(done);
        Assert.AreEqual(SongEndState.Playing, machine.State);
    }

    [Test]
    public void TailNoteKeepsResultWaitingUntilSettled()
    {
        // Issue #226 の再現シナリオ: duration 10s (リードアウト 1s) で judge 時刻 9s に
        // 終端条件が立ち、末尾ノーツ (10.2s に確定) が未確定のまま残る。
        var machine = new SongEndMachine();

        bool done = machine.Update(Tick(true, 62.0f, 60.4f, 3, 10));
        Assert.IsFalse(done);
        Assert.AreEqual(SongEndState.Finishing, machine.State);

        // Miss 窓が流れる前 (10.2s 未満) は完了しない。
        done = machine.Update(Tick(true, 10.1f, 10.2f, 1, 2));
        Assert.IsFalse(done);
        Assert.AreEqual(SongEndState.Finishing, machine.State);

        // judge が時切れ Miss として確定し (pending 0)、初めて完了する。
        done = machine.Update(Tick(true, 10.3f, 10.2f, 0, 2));
        Assert.IsTrue(done);
        Assert.AreEqual(SongEndState.Completed, machine.State);
        Assert.IsFalse(machine.ForcedByDeadline);
    }

    [Test]
    public void SettledNotesAndMediaEndCompleteOnTheSameTick()
    {
        var machine = new SongEndMachine();

        bool done = machine.Update(Tick(true, 60f, 60.4f, 0, 10));

        Assert.IsTrue(done);
        Assert.AreEqual(SongEndState.Completed, machine.State);
        Assert.IsFalse(machine.ForcedByDeadline);
    }

    [Test]
    public void SettledNotesDoNotCompleteBeforeTheEnd()
    {
        // ノーツが全て確定しても、終端条件が成立するまで Result へは進まない
        // (曲の残りを勝手に打ち切らない)。
        var machine = new SongEndMachine();

        bool done = machine.Update(Tick(false, 30f, 10.2f, 0, 10));

        Assert.IsFalse(done);
        Assert.AreEqual(SongEndState.Playing, machine.State);
    }

    [Test]
    public void ZeroNotesCompleteAtTheEnd()
    {
        // 譜面なし (noteNum == 0) でも曲は終わる。
        var machine = new SongEndMachine();

        bool done = machine.Update(Tick(true, 47f, 0f, 0, 0));

        Assert.IsTrue(done);
        Assert.AreEqual(SongEndState.Completed, machine.State);
    }

    // ---- 期限切れの強制完了 ----

    [Test]
    public void DeadlineForcesCompletionWithUnsettledNotes()
    {
        var machine = new SongEndMachine();
        machine.Update(Tick(true, 60f, 60.4f, 3, 10));
        Assert.AreEqual(SongEndState.Finishing, machine.State);

        // 猶予内 (SettleDeadline + SettleGraceSeconds 未満) は待つ。
        bool done = machine.Update(Tick(true, 62.5f, 60.4f, 3, 10));
        Assert.IsFalse(done);

        // 期限を過ぎても確定しない場合は強制完了 (未確定を残す印付き)。
        done = machine.Update(Tick(true, 62.41f, 60.4f, 3, 10));
        Assert.IsTrue(done);
        Assert.AreEqual(SongEndState.Completed, machine.State);
        Assert.IsTrue(machine.ForcedByDeadline);
    }

    [Test]
    public void ForcedCompletionDoesNotHappenBeforeTheGraceElapses()
    {
        var machine = new SongEndMachine();
        machine.Update(Tick(true, 60f, 60.4f, 1, 10));

        bool done = machine.Update(Tick(true, 60.4f + SongEndMachine.SettleGraceSeconds - 0.01f, 60.4f, 1, 10));

        Assert.IsFalse(done);
        Assert.AreEqual(SongEndState.Finishing, machine.State);
    }

    // ---- Failed 経路 ----

    [Test]
    public void FailedDuringPlayingNeverCompletes()
    {
        var machine = new SongEndMachine();
        machine.Fail(SongEndFailure.MediaError);

        bool done = machine.Update(Tick(true, 999f, 10f, 0, 10));

        Assert.IsFalse(done);
        Assert.AreEqual(SongEndState.Failed, machine.State);
        Assert.AreEqual(SongEndFailure.MediaError, machine.Failure);
    }

    [Test]
    public void FailDuringFinishingKeepsFailedState()
    {
        var machine = new SongEndMachine();
        machine.Update(Tick(true, 60f, 60.4f, 2, 10));
        Assert.AreEqual(SongEndState.Finishing, machine.State);

        machine.Fail(SongEndFailure.MediaStalled);

        bool done = machine.Update(Tick(true, 999f, 60.4f, 0, 10));
        Assert.IsFalse(done);
        Assert.AreEqual(SongEndState.Failed, machine.State);
        Assert.AreEqual(SongEndFailure.MediaStalled, machine.Failure);
    }

    [Test]
    public void FailAfterCompletionIsIgnored()
    {
        var machine = new SongEndMachine();
        Assert.IsTrue(machine.Update(Tick(true, 60f, 60.4f, 0, 10)));

        machine.Fail(SongEndFailure.MediaError);

        Assert.AreEqual(SongEndState.Completed, machine.State);
        Assert.AreEqual(SongEndFailure.None, machine.Failure);
    }

    [Test]
    public void FailWithoutReasonIsIgnored()
    {
        var machine = new SongEndMachine();
        machine.Fail(SongEndFailure.None);

        Assert.AreEqual(SongEndState.Playing, machine.State);
    }
}

/// <summary>
/// duration を終了条件に使う際の MediaTimeline の契約 (Issue #226)。
/// 呼び出し側 (MusicManager) は
///   - 実メディアあり: HasSongEnded (不正な duration では常に false)
///   - メディアなし (壁時計フォールバック): mediaTime >= FallbackSongEnd
/// で終端を判定し、メディア自体の終端 (loop point / 再生停止) が常に優先される。
/// </summary>
public class MediaTimelineSongEndContractTests
{
    [Test]
    public void UsableDurationRejectsBrokenValues()
    {
        Assert.IsFalse(MediaTimeline.IsUsableDuration(0f));
        Assert.IsFalse(MediaTimeline.IsUsableDuration(-1f));
        Assert.IsFalse(MediaTimeline.IsUsableDuration(float.NaN));
        Assert.IsFalse(MediaTimeline.IsUsableDuration(float.PositiveInfinity));
        Assert.IsFalse(MediaTimeline.IsUsableDuration(float.NegativeInfinity));
        Assert.IsTrue(MediaTimeline.IsUsableDuration(0.5f));
        Assert.IsTrue(MediaTimeline.IsUsableDuration(48f));
        Assert.IsTrue(MediaTimeline.IsUsableDuration(1e9f));
    }

    [Test]
    public void BrokenDurationNeverEndsTheSongByDuration()
    {
        // duration 0 (曲情報なし) で即フェードしていた旧挙動を、
        // メディアがある場合は禁止する (メディア終端が正)。
        Assert.IsFalse(MediaTimeline.HasSongEnded(0f, 0f));
        Assert.IsFalse(MediaTimeline.HasSongEnded(1000f, 0f));
        Assert.IsFalse(MediaTimeline.HasSongEnded(100f, float.NaN));
        Assert.IsFalse(MediaTimeline.HasSongEnded(100f, float.PositiveInfinity));
    }

    [Test]
    public void NormalDurationStillEndsWithLeadOutTest()
    {
        // 既存の契約 (リードアウト 1s) はそのまま。
        Assert.IsFalse(MediaTimeline.HasSongEnded(46.9f, 48f));
        Assert.IsTrue(MediaTimeline.HasSongEnded(47f, 48f));
        Assert.IsTrue(MediaTimeline.HasSongEnded(120f, 48f));
    }

    [Test]
    public void FallbackSongEndKeepsUsableDurationAuthoritative()
    {
        Assert.AreEqual(47f, MediaTimeline.FallbackSongEnd(48f), 0.0001f);
        Assert.AreEqual(0f, MediaTimeline.FallbackSongEnd(0.5f), 0.0001f);
    }

    [Test]
    public void FallbackSongEndWithBrokenDurationDoesNotTrapThePlayer()
    {
        // メディアなし + duration 不正: 曲の長さの根拠がないので即終端とする
        // (旧挙動と同じ。プレイヤーを閉じ込めない)。
        Assert.AreEqual(0f, MediaTimeline.FallbackSongEnd(0f), 0.0001f);
        Assert.AreEqual(0f, MediaTimeline.FallbackSongEnd(-5f), 0.0001f);
        Assert.AreEqual(0f, MediaTimeline.FallbackSongEnd(float.NaN), 0.0001f);
    }

    [Test]
    public void EndConditionComputationSurvivesExtremeDurations()
    {
        // 呼び出し側の判定式 (MusicManager.Update と同じ形) で破綻しないこと。
        // 極端に長い duration: メディア終端 (mediaEnded) だけが終端を作る。
        bool durationEnded = MediaTimeline.HasSongEnded(60f, 1e9f);
        Assert.IsFalse(durationEnded);
        var machine = new SongEndMachine();
        Assert.IsTrue(machine.Update(new SongEndTick
        {
            EndReached = true, // mediaEnded
            JudgeTime = 60.5f,
            SettleDeadline = 60.4f,
            PendingNotes = 0,
            TotalNotes = 5
        }));

        // 極端に短い duration (0.001s): すぐ終端条件が立つが、ノーツの確定は待つ。
        Assert.IsTrue(MediaTimeline.HasSongEnded(0f, 0.001f));
        var shortMachine = new SongEndMachine();
        Assert.IsFalse(shortMachine.Update(new SongEndTick
        {
            EndReached = true,
            JudgeTime = 0f,
            SettleDeadline = 0.7f,
            PendingNotes = 5,
            TotalNotes = 5
        }));
        Assert.AreEqual(SongEndState.Finishing, shortMachine.State);
    }
}
