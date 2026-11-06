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

        // 88 seconds after the start the song is not over yet ...
        float beforeEnd = MediaTimeline.FallbackMediaTime(playbackStart + 88f, playbackStart);
        Assert.IsFalse(MediaTimeline.HasSongEnded(beforeEnd, duration));

        // ... one second later it is, so the game leaves the game scene and the
        // player can retry or go back to the song list.
        float atEnd = MediaTimeline.FallbackMediaTime(playbackStart + duration, playbackStart);
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
        const float speed = 10f;

        Assert.AreEqual(0f, MediaTimeline.NotePositionZ(noteTime, noteTime, speed), 0.0001f);
        Assert.Greater(MediaTimeline.NotePositionZ(noteTime, noteTime - 1f, speed), 0f);
        Assert.Less(MediaTimeline.NotePositionZ(noteTime, noteTime + 1f, speed), 0f);
    }

    [Test]
    public void TheNotesDoNotJumpWhenTheSongStarts()
    {
        const float noteTime = 4.5f;
        const float speed = 10f;

        // The NotesManager creates the note at z = noteTime * noteSpeed.
        float initialZ = noteTime * speed;
        Assert.AreEqual(initialZ, MediaTimeline.NotePositionZ(noteTime, 0f, speed), 0.0001f);

        // The note time is recovered from that position again.
        Assert.AreEqual(noteTime, MediaTimeline.NoteTimeFromPositionZ(initialZ, speed), 0.0001f);
    }

    [Test]
    public void AStalledMediaClockHoldsTheNotesInsteadOfLettingThemRunAhead()
    {
        const float noteTime = 4.5f;
        const float speed = 10f;

        float whileBuffering = MediaTimeline.NotePositionZ(noteTime, 2f, speed);
        float whileBufferingLater = MediaTimeline.NotePositionZ(noteTime, 2f, speed);

        Assert.AreEqual(whileBuffering, whileBufferingLater, 0.0001f);
        Assert.Greater(whileBuffering, 0f);
    }

    [Test]
    public void APictureSoundDriftAboveTheThresholdIsCorrected()
    {
        Assert.IsFalse(MediaTimeline.ShouldResync(10f, 10.05f));
        Assert.IsTrue(MediaTimeline.ShouldResync(10f, 10.5f));
        Assert.IsTrue(MediaTimeline.ShouldResync(10f, 9.5f));
    }
}
