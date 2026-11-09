using NUnit.Framework;
using UnityEngine;
using FlashBeat.Gameplay;

/// <summary>
/// Issue #226 の受入条件「動画長 &lt; duration」の回帰テスト。
///
/// カタログの duration が実メディアより長いと、旧実装では
///   - duration だけを見て終わる条件が成立せず、
///   - メディア終端がそのまま使われず、
/// Result へ自動遷移しない (= プレイヤーが中途半端な画面で待たされる) 状態になっていた。
///
/// ここで固定する契約:
///
///   1. duration だけ (60 秒 / 120 秒) では終端条件は成立しない。
///   2. メディアの終端 (loop point) は **本番の経路**で終端条件として記録される。
///   3. メディア終端後、末尾ノーツが確定すれば曲は終わる (壁時計を待たない)。
///   4. メディア終端時に未再生ノーツが残っている場合、**強制完了** (印付き) になり、
///      「全部成功した」とは言わない (判定合計が noteCount に届かない)。
/// </summary>
public class SongEndMediaShorterThanDurationTests
{
    private GameObject rigRoot;

    [SetUp]
    public void SetUp()
    {
        SongEndAcceptanceTests.ResetSessionState();
    }

    [TearDown]
    public void TearDown()
    {
        if (rigRoot != null)
        {
            Object.DestroyImmediate(rigRoot);
            rigRoot = null;
        }
        SongEndAcceptanceTests.ClearSessionState();
    }

    /// <summary>カタログの duration (実メディアより長い)。</summary>
    private const float CatalogDuration = 120f;

    /// <summary>実メディアの長さ (終端はここで来る)。</summary>
    private const float MediaLength = 60f;

    [Test]
    public void TheDurationAloneDoesNotEndTheSong()
    {
        // 実メディア (60 秒) が終わった時点では、カタログの duration (120 秒) は
        // まだ来ていない → duration だけでは終端にできない。
        Assert.IsFalse(MediaTimeline.HasSongEnded(MediaLength, CatalogDuration),
            "duration だけで終端が成立している (動画長 < duration が救済されない)");
        Assert.IsFalse(MediaTimeline.HasSongEnded(CatalogDuration - 2f, CatalogDuration));
    }

    [Test]
    public void TheMediaEndIsRecorded_ByTheProductionPath()
    {
        GameObject root = new GameObject("MediaEndProbe");
        root.SetActive(false);
        rigRoot = root;

        MusicManager music = root.AddComponent<MusicManager>();

        // 再生中 (playbackStarted) にして、loop point のコールバックを呼ぶ。
        // songVideo は null のまま (Compare: source != songVideo → false なので通る)。
        SongEndAcceptanceRig.SetPrivateField(music, "playbackStarted", true);

        var loopMethod = typeof(MusicManager).GetMethod("OnSongVideoLoopReached",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.IsNotNull(loopMethod, "MusicManager.OnSongVideoLoopReached が見つからない");

        loopMethod.Invoke(music, new object[] { null });

        bool mediaEnded = SongEndAcceptanceRig.GetPrivateField<bool>(music, "mediaEnded");
        Assert.IsTrue(mediaEnded, "メディア終端が記録されていない (動画長 < duration で Result へ進めない)");
    }

    [Test]
    public void TheSongCompletesAtTheMediaEnd_WithoutWaitingForTheCatalogDuration()
    {
        // メディア終端 (60 秒) が終端条件として立ったとき、ノーツが確定していれば
        // 曲が終わる。カタログの duration (120 秒) を待つ必要はない。
        var machine = new SongEndMachine();

        bool done = machine.Update(new SongEndTick
        {
            EndReached = true,          // メディア終端
            JudgeTime = MediaLength,
            SettleDeadline = MediaLength + 0.2f,
            PendingNotes = 0,
            TotalNotes = 4
        });

        Assert.IsTrue(done, "メディア終端で曲が終わらない");
        Assert.AreEqual(SongEndState.Completed, machine.State);
        Assert.IsFalse(machine.ForcedByDeadline, "強制完了ではない");
    }

    [Test]
    public void TheSongWaitsForTheTailNotesAfterTheMediaEnd()
    {
        // メディア終端で末尾ノーツが未判定なら、判定猶予が終わるまで終わらない。
        var machine = new SongEndMachine();

        bool done = machine.Update(new SongEndTick
        {
            EndReached = true,
            JudgeTime = MediaLength,
            SettleDeadline = MediaLength + 0.2f,
            PendingNotes = 1,
            TotalNotes = 4
        });

        Assert.IsFalse(done, "未判定ノーツが残っているのに曲が終わった");
        Assert.AreEqual(SongEndState.Finishing, machine.State);

        // 期限を過ぎても未判定なら強制完了 (印付き = 成功ではない)。
        done = machine.Update(new SongEndTick
        {
            EndReached = true,
            JudgeTime = MediaLength + 0.2f + SongEndMachine.SettleGraceSeconds + 0.01f,
            SettleDeadline = MediaLength + 0.2f,
            PendingNotes = 1,
            TotalNotes = 4
        });

        Assert.IsTrue(done, "期限切れでも曲が終わらない");
        Assert.AreEqual(SongEndState.Completed, machine.State);
        Assert.IsTrue(machine.ForcedByDeadline, "未再生ノーツがあるのに強制完了の印がない");
    }

    [Test]
    public void AMediaEndWithUnplayedNotes_DoesNotLookLikeASuccessfulCompletion()
    {
        // メディア終端時にノーツが 1 つも判定されていない場合:
        //   - 強制完了の印 (ForcedByDeadline) が付く
        //   - 判定合計は noteCount に届かない (ランキング送信は API が拒否する)
        var machine = new SongEndMachine();
        machine.Update(new SongEndTick
        {
            EndReached = true,
            JudgeTime = MediaLength,
            SettleDeadline = MediaLength + 0.2f,
            PendingNotes = 3,
            TotalNotes = 3
        });
        machine.Update(new SongEndTick
        {
            EndReached = true,
            JudgeTime = MediaLength + 0.2f + SongEndMachine.SettleGraceSeconds + 0.01f,
            SettleDeadline = MediaLength + 0.2f,
            PendingNotes = 3,
            TotalNotes = 3
        });

        Assert.IsTrue(machine.ForcedByDeadline);
        Assert.AreEqual(0, SongEndAcceptanceTests.JudgedCount(),
            "判定されていないはずのノーツが判定数に混ざっている");
        Assert.Less(SongEndAcceptanceTests.JudgedCount(), 3,
            "未再生ノーツがあるのに判定合計が noteCount に達している");
    }
}
