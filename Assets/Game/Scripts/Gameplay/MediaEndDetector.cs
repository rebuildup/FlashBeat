namespace FlashBeat.Gameplay
{
    /// <summary>The outcome of one <see cref="MediaEndDetector.Tick"/> observation.</summary>
    public enum MediaEndDecision
    {
        /// <summary>The media has not (yet) convincingly reached its end.</summary>
        None,

        /// <summary>The media end is confirmed. The detector is monotone: every
        /// later tick returns <see cref="Ended"/> as well.</summary>
        Ended,
    }

    /// <summary>
    /// Decides whether the media (video / audio) has reached the end of the
    /// song, from frame-by-frame player observations.
    ///
    /// A single frame in which the player reports "not playing" proves nothing:
    /// a buffering hiccup, a seek or a decoder stall can stop the clock for one
    /// tick and resume right after. Ending the song on that single frame cuts
    /// the music off mid-song (issue #226). So the stop has to be sustained
    /// (<see cref="ConfirmSeconds"/>) and - when the duration of the media
    /// itself is known - the position has to sit at the end
    /// (<see cref="EndToleranceSeconds"/>).
    ///
    /// The rules live here (and not in the MonoBehaviour) so they can be
    /// verified by EditMode tests without any player machinery.
    /// </summary>
    public sealed class MediaEndDetector
    {
        /// <summary>
        /// How long the player must stay stopped before the stop counts as the
        /// end of the media. A transient stall (buffering tick, seek, dropped
        /// decode) recovers within a frame or two, far below this window; the
        /// existing stall logging starts at 1s and the clock-freeze failure at
        /// 30s. The window delays the result screen by at most half a second
        /// after the media really stopped - and the loop point event usually
        /// confirms the end first anyway.
        /// </summary>
        public const float ConfirmSeconds = 0.5f;

        /// <summary>
        /// How close to the known media duration the position has to be for a
        /// stop to count as the end. Players report the end position a hair
        /// before the exact length, so a small tolerance is enough (the bundled
        /// wave path used the same 0.05s before the detector existed).
        /// </summary>
        public const float EndToleranceSeconds = 0.05f;

        bool seenPlaying;
        bool ended;

        /// <summary>Wall clock time of the first sustained "not playing" observation; -1 while not stopping.</summary>
        float stopStarted = -1f;

        /// <summary>
        /// Feeds one observation. <paramref name="songDuration"/> is the duration
        /// of the media itself; pass 0 when it is unknown - then a sustained
        /// stop alone decides (the caller cannot verify the position).
        /// </summary>
        public MediaEndDecision Tick(float now, float mediaTime, bool isPrepared, bool isPlaying, float songDuration)
        {
            if (ended)
            {
                // Monotone: the caller must not fire the end twice, and a later
                // flapping "isPlaying" must not un-decide the end.
                return MediaEndDecision.Ended;
            }
            if (!isPrepared)
            {
                stopStarted = -1f;
                return MediaEndDecision.None;
            }
            if (isPlaying)
            {
                seenPlaying = true;
                stopStarted = -1f;
                return MediaEndDecision.None;
            }
            if (!seenPlaying)
            {
                // The media never played: a stop before the first playing frame
                // is a preparation stall, not the end of the song.
                return MediaEndDecision.None;
            }
            if (MediaTimeline.IsUsableDuration(songDuration) &&
                mediaTime < songDuration - EndToleranceSeconds)
            {
                // Sustained stop well before the end of a media whose length is
                // known: a stall, not the end. The stall path of the caller
                // (clock freeze) owns this case.
                stopStarted = -1f;
                return MediaEndDecision.None;
            }
            if (stopStarted < 0f)
            {
                stopStarted = now;
                return MediaEndDecision.None;
            }
            if (now - stopStarted < ConfirmSeconds)
            {
                return MediaEndDecision.None;
            }
            ended = true;
            return MediaEndDecision.Ended;
        }

        /// <summary>
        /// Forgets the running stop observation (and the seen-playing mark).
        /// Used when a pause gate froze the media: the stop seconds before the
        /// pause must not add up with the ones after the resume.
        /// </summary>
        public void Reset()
        {
            seenPlaying = false;
            stopStarted = -1f;
        }
    }
}
