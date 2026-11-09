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

        /// <summary>
        /// True when the catalog duration can be used as an end condition at all.
        /// A missing (0 / negative) or broken (NaN, Infinity) duration must never
        /// decide the song end: with a real media clock the media end decides
        /// instead, without one the fallback end (<see cref="FallbackSongEnd"/>)
        /// keeps the game from trapping the player (issue #226).
        /// </summary>
        public static bool IsUsableDuration(float duration)
        {
            return duration > 0f && !float.IsNaN(duration) && !float.IsInfinity(duration);
        }

        /// <summary>
        /// True once the song has played long enough to fade into the result screen.
        /// A broken duration (<see cref="IsUsableDuration"/>) never ends the song
        /// here — the media end decides instead (see SongEndMachine, issue #226).
        /// </summary>
        public static bool HasSongEnded(float mediaTime, float songDuration, float leadOutSeconds = 1.0f)
        {
            if (!IsUsableDuration(songDuration))
            {
                return false;
            }
            return mediaTime >= songDuration - leadOutSeconds;
        }

        /// <summary>
        /// End position for a play without a real media clock (the wall clock
        /// fallback in MusicManager.CurrentMediaTime). A usable catalog duration
        /// stays the authority; a broken duration ends the song immediately —
        /// a zero-length song must not trap the player (issue #226).
        /// </summary>
        public static float FallbackSongEnd(float songDuration, float leadOutSeconds = 1.0f)
        {
            if (!IsUsableDuration(songDuration))
            {
                return 0f;
            }
            return Mathf.Max(0f, songDuration - leadOutSeconds);
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
        ///
        /// The third argument is the world z units per second of chart time
        /// (see <see cref="ZScaleFor"/>), not the raw note speed option.
        /// </summary>
        public static float NotePositionZ(float noteTime, float mediaTime, float zScale)
        {
            return (noteTime - mediaTime) * zScale;
        }

        /// <summary>
        /// Time of a note, recovered from the position the NotesManager created
        /// it at (z = noteTime * zScale).
        /// </summary>
        public static float NoteTimeFromPositionZ(float z, float zScale)
        {
            return zScale > 0f ? z / zScale : 0f;
        }

        /// <summary>
        /// How far the game camera sees along the lane, in world z units from
        /// the hit line (z = 0).
        ///
        /// This is a property of the Main Camera in GameScene.unity
        /// (position 0 / 4.9 / -0.8, pitch 54.8 deg, vertical FOV 60 deg):
        /// the top edge of the frustum hits the note plane (y = 0.55) at
        /// z = 8.61. PlayfieldGeometryTests re-derives the number from the
        /// scene file, so a camera change cannot silently break the window.
        /// </summary>
        public const float VisibleDepthZ = 8.61f;

        /// <summary>
        /// The time a note stays visible before it has to be hit, at note speed
        /// 10 - the design intent of the playfield (the v6 perspective
        /// playfield uses the same constant).
        ///
        /// Before Issue #233 the 3D lane only showed 8.61 / 10 = 0.86 s of the
        /// chart, so the playfield looked empty on most charts.
        /// </summary>
        public const float ApproachSecondsAtSpeed10 = 1.6f;

        /// <summary>
        /// World z units per second of chart time for the given note speed.
        ///
        /// The note speed option keeps its meaning (a higher speed = a shorter
        /// time to react), but the conversion into the world is pinned to the
        /// camera: at note speed 10 a note is on the screen 1.6 s before it
        /// has to be hit, i.e. exactly as long as the camera can see.
        /// </summary>
        public static float ZScaleFor(float noteSpeed)
        {
            float speed = Mathf.Max(0.1f, noteSpeed);
            return speed * (VisibleDepthZ / (ApproachSecondsAtSpeed10 * 10f));
        }

        /// <summary>
        /// The z scale for the note speed the player currently has configured.
        ///
        /// This is the single entry point for the 3D lane: the note creation
        /// (NotesManager), the recovery of the note time from the start
        /// position (Notes.Start) and the per-frame movement (Notes.Update)
        /// all read the scale from here. One value for all three is what keeps
        /// "one note = one judgement" and "the note reaches the hit line
        /// exactly at its own judge time" true (Issue #233).
        /// </summary>
        public static float CurrentZScale
        {
            get { return ZScaleFor(GManager.noteSpeed); }
        }
    }
}
