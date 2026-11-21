namespace FlashBeat.Gameplay
{
    /// <summary>
    /// The stage of the media pipeline. A failure log names the stage, so it is
    /// visible at once where the playback broke (stream resolution, download,
    /// video preparation, audio preparation).
    /// </summary>
    public enum MediaStage
    {
        Idle,
        ResolvingStream,
        DownloadingMedia,
        PreparingVideo,
        PreparingAudio,
        Ready,
        Playing,
        Failed,
    }

    /// <summary>Why the media pipeline did not deliver a playable song.</summary>
    public enum MediaFailure
    {
        None,

        /// <summary>yt-dlp could not tell which formats the video offers.</summary>
        StreamUnresolved,

        /// <summary>The media file could not be downloaded into the cache.</summary>
        MediaUnavailable,

        /// <summary>The file is there, but the player could not open it.</summary>
        VideoPrepareFailed,

        /// <summary>No sound track could be prepared next to the picture.</summary>
        AudioUnavailable,

        /// <summary>The sound track was opened, but stays silent.</summary>
        AudioSilent,

        /// <summary>Issue #632/#792: the chart data was rejected at the input boundary.</summary>
        ChartRejected,
    }

    /// <summary>
    /// What the game does when the media pipeline fails.
    ///
    /// A rhythm game needs the music: without a sound track the song cannot be
    /// played, so the failure is shown on the loading screen with retry and
    /// back-to-select actions instead of leaving the player in front of a
    /// silent song. A failure that still leaves a sound (the picture is
    /// missing, for example) is only logged - the song is playable.
    ///
    /// Issue #792: the old automatic return to the song list (a fade after a
    /// few seconds) was removed - it gave no explanation and was rejected in
    /// device review. The player now chooses: retry (reload GameScene) or
    /// back to the song list, both on the error view.
    ///
    /// The rules live here (and not in the MonoBehaviour) so they can be
    /// verified by EditMode tests.
    /// </summary>
    public static class PlaybackRecovery
    {
        /// <summary>
        /// Decides where the sound of the song comes from. The wanted source is
        /// the YouTube media (the merged file, else the separate audio track);
        /// the bundled wav is only a fallback for development builds. When the
        /// wav is missing and no media could be prepared the decision is
        /// <see cref="PlaybackSource.None"/> - it never throws, the caller
        /// starts the recovery instead.
        /// </summary>
        public static PlaybackSource DecideAudioSource(bool combined, bool videoReady, bool audioReady, bool hasBundledWave)
        {
            if (combined && videoReady) return PlaybackSource.StreamCombined;
            if (audioReady) return PlaybackSource.StreamSeparate;
            if (hasBundledWave) return PlaybackSource.BundledWave;
            return PlaybackSource.None;
        }

        /// <summary>
        /// True when the song cannot be played and the failure has to be shown
        /// (the error view offers retry / back-to-select; there is no automatic
        /// scene change anymore - Issue #792).
        /// </summary>
        public static bool IsUnplayable(MediaFailure failure, bool hasSound)
        {
            if (failure == MediaFailure.None) return false;
            return !hasSound;
        }

        /// <summary>Readable name of a stage (used in the log lines).</summary>
        public static string StageName(MediaStage stage)
        {
            switch (stage)
            {
                case MediaStage.ResolvingStream: return "resolving-stream";
                case MediaStage.DownloadingMedia: return "downloading-media";
                case MediaStage.PreparingVideo: return "preparing-video";
                case MediaStage.PreparingAudio: return "preparing-audio";
                case MediaStage.Ready: return "ready";
                case MediaStage.Playing: return "playing";
                case MediaStage.Failed: return "failed";
                default: return "idle";
            }
        }

        /// <summary>The reason of a failure in words.</summary>
        public static string Reason(MediaFailure failure)
        {
            switch (failure)
            {
                case MediaFailure.StreamUnresolved: return "the YouTube stream could not be resolved";
                case MediaFailure.MediaUnavailable: return "the media file could not be downloaded";
                case MediaFailure.VideoPrepareFailed: return "the media file could not be opened by the player";
                case MediaFailure.AudioUnavailable: return "no sound track could be prepared";
                case MediaFailure.AudioSilent: return "the sound track stays silent";
                case MediaFailure.ChartRejected: return "the chart data is invalid";
                default: return "no failure";
            }
        }

        /// <summary>
        /// The info log line of a failure. The level is the same as the
        /// UpdateChecker lines (Debug.Log), so the failure is visible in the
        /// player log of the exhibition machine.
        /// </summary>
        public static string FailureLog(MediaStage stage, MediaFailure failure, bool hasPicture)
        {
            return $"[MusicManager] playback failed at stage={StageName(stage)}: {Reason(failure)} " +
                   $"(picture={(hasPicture ? "ok" : "missing")})";
        }

        /// <summary>
        /// The message shown on the error view, so the player knows what
        /// happened and what the buttons do (Issue #792).
        /// </summary>
        public static string StartHint(MediaFailure failure)
        {
            return $"この曲を再生できません ({Reason(failure)})\n再試行するか、選曲画面へ戻ってください";
        }
    }
}
