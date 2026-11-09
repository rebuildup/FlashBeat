using System.IO;
using System.Text.RegularExpressions;
using FlashBeat.Gameplay;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Playback clock contract of a real playthrough (issue #48/#31):
///
///   * the song has to end even when no media could be played, otherwise the
///     game never reaches the result screen and the player is stuck;
///   * the notes have to follow the media clock, so they cannot run ahead of
///     the music while the stream buffers;
///   * a picture that drifts away from the separate sound track has to be
///     corrected.
/// </summary>
public class PlaybackClockTests
{
    [Test]
    public void TheSongEndsWithoutAnyMediaSoTheResultSceneIsReached()
    {
        const float duration = 89f;
        const float playbackStart = 1000f;

        // 87 seconds after the start the song is not over yet ...
        float beforeEnd = MediaTimeline.FallbackMediaTime(playbackStart + 87f, playbackStart);
        Assert.IsFalse(MediaTimeline.HasSongEnded(beforeEnd, duration));

        // ... one second before the duration it is (the lead out of one second),
        // so the game leaves the game scene and the player can retry or go back
        // to the song list.
        float atEnd = MediaTimeline.FallbackMediaTime(playbackStart + duration - 1f, playbackStart);
        Assert.IsTrue(MediaTimeline.HasSongEnded(atEnd, duration));
    }

    [Test]
    public void TheFallbackClockNeverRunsBackwards()
    {
        Assert.AreEqual(0f, MediaTimeline.FallbackMediaTime(10f, 20f));
    }

    [Test]
    public void TheJudgementStaysOnTheChartTimelineWhileTheFallbackClockRuns()
    {
        const float playbackStart = 500f;
        float wallClock = playbackStart + 12f;

        float mediaTime = MediaTimeline.FallbackMediaTime(wallClock, playbackStart);
        float judgeStart = MediaTimeline.JudgeStartTime(wallClock, mediaTime);

        // The note at 12s of the song has to be hit exactly now.
        Assert.AreEqual(0f, MediaTimeline.JudgeLag(wallClock, judgeStart, 12f), 0.0001f);
    }

    [Test]
    public void ANoteReachesTheHitLineExactlyWhenTheMediaReachesItsTime()
    {
        const float noteTime = 4.5f;
        float zScale = MediaTimeline.ZScaleFor(10f);

        Assert.AreEqual(0f, MediaTimeline.NotePositionZ(noteTime, noteTime, zScale), 0.0001f);
        Assert.Greater(MediaTimeline.NotePositionZ(noteTime, noteTime - 1f, zScale), 0f);
        Assert.Less(MediaTimeline.NotePositionZ(noteTime, noteTime + 1f, zScale), 0f);
    }

    [Test]
    public void TheNotesDoNotJumpWhenTheSongStarts()
    {
        const float noteTime = 4.5f;
        float zScale = MediaTimeline.ZScaleFor(10f);

        // The NotesManager creates the note at z = noteTime * zScale.
        float initialZ = noteTime * zScale;
        Assert.AreEqual(initialZ, MediaTimeline.NotePositionZ(noteTime, 0f, zScale), 0.0001f);

        // The note time is recovered from that position again.
        Assert.AreEqual(noteTime, MediaTimeline.NoteTimeFromPositionZ(initialZ, zScale), 0.0001f);
    }

    [Test]
    public void AStalledMediaClockHoldsTheNotesInsteadOfLettingThemRunAhead()
    {
        const float noteTime = 4.5f;
        float zScale = MediaTimeline.ZScaleFor(10f);

        float whileBuffering = MediaTimeline.NotePositionZ(noteTime, 2f, zScale);
        float whileBufferingLater = MediaTimeline.NotePositionZ(noteTime, 2f, zScale);

        Assert.AreEqual(whileBuffering, whileBufferingLater, 0.0001f);
        Assert.Greater(whileBuffering, 0f);
    }

    /// <summary>
    /// Issue #233: z スケールの決め方の契約。
    ///
    /// MediaTimeline の第 3 引数は「chart time 1 秒あたりのワールド z 移動量」
    /// (zScale) であって、素の noteSpeed オプション **ではない**。
    /// 変換は <see cref="MediaTimeline.ZScaleFor"/> が持ち、
    /// 実運用の 3 箇所 (生成 / 時刻復元 / 移動) は
    /// <see cref="MediaTimeline.CurrentZScale"/> 1 箇所から読む。
    ///
    /// 旧来の意味 (noteSpeed をそのまま z スケールにする) を保つ wrapper は
    /// **残さない**: その解釈だとカメラ可視範囲 8.61 / 10 = 0.86 秒しか
    /// 譜面が見えず、Issue #233 の直接原因そのものである。
    /// </summary>
    [Test]
    public void TheZScaleComesFromTheCameraPinnedConversion()
    {
        float zScale = MediaTimeline.ZScaleFor(10f);

        // ノーツ速度 10 のとき 1.6 秒 (設計意図) が画面に入る。
        Assert.AreEqual(MediaTimeline.ApproachSecondsAtSpeed10,
            MediaTimeline.VisibleDepthZ / zScale, 0.0001f);

        // 速度に比例する (速い = 同じ画面距離を速く移動)。
        Assert.AreEqual(zScale * 2f, MediaTimeline.ZScaleFor(20f), 0.0001f);
        Assert.AreEqual(zScale * 0.5f, MediaTimeline.ZScaleFor(5f), 0.0001f);

        // 0 や負の速度でも破綻しない (軸が反転しない)。
        Assert.Greater(MediaTimeline.ZScaleFor(0f), 0f);
        Assert.Greater(MediaTimeline.ZScaleFor(-3f), 0f);
    }

    /// <summary>
    /// 実運用が使うスケールは <see cref="MediaTimeline.CurrentZScale"/> 1 箇所から
    /// 来る (生成・復元・移動で同じ値)。
    /// </summary>
    [Test]
    public void TheCurrentZScaleFollowsTheConfiguredNoteSpeed()
    {
        float saved = GManager.noteSpeed;
        try
        {
            GManager.noteSpeed = 10f;
            Assert.AreEqual(MediaTimeline.ZScaleFor(10f), MediaTimeline.CurrentZScale, 0.0001f);

            GManager.noteSpeed = 25f;
            Assert.AreEqual(MediaTimeline.ZScaleFor(25f), MediaTimeline.CurrentZScale, 0.0001f);
            Assert.AreEqual(MediaTimeline.ZScaleFor(25f) * 2.5f, MediaTimeline.CurrentZScale, 0.0001f);
        }
        finally
        {
            GManager.noteSpeed = saved;
        }
    }

    /// <summary>
    /// 3D レーンの 3 箇所 (生成 / 時刻復元 / 移動) が同じスケールを使うことを
    /// **ソースレベル** で固定する。
    ///
    /// 片方だけ直すと「1 ノーツ = 1 判定」「判定線に着く時刻 = メディア時刻」が
    /// 崩れるので、数値テストだけでは守れない。
    /// </summary>
    [Test]
    public void TheThreeLaneSitesUseTheSameZScale()
    {
        string notes = File.ReadAllText(SourcePath("Assets/Game/Scripts/Gameplay/Notes.cs"));
        string manager = File.ReadAllText(SourcePath("Assets/Game/Scripts/Gameplay/NotesManager.cs"));
        string timeline = File.ReadAllText(SourcePath("Assets/Game/Scripts/Gameplay/MediaTimeline.cs"));

        // 1. 生成 (NotesManager)
        StringAssert.Contains("positionsZ.Add(time * FlashBeat.Gameplay.MediaTimeline.CurrentZScale)", manager,
            "The note generation must use the shared z scale.");

        // 2. 時刻復元 (Notes.Start) と 3. 移動 (Notes.Update)
        StringAssert.Contains("MediaTimeline.NoteTimeFromPositionZ(transform.position.z, ZScale)", notes,
            "The note time recovery must use the shared z scale.");
        StringAssert.Contains("MediaTimeline.NotePositionZ(noteTime, GManager.step_time, ZScale)", notes,
            "The note movement must use the shared z scale.");

        // どれか 1 箇所でも生の noteSpeed を z スケールを渡してはいけない。
        Assert.IsFalse(Regex.IsMatch(notes, @"Note(PositionZ|TimeFromPositionZ)\([^)]*GManager\.noteSpeed"),
            "A note position call passes the raw note speed instead of the shared z scale.");
        Assert.IsFalse(Regex.IsMatch(manager, @"Note(PositionZ|TimeFromPositionZ)\([^)]*GManager\.noteSpeed"),
            "A note position call passes the raw note speed instead of the shared z scale.");

        // CurrentZScale は ZScaleFor(GManager.noteSpeed) そのもの。
        StringAssert.Contains("ZScaleFor(GManager.noteSpeed)", timeline,
            "MediaTimeline.CurrentZScale must be the camera pinned conversion of the note speed.");
    }

    private static string SourcePath(string relative)
    {
        string absolute = Path.Combine(Application.dataPath, relative.Substring("Assets/".Length));
        if (File.Exists(absolute)) return absolute;
        return relative;
    }

    [Test]
    public void APictureSoundDriftAboveTheThresholdIsCorrected()
    {
        Assert.IsFalse(MediaTimeline.ShouldResync(10f, 10.05f));
        Assert.IsTrue(MediaTimeline.ShouldResync(10f, 10.5f));
        Assert.IsTrue(MediaTimeline.ShouldResync(10f, 9.5f));
    }
}
