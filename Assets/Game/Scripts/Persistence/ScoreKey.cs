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

        /// <summary>
        /// The scoring formula version the score was computed with
        /// (ADR-0028, Issue #577). 1 = the legacy formula, 2 = the v2 formula
        /// (combo continuation + no-miss bonus). v1 and v2 records are never
        /// overwritten or ranked together.
        /// </summary>
        public readonly int scoringVersion;

        public ScoreKey(string songId, int chartVersion, int scoringVersion = 1)
        {
            this.songId = songId ?? string.Empty;
            this.chartVersion = chartVersion;
            this.scoringVersion = scoringVersion;
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
        /// The format is `<songId>@<chartVersion>` for a v1 record and
        /// `<songId>@<chartVersion>@<scoringVersion>` for any other version
        /// (ADR-0028). The separator `@` cannot appear in a cloud song id
        /// (the API only accepts `^[a-z0-9-]{3,64}$`) nor in a built-in key,
        /// so the form is unambiguous. A bare two-part key reads as v1, which
        /// is what every record written before scoring v2 meant.
        /// </summary>
        public string ToStorageString()
        {
            string text = songId + "@" + chartVersion.ToString(CultureInfo.InvariantCulture);
            if (scoringVersion != 1)
            {
                text += "@" + scoringVersion.ToString(CultureInfo.InvariantCulture);
            }
            return text;
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

            // songId は '@' を含まないので、最初の '@' が songId と
            // バージョン部の境界。残りは "<chartVersion>" か
            // "<chartVersion>@<scoringVersion>"。
            int firstSeparator = text.IndexOf('@');
            if (firstSeparator <= 0 || firstSeparator >= text.Length - 1) return false;

            string songId = text.Substring(0, firstSeparator);
            string rest = text.Substring(firstSeparator + 1);

            int secondSeparator = rest.IndexOf('@');
            string versionText = secondSeparator < 0 ? rest : rest.Substring(0, secondSeparator);
            string scoringText = secondSeparator < 0 ? null : rest.Substring(secondSeparator + 1);
            if (versionText.Length == 0 || (scoringText != null && scoringText.Length == 0))
            {
                return false;
            }

            int version;
            if (!int.TryParse(versionText, NumberStyles.Integer, CultureInfo.InvariantCulture, out version))
            {
                return false;
            }
            if (version < 0) return false;

            // 省略時は v1 (ADR-0028: 旧クライアント互換と同じ規則)。
            int scoringVersion = 1;
            if (scoringText != null
                && !int.TryParse(scoringText, NumberStyles.Integer, CultureInfo.InvariantCulture, out scoringVersion))
            {
                return false;
            }
            if (scoringVersion < 1) return false;

            key = new ScoreKey(songId, version, scoringVersion);
            return true;
        }

        public bool Equals(ScoreKey other)
        {
            return string.Equals(songId, other.songId, StringComparison.Ordinal)
                && chartVersion == other.chartVersion
                && scoringVersion == other.scoringVersion;
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
                hash = (hash * 397) ^ chartVersion;
                return (hash * 397) ^ scoringVersion;
            }
        }

        public override string ToString()
        {
            return ToStorageString();
        }
    }
}
