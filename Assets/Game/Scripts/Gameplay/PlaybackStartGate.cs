namespace FlashBeat.Gameplay
{
    /// <summary>
    /// Keeps a Space press until the song can actually start (Issue #351).
    ///
    /// The GameScene HUD shows "Space to Start" from the moment the scene opens,
    /// but the media and the chart are still being prepared for a while. Before
    /// this gate, a press in that window was silently dropped, so the player saw
    /// the prompt, pressed Space and nothing happened. The gate remembers the
    /// press and starts the song on the first frame where the media and the
    /// notes are ready.
    /// </summary>
    public static class PlaybackStartGate
    {
        /// <summary>
        /// Records the press and returns true exactly once - on the first frame
        /// where <paramref name="canStart"/> is true after a press.
        /// </summary>
        /// <param name="canStart">The media and the notes are ready.</param>
        /// <param name="pressed">Space was pressed in this frame.</param>
        /// <param name="requested">The buffered request; the caller owns it.</param>
        public static bool ShouldStart(bool canStart, bool pressed, ref bool requested)
        {
            if (pressed)
            {
                requested = true;
            }

            if (!requested || !canStart)
            {
                return false;
            }

            requested = false;
            return true;
        }
    }
}
