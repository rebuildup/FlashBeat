using System.Collections.Generic;
using FlashBeat.Gameplay;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 曲終了ハーネス (Issue #233 / #226) が前提とするもののテスト。
///
/// 実機ハーネス (AutoTestV6SongEnd) はカタログ内で一番短い曲 (ツイッターランド) を
/// 最後まで再生して「曲終了 → Result」を確かめる。だからこの曲は
///   - カタログに実在して選択できること
///   - 譜面 (chart) がありノーツがあること
///   - **末尾ノーツが確定する時刻が「曲長 − leadOut」より前** であること
///     (そうでないと曲終了が「期限切れ強制完了」になり、判定合計がノーツ数に
///      合わない結果になる = 成功偽装になる)
/// を満たさなければならない。ここではその前提をデータで固定する。
///
/// さらに、その前提のもとで本物の SongEndMachine が Completed へ到達することを
/// シミュレーションして確かめる (EditMode で時刻を明示的に進められる)。
/// </summary>
public class SongEndHarnessTests
{
    /// <summary>ハーネスが既定で再生する曲。</summary>
    const string HarnessSong = AutoTestV6SongEnd.DefaultSongTitle;

    static int SongIndex(string title)
    {
        for (int i = 0; i < GManager.SongName.Length; i++)
        {
            if (GManager.SongName[i] == title) { return i; }
        }
        return -1;
    }

    static SongData Song(string title)
    {
        int index = SongIndex(title);
        if (index < 0) { return null; }
        var songs = GManager.ActiveSongs;
        if (songs == null || index >= songs.Length) { return null; }
        return songs[index];
    }

    static Data Chart(string title)
    {
        var asset = Resources.Load<TextAsset>(title);
        if (asset == null) { return null; }
        return JsonUtility.FromJson<Data>(asset.text);
    }

    /// <summary>末尾ノーツが確定する時刻 (タップ: 時刻 + Miss 窓)。</summary>
    static float LastSettleTime(Data chart)
    {
        float last = 0f;
        for (int i = 0; i < chart.notes.Length; i++)
        {
            float settle = NotesManager.NoteTimeSeconds(chart.BPM, chart.notes[i]) + LongNote.BAD_WINDOW;
            if (settle > last) { last = settle; }
        }
        return last;
    }

    [Test]
    public void TheHarnessSongIsInTheCatalogAndPlayable()
    {
        int index = SongIndex(HarnessSong);
        Assert.GreaterOrEqual(index, 1, "the harness song is not in the catalog (index 0 is the placeholder)");
        SongData song = Song(HarnessSong);
        Assert.IsNotNull(song, "the harness song has no SongData");
        Assert.IsFalse(song.unplayable, "the harness song must be playable");
        Assert.Greater(song.duration, 0f, "the harness song has no usable duration");
    }

    [Test]
    public void TheParallelArraysStayInLockstepWithTheHarnessSong()
    {
        int index = SongIndex(HarnessSong);
        Assert.GreaterOrEqual(index, 0);
        Assert.AreEqual(GManager.SongName.Length, GManager.Musician.Length);
        Assert.AreEqual(GManager.SongName.Length, GManager.SongURL.Length);
        Assert.AreEqual(GManager.SongName.Length, GManager.SBPM.Length);
        Assert.AreEqual(GManager.SongName.Length, GManager.Slevel.Length);
        Assert.AreEqual(GManager.SongName.Length, GManager.Shit.Length);
        Assert.AreEqual(GManager.SongName.Length, GManager.SongLong.Length);
    }

    [Test]
    public void TheHarnessSongHasAChartWithNotes()
    {
        Data chart = Chart(HarnessSong);
        Assert.IsNotNull(chart, "the harness song has no chart in Resources");
        Assert.IsNotNull(chart.notes, "the harness chart has no notes array");
        Assert.Greater(chart.notes.Length, 0, "the harness chart has no notes");
    }

    [Test]
    public void TheHarnessSongHasNoHoldNotes()
    {
        Data chart = Chart(HarnessSong);
        Assert.IsNotNull(chart);

        // HOLD が 1 つも無いこと。理由は 2 つ:
        //  1) HOLD 中断 (BAD 差し替え) は判定 *イベント* を 2 回発生させるため、
        //     「確定ノーツ数」の出し方 (GManager.nextNotes = 判定イベント数) が
        //     HOLD の打断でずれる (Issue #182 / PR #256)。その影響を
        //     ハーネスの結果 (曲終了の観測) に取り込まないため、HOLD の無い譜面で
        //     固定する。
        //  2) 判定の内訳 (perfect / great / bad / miss) はノーツ 1 個につき 1 個なので
        //     HOLD でも二重計上されない。ハーネスが読むのはこの쪽だけ。
        int holds = 0;
        for (int i = 0; i < chart.notes.Length; i++)
        {
            if (chart.notes[i].type == LongNote.TYPE_HOLD) { holds++; }
        }
        Assert.AreEqual(0, holds, "the harness song must not contain HOLD notes (the catalogue charts are tap only)");
    }

    [Test]
    public void TheHarnessSongIsShortEnoughForATestRun()
    {
        SongData song = Song(HarnessSong);
        Assert.IsNotNull(song);
        Assert.LessOrEqual(song.duration, 60f, "the harness song is too long for a test run");
    }

    [Test]
    public void TheLastNoteSettlesBeforeTheSongEnd()
    {
        SongData song = Song(HarnessSong);
        Data chart = Chart(HarnessSong);
        Assert.IsNotNull(song);
        Assert.IsNotNull(chart);

        float lastSettle = LastSettleTime(chart);
        float songEnd = song.duration - 1f; // MediaTimeline.HasSongEnded の leadOut (1 秒)
        Assert.Less(lastSettle, songEnd,
            $"the last note settles at {lastSettle:F3}s but the song ends at {songEnd:F3}s - the end would be a forced completion");
    }

    [Test]
    public void TheSongEndMachineCompletesTheHarnessSongWithoutForcing()
    {
        SongData song = Song(HarnessSong);
        Data chart = Chart(HarnessSong);
        Assert.IsNotNull(song);
        Assert.IsNotNull(chart);

        var machine = new SongEndMachine();
        int total = chart.notes.Length;
        float settleDeadline = LastSettleTime(chart);

        bool completed = false;
        // 実際の MusicManager と同じ順序で、終了条件と判定会計を進める。
        for (float mediaTime = 0f; mediaTime <= song.duration + 3f; mediaTime += 0.05f)
        {
            int pending = 0;
            for (int i = 0; i < total; i++)
            {
                float settle = NotesManager.NoteTimeSeconds(chart.BPM, chart.notes[i]) + LongNote.BAD_WINDOW;
                if (settle > mediaTime) { pending++; }
            }

            var tick = new SongEndTick
            {
                EndReached = MediaTimeline.HasSongEnded(mediaTime, song.duration),
                JudgeTime = mediaTime,
                SettleDeadline = settleDeadline,
                PendingNotes = pending,
                TotalNotes = total
            };
            if (machine.Update(in tick)) { completed = true; break; }
        }

        Assert.IsTrue(completed, "the song end machine never completed for the harness song");
        Assert.AreEqual(SongEndState.Completed, machine.State);
        Assert.IsFalse(machine.ForcedByDeadline, "the song ended by the deadline instead of the settled notes");
    }

    [Test]
    public void TheMusicManagerExposesTheSongEndState()
    {
        var go = new GameObject("SongEndHarnessProbe");
        try
        {
            var music = go.AddComponent<MusicManager>();
            Assert.AreEqual(SongEndState.Playing, music.SongEndState, "a fresh music manager is playing");
            Assert.IsFalse(music.ResultTriggered, "a fresh music manager has not triggered the result");
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }
}
