using UnityEngine;

namespace FlashBeat.Gameplay
{
    /// <summary>
    /// Timing helpers that keep the note judgement on the media clock instead of
    /// on the raw frame clock.
    ///
    /// The chart times (see NotesManager) are counted from the first frame of the
    /// song, i.e. they are media times. The judge (see Judge) compares
    /// `Time.time` against `noteTime + GManager.StartTime`. So the only thing
    /// needed to line the notes up with the picture and the sound is a
    /// `GManager.StartTime` that is derived from the media clock:
    ///
    ///     StartTime = wallClockTime - mediaTime
    ///
    /// Recomputing it every frame (instead of accumulating Time.deltaTime) means a
    /// buffering stall or a dropped frame cannot make the judgement drift: the
    /// reference point follows the media, not the wall clock.
    /// </summary>
    public static class MediaTimeline
    {
        /// <summary>
        /// Value for GManager.StartTime so that `wallClockTime` maps onto
        /// `mediaTime` on the chart timeline.
        /// </summary>
        public static float JudgeStartTime(float wallClockTime, float mediaTime)
        {
            return wallClockTime - mediaTime;
        }

        /// <summary>Media position the judge sees at the given wall clock time.</summary>
        public static float MediaTimeAt(float wallClockTime, float judgeStartTime)
        {
            return wallClockTime - judgeStartTime;
        }

        /// <summary>Judgement lag as the game computes it (see Judge.CheckAll).</summary>
        public static float JudgeLag(float wallClockTime, float judgeStartTime, float noteTime)
        {
            return Mathf.Abs(wallClockTime - (noteTime + judgeStartTime));
        }

        /// <summary>
        /// Judgement lag expressed purely on the media timeline. With a start time
        /// derived from the media clock this is identical to <see cref="JudgeLag"/>,
        /// which is the invariant that keeps notes, picture and sound together.
        /// </summary>
        public static float MediaLag(float mediaTime, float noteTime)
        {
            return Mathf.Abs(mediaTime - noteTime);
        }

        /// <summary>True once the song has played long enough to fade into the result screen.</summary>
        public static bool HasSongEnded(float mediaTime, float songDuration, float leadOutSeconds = 1.0f)
        {
            return mediaTime >= songDuration - leadOutSeconds;
        }

        /// <summary>
        /// Media position used when no media player runs at all - the download
        /// failed, the file could not be opened or the stream is not available.
        /// The song still has to end: without a clock the game would never reach
        /// the result screen and the player would stay trapped in the game scene.
        /// </summary>
        public static float FallbackMediaTime(float wallClockTime, float playbackStartTime)
        {
            return Mathf.Max(0f, wallClockTime - playbackStartTime);
        }

        /// <summary>
        /// True when the picture and the separate sound track drift apart far
        /// enough to be corrected. The picture is the reference (the chart was
        /// authored against the video), the sound is re-seeked to it.
        /// </summary>
        public static bool ShouldResync(float pictureTime, float soundTime, float threshold = 0.15f)
        {
            return Mathf.Abs(pictureTime - soundTime) > threshold;
        }

        /// <summary>
        /// Position of a note on the lane, derived from the media clock: the note
        /// reaches the hit line exactly when the media reaches its time. A
        /// buffering stall therefore holds the notes instead of letting them run
        /// ahead of the music.
        /// </summary>
        public static float NotePositionZ(float noteTime, float mediaTime, float noteSpeed)
        {
            return (noteTime - mediaTime) * noteSpeed;
        }

        /// <summary>
        /// Time of a note, recovered from the position the NotesManager created
        /// it at (z = noteTime * noteSpeed).
        /// </summary>
        public static float NoteTimeFromPositionZ(float z, float noteSpeed)
        {
            return noteSpeed > 0f ? z / noteSpeed : 0f;
        }
    }
}
