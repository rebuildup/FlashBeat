using FlashBeat.Gameplay;
using NUnit.Framework;

/// <summary>
/// Media end contract (issue #226): the decision that ends the song must not
/// rest on a single frame in which the player reports "not playing".
///
///   * a transient stall (buffering tick, seek, dropped decode) must NOT end
///     the song - the music would be cut off mid-song;
///   * a stop that really sits at the end of the media and stays stopped must
///     end the song, exactly once (the caller fires the end transition once);
///   * the decision is monotone: once the end is confirmed, later ticks (even
///     flapping "isPlaying" reports) keep confirming it.
/// </summary>
public class MediaEndDetectorTests
{
    const float Duration = 180f;
    const float End = Duration;

    static MediaEndDetector NewDetector()
    {
        return new MediaEndDetector();
    }

    /// <summary>Plays from 0 to <paramref name="untilTime"/>, one 1/60s tick at a time.</summary>
    static float Play(MediaEndDetector detector, float untilTime, float duration, float now = 0f)
    {
        const float frame = 1f / 60f;
        for (float t = 0f; t < untilTime; t += frame)
        {
            Assert.That(detector.Tick(now + t, t, true, true, duration),
                        Is.EqualTo(MediaEndDecision.None));
        }
        return now + untilTime;
    }

    [Test]
    public void ATransientSingleFrameStallDoesNotEndTheSong()
    {
        var detector = NewDetector();
        float now = Play(detector, 60f, Duration);

        // One frame (16ms) of "not playing" in the middle of the song ...
        Assert.That(detector.Tick(now, 60f, true, false, Duration), Is.EqualTo(MediaEndDecision.None));
        // ... then the media resumes and plays on to the real end.
        now = Play(detector, Duration - 60f, Duration, now);
        Assert.That(detector.Tick(now, End, true, false, Duration), Is.EqualTo(MediaEndDecision.None));

        // The stop stays (the media finished): after ConfirmSeconds it ends.
        Assert.That(detector.Tick(now + MediaEndDetector.ConfirmSeconds, End, true, false, Duration),
                    Is.EqualTo(MediaEndDecision.Ended));
    }

    [Test]
    public void ASustainedStopAtTheEndEndsTheSongExactlyOnce()
    {
        var detector = NewDetector();
        float now = Play(detector, Duration, Duration);

        int endedCount = 0;
        // The stop is observed for several seconds after the media finished.
        for (float elapsed = 0f; elapsed <= 3f; elapsed += 1f / 60f)
        {
            if (detector.Tick(now + elapsed, End, true, false, Duration) == MediaEndDecision.Ended)
            {
                endedCount++;
            }
        }

        // The detector turns Ended exactly at the confirmation threshold and
        // then keeps confirming - the caller can fire the end transition once.
        Assert.Greater(endedCount, 1, "the end must be confirmed on every later tick");
        Assert.That(detector.Tick(now + 10f, End, true, false, Duration),
                    Is.EqualTo(MediaEndDecision.Ended));
    }

    [Test]
    public void ASustainedStopWellBeforeTheEndOfAMediaOfKnownLengthIsNotTheEnd()
    {
        var detector = NewDetector();
        float now = Play(detector, 60f, Duration);

        // The media stalls 120s before its end and stays stopped for a minute.
        for (float elapsed = 0f; elapsed <= 60f; elapsed += 1f / 60f)
        {
            Assert.That(detector.Tick(now + elapsed, 60f, true, false, Duration),
                        Is.EqualTo(MediaEndDecision.None),
                        "a sustained stop before the end of a media of known length is a stall, not the song end");
        }
    }

    /// <summary>
    /// Known-remaining behaviour, pinned: when the duration of the media itself
    /// is unknown (the picture player and the separate audio stream pass 0 -
    /// the catalog duration can be longer than the actual video, issue #226),
    /// a sustained stop decides the end regardless of the position. A long
    /// video stall therefore still ends the song as a success today; only the
    /// single-frame misfire is fixed. The clock-freeze path (StallFailSeconds)
    /// does not get a chance because mediaEnded short-circuits it.
    /// </summary>
    [Test]
    public void WithAnUnknownDurationASustainedStopDecidesTheEndWhereverItHappens()
    {
        var detector = NewDetector();
        float now = Play(detector, 60f, 0f);

        Assert.That(detector.Tick(now + MediaEndDetector.ConfirmSeconds, 60f, true, false, 0f),
                    Is.EqualTo(MediaEndDecision.Ended));
    }

    [Test]
    public void TheDecisionIsMonotoneOnceEndedIsReturned()
    {
        var detector = NewDetector();
        float now = Play(detector, Duration, Duration);
        Assert.That(detector.Tick(now + MediaEndDetector.ConfirmSeconds, End, true, false, Duration),
                    Is.EqualTo(MediaEndDecision.Ended));

        // Later observations - even nonsensical ones (playing again, position
        // back at zero, unprepared player) - must keep confirming the end.
        Assert.That(detector.Tick(now + 1f, End, true, true, Duration), Is.EqualTo(MediaEndDecision.Ended));
        Assert.That(detector.Tick(now + 2f, 0f, true, false, Duration), Is.EqualTo(MediaEndDecision.Ended));
        Assert.That(detector.Tick(now + 3f, End, false, false, Duration), Is.EqualTo(MediaEndDecision.Ended));
    }

    [Test]
    public void AMediaThatNeverPlayedIsNotEndedByAStop()
    {
        var detector = NewDetector();
        // The player reports "not playing" from the first tick (preparation
        // stall) and keeps doing so: no end, because the song never started.
        for (float elapsed = 0f; elapsed <= 10f; elapsed += 1f / 60f)
        {
            Assert.That(detector.Tick(elapsed, 0f, true, false, Duration),
                        Is.EqualTo(MediaEndDecision.None));
        }
    }

    [Test]
    public void ResetClearsTheStopObservationSoPauseSecondsDoNotCarryOver()
    {
        var detector = NewDetector();
        float now = Play(detector, 60f, Duration);

        // The stop runs almost to the confirmation threshold, then a pause gate
        // freezes the media and the play resumes afterwards.
        Assert.That(detector.Tick(now, 60f, true, false, Duration), Is.EqualTo(MediaEndDecision.None));
        Assert.That(detector.Tick(now + MediaEndDetector.ConfirmSeconds - 0.1f, 60f, true, false, Duration),
                    Is.EqualTo(MediaEndDecision.None));
        detector.Reset();

        // After the resume the media plays on; the carried-over stop seconds
        // must not confirm an end shortly after the next hiccup.
        now = Play(detector, 60f, Duration, now + 10f);
        Assert.That(detector.Tick(now, 120f, true, false, Duration), Is.EqualTo(MediaEndDecision.None));
        Assert.That(detector.Tick(now + MediaEndDetector.ConfirmSeconds - 0.1f, 120f, true, false, Duration),
                    Is.EqualTo(MediaEndDecision.None));
    }
}
