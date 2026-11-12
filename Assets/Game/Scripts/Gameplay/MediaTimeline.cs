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
        /// Which clock drives the judgement (issue #58, regression 86ad62d).
        ///
        /// The picture time is used only after the picture playback was started
        /// (pictureStarted). A file that becomes prepared while the song is
        /// already running must not throw the clock back to zero - before the
        /// picture plays, its time is 0 and the running clock (the audio or the
        /// wall clock fallback) stays the authority. Once the picture plays it
        /// is the master and a later Ready event of another stream must not
        /// reset the clock either.
        /// </summary>
        public static float SelectMediaTime(bool pictureStarted, bool picturePrepared, float pictureTime,
            bool audioReady, float audioTime, float fallbackTime)
        {
            if (pictureStarted && picturePrepared) return pictureTime;
            if (audioReady) return audioTime;
            return fallbackTime;
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
        /// The parameter is named after its unit on purpose (PR #263 review N7):
        /// passing `GManager.noteSpeed` here would be a silent factor of ~1.86.
        /// </summary>
        public static float NotePositionZ(float noteTime, float mediaTime, float zUnitsPerSecond)
        {
            return (noteTime - mediaTime) * zUnitsPerSecond;
        }

        /// <summary>
        /// Time of a note, recovered from the position the NotesManager created
        /// it at (z = noteTime * zScale).
        /// </summary>
        public static float NoteTimeFromPositionZ(float z, float zUnitsPerSecond)
        {
            return zUnitsPerSecond > 0f ? z / zUnitsPerSecond : 0f;
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

        /// <summary>
        /// The z scale of the running play session.
        ///
        /// The scale is decided **once per song** (see <see cref="BeginSession"/>)
        /// and then read by all three lane sites (generation, time recovery,
        /// movement) plus the hold body length. A note speed change in the
        /// middle of a song would otherwise move the notes discontinuously
        /// (the scale is a ratio) and would make the recovered note time differ
        /// from the judged one (PR #263 review S7). The new speed applies to
        /// the next song.
        /// </summary>
        public static float SessionZScale
        {
            get { return sessionZScale > 0f ? sessionZScale : CurrentZScale; }
        }

        private static float sessionZScale;

        /// <summary>
        /// Fix the z scale of the play session that is about to start.
        /// Called by the NotesManager when it loads the chart, i.e. before the
        /// first note is created.
        /// </summary>
        public static void BeginSession(float noteSpeed)
        {
            sessionZScale = ZScaleFor(noteSpeed);
        }

        /// <summary>
        /// Forget the session scale (tests / a new play session).
        /// </summary>
        public static void ResetSession()
        {
            sessionZScale = 0f;
        }

        /// <summary>
        /// Height of the note plane (y). The NotesManager puts the notes here
        /// and the playfield geometry is derived from it.
        /// </summary>
        public const float NotePlaneY = 0.55f;

        /// <summary>The judge line (z = 0). The notes reach it at their judge time.</summary>
        public const float HitLineZ = 0f;

        /// <summary>
        /// How far the camera sees along the lane, in world z units from the hit
        /// line, derived from the camera itself.
        ///
        /// The top edge of the frustum hits the note plane (y = notePlaneY) at
        /// this z. For the Main Camera of GameScene.unity this is 8.6143,
        /// which is what <see cref="VisibleDepthZ"/> holds.
        /// </summary>
        public static float VisibleDepth(Camera camera, float notePlaneY = NotePlaneY)
        {
            if (camera == null) return 0f;

            Transform t = camera.transform;
            float half = camera.fieldOfView * 0.5f * Mathf.Deg2Rad;
            Vector3 top = t.forward * Mathf.Cos(half) + t.up * Mathf.Sin(half);
            if (Mathf.Abs(top.y) < 1e-6f) return 0f;

            float step = (notePlaneY - t.position.y) / top.y;
            return t.position.z + top.z * step;
        }

        /// <summary>
        /// How far in front of the hit line the bottom edge of the frustum
        /// still sees the plane at <paramref name="notePlaneY"/>, in world z
        /// units (returned as a positive distance).
        ///
        /// The 3D playfield draws the lane guides on the plane below the judge
        /// line too, and they have to leave the screen at its bottom edge
        /// rather than stop at z = 0 (Issue #380). This is where the bottom
        /// edge of the camera hits that plane; anything nearer is outside the
        /// screen. Mirrors <see cref="VisibleDepth"/> for the top edge.
        /// </summary>
        public static float VisibleFrontDepth(Camera camera, float notePlaneY = NotePlaneY)
        {
            if (camera == null) return 0f;

            Transform t = camera.transform;
            float half = camera.fieldOfView * 0.5f * Mathf.Deg2Rad;
            Vector3 bottom = t.forward * Mathf.Cos(half) - t.up * Mathf.Sin(half);
            if (Mathf.Abs(bottom.y) < 1e-6f) return 0f;

            float step = (notePlaneY - t.position.y) / bottom.y;
            float z = t.position.z + bottom.z * step;
            return Mathf.Max(0f, -z);
        }

        /// <summary>
        /// Half of the world width the camera shows at the judge line
        /// (x = +- this is on the screen, z = hitLineZ).
        ///
        /// The horizontal extent depends on the aspect the camera renders with
        /// (see <see cref="PlayfieldCameraFit"/>), so a narrow screen shows a
        /// narrower lane unless the fit is applied.
        /// </summary>
        public static float HalfWidthAtHitLine(Camera camera, float notePlaneY = NotePlaneY, float hitLineZ = HitLineZ)
        {
            if (camera == null) return 0f;

            Transform t = camera.transform;
            Vector3 d = new Vector3(0f, notePlaneY, hitLineZ) - t.position;
            float depth = Vector3.Dot(d, t.forward);
            if (depth <= 0f) return 0f;

            float tanVertical = Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            return depth * tanVertical * camera.aspect;
        }

        /// <summary>
        /// True when the world point is inside the frustum of the camera
        /// (with the aspect the camera renders with).
        /// </summary>
        public static bool IsVisible(Camera camera, Vector3 worldPoint)
        {
            if (camera == null) return false;

            Transform t = camera.transform;
            Vector3 d = worldPoint - t.position;
            float depth = Vector3.Dot(d, t.forward);
            if (depth <= 0f) return false;

            float tanVertical = Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float tanHorizontal = tanVertical * camera.aspect;

            return Mathf.Abs(Vector3.Dot(d, t.right)) / depth <= tanHorizontal
                && Mathf.Abs(Vector3.Dot(d, t.up)) / depth <= tanVertical;
        }
    }

    /// <summary>
    /// Keeps the whole playfield (8 lanes **and** the note width) inside the
    /// camera view on every display (Issue #233, PR #263 review S1).
    ///
    /// Background:
    ///   * The Main Camera of GameScene uses GateFit = Vertical, so the
    ///     vertical FOV (60 deg) is fixed and the horizontal extent shrinks
    ///     with the aspect ratio.
    ///   * At 2560x1664 (aspect 1.538) the visible half width at the judge
    ///     line was 3.567 while the outer lane note reaches x = -4.0, so
    ///     ~43 % of the outer notes were cut off.
    ///
    /// How it is fixed (runtime only - the scene file is not touched):
    /// the aspect the camera renders with is pinned to the larger of the
    /// screen aspect and the design aspect (16:9). Because the gate fit stays
    /// "Vertical", the **vertical** geometry does not change at all:
    ///
    ///   * the visible depth stays 8.6143, so the z scale and the designed
    ///     1.6 s approach time are untouched;
    ///   * the notes still cross the judge line (z = 0) exactly at their own
    ///     judge time, so the judgement is bit-identical.
    ///
    /// The whole scene is squeezed horizontally by the same ratio, so the
    /// picture and the notes keep their relative position (the chart is
    /// authored against the video).
    /// </summary>
    public static class PlayfieldCameraFit
    {
        /// <summary>
        /// The aspect the playfield was designed for (16:9). The lane span
        /// (+-4.0) plus the note width (1.0) fits into it.
        /// </summary>
        public const float DesignAspect = 16f / 9f;

        /// <summary>
        /// Half width that has to be visible at the judge line: the outer
        /// edge of the outer lane note (4.0) plus a small margin.
        /// </summary>
        public const float RequiredHalfWidth = 4.1f;

        /// <summary>The aspect the camera has to render with.</summary>
        public static float FittedAspect(float screenAspect)
        {
            if (screenAspect <= 0f) return DesignAspect;
            return Mathf.Max(screenAspect, DesignAspect);
        }

        /// <summary>The aspect of the screen the game is running on.</summary>
        public static float ScreenAspect()
        {
            return (float)Screen.width / Mathf.Max(1, Screen.height);
        }

        /// <summary>
        /// Apply the fit to the camera and return the aspect it renders with.
        /// </summary>
        public static float Apply(Camera camera, float screenAspect)
        {
            if (camera == null) return 0f;

            float aspect = FittedAspect(screenAspect);
            if (!Mathf.Approximately(camera.aspect, aspect)) camera.aspect = aspect;
            return aspect;
        }

        /// <summary>
        /// Apply the fit to the game camera. Safe to call every frame (the
        /// value only changes when the screen resolution changes).
        /// </summary>
        public static void EnsureApplied()
        {
            Camera camera = Camera.main;
            if (camera == null) return;
            Apply(camera, ScreenAspect());
        }
    }
}
