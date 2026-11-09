using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using FlashBeat.Gameplay;

/// <summary>
/// Issue #226 の受入条件「フレーム落ち / メディアの時計停止」の回帰テスト。
///
/// 2 つの別々の故障を区別する:
///
///   A. **フレーム落ち** (描画が重くて 1 フレームで時間が大きく飛ぶ):
///      判定の時計が飛ばされ、複数ノーツがまとめて時刻切れになる。
///      それでも「1 ノーツ = 1 判定」は保たれ、曲は強制完了ではなく
///      普通に終わる (取りこぼし / 二重判定が起きない)。
///
///   B. **メディアの時計停止** (デコードの破綻): メディア時刻が 30 秒以上動かない。
///      ここで歌を **成功として終わらせない** (偽の完走にしない)。
///      本番は 30 秒の閾値で Failed として扱い、Result への遷移はしない
///      (プレイヤーを待ち状態にはせずに、既存の回復動線へ流す)。
/// </summary>
public class SongEndFrameDropAndStallTests
{
    private GameObject rigRoot;
    private SongEndAcceptanceRig.Rig rig;

    [SetUp]
    public void SetUp()
    {
        SongEndAcceptanceTests.ResetSessionState();
        PauseGate.HardReset();
    }

    [TearDown]
    public void TearDown()
    {
        if (rig != null)
        {
            SongEndAcceptanceRig.DestroyNotes(rig);
        }
        if (rigRoot != null)
        {
            Object.DestroyImmediate(rigRoot);
            rigRoot = null;
        }
        PauseGate.HardReset();
        SongEndAcceptanceTests.ClearSessionState();
    }

    /// <summary>3 ノーツ (Tap 2 + HOLD 1) の譜面。</summary>
    private const string ChartJson =
        "{\"name\":\"frame drop\",\"maxBlock\":8,\"BPM\":120,\"offset\":0,\"notes\":[" +
        "{\"type\":1,\"num\":4,\"block\":0,\"LPB\":4}," +   // 0.5s lane 0
        "{\"type\":1,\"num\":8,\"block\":1,\"LPB\":4}," +   // 1.0s lane 1
        "{\"type\":2,\"num\":12,\"endNum\":24,\"block\":2,\"LPB\":4}" + // 1.5s -> 3.0s lane 2
        "]}";

    private const int NoteCount = 3;

    // ------------------------------------------------------------------
    // A) フレーム落ち
    // ------------------------------------------------------------------

    /// <summary>
    /// フレーム落ちで判定の時計が 1.0 秒 → 3.5 秒へ飛んでも、
    /// 各ノーツはちょうど 1 回だけ確定し、曲は強制完了ではなく普通に終わる。
    /// </summary>
    [Test]
    public void FrameDrop_SettlesEveryNoteExactlyOnce_AndEndsCleanly()
    {
        rig = SongEndAcceptanceRig.BuildRig(ChartJson, NoteCount);
        rigRoot = rig.Root;
        SongEndAcceptanceRig.Player player = new SongEndAcceptanceRig.Player(rig);

        // 先頭の Tap だけを押さえる。残りはフレーム落ちでまとめて時刻切れにする。
        player.SchedulePress(rig.NoteTime[0], rig.NoteLane[0]);

        List<float> frames = new List<float>();
        frames.AddRange(SongEndAcceptanceRig.Frames(0f, 0.6f, 0.05f));
        frames.Add(3.5f); // ここがフレーム落ち (1 フレームで 2.9 秒飛ぶ)
        frames.AddRange(SongEndAcceptanceRig.Frames(3.55f, 4f, 0.05f));
        player.Run(frames);

        // 1 ノーツ = 1 判定。
        Assert.AreEqual(NoteCount, SongEndAcceptanceTests.JudgedCount(),
            "フレーム落ちで判定数が noteCount に一致しない");
        Assert.AreEqual(NoteCount, GManager.settledNoteCount,
            "フレーム落ちで確定ノーツ数が noteCount に一致しない");
        Assert.AreEqual(1, GManager.perfect, "押した Tap だけ Perfect");
        Assert.AreEqual(NoteCount - 1, GManager.miss, "時刻切れのノーツが Miss になっていない");
        Assert.AreEqual(0, rig.Queue.TotalPendingCount, "未判定のノーツが残っている");

        // 曲終了: 全ノーツ確定 → 強制完了ではない。
        var machine = new SongEndMachine();
        bool done = machine.Update(new SongEndTick
        {
            EndReached = true,
            JudgeTime = 4f,
            SettleDeadline = 3.2f,
            PendingNotes = NoteCount - GManager.settledNoteCount,
            TotalNotes = NoteCount
        });

        Assert.IsTrue(done, "フレーム落ち後に曲が終わらない");
        Assert.AreEqual(SongEndState.Completed, machine.State);
        Assert.IsFalse(machine.ForcedByDeadline, "フレーム落ちが強制完了になっている");
    }

    /// <summary>
    /// フレーム落ち (時計が飛ばされる) だけでは、曲は終わらない。
    /// 終端条件 (メディア終端 / duration) が成立するまで Playing のまま。
    /// </summary>
    [Test]
    public void AClockJumpAloneDoesNotEndTheSong()
    {
        var machine = new SongEndMachine();

        bool done = machine.Update(new SongEndTick
        {
            EndReached = false, // メディアも duration も終端していない
            JudgeTime = 3.5f,   // 時計が大きく飛んだ
            SettleDeadline = 3.2f,
            PendingNotes = 0,
            TotalNotes = NoteCount
        });

        Assert.IsFalse(done, "フレーム落ちだけで曲が終わった");
        Assert.AreEqual(SongEndState.Playing, machine.State);
    }

    // ------------------------------------------------------------------
    // B) メディアの時計停止 (ストール)
    // ------------------------------------------------------------------

    /// <summary>
    /// メディア時計が 30 秒以上動かないとき、本番は曲を **Failed** にして
    /// Result への遷移を止める (成功偽装の完走にしない / 待たされすぎない)。
    /// </summary>
    [Test]
    public void AStalledMediaClock_FailsTheSong_InsteadOfCompletingIt()
    {
        GameObject root = new GameObject("StallProbe");
        root.SetActive(false);
        rigRoot = root;

        MusicManager music = root.AddComponent<MusicManager>();

        // 実メディアの時計がある状態を作る (HasRealMediaClock が真になるように
        // バンドル wav の再生モードにして、オーディオ源にクリップを 1 本置く)。
        AudioSource audio = root.AddComponent<AudioSource>();
        audio.clip = AudioClip.Create("stall", 44100, 1, 44100, false);
        SongEndAcceptanceRig.SetPrivateField(music, "audioS", audio);
        SongEndAcceptanceRig.SetPrivateField(music, "audioMode", PlaybackSource.BundledWave);
        SongEndAcceptanceRig.SetPrivateField(music, "playbackStarted", true);

        // メディア時計は 0 のまま (進まない)。最後の進行時刻は 31 秒前。
        SongEndAcceptanceRig.SetPrivateField(music, "lastMediaTime", 0f);
        SongEndAcceptanceRig.SetPrivateField(music, "lastMediaProgressWallTime", Time.time - 31f);

        MethodInfo update = typeof(MusicManager).GetMethod("Update",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(update, "MusicManager.Update が見つからない");

        update.Invoke(music, null);

        Assert.AreEqual(SongEndState.Failed, music.SongEndState,
            "メディア時計の停止が Failed として扱われていない (成功偽装の完走になる)");
        Assert.IsFalse(music.ResultTriggered, "Failed なのに Result へ遷移した");
        Assert.IsFalse(music.SongEndForcedByDeadline, "Failed は期限切れ強制完了ではない");
    }

    /// <summary>
    /// 失敗した曲はその後も Completed にしない (結果画面へ自動遷移しない)。
    /// </summary>
    [Test]
    public void AFailedSongNeverCompletesAfterwards()
    {
        GameObject root = new GameObject("FailProbe");
        root.SetActive(false);
        rigRoot = root;

        MusicManager music = root.AddComponent<MusicManager>();
        SongEndAcceptanceRig.InvokeProductionFailSongEnd(root, SongEndFailure.MediaStalled);

        Assert.AreEqual(SongEndState.Failed, music.SongEndState);
        Assert.IsFalse(music.ResultTriggered, "失敗した曲が Result へ遷移した");

        // 状態機械側でも Failed は Completed にしない。
        var machine = new SongEndMachine();
        machine.Fail(SongEndFailure.MediaStalled);
        bool done = machine.Update(new SongEndTick
        {
            EndReached = true,
            JudgeTime = 999f,
            SettleDeadline = 10f,
            PendingNotes = 0,
            TotalNotes = 4
        });

        Assert.IsFalse(done, "失敗した曲が完了した");
        Assert.AreEqual(SongEndState.Failed, machine.State);
        Assert.AreEqual(SongEndFailure.MediaStalled, machine.Failure);
    }
}
