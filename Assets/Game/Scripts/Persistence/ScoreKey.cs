using System;
using System.Globalization;

namespace FlashBeat.Persistence
{
    /// <summary>
    /// The stable identity of a song and of one of its chart versions
    /// (issue #189).
    ///
    /// The best score used to be stored in a fixed array that was indexed by
    /// the position of the song in the catalog. Adding a song (the cloud
    /// catalog appends the songs it does not know at the end, and the API
    /// orders the songs by id, so a song published later can land *before* an
    /// existing one) shifted every later index, and the best score of one
    /// song ended up on another one.
    ///
    /// A <see cref="ScoreKey"/> is built from two things that never move:
    ///
    ///   - the **stable song id**: the cloud song id (`remoteId`) of a
    ///     published song, or the built-in key of a song that ships with the
    ///     game (`builtin:<n>`, see <see cref="BuiltInSongKey"/>).
    ///   - the **chart version**: the version of the chart the score was
    ///     played on. A new chart version is a new chart, so its best score
    ///     starts at 0 instead of inheriting the record of the old one.
    /// </summary>
    [Serializable]
    public struct ScoreKey : IEquatable<ScoreKey>
    {
        /// <summary>The stable song id (a cloud song id or a built-in key).</summary>
        public readonly string songId;

        /// <summary>The chart version the score belongs to (0 = unknown).</summary>
        public readonly int chartVersion;

        public ScoreKey(string songId, int chartVersion)
        {
            this.songId = songId ?? string.Empty;
            this.chartVersion = chartVersion;
        }

        /// <summary>
        /// The stable key of a song that ships with the game.
        ///
        /// The built-in songs have no cloud id, so they get a key of their
        /// own. The number is the index of the song in the *fixed* built-in
        /// table, which never changes (the built-in table is a constant of the
        /// game; only the cloud snapshot is dynamic).
        /// </summary>
        public static string BuiltInSongKey(int builtInIndex)
        {
            return "builtin:" + builtInIndex.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// The text form of the key, as it is written to the save file.
        ///
        /// The format is `<songId>@<chartVersion>`. The separator `@` cannot
        /// appear in a cloud song id (the API only accepts `^[a-z0-9-]{3,64}$`)
        /// nor in a built-in key, so the form is unambiguous.
        /// </summary>
        public string ToStorageString()
        {
            return songId + "@" + chartVersion.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Read a key back from its text form.
        ///
        /// Returns false when the text is not a key of this format, so a
        /// corrupted save file cannot make the game read a wrong score.
        /// </summary>
        public static bool TryParse(string text, out ScoreKey key)
        {
            key = new ScoreKey(string.Empty, 0);
            if (string.IsNullOrEmpty(text)) return false;

            int separator = text.LastIndexOf('@');
            if (separator <= 0 || separator >= text.Length - 1) return false;

            string songId = text.Substring(0, separator);
            string versionText = text.Substring(separator + 1);

            int version;
            if (!int.TryParse(versionText, NumberStyles.Integer, CultureInfo.InvariantCulture, out version))
            {
                return false;
            }
            if (version < 0) return false;

            key = new ScoreKey(songId, version);
            return true;
        }

        public bool Equals(ScoreKey other)
        {
            return string.Equals(songId, other.songId, StringComparison.Ordinal)
                && chartVersion == other.chartVersion;
        }

        public override bool Equals(object obj)
        {
            return obj is ScoreKey && Equals((ScoreKey)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = songId != null ? songId.GetHashCode() : 0;
                return (hash * 397) ^ chartVersion;
            }
        }

        public override string ToString()
        {
            return ToStorageString();
        }
    }
}
