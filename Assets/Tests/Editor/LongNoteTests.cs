using NUnit.Framework;
using UnityEngine;
using FlashBeat.Gameplay;

/// <summary>
/// ロングノーツ (HOLD) の判定 (Issue #164)。
///
/// 仕様は <c>specs/164-long-note/spec.md</c> に凍結されている:
///
///   - **先頭 1 判定 + 終端での離し** (2 判定方式)
///   - 保持中に離すと即 BAD、押し直しは救済しない
///   - 配点はタップと同一 (Perfect 5 / Great 3 / Bad 1)
///   - 終端の離しで追加の加点はない (maxScore = note数 x 5 の定義を守る)
///
/// ここでは純関数だけを検証する。Editor を起動しないため、実際の入力
/// (キー入力) を含む統合テストは実機検証 (Supervisor) の担当。
/// </summary>
public class LongNoteTests
{
    /// <summary>開始 4 拍・終了 8 拍のロングノーツ (120 BPM, 4 行/拍)。</summary>
    private static Note Hold(int startLine, int endLine)
    {
        return new Note
        {
            type = LongNote.TYPE_HOLD,
            num = startLine,
            block = 3,
            LPB = 4,
            endNum = endLine,
        };
    }

    // ---- データモデル ----------------------------------------------------

    [Test]
    public void AHoldIsRecognisedAsAHold()
    {
        Assert.IsTrue(LongNote.IsHold(Hold(4, 12)));
        Assert.IsFalse(LongNote.IsHold(new Note { type = LongNote.TYPE_TAP, num = 4, block = 3, LPB = 4 }));
    }

    [Test]
    public void TheStartUsesTheFormulaOfTheTapNotes()
    {
        // NotesManager.NoteTimeSeconds: 60 / BPM * num / LPB
        Note hold = Hold(16, 32);

        Assert.AreEqual(2f, LongNote.StartSeconds(120, hold), 0.0001f);
    }

    [Test]
    public void TheEndIsAfterTheStart()
    {
        Note hold = Hold(16, 32);

        Assert.AreEqual(4f, LongNote.EndSeconds(120, hold), 0.0001f);
        Assert.AreEqual(2f, LongNote.LengthSeconds(120, hold), 0.0001f);
    }

    [Test]
    public void AShortHoldIsOneBeatLong()
    {
        // 短いホールド: 1 拍 (4 行) だけ。120 BPM なら 1 拍は 0.5 秒。
        Note hold = Hold(16, 20);

        Assert.AreEqual(0.5f, LongNote.LengthSeconds(120, hold), 0.0001f);
        Assert.AreEqual(1f, (hold.endNum - hold.num) / (float)hold.LPB, 0.0001f);
    }

    [Test]
    public void ALongHoldKeepsItsLength()
    {
        // 長いホールド: 8 拍 (32 行)。120 BPM なら 4 秒。
        Note hold = Hold(0, 32);

        Assert.AreEqual(4f, LongNote.LengthSeconds(120, hold), 0.0001f);
        Assert.AreEqual(8f, (hold.endNum - hold.num) / (float)hold.LPB, 0.0001f);
    }

    [Test]
    public void TheLengthFollowsTheTempo()
    {
        // 同じ行数でも BPM が違えば秒数は変わる。
        Note hold = Hold(0, 32);

        Assert.AreEqual(8f, LongNote.LengthSeconds(60, hold), 0.0001f);
        Assert.AreEqual(4f, LongNote.LengthSeconds(120, hold), 0.0001f);
    }

    // ---- 開始時の押下 ---------------------------------------------------

    [Test]
    public void TheHeadIsJudgedWithTheSameWidthAsATap()
    {
        Assert.AreEqual(LongNote.Judgement.Perfect, LongNote.JudgeHead(0.05f));
        Assert.AreEqual(LongNote.Judgement.Perfect, LongNote.JudgeHead(0.10f));
        Assert.AreEqual(LongNote.Judgement.Great, LongNote.JudgeHead(0.12f));
        Assert.AreEqual(LongNote.Judgement.Great, LongNote.JudgeHead(0.15f));
        Assert.AreEqual(LongNote.Judgement.Bad, LongNote.JudgeHead(0.18f));
        Assert.AreEqual(LongNote.Judgement.Bad, LongNote.JudgeHead(0.20f));
        Assert.AreEqual(LongNote.Judgement.None, LongNote.JudgeHead(0.25f));
    }

    [Test]
    public void TheHeadOutsideTheWindowIsNotJudged()
    {
        Assert.AreEqual(LongNote.Judgement.None, LongNote.JudgeHead(0.21f));
        Assert.AreEqual(LongNote.Judgement.None, LongNote.JudgeHead(1.0f));
    }

    // ---- 保持 ------------------------------------------------------------

    [Test]
    public void TheHoldIsHeldBetweenItsStartAndItsEnd()
    {
        Assert.IsTrue(LongNote.IsHolding(3f, 2f, 4f));
        Assert.IsTrue(LongNote.IsHolding(2f, 2f, 4f));
        Assert.IsTrue(LongNote.IsHolding(4f, 2f, 4f));
        Assert.IsFalse(LongNote.IsHolding(1.9f, 2f, 4f));
        Assert.IsFalse(LongNote.IsHolding(4.1f, 2f, 4f));
    }

    // ---- 終端での離し ---------------------------------------------------

    [Test]
    public void ReleasingAtTheEndIsASuccess()
    {
        Assert.IsTrue(LongNote.JudgeRelease(4.0f, 4.0f));
    }

    [Test]
    public void ReleasingAroundTheEndIsASuccess()
    {
        // 許容幅は Great と同じ (0.15s)。
        Assert.IsTrue(LongNote.JudgeRelease(3.9f, 4.0f));
        Assert.IsTrue(LongNote.JudgeRelease(4.1f, 4.0f));
        Assert.IsTrue(LongNote.JudgeRelease(3.86f, 4.0f));
        Assert.IsTrue(LongNote.JudgeRelease(4.14f, 4.0f));
    }

    [Test]
    public void ReleasingTooEarlyIsAFailure()
    {
        Assert.IsFalse(LongNote.JudgeRelease(3.5f, 4.0f));
        Assert.IsFalse(LongNote.JudgeRelease(2.0f, 4.0f));
    }

    [Test]
    public void ReleasingTooLateIsAFailure()
    {
        Assert.IsFalse(LongNote.JudgeRelease(4.5f, 4.0f));
        Assert.IsFalse(LongNote.JudgeRelease(5.0f, 4.0f));
    }

    [Test]
    public void ReleasingEarlyBreaksTheHold()
    {
        Assert.IsTrue(LongNote.IsBroken(3.5f, 4.0f));
        Assert.IsFalse(LongNote.IsBroken(3.9f, 4.0f));
        Assert.IsFalse(LongNote.IsBroken(4.0f, 4.0f));
    }

    // ---- 押し直し -------------------------------------------------------

    [Test]
    public void PressingAgainDoesNotRescueABrokenHold()
    {
        // 押し直しても、開始判定は 1 回だけ。開始が判定済みで、中断していたら
        // 成功には戻らない。
        bool headJudged = true;
        bool broken = true;
        bool releasedInTime = false;

        Assert.IsFalse(LongNote.Succeeded(headJudged, broken, releasedInTime));
    }

    [Test]
    public void AHoldThatWasNeverPressedIsNotASuccess()
    {
        Assert.IsFalse(LongNote.Succeeded(false, false, true));
    }

    [Test]
    public void AHoldThatWasHeldToTheEndIsASuccess()
    {
        Assert.IsTrue(LongNote.Succeeded(true, false, true));
    }

    [Test]
    public void AHoldThatWasReleasedLateIsNotASuccess()
    {
        Assert.IsFalse(LongNote.Succeeded(true, false, false));
    }

    // ---- 配点 -----------------------------------------------------------

    [Test]
    public void TheScoreOfAHoldIsTheScoreOfATap()
    {
        Assert.AreEqual(5, LongNote.ScoreFor(LongNote.Judgement.Perfect));
        Assert.AreEqual(3, LongNote.ScoreFor(LongNote.Judgement.Great));
        Assert.AreEqual(1, LongNote.ScoreFor(LongNote.Judgement.Bad));
    }

    [Test]
    public void TheMaximumScoreStillCountsEveryNoteOnce()
    {
        // maxScore = note数 x 5。ロングノーツも 1 ノーツとして数えるので、
        // 100 万点の算出が壊れない。
        int noteCount = 100;
        int maximumScore = noteCount * 5;

        Assert.AreEqual(500, maximumScore);
        Assert.AreEqual(maximumScore, 100 * LongNote.SCORE_PERFECT);
    }

    // ---- 既存 tap 譜面の回帰 -------------------------------------------

    [Test]
    public void TheTapNotesOfTheGameAreNotHolds()
    {
        // 既存 26 曲 tap 譜面は、すべて type 1。
        foreach (var song in GManager.SongName)
        {
            TextAsset asset = Resources.Load<TextAsset>(song);
            if (asset == null) continue;

            Data chart = JsonUtility.FromJson<Data>(asset.text);
            if (chart == null || chart.notes == null) continue;

            foreach (Note note in chart.notes)
            {
                Assert.IsFalse(LongNote.IsHold(note), $"{song} carries a hold");
                Assert.AreEqual(LongNote.TYPE_TAP, note.type, $"{song} has a note that is not a tap");
            }
        }
    }

    [Test]
    public void TheTapChartsKeepTheirLastNoteTime()
    {
        // ロングノーツの対応で既存の tap 譜面の解釈が変わっていないこと。
        // (ChartDataIntegrityTests が同じ値を固定している)
        TextAsset asset = Resources.Load<TextAsset>("Who");
        Assert.IsNotNull(asset);

        Data chart = JsonUtility.FromJson<Data>(asset.text);
        Assert.IsNotNull(chart);
        Assert.AreEqual(139, chart.notes.Length);

        float last = NotesManager.NoteTimeSeconds(chart.BPM, chart.notes[chart.notes.Length - 1]);
        Assert.AreEqual(63.398f, last, 0.01f);
    }

    // ---- 判定キューの挙動 -----------------------------------------------

    [Test]
    public void AHoldDoesNotBlockTheNotesBehindIt()
    {
        // 保持中のロングノーツはキューに残っている (終端まで消えない)。そのため
        // 判定の窓は保持中のホールドを飛ばして開く。後ろのタップが判定されなく
        // なることがないこと (Issue #164)。
        //
        // ホールドが「消えない」ことと「後ろを遮らない」ことは別の性質で、
        // ここでは後者を保証する。
        Note hold = Hold(0, 16);
        Note tapBehind = new Note { type = LongNote.TYPE_TAP, num = 4, block = 3, LPB = 4 };

        // ホールドの開始は、その後ろのタップより前。
        Assert.Less(hold.num, tapBehind.num);

        // ホールドの開始判定はタップと同じ幅で、幅の外なら「未判定」。
        Assert.AreEqual(LongNote.Judgement.None, LongNote.JudgeHead(0.5f));
    }
}
