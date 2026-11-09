using NUnit.Framework;
using UnityEngine;
using FlashBeat.Gameplay;

/// <summary>
/// Issue #226 の受入条件「通常終端」の回帰テスト。
///
/// 特殊ケース (duration 端点 / 動画長 &lt; duration / フレーム落ち) ではなく、
/// **ふつうの終わり方** が壊れていないことを確認する:
///
///   1. ノーツが全部判定されて新媒体 / duration の終端条件が成立したら、
///      期限切れ強制完了 (ForcedByDeadline) ではなく **普通に** 終わる。
///   2. 判定合計が noteCount に一致する (結果のスナップショットが完全)。
///   3. 終端条件が成立する前 (曲が残っている間) は終わらない。
/// </summary>
public class SongEndOrdinaryEndTests
{
    private GameObject rigRoot;
    private SongEndAcceptanceRig.Rig rig;

    [SetUp]
    public void SetUp()
    {
        SongEndAcceptanceTests.ResetSessionState();
    }

    [TearDown]
    public void TearDown()
    {
        if (rig != null)
        {
            SongEndAcceptanceRig.DestroyNotes(rig);
            rig = null;
        }
        if (rigRoot != null)
        {
            Object.DestroyImmediate(rigRoot);
            rigRoot = null;
        }
        SongEndAcceptanceTests.ClearSessionState();
    }

    /// <summary>ふつうの譜面 (Tap 2 + HOLD 1)。</summary>
    private const string ChartJson =
        "{\"name\":\"通常終端\",\"maxBlock\":8,\"BPM\":120,\"offset\":0,\"notes\":[" +
        "{\"type\":1,\"num\":4,\"block\":0,\"LPB\":4}," +              // 0.5s  lane 0
        "{\"type\":1,\"num\":8,\"block\":1,\"LPB\":4}," +              // 1.0s  lane 1
        "{\"type\":2,\"num\":12,\"endNum\":24,\"block\":2,\"LPB\":4}" +  // 1.5s -> 3.0s lane 2
        "]}";

    private const int NoteCount = 3;

    [Test]
    public void TheOrdinaryEnd_CompletesWithEveryNoteJudged()
    {
        rig = SongEndAcceptanceRig.BuildRig(ChartJson, NoteCount);
        rigRoot = rig.Root;
        SongEndAcceptanceRig.Player player = new SongEndAcceptanceRig.Player(rig);

        foreach (int index in rig.NoteIndices)
        {
            player.SchedulePress(rig.NoteTime[index], rig.NoteLane[index]);
            if (rig.NoteType[index] == LongNote.TYPE_HOLD)
            {
                player.ScheduleRelease(rig.NoteEndTime[index], rig.NoteLane[index]);
            }
        }
        player.Run(SongEndAcceptanceRig.Frames(0f, 4f, 0.05f));

        Assert.AreEqual(NoteCount, SongEndAcceptanceTests.JudgedCount(),
            "判定合計が noteCount に一致しない");
        Assert.AreEqual(NoteCount, GManager.settledNoteCount, "確定ノーツ数が noteCount に一致しない");
        Assert.AreEqual(NoteCount, GManager.perfect, "全ノーツ Perfect になっていない");
        Assert.AreEqual(1000000, GManager.score, "全 Perfect のスコアが満点になっていない");

        // 終端条件 (メディア終端) が成立した_FRAME で曲が終わる。
        float deadline = SongEndAcceptanceRig.ProductionSettleDeadline(rig.Notes);
        var machine = new SongEndMachine();
        bool done = machine.Update(new SongEndTick
        {
            EndReached = true,
            JudgeTime = 3.5f,
            SettleDeadline = deadline,
            PendingNotes = NoteCount - GManager.settledNoteCount,
            TotalNotes = NoteCount
        });

        Assert.IsTrue(done, "通常終端で曲が終わらない");
        Assert.AreEqual(SongEndState.Completed, machine.State);
        Assert.IsFalse(machine.ForcedByDeadline, "通常終端が期限切れ強制完了になっている");
    }

    [Test]
    public void TheOrdinaryEnd_WaitsForTheMediaEnd()
    {
        rig = SongEndAcceptanceRig.BuildRig(ChartJson, NoteCount);
        rigRoot = rig.Root;
        SongEndAcceptanceRig.Player player = new SongEndAcceptanceRig.Player(rig);

        foreach (int index in rig.NoteIndices)
        {
            player.SchedulePress(rig.NoteTime[index], rig.NoteLane[index]);
            if (rig.NoteType[index] == LongNote.TYPE_HOLD)
            {
                player.ScheduleRelease(rig.NoteEndTime[index], rig.NoteLane[index]);
            }
        }
        player.Run(SongEndAcceptanceRig.Frames(0f, 4f, 0.05f));

        // 全ノーツが確定していても、終端条件が成立するまでは終わらない。
        var machine = new SongEndMachine();
        bool done = machine.Update(new SongEndTick
        {
            EndReached = false,
            JudgeTime = 3.5f,
            SettleDeadline = 3.2f,
            PendingNotes = 0,
            TotalNotes = NoteCount
        });

        Assert.IsFalse(done, "曲が残っているのに終わった");
        Assert.AreEqual(SongEndState.Playing, machine.State);
    }
}
