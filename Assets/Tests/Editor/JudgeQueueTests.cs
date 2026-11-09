using NUnit.Framework;
using UnityEngine;
using FlashBeat.Gameplay;

/// <summary>
/// 判定キューとスコア集計の EditMode テスト (Issue #164 / #182)。
///
/// <see cref="JudgeQueue"/> と <see cref="JudgeScoreboard"/> は純粋クラス
/// (Input / Time 非依存) なので、時刻を明示的に与えて判定の全経路を
/// 検証できる。実際のキー入力を含む統合は実機検証 (Supervisor) の担当。
/// </summary>
public class JudgeQueueTests
{
    private const int Lanes = 8;

    private static JudgeQueue.NoteEntry Tap(int lane, float time, int sourceIndex)
    {
        return new JudgeQueue.NoteEntry
        {
            Time = time,
            EndTime = 0f,
            Type = LongNote.TYPE_TAP,
            Lane = lane,
            SourceIndex = sourceIndex,
        };
    }

    private static JudgeQueue.NoteEntry Hold(int lane, float time, float endTime, int sourceIndex)
    {
        return new JudgeQueue.NoteEntry
        {
            Time = time,
            EndTime = endTime,
            Type = LongNote.TYPE_HOLD,
            Lane = lane,
            SourceIndex = sourceIndex,
        };
    }

    // ------------------------------------------------------------------
    // Issue #182: レーン別・時刻順キュー
    // ------------------------------------------------------------------

    [Test]
    public void SimultaneousPress_TwoLanes_BothJudged()
    {
        JudgeQueue queue = new JudgeQueue(Lanes);
        queue.Add(Tap(0, 5.0f, 0));
        queue.Add(Tap(3, 5.0f, 1));

        // 同じ時刻に 2 レーンを叩く: 別レーンなので別入力として両方成立する。
        Assert.IsTrue(queue.TryPress(0, 5.0f, LongNote.BAD_WINDOW, out JudgeQueue.NoteEntry hit0, out float lag0));
        Assert.AreEqual(0, hit0.SourceIndex);
        Assert.AreEqual(0f, lag0, 0.0001f);

        Assert.IsTrue(queue.TryPress(3, 5.0f, LongNote.BAD_WINDOW, out JudgeQueue.NoteEntry hit3, out float lag3));
        Assert.AreEqual(1, hit3.SourceIndex);
        Assert.AreEqual(0f, lag3, 0.0001f);
    }

    [Test]
    public void UnprocessedNoteOnOtherLane_DoesNotBlockHit()
    {
        JudgeQueue queue = new JudgeQueue(Lanes);
        // レーン 2 のノーツはまだ押されていない (未処理のまま残る)。
        queue.Add(Tap(2, 5.0f, 0));
        // レーン 5 のノーツは少し後にある。
        queue.Add(Tap(5, 5.1f, 1));

        // 旧実装 (全体の先頭ノーツだけを見る) ではレーン 5 の入力が
        // レーン 2 の未処理ノーツに阻害されていた。レーン別キューでは成立する。
        Assert.IsTrue(queue.TryPress(5, 5.1f, LongNote.BAD_WINDOW, out JudgeQueue.NoteEntry hit, out _));
        Assert.AreEqual(1, hit.SourceIndex);
    }

    [Test]
    public void EditOrderDiffersFromTimeOrder_NoNoteIsSkipped()
    {
        JudgeQueue queue = new JudgeQueue(Lanes);
        // 譜面ファイル (Web 編集順) では時刻 2.0 が先、時刻 1.0 が後。
        queue.Add(Tap(4, 2.0f, 0));
        queue.Add(Tap(4, 1.0f, 1));

        // 時刻順に判定される: 先に来た入力 (1.0) は 1.0 のノーツに当たり、
        // 2.0 のノーツが先に判定されて 1.0 が飛ぶことはない。
        Assert.IsTrue(queue.TryPress(4, 1.0f, LongNote.BAD_WINDOW, out JudgeQueue.NoteEntry first, out _));
        Assert.AreEqual(1, first.SourceIndex);
        queue.ConsumeTap(first);

        Assert.IsTrue(queue.TryPress(4, 2.0f, LongNote.BAD_WINDOW, out JudgeQueue.NoteEntry second, out _));
        Assert.AreEqual(0, second.SourceIndex);
    }

    [Test]
    public void Press_NearestNoteInTheWindowIsChosen()
    {
        JudgeQueue queue = new JudgeQueue(Lanes);
        queue.Add(Tap(1, 5.0f, 0));
        queue.Add(Tap(1, 5.12f, 1));

        // 5.1 で押したら、窓内で最も近い 5.12 のノーツに当たる。
        Assert.IsTrue(queue.TryPress(1, 5.1f, LongNote.BAD_WINDOW, out JudgeQueue.NoteEntry hit, out float lag));
        Assert.AreEqual(1, hit.SourceIndex);
        Assert.AreEqual(0.02f, lag, 0.0001f);
    }

    [Test]
    public void Press_OutsideWindow_NothingIsJudged()
    {
        JudgeQueue queue = new JudgeQueue(Lanes);
        queue.Add(Tap(1, 5.0f, 0));

        Assert.IsFalse(queue.TryPress(1, 5.21f, LongNote.BAD_WINDOW, out _, out _));
    }

    [Test]
    public void Miss_IsPerLane_OtherLanesUnaffected()
    {
        JudgeQueue queue = new JudgeQueue(Lanes);
        queue.Add(Tap(2, 5.0f, 0));
        queue.Add(Tap(6, 5.0f, 1));

        // レーン 6 は時刻内に叩かれて消える。
        Assert.IsTrue(queue.TryPress(6, 5.05f, LongNote.BAD_WINDOW, out JudgeQueue.NoteEntry hit, out _));
        queue.ConsumeTap(hit);

        // レーン 2 のノーツだけ時刻切れ。
        Assert.IsTrue(queue.TryPeekMiss(2, 5.21f, LongNote.BAD_WINDOW, out JudgeQueue.NoteEntry missed));
        Assert.AreEqual(0, missed.SourceIndex);
        queue.Remove(missed);

        Assert.IsFalse(queue.TryPeekMiss(6, 5.21f, LongNote.BAD_WINDOW, out _));
    }

    // ------------------------------------------------------------------
    // Issue #164: HOLD の状態機械 (1 ノーツ = 1 判定)
    // ------------------------------------------------------------------

    [Test]
    public void Hold_BrokenRelease_DoesNotDoubleScore()
    {
        // 先頭の押下で Perfect (5 点) → 途中で離す (中断)。
        JudgeScoreboard board = new JudgeScoreboard();
        board.ApplyHead(LongNote.Judgement.Perfect);
        Assert.AreEqual(1, board.Combo);

        // 中断: 先頭判定が BAD に差し替わるだけ。追加加点しない。
        board.DowngradeHoldToBad(LongNote.Judgement.Perfect);

        Assert.AreEqual(0, board.Perfect);
        Assert.AreEqual(0, board.Great);
        Assert.AreEqual(1, board.Bad);
        Assert.AreEqual(1, board.Combo); // 中断でコンボは維持 (spec 164 R4)
        Assert.AreEqual(LongNote.SCORE_BAD, board.RatioScore, 0.0001f);
    }

    [Test]
    public void Hold_RepressWhileHolding_DoesNotScoreAgain()
    {
        JudgeQueue queue = new JudgeQueue(Lanes);
        queue.Add(Hold(3, 5.0f, 7.0f, 0));

        // 開始の押下。
        Assert.IsTrue(queue.TryPress(3, 5.0f, LongNote.BAD_WINDOW, out JudgeQueue.NoteEntry head, out _));
        queue.BeginHold(head);

        // 保持中の押し直し: 同じノーツが再度判定されることはない。
        Assert.IsFalse(queue.TryPress(3, 5.5f, LongNote.BAD_WINDOW, out _, out _));
        Assert.IsFalse(queue.TryPress(3, 5.0f, LongNote.BAD_WINDOW, out _, out _));
    }

    [Test]
    public void Hold_RepressAfterBreak_DoesNotReviveTheNote()
    {
        JudgeQueue queue = new JudgeQueue(Lanes);
        queue.Add(Hold(3, 5.0f, 7.0f, 0));

        Assert.IsTrue(queue.TryPress(3, 5.0f, LongNote.BAD_WINDOW, out JudgeQueue.NoteEntry head, out _));
        queue.BeginHold(head);

        // 途中で離す (中断)。
        Assert.AreEqual(JudgeQueue.HoldResult.Broken,
            queue.UpdateHold(3, 5.8f, keyDown: false, LongNote.GREAT_WINDOW, out _));

        // 中断後の押し直しも救済しない: ノーツはもうキューにない。
        Assert.AreEqual(0, queue.TotalPendingCount);
        Assert.IsFalse(queue.TryPress(3, 6.0f, LongNote.BAD_WINDOW, out _, out _));
    }

    [Test]
    public void Hold_ReleaseAtEnd_SucceedsWithoutExtraScore()
    {
        JudgeQueue queue = new JudgeQueue(Lanes);
        queue.Add(Hold(3, 5.0f, 7.0f, 0));

        Assert.IsTrue(queue.TryPress(3, 5.0f, LongNote.BAD_WINDOW, out JudgeQueue.NoteEntry head, out _));
        queue.BeginHold(head);

        // 終端 (±Great 幅) で離す: 成功。加点は先頭の押下だけ。
        Assert.AreEqual(JudgeQueue.HoldResult.Released,
            queue.UpdateHold(3, 7.1f, keyDown: false, LongNote.GREAT_WINDOW, out JudgeQueue.NoteEntry ended));
        Assert.AreEqual(0, ended.SourceIndex);

        JudgeScoreboard board = new JudgeScoreboard();
        board.ApplyHead(LongNote.Judgement.Perfect);
        Assert.AreEqual(LongNote.SCORE_PERFECT, board.RatioScore, 0.0001f);
    }

    [Test]
    public void Hold_KeptPastEnd_CountsAsSuccess()
    {
        JudgeQueue queue = new JudgeQueue(Lanes);
        queue.Add(Hold(0, 5.0f, 7.0f, 0));

        Assert.IsTrue(queue.TryPress(0, 5.0f, LongNote.BAD_WINDOW, out JudgeQueue.NoteEntry head, out _));
        queue.BeginHold(head);

        // 押し続けたまま終端 + 許容幅をすぎた: 成功扱いで確定する。
        Assert.AreEqual(JudgeQueue.HoldResult.Expired,
            queue.UpdateHold(0, 7.2f, keyDown: true, LongNote.GREAT_WINDOW, out _));
        Assert.IsFalse(queue.IsHolding(0));
    }

    [Test]
    public void Hold_WhileHolding_IsNeverMissed()
    {
        JudgeQueue queue = new JudgeQueue(Lanes);
        queue.Add(Hold(3, 5.0f, 7.0f, 0));

        Assert.IsTrue(queue.TryPress(3, 5.0f, LongNote.BAD_WINDOW, out JudgeQueue.NoteEntry head, out _));
        queue.BeginHold(head);

        // 保持中のロングノーツは Miss 判定の対象にならない。
        Assert.IsFalse(queue.TryPeekMiss(3, 6.5f, LongNote.BAD_WINDOW, out _));
    }

    [Test]
    public void Hold_Unpressed_BecomesMissAfterWindow()
    {
        JudgeQueue queue = new JudgeQueue(Lanes);
        queue.Add(Hold(3, 5.0f, 7.0f, 0));

        // 押されなかったロングノーツは開始 + Bad 幅をすぎたら Miss。
        Assert.IsFalse(queue.TryPeekMiss(3, 5.2f, LongNote.BAD_WINDOW, out _));
        Assert.IsTrue(queue.TryPeekMiss(3, 5.21f, LongNote.BAD_WINDOW, out JudgeQueue.NoteEntry missed));
        Assert.AreEqual(LongNote.TYPE_HOLD, missed.Type);
    }

    // ------------------------------------------------------------------
    // 判定キューとスコアの組み合わせ (Judge の呼び出し順と同じ経路)
    // ------------------------------------------------------------------

    [Test]
    public void FullPath_HoldHeadThenBroken_YieldsExactlyOneJudgement()
    {
        // Judge.PressLane → queue.BeginHold → UpdateHold(Broken) →
        // board.DowngradeHoldToBad の順に呼ぶ (実装と同じ経路)。
        JudgeQueue queue = new JudgeQueue(Lanes);
        JudgeScoreboard board = new JudgeScoreboard();
        queue.Add(Hold(3, 5.0f, 7.0f, 0));

        Assert.IsTrue(queue.TryPress(3, 5.05f, LongNote.BAD_WINDOW, out JudgeQueue.NoteEntry head, out float lag));
        LongNote.Judgement headJudgement = LongNote.JudgeHead(lag);
        Assert.AreEqual(LongNote.Judgement.Perfect, headJudgement);
        board.ApplyHead(headJudgement);
        queue.BeginHold(head);

        Assert.AreEqual(JudgeQueue.HoldResult.Broken,
            queue.UpdateHold(3, 5.5f, keyDown: false, LongNote.GREAT_WINDOW, out _));
        board.DowngradeHoldToBad(headJudgement);

        // 1 ノーツ = 1 判定: 判定回数の合計は 1、配点は BAD の 1 点だけ。
        Assert.AreEqual(1, board.Perfect + board.Great + board.Bad + board.Miss);
        Assert.AreEqual(LongNote.SCORE_BAD, board.RatioScore, 0.0001f);
        Assert.IsFalse(queue.IsHolding(3));
    }

    [Test]
    public void FullPath_SimultaneousTapAndHold_BothScored()
    {
        JudgeQueue queue = new JudgeQueue(Lanes);
        JudgeScoreboard board = new JudgeScoreboard();
        queue.Add(Tap(0, 5.0f, 0));
        queue.Add(Hold(5, 5.0f, 8.0f, 1));

        // 同時押し: タップと HOLD の開始が両方とも 1 回ずつ判定される。
        Assert.IsTrue(queue.TryPress(0, 5.0f, LongNote.BAD_WINDOW, out JudgeQueue.NoteEntry tap, out float tapLag));
        board.ApplyHead(LongNote.JudgeHead(tapLag));
        queue.ConsumeTap(tap);

        Assert.IsTrue(queue.TryPress(5, 5.0f, LongNote.BAD_WINDOW, out JudgeQueue.NoteEntry hold, out float holdLag));
        board.ApplyHead(LongNote.JudgeHead(holdLag));
        queue.BeginHold(hold);

        Assert.AreEqual(2, board.Combo);
        Assert.AreEqual(2 * LongNote.SCORE_PERFECT, board.RatioScore, 0.0001f);
    }

    // ------------------------------------------------------------------
    // HOLD の判定ルールは LongNote が単一の出所であること
    // (テストと実挙動の分離を解消する回帰テスト)
    // ------------------------------------------------------------------

    /// <summary>
    /// 実行時経路 (JudgeQueue.UpdateHold) の結果が、LongNote の判定関数と
    /// 一致することを確認する。両者が二重実装になると、LongNote 側のテストが
    /// 緑でも実挙動は壊れている状態になる (既知の不具合)。
    /// </summary>
    [Test]
    public void Hold_RuntimeResult_MatchesLongNoteRules()
    {
        float[] releaseTimes = { 5.5f, 6.7f, 6.86f, 7.0f, 7.1f, 7.14f, 7.5f };
        foreach (float releaseTime in releaseTimes)
        {
            JudgeQueue queue = new JudgeQueue(Lanes);
            queue.Add(Hold(3, 5.0f, 7.0f, 0));
            Assert.IsTrue(queue.TryPress(3, 5.0f, LongNote.BAD_WINDOW, out JudgeQueue.NoteEntry head, out _));
            queue.BeginHold(head);

            JudgeQueue.HoldResult actual =
                queue.UpdateHold(3, releaseTime, keyDown: false, LongNote.GREAT_WINDOW, out _);

            // 期待値は LongNote の判定そのものを単一の出所として作る。
            bool broken = LongNote.IsBroken(releaseTime, 7.0f, LongNote.GREAT_WINDOW);
            JudgeQueue.HoldResult expected = broken
                ? JudgeQueue.HoldResult.Broken
                : JudgeQueue.HoldResult.Released;

            Assert.AreEqual(expected, actual, $"releaseTime={releaseTime}");
            Assert.AreEqual(
                LongNote.JudgeRelease(releaseTime, 7.0f, LongNote.GREAT_WINDOW),
                actual == JudgeQueue.HoldResult.Released,
                $"releaseTime={releaseTime}");
        }
    }

    [Test]
    public void Hold_ExpiryBoundary_MatchesLongNoteRule()
    {
        float[] mediaTimes = { 7.0f, 7.1f, 7.14f, 7.2f, 7.5f };
        foreach (float mediaTime in mediaTimes)
        {
            JudgeQueue queue = new JudgeQueue(Lanes);
            queue.Add(Hold(0, 5.0f, 7.0f, 0));
            Assert.IsTrue(queue.TryPress(0, 5.0f, LongNote.BAD_WINDOW, out JudgeQueue.NoteEntry head, out _));
            queue.BeginHold(head);

            JudgeQueue.HoldResult actual =
                queue.UpdateHold(0, mediaTime, keyDown: true, LongNote.GREAT_WINDOW, out _);

            bool expired = LongNote.IsHoldExpired(mediaTime, 7.0f, LongNote.GREAT_WINDOW);
            JudgeQueue.HoldResult expected = expired
                ? JudgeQueue.HoldResult.Expired
                : JudgeQueue.HoldResult.None;

            Assert.AreEqual(expected, actual, $"mediaTime={mediaTime}");
        }
    }

    [Test]
    public void Hold_NeverPressed_BecomesMissAndNotHold()
    {
        JudgeQueue queue = new JudgeQueue(Lanes);
        queue.Add(Hold(3, 5.0f, 7.0f, 0));

        // 押し下げ失敗: 保持は始まらない。時間切れで Miss になる。
        Assert.IsFalse(queue.TryPress(3, 5.0f, LongNote.BAD_WINDOW, out _, out _));
        Assert.IsTrue(queue.TryPeekMiss(3, 5.5f, LongNote.BAD_WINDOW, out JudgeQueue.NoteEntry missed));
        Assert.AreEqual(0, missed.SourceIndex);
    }
}
