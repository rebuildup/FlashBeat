namespace FlashBeat.Gameplay
{
    /// <summary>
    /// Gameplay input gate shared by judgement and the lane-key click sound
    /// (Issue #604).
    ///
    /// True only while a play session is actually running:
    /// the song has started (<c>GManager.Start</c> — set by MusicManager when
    /// playback begins), the game is not paused (<see cref="PauseGate"/>), and
    /// the song-end sequence has not begun (<see cref="SongEndGate"/>).
    /// This is the same gate <see cref="Judge"/> applies in Update, so the
    /// click sound never fires before the song starts, while paused, or after
    /// the song end was shown.
    /// </summary>
    public static class GameplayInputGate
    {
        /// <summary>
        /// Whether lane-key input may produce gameplay feedback (judgement and
        /// click sound) this frame.
        /// </summary>
        /// <param name="gameStarted">GManager.Start — playback has begun.</param>
        /// <param name="isPaused">PauseGate.IsPaused.</param>
        /// <param name="songEndShown">SongEndGate.BlocksJudging.</param>
        public static bool CanAcceptInput(bool gameStarted, bool isPaused, bool songEndShown)
        {
            return gameStarted && SongEndGate.CanJudge(isPaused, songEndShown);
        }
    }
}
