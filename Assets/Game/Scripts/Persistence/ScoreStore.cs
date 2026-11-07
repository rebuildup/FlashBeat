using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace FlashBeat.Persistence
{
    /// <summary>
    /// The best scores of the player, keyed by a stable song id and a chart
    /// version (issue #189).
    ///
    /// The scores used to live in a fixed array that was indexed by the
    /// position of the song in the catalog. The catalog is dynamic (the cloud
    /// catalog appends unknown songs and the API orders the songs by id), so
    /// the index of a song could change between two launches: the best score
    /// of one song then showed up on another one, and the songs past the end
    /// of the fixed array all shared slot 0.
    ///
    /// This store replaces that model:
    ///
    ///   - a score belongs to a <see cref="ScoreKey"/> (stable song id +
    ///     chart version), never to a position;
    ///   - the old save data is migrated **once**, and only for the songs
    ///     whose built-in slot is known. A slot that belongs to a cloud song
    ///     is **not** guessed: it is kept aside instead of being handed to a
    ///     song it may not belong to.
    /// </summary>
    public class ScoreStore
    {
        /// <summary>The PlayerPrefs key of the new format.</summary>
        public const string StorageKey = "FlashBeat.ScoreStore";

        /// <summary>The PlayerPrefs key of the legacy format (index based CSV).</summary>
        public const string LegacyStorageKey = "Hiscore";

        /// <summary>
        /// The PlayerPrefs key of the flag that says the legacy data has been
        /// migrated. Without it the migration would run again on every
        /// launch and would overwrite the new scores with the old ones.
        /// </summary>
        public const string MigrationFlagKey = "FlashBeat.ScoreStore.LegacyMigrated";

        /// <summary>
        /// The version of the storage format. Bumped when the meaning of the
        /// saved data changes, so an old file is not read as a new one.
        /// </summary>
        public const int FormatVersion = 1;

        /// <summary>The best score of every key.</summary>
        readonly Dictionary<ScoreKey, int> scores = new Dictionary<ScoreKey, int>();

        /// <summary>
        /// The legacy slots that could not be migrated.
        ///
        /// They are the slots of the old fixed array that do not belong to a
        /// built-in song (the cloud songs used to be appended to the array).
        /// Nothing is guessed about them; they are kept so that the data is
        /// not silently lost, and a later version can map them once the
        /// mapping is known.
        /// </summary>
        readonly List<int> unmappedLegacySlots = new List<int>();

        /// <summary>The best score of a key (0 when the key is unknown).</summary>
        public int Get(ScoreKey key)
        {
            int score;
            return scores.TryGetValue(key, out score) ? score : 0;
        }

        /// <summary>
        /// The best score of a key, updated when the new score is higher.
        ///
        /// Returns true when the record was beaten.
        /// </summary>
        public bool Submit(ScoreKey key, int score)
        {
            if (score <= 0) return false;

            int current;
            if (scores.TryGetValue(key, out current) && current >= score)
            {
                return false;
            }

            scores[key] = score;
            return true;
        }

        /// <summary>Forget the record of a key.</summary>
        public void Remove(ScoreKey key)
        {
            scores.Remove(key);
        }

        /// <summary>Forget every record.</summary>
        public void Clear()
        {
            scores.Clear();
            unmappedLegacySlots.Clear();
        }

        /// <summary>The number of songs that have a record.</summary>
        public int RecordedSongCount
        {
            get { return scores.Count; }
        }

        /// <summary>The legacy slots that could not be mapped to a song.</summary>
        public IReadOnlyList<int> UnmappedLegacySlots
        {
            get { return unmappedLegacySlots; }
        }

        /// <summary>
        /// Remember a legacy slot that could not be mapped to a song.
        ///
        /// The slot is kept so that the data is not silently lost. Nothing is
        /// guessed about the song it belonged to.
        /// </summary>
        public void KeepUnmappedLegacySlot(int slot)
        {
            if (slot < 0) return;
            if (!unmappedLegacySlots.Contains(slot))
            {
                unmappedLegacySlots.Add(slot);
            }
        }

        /// <summary>
        /// The best score of every key, as a copy that can be enumerated.
        /// </summary>
        public IEnumerable<KeyValuePair<ScoreKey, int>> Entries
        {
            get { return scores; }
        }

        /// <summary>
        /// Write the store into a text form.
        ///
        /// The format is line based: the first line is the format version,
        /// every other line is `<songId>@<chartVersion>=<score>`. A simple
        /// line format keeps the file readable and easy to repair by hand.
        /// </summary>
        public string Serialize()
        {
            var builder = new StringBuilder();
            builder.Append(FormatVersion.ToString(CultureInfo.InvariantCulture));
            builder.Append('\n');

            var keys = new List<ScoreKey>(scores.Keys);
            keys.Sort((a, b) => string.CompareOrdinal(a.ToStorageString(), b.ToStorageString()));
            foreach (ScoreKey key in keys)
            {
                builder.Append(key.ToStorageString());
                builder.Append('=');
                builder.Append(scores[key].ToString(CultureInfo.InvariantCulture));
                builder.Append('\n');
            }

            foreach (int slot in unmappedLegacySlots)
            {
                builder.Append("legacy=");
                builder.Append(slot.ToString(CultureInfo.InvariantCulture));
                builder.Append('\n');
            }

            return builder.ToString();
        }

        /// <summary>
        /// Read a store back from its text form.
        ///
        /// A line that cannot be read is skipped instead of aborting the
        /// whole load: a corrupted save file must not cost the player every
        /// record.
        /// </summary>
        public static ScoreStore Deserialize(string text)
        {
            var store = new ScoreStore();
            if (string.IsNullOrEmpty(text)) return store;

            string[] lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0) continue;

                // The first line is the format version.
                if (i == 0)
                {
                    int version;
                    if (!int.TryParse(line, NumberStyles.Integer, CultureInfo.InvariantCulture, out version))
                    {
                        continue;
                    }
                    if (version > FormatVersion)
                    {
                        // A file of a newer format is not understood. Keep it
                        // untouched instead of guessing its meaning.
                        return store;
                    }
                    continue;
                }

                int separator = line.LastIndexOf('=');
                if (separator <= 0 || separator >= line.Length - 1) continue;

                string keyText = line.Substring(0, separator);
                string scoreText = line.Substring(separator + 1);

                if (line.StartsWith("legacy=", StringComparison.Ordinal))
                {
                    int slot;
                    if (int.TryParse(scoreText, NumberStyles.Integer, CultureInfo.InvariantCulture, out slot)
                        && !store.unmappedLegacySlots.Contains(slot))
                    {
                        store.unmappedLegacySlots.Add(slot);
                    }
                    continue;
                }

                ScoreKey key;
                int score;
                if (!ScoreKey.TryParse(keyText, out key)) continue;
                if (!int.TryParse(scoreText, NumberStyles.Integer, CultureInfo.InvariantCulture, out score)) continue;
                if (score <= 0) continue;

                store.scores[key] = score;
            }

            return store;
        }
    }
}
