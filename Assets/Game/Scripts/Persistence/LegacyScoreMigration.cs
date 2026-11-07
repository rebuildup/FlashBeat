using System;
using System.Collections.Generic;

namespace FlashBeat.Persistence
{
    /// <summary>
    /// The migration of the legacy, index based save data into the new
    /// key based store (issue #189).
    ///
    /// The legacy data is a CSV of scores, one per slot of the old fixed
    /// array. A slot carried the record of "the song at that position", so
    /// the slot itself carries no information about *which* song it was.
    ///
    /// The migration therefore uses the **only** mapping that is known:
    ///
    ///   - slots `0..builtInSongCount-1` belong to the built-in songs, whose
    ///     order is a constant of the game. They are migrated to the built-in
    ///     key of that song.
    ///   - every other slot used to hold a cloud song, and the cloud songs
    ///     were appended in the order the API returned them. There is no
    ///     reliable way to tell which song a slot belonged to, so the slot is
    ///     **not** migrated: guessing would hand the record of one song to
    ///     another one, which is exactly the bug this issue is about.
    ///
    /// The unmapped slots are kept in the store so that the data is not
    /// silently dropped; a later version can map them once the mapping is
    /// known.
    /// </summary>
    public static class LegacyScoreMigration
    {
        /// <summary>
        /// The legacy CSV of the scores, as it was written by the old
        /// `SaveLoadManager`.
        /// </summary>
        public static List<int> ParseLegacyCsv(string csv)
        {
            var scores = new List<int>();
            if (string.IsNullOrEmpty(csv)) return scores;

            string[] parts = csv.Split(',');
            foreach (string part in parts)
            {
                string text = part.Trim();
                if (text.Length == 0)
                {
                    scores.Add(0);
                    continue;
                }

                int value;
                if (!int.TryParse(text, System.Globalization.NumberStyles.Integer,
                        System.Globalization.CultureInfo.InvariantCulture, out value))
                {
                    // A broken element is kept as 0 so that the other
                    // elements of the row still migrate.
                    value = 0;
                }
                scores.Add(value);
            }

            return scores;
        }

        /// <summary>
        /// Migrate the legacy scores into a store.
        ///
        /// `builtInSongCount` is the number of songs of the fixed built-in
        /// table. Only the slots below it are migrated; the others are kept
        /// as unmapped slots.
        ///
        /// The migration is **idempotent**: migrating the same legacy data
        /// twice into the same store does not change anything, because a
        /// migrated slot never overwrites a score that is already there.
        /// </summary>
        public static void Migrate(ScoreStore store, List<int> legacyScores, int builtInSongCount)
        {
            if (store == null || legacyScores == null) return;
            if (builtInSongCount < 0) builtInSongCount = 0;

            for (int slot = 0; slot < legacyScores.Count; slot++)
            {
                int score = legacyScores[slot];
                if (score <= 0) continue;

                if (slot < builtInSongCount)
                {
                    // A built-in song: the order of the built-in table is a
                    // constant of the game, so the slot is known.
                    var key = new ScoreKey(ScoreKey.BuiltInSongKey(slot), 0);
                    int current = store.Get(key);
                    if (score > current) store.Submit(key, score);
                    continue;
                }

                // A cloud song: the slot does not say which song it was.
                // The score is kept aside instead of being guessed.
                store.KeepUnmappedLegacySlot(slot);
            }
        }

        /// <summary>
        /// Migrate the legacy CSV into a store.
        /// </summary>
        public static void Migrate(ScoreStore store, string legacyCsv, int builtInSongCount)
        {
            Migrate(store, ParseLegacyCsv(legacyCsv), builtInSongCount);
        }
    }
}
