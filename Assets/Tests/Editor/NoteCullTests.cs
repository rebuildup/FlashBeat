using FlashBeat.Gameplay;
using NUnit.Framework;

/// <summary>
/// Issue #394: 画面に関係ないノーツの Transform 更新を省く判定。
///
/// ノーツは 1 曲で最大 603 個 (Dogbite)。以前は全ノーツが毎フレーム
/// <c>transform.position</c> を書き換えていた。判定は時刻表とキー入力で行われ、
/// Transform は読み返さないので、画面外のノーツを書かなくても判定は変わらない。
///
/// ここでは「どの範囲を書くか」の境界を固定する。
/// </summary>
public class NoteCullTests
{
    [Test]
    public void NotesFarAheadAreNotUpdated()
    {
        // まだ遠い (アプローチ時間 1.6 秒より十分手前)。
        Assert.IsFalse(Notes.ShouldUpdateTransform(10f), "まだ来ないノーツは書かない");
        Assert.IsFalse(Notes.ShouldUpdateTransform(3f));
    }

    [Test]
    public void NotesInTheApproachWindowAreUpdated()
    {
        // 画面に入る範囲 (1.6 秒前〜判定線)。
        Assert.IsTrue(Notes.ShouldUpdateTransform(1.6f));
        Assert.IsTrue(Notes.ShouldUpdateTransform(0.8f));
        Assert.IsTrue(Notes.ShouldUpdateTransform(0f), "判定線の瞬間は必ず書く");
    }

    [Test]
    public void NotesJustBehindTheJudgeLineAreStillUpdated()
    {
        // 判定直後はまだ画面下に見えている。
        Assert.IsTrue(Notes.ShouldUpdateTransform(-0.2f));
        Assert.IsTrue(Notes.ShouldUpdateTransform(-0.9f));
    }

    [Test]
    public void NotesFarBehindAreNotUpdated()
    {
        Assert.IsFalse(Notes.ShouldUpdateTransform(-10f), "画面下へ抜けたノーツは書かない");
        Assert.IsFalse(Notes.ShouldUpdateTransform(-1.5f));
    }

    [Test]
    public void TheUpdateWindowIsContinuous()
    {
        // 窓が飛び飛びだと、ノーツが一瞬止まって見える。
        // 「書く → 書かない」の境界が 1 箇所ずつであることを確かめる。
        bool previous = Notes.ShouldUpdateTransform(10f);
        int transitions = 0;
        for (float t = 10f; t >= -10f; t -= 0.05f)
        {
            bool current = Notes.ShouldUpdateTransform(t);
            if (current != previous)
            {
                transitions++;
            }

            previous = current;
        }

        Assert.AreEqual(2, transitions, "更新する窓は 1 つだけ (入る/出るの 2 回)");
    }

    [Test]
    public void TheWindowCoversTheDesignedApproachTime()
    {
        // design のアプローチは 1.6 秒 (MediaTimeline.ApproachSecondsAtSpeed10)。
        // 窓がそれより狭いと、画面に入った瞬間にノーツが飛ぶ。
        float approach = MediaTimeline.ApproachSecondsAtSpeed10;
        Assert.IsTrue(Notes.ShouldUpdateTransform(approach),
            "アプローチ時間ぶん手前まで更新する");
        Assert.IsTrue(Notes.ShouldUpdateTransform(approach + 0.5f),
            "境界の揺れ (フレーム落ち) を吸収する余裕がある");
    }
}
