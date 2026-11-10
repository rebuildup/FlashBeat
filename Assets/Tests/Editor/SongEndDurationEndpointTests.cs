using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using FlashBeat.Gameplay;

/// <summary>
/// Issue #226 の受入条件「duration 端点の Tap / HOLD」の回帰テスト。
///
/// 旧実装は曲終了をカタログの duration だけで決めていたため、
/// **duration ちょうどに終わるノーツ** (Tap と HOLD の終端) が
/// 判定される前に Result へ遷移すると、末尾ノーツが未判定のまま
/// 確定し、判定合計が noteCount に届かなかった。
///
/// ここで固定する契約:
///
///   1. 曲終了の期限 (<c>SettleDeadline</c>) は **本番の計算**
///      (<c>MusicManager.ComputeSettleDeadline</c>) で、duration 端点の
///      Tap なら <c>duration + BAD_WINDOW</c>、HOLD の終端なら
///      <c>duration + GREAT_WINDOW</c> になる (判定猶予が期限に含まれる)。
///   2. duration 端点の Tap は **判定される** (未判定のまま落ちない)。
///   3. duration 端点で始まる HOLD の先頭も判定される。
///   4. duration 端点で終端する HOLD は成功扱い (中断にされない) で、
///      判定合計は noteCount のまま。
///   5. 終端条件が成立してからでも、未判定ノーツが残っている間は
///      Completed にならない (判定猶予より前に Result へ進まない)。
///
/// 判定は <see cref="Judge.JudgeStep"/> を直接呼んで行う
/// (テスト側で判定を再現しない = false green を避ける)。
/// </summary>
public class SongEndDurationEndpointTests
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
        DestroyNotes();
        if (rigRoot != null)
        {
            Object.DestroyImmediate(rigRoot);
            rigRoot = null;
        }
        SongEndAcceptanceTests.ClearSessionState();
    }

    /// <summary>BPM 120 / LPB 4 なので 1 行 = 0.125 秒。</summary>
    private const int Bpm = 120;

    /// <summary>duration 端点 = 10.0 秒 (num 80 / LPB 4 = 0.125 x 80)。</summary>
    private const float Duration = 10f;

    /// <summary>末尾の Tap だけが 10.0 秒の譜面。</summary>
    private const string TapAtEndChart =
        "{\"name\":\"duration端点Tap\",\"maxBlock\":8,\"BPM\":120,\"offset\":0,\"notes\":[" +
        "{\"type\":1,\"num\":8,\"block\":0,\"LPB\":4}," +   // 1.0s  lane 0
        "{\"type\":1,\"num\":80,\"block\":1,\"LPB\":4}" +   // 10.0s lane 1 (= duration 端点)
        "]}";

    /// <summary>HOLD の **終端** が 10.0 秒の譜面 (開始は 2.0 秒)。</summary>
    private const string HoldEndingAtEndChart =
        "{\"name\":\"duration端点HOLD終端\",\"maxBlock\":8,\"BPM\":120,\"offset\":0,\"notes\":[" +
        "{\"type\":1,\"num\":8,\"block\":0,\"LPB\":4}," +            // 1.0s  lane 0
        "{\"type\":2,\"num\":16,\"endNum\":80,\"block\":2,\"LPB\":4}" + // 2.0s -> 10.0s lane 2
        "]}";

    /// <summary>HOLD の **開始** が 10.0 秒の譜面 (終端は 11.0 秒)。</summary>
    private const string HoldStartingAtEndChart =
        "{\"name\":\"duration端点HOLD開始\",\"maxBlock\":8,\"BPM\":120,\"offset\":0,\"notes\":[" +
        "{\"type\":1,\"num\":8,\"block\":0,\"LPB\":4}," +              // 1.0s  lane 0
        "{\"type\":2,\"num\":80,\"endNum\":88,\"block\":2,\"LPB\":4}" +  // 10.0s -> 11.0s lane 2
        "]}";

    // ------------------------------------------------------------------
    // 1) duration 端点の Tap
    // ------------------------------------------------------------------

    /// <summary>
    /// duration 端点 (10.0 秒) の Tap は判定され、終端条件が成立してから
    /// 判定されるまで曲が終わらない。
    /// </summary>
    [Test]
    public void TapAtDurationEndpoint_IsJudged_AndTheSongWaitsForIt()
    {
        rig = SongEndAcceptanceRig.BuildRig(TapAtEndChart, 2);
        rigRoot = rig.Root;
        SongEndAcceptanceRig.Player player = new SongEndAcceptanceRig.Player(rig);

        // 先頭の Tap (1.0 秒) を押さえる。末尾の Tap (10.0 秒) の入力は
        // 予約するが、9.0 秒までしか回さないので **まだ適用されない**
        // (未判定のまま曲が終わるのが旧バグ)。
        player.SchedulePress(rig.NoteTime[0], rig.NoteLane[0]);
        player.SchedulePress(rig.NoteTime[1], rig.NoteLane[1]);

        // 1. 終端条件は duration - 1 秒 (リードアウト) で成立する。
        Assert.IsTrue(MediaTimeline.HasSongEnded(Duration - 1f, Duration),
            "duration - 1 秒で終端条件が成立していない");

        // 2. 9.0 秒まで進める (末尾の Tap はまだ判定時刻が来ていない)。
        player.Run(SongEndAcceptanceRig.Frames(0f, Duration - 1f, 0.05f));

        Assert.AreEqual(1, GManager.settledNoteCount, "先頭の Tap が確定していない");
        Assert.AreEqual(1, SongEndAcceptanceTests.JudgedCount());
        Assert.AreEqual(1, GManager.perfect);

        // 3. 本番の判定期限は duration 端点の Tap の Miss 窓を含む。
        float deadline = SongEndAcceptanceRig.ProductionSettleDeadline(rig.Notes);
        Assert.AreEqual(Duration + LongNote.BAD_WINDOW, deadline, 0.001f,
            "判定期限が duration 端点 + BAD_WINDOW になっていない");

        // 4. 終端条件が成立していても、未判定ノーツが残る限り Completed にならない。
        var machine = new SongEndMachine();
        bool done = machine.Update(new SongEndTick
        {
            EndReached = true,
            JudgeTime = Duration - 1f,
            SettleDeadline = deadline,
            PendingNotes = 2 - GManager.settledNoteCount,
            TotalNotes = 2
        });
        Assert.IsFalse(done, "末尾の Tap が未判定なのに曲が終わった");
        Assert.AreEqual(SongEndState.Finishing, machine.State);

        // 5. 末尾の Tap が判定される直前 (10.1 秒、Miss 窓之内) でも終わらない。
        done = machine.Update(new SongEndTick
        {
            EndReached = true,
            JudgeTime = Duration + 0.1f,
            SettleDeadline = deadline,
            PendingNotes = 2 - GManager.settledNoteCount,
            TotalNotes = 2
        });
        Assert.IsFalse(done, "判定猶予が終わる前に曲が終わった");
        Assert.AreEqual(SongEndState.Finishing, machine.State);

        // 6. duration 端点で Tap を押す → 判定される (未判定のまま落ちない)。
        player.Run(SongEndAcceptanceRig.Frames(Duration - 1f, Duration + 0.05f, 0.05f));

        Assert.AreEqual(2, GManager.perfect, "duration 端点の Tap が Perfect になっていない");
        Assert.AreEqual(2, GManager.settledNoteCount, "duration 端点の Tap が確定していない");
        Assert.AreEqual(2, SongEndAcceptanceTests.JudgedCount(), "判定合計が noteCount に一致しない");

        // 7. 全ノーツが確定してから初めて曲終わる (強制完了ではない)。
        done = machine.Update(new SongEndTick
        {
            EndReached = true,
            JudgeTime = Duration + 0.1f,
            SettleDeadline = deadline,
            PendingNotes = 2 - GManager.settledNoteCount,
            TotalNotes = 2
        });
        Assert.IsTrue(done, "全ノーツ確定後に曲が終わらない");
        Assert.AreEqual(SongEndState.Completed, machine.State);
        Assert.IsFalse(machine.ForcedByDeadline);
    }

    /// <summary>
    /// duration 端点の Tap を **押さなかった** 場合も Miss として 1 回だけ確定する
    /// (未判定のまま落ちない = 判定合計が noteCount に届く)。
    /// </summary>
    [Test]
    public void TapAtDurationEndpoint_NotPressed_SettlesAsMissExactlyOnce()
    {
        rig = SongEndAcceptanceRig.BuildRig(TapAtEndChart, 2);
        rigRoot = rig.Root;
        SongEndAcceptanceRig.Player player = new SongEndAcceptanceRig.Player(rig);

        player.SchedulePress(rig.NoteTime[0], rig.NoteLane[0]);
        // 末尾の Tap は押さえない。Miss 窓 (10.0 + 0.2) を越すまで回す。
        player.Run(SongEndAcceptanceRig.Frames(0f, Duration + 0.3f, 0.05f));

        Assert.AreEqual(1, GManager.miss, "duration 端点の Tap が Miss になっていない");
        Assert.AreEqual(2, SongEndAcceptanceTests.JudgedCount(), "判定合計が noteCount に一致しない");
        Assert.AreEqual(2, GManager.settledNoteCount, "確定ノーツ数が noteCount に一致しない");
    }

    // ------------------------------------------------------------------
    // 2) duration 端点で終端する HOLD
    // ------------------------------------------------------------------

    /// <summary>
    /// HOLD の終端が duration 端点 (10.0 秒) のとき、終端で離しても
    /// 中断にならず、判定合計は noteCount のままになる。
    /// </summary>
    [Test]
    public void HoldEndingAtDurationEndpoint_IsCleared_NotBroken()
    {
        rig = SongEndAcceptanceRig.BuildRig(HoldEndingAtEndChart, 2);
        rigRoot = rig.Root;
        SongEndAcceptanceRig.Player player = new SongEndAcceptanceRig.Player(rig);

        // 先頭の Tap と HOLD の先頭を押す。HOLD は **終端ちょうど** で離す。
        player.SchedulePress(rig.NoteTime[0], rig.NoteLane[0]);
        player.SchedulePress(rig.NoteTime[1], rig.NoteLane[1]);
        player.ScheduleRelease(rig.NoteEndTime[1], rig.NoteLane[1]);

        // 本番の判定期限は HOLD 終端 + GREAT_WINDOW (duration + 0.15)。
        float deadline = SongEndAcceptanceRig.ProductionSettleDeadline(rig.Notes);
        Assert.AreEqual(Duration + LongNote.GREAT_WINDOW, deadline, 0.001f,
            "判定期限が HOLD 終端 + GREAT_WINDOW になっていない");

        player.Run(SongEndAcceptanceRig.Frames(0f, Duration + 0.1f, 0.05f));

        // HOLD は終端で離したので中断 (BAD) にならず、Perfect のまま。
        Assert.AreEqual(2, GManager.perfect, "duration 端点で終端する HOLD が Perfect になっていない");
        Assert.AreEqual(0, GManager.bad, "duration 端点で終端する HOLD が中断された");
        Assert.AreEqual(2, SongEndAcceptanceTests.JudgedCount(), "判定合計が noteCount に一致しない");
        Assert.AreEqual(2, GManager.settledNoteCount, "確定ノーツ数が noteCount に一致しない");

        // HOLD は保持中でない (終端で解放済み)。
        Assert.IsFalse(rig.Queue.IsHolding(rig.NoteLane[1]), "HOLD が保持中のまま");
    }

    /// <summary>
    /// HOLD の終端が duration 端点でも、**押し続けたまま** 終端を越えれば
    /// 期限切れ (成功) として片付き、判定合計は noteCount のままになる。
    /// </summary>
    [Test]
    public void HoldEndingAtDurationEndpoint_HeldPastTheEnd_SettlesOnce()
    {
        rig = SongEndAcceptanceRig.BuildRig(HoldEndingAtEndChart, 2);
        rigRoot = rig.Root;
        SongEndAcceptanceRig.Player player = new SongEndAcceptanceRig.Player(rig);

        player.SchedulePress(rig.NoteTime[0], rig.NoteLane[0]);
        player.SchedulePress(rig.NoteTime[1], rig.NoteLane[1]);
        // 終端 (+ GREAT_WINDOW) を越すまで押し続ける (離さない)。

        player.Run(SongEndAcceptanceRig.Frames(0f, Duration + LongNote.GREAT_WINDOW + 0.1f, 0.05f));

        Assert.AreEqual(2, SongEndAcceptanceTests.JudgedCount(), "判定合計が noteCount に一致しない");
        Assert.AreEqual(2, GManager.settledNoteCount, "確定ノーツ数が noteCount に一致しない");
        Assert.AreEqual(0, GManager.bad, "押し切った HOLD が中断された");
        Assert.IsFalse(rig.Queue.IsHolding(rig.NoteLane[1]), "HOLD が保持中のまま");
    }

    // ------------------------------------------------------------------
    // 3) duration 端点で始まる HOLD
    // ------------------------------------------------------------------

    /// <summary>
    /// HOLD の **開始** が duration 端点 (10.0 秒) でも、その先頭は判定される。
    /// 終端条件が 9.0 秒で成立していても、HOLD の先頭が判定されるまでは
    /// 曲が終わらない。
    /// </summary>
    [Test]
    public void HoldStartingAtDurationEndpoint_HeadIsJudged_AndTheSongWaitsForIt()
    {
        rig = SongEndAcceptanceRig.BuildRig(HoldStartingAtEndChart, 2);
        rigRoot = rig.Root;
        SongEndAcceptanceRig.Player player = new SongEndAcceptanceRig.Player(rig);

        player.SchedulePress(rig.NoteTime[0], rig.NoteLane[0]);

        // 9.0 秒 (終端条件成立) まで進める。HOLD の先頭はまだ押さない。
        player.Run(SongEndAcceptanceRig.Frames(0f, Duration - 1f, 0.05f));

        Assert.AreEqual(1, GManager.settledNoteCount, "先頭の Tap が確定していない");

        float deadline = SongEndAcceptanceRig.ProductionSettleDeadline(rig.Notes);
        Assert.AreEqual(11f + LongNote.GREAT_WINDOW, deadline, 0.001f,
            "判定期限が HOLD 終端 + GREAT_WINDOW になっていない");

        var machine = new SongEndMachine();
        bool done = machine.Update(new SongEndTick
        {
            EndReached = true,
            JudgeTime = Duration - 1f,
            SettleDeadline = deadline,
            PendingNotes = 2 - GManager.settledNoteCount,
            TotalNotes = 2
        });
        Assert.IsFalse(done, "duration 端点の HOLD が未判定なのに曲が終わった");
        Assert.AreEqual(SongEndState.Finishing, machine.State);

        // duration 端点で HOLD の先頭を押す → 判定される。
        player.SchedulePress(rig.NoteTime[1], rig.NoteLane[1]);
        player.Run(SongEndAcceptanceRig.Frames(Duration - 1f, Duration + 0.05f, 0.05f));

        Assert.AreEqual(2, GManager.perfect, "duration 端点が始まる HOLD の先頭が Perfect になっていない");
        Assert.AreEqual(2, GManager.settledNoteCount, "duration 端点が始まる HOLD が確定していない");
        Assert.AreEqual(2, SongEndAcceptanceTests.JudgedCount(), "判定合計が noteCount に一致しない");

        done = machine.Update(new SongEndTick
        {
            EndReached = true,
            JudgeTime = Duration + 0.05f,
            SettleDeadline = deadline,
            PendingNotes = 2 - GManager.settledNoteCount,
            TotalNotes = 2
        });
        Assert.IsTrue(done, "全ノーツ確定後に曲が終わらない");
        Assert.AreEqual(SongEndState.Completed, machine.State);
        Assert.IsFalse(machine.ForcedByDeadline);
    }

    // ------------------------------------------------------------------

    private void DestroyNotes()
    {
        if (rig == null) return;
        SongEndAcceptanceRig.DestroyNotes(rig);
    }
}
