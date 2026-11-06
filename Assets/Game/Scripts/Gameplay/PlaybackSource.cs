namespace FlashBeat.Gameplay
{
    /// <summary>
    /// Where the sound of the running song comes from.
    ///
    /// The evidence of a real playthrough has to show that the song was played
    /// from the YouTube media and not from a fallback file: the release build
    /// ships no third party media at all, so a run that only reaches
    /// <see cref="BundledWave"/> or <see cref="None"/> never had the song the
    /// chart belongs to.
    /// </summary>
    public enum PlaybackSource
    {
        /// <summary>No sound at all - the media could not be obtained or opened.</summary>
        None,

        /// <summary>
        /// The wav that ships with the chart. Only a development build has it;
        /// the release excludes third party media.
        /// </summary>
        BundledWave,

        /// <summary>One YouTube file that carries the picture and the sound.</summary>
        StreamCombined,

        /// <summary>The YouTube picture plus a separate YouTube sound track.</summary>
        StreamSeparate,
    }
}
