using System;
using System.Collections.Generic;
using System.Globalization;

namespace FlashBeat.Persistence
{
    /// <summary>
    /// The bridge between the <see cref="ScoreStore"/> and the save file
    /// (issue #189).
    ///
    /// It owns three things:
    ///
    ///   - the **save file** itself (`PlayerPrefs`, one key);
    ///   - the **migration** of the legacy index based data, which runs
    ///     exactly once (a flag in `PlayerPrefs` remembers that it ran);
    ///   - the **mapping** from a song of the catalog to its stable key.
    ///
    /// The mapping is the important part. The catalog is dynamic, so the
    /// position of a song is not an identity. The built-in songs keep their
    /// slot in the fixed built-in table (a constant of the game), and a cloud
    /// song is identified by its cloud song id.
    /// </summary>
    public static class ScorePersistence
    {
        /// <summary>
        /// The store of the running game. It is loaded once (by
        /// <see cref="Load"/>) and read and written from then on.
        /// </summary>
        static ScoreStore current;

        /// <summary>
        /// The built-in songs of the fixed table, in their order.
        ///
        /// The list is filled by <see cref="BindCatalog"/> and is used to
        /// tell a built-in song (slot in the fixed table) from a cloud song
        /// (appended at the end of the merged catalog).
        /// </summary>
        static readonly List<SongData> builtInSongs = new List<SongData>();

        /// <summary>
        /// Where the store is persisted. The game uses `PlayerPrefs`; the
        /// tests put an in-memory storage here, so a test run never touches
        /// the save data of the player and never leaks its scores into the
        /// next run.
        /// </summary>
        static IScoreStorage storage = new PlayerPrefsScoreStorage();

        /// <summary>The storage the store is read from and written to.</summary>
        public static IScoreStorage Storage
        {
            get { return storage; }
            set { storage = value != null ? value : (IScoreStorage)new PlayerPrefsScoreStorage(); }
        }

        /// <summary>The store of the running game (null before it is loaded).</summary>
        public static ScoreStore Current
        {
            get { return current; }
        }

        /// <summary>
        /// Remember the built-in songs of the fixed table.
        ///
        /// The order of the built-in table never changes, so the index of a
        /// built-in song inside it is a stable identity.
        /// </summary>
        public static void BindCatalog(SongData[] builtInTable)
        {
            builtInSongs.Clear();
            if (builtInTable == null) return;

            for (int i = 0; i < builtInTable.Length; i++)
            {
                if (builtInTable[i] == null) continue;
                if (!builtInTable[i].isRemote)
                {
                    builtInSongs.Add(builtInTable[i]);
                }
            }
        }

        /// <summary>The number of songs of the fixed built-in table.</summary>
        public static int BuiltInSongCount
        {
            get { return builtInSongs.Count; }
        }

        /// <summary>
        /// The stable key of a song of the catalog.
        ///
        /// A cloud song is identified by its cloud song id, a built-in song
        /// by its slot in the fixed built-in table. `catalogIndex` is only
        /// used for the last-resort fallback of a song that has no identity
        /// at all; it never becomes the key of a real song.
        /// </summary>
        public static ScoreKey KeyOf(SongData song, int catalogIndex)
        {
            // A cloud song is its cloud song id (with the chart version, so
            // a new chart version is a new key, R4).
            if (song != null && !string.IsNullOrEmpty(song.remoteId))
            {
                int version = song.isRemote ? song.chartVersion : 0;
                return new ScoreKey(song.remoteId, version);
            }

            // A built-in song is its slot in the fixed built-in table (a
            // constant of the game, R2).
            EnsureBuiltInCatalogBound();
            int builtInSlot = BuiltInSlotOf(song);
            if (builtInSlot >= 0)
            {
                return new ScoreKey(ScoreKey.BuiltInSongKey(builtInSlot), 0);
            }

            // A song with no cloud id that is not in the built-in table: the
            // video id is the only stable identity it has. The position in
            // the catalog is never used as an identity while a stable one
            // exists (R1).
            if (song != null && !string.IsNullOrEmpty(song.videoId))
            {
                return new ScoreKey(VideoSongKey(song.videoId), 0);
            }

            // No identity at all (a null song, an out of range index): the
            // position is the last resort, so that the key is at least
            // deterministic. No legacy slot is ever mapped this way — the
            // migration only maps the slots of the built-in table — so
            // nothing is guessed about a real song (R5).
            return new ScoreKey(UnknownSongKey(catalogIndex), 0);
        }

        /// <summary>The songId of a song that is only known by its video id.</summary>
        public static string VideoSongKey(string videoId)
        {
            return "yt:" + videoId;
        }

        /// <summary>
        /// The songId of a song that has no identity at all. It can never
        /// collide with the key of a real song (cloud ids, built-in keys and
        /// video id keys all use other prefixes).
        /// </summary>
        public static string UnknownSongKey(int catalogIndex)
        {
            return "unknown:" + catalogIndex.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// The stable key of the song at the given position of the catalog.
        /// </summary>
        public static ScoreKey KeyOfCatalogIndex(int catalogIndex)
        {
            SongData song = CatalogSongAt(catalogIndex);
            return KeyOf(song, catalogIndex);
        }

        /// <summary>
        /// The slot of a song in the fixed built-in table, or -1 when the
        /// song is not a built-in one (a cloud song, a song without any
        /// identity). No position is guessed for a song that is not in the
        /// table: the caller decides what a -1 means.
        /// </summary>
        public static int BuiltInSlotOf(SongData song)
        {
            if (song == null) return -1;
            if (!string.IsNullOrEmpty(song.remoteId)) return -1;

            for (int i = 0; i < builtInSongs.Count; i++)
            {
                if (ReferenceEquals(builtInSongs[i], song)) return i;
                if (builtInSongs[i] != null && builtInSongs[i].id == song.id
                    && builtInSongs[i].title == song.title)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Make sure the fixed built-in table is known.
        ///
        /// The migration maps a legacy slot to the built-in song of that
        /// slot, so the table has to be bound before it runs. `GManager`
        /// binds it while its static catalog is built; reading
        /// `GManager.Songs` here makes the mapping available even when the
        /// store is loaded before anything else touched the catalog.
        /// </summary>
        static void EnsureBuiltInCatalogBound()
        {
            if (builtInSongs.Count == 0)
            {
                BindCatalog(GManager.Songs);
            }
        }

        /// <summary>
        /// The song at the given position of the merged catalog, or null.
        /// </summary>
        static SongData CatalogSongAt(int catalogIndex)
        {
            SongData[] songs = GManager.ActiveSongs;
            if (songs == null) return null;
            if (catalogIndex < 0 || catalogIndex >= songs.Length) return null;
            return songs[catalogIndex];
        }

        /// <summary>
        /// Load the store and migrate the legacy data when it is there.
        ///
        /// The migration runs **once**: the flag in the storage remembers
        /// that it ran, so a later launch does not overwrite the new scores
        /// with the old ones.
        /// </summary>
        public static ScoreStore Load()
        {
            if (current == null)
            {
                current = ScoreStore.Deserialize(storage.GetString(ScoreStore.StorageKey, ""));
            }

            MigrateLegacyIfNeeded();
            return current;
        }

        /// <summary>
        /// Migrate the legacy data, once.
        ///
        /// The legacy key is **not** deleted or written: it stays readable,
        /// so a rollback of this change still finds the old data (R3).
        /// </summary>
        public static void MigrateLegacyIfNeeded()
        {
            if (current == null) return;
            EnsureBuiltInCatalogBound();
            if (storage.GetInt(ScoreStore.MigrationFlagKey, 0) != 0) return;

            string legacy = storage.GetString(ScoreStore.LegacyStorageKey, "");
            if (!string.IsNullOrEmpty(legacy))
            {
                LegacyScoreMigration.Migrate(current, legacy, BuiltInSongCount);

                // The migrated records are persisted right away. The
                // migration runs once, so a launch that never reaches a save
                // (the player starts the game and quits) must not lose them
                // on the next one.
                Save();

                // The flag is set only when legacy data was really consumed:
                // with no legacy file there is nothing to protect, and a
                // file that shows up later still gets migrated.
                storage.SetInt(ScoreStore.MigrationFlagKey, 1);
                storage.Save();
            }
        }

        /// <summary>
        /// Write the store to the save file.
        /// </summary>
        public static void Save()
        {
            if (current == null) return;

            storage.SetString(ScoreStore.StorageKey, current.Serialize());
            storage.Save();
        }

        /// <summary>
        /// Forget every record, in the store and in the save file.
        ///
        /// The legacy key is deleted as well, so a later migration cannot
        /// bring the old scores back (this is the "リセット" of the option
        /// screen).
        /// </summary>
        public static void ResetAll()
        {
            if (current != null) current.Clear();

            storage.DeleteKey(ScoreStore.StorageKey);
            storage.DeleteKey(ScoreStore.LegacyStorageKey);
            storage.SetInt(ScoreStore.MigrationFlagKey, 1);
            storage.Save();
        }

        /// <summary>
        /// Forget the store of the running game (the save file is kept).
        ///
        /// Used by the tests and by a scene change that has to start over.
        /// </summary>
        public static void Unload()
        {
            current = null;
            builtInSongs.Clear();
        }

        /// <summary>
        /// The best score of a song of the catalog.
        /// </summary>
        public static int BestOf(int catalogIndex)
        {
            if (current == null) Load();
            return current.Get(KeyOfCatalogIndex(catalogIndex));
        }

        /// <summary>
        /// Submit a score for the song at the given position of the catalog.
        ///
        /// Returns true when the record was beaten.
        /// </summary>
        public static bool SubmitForCatalogIndex(int catalogIndex, int score)
        {
            if (current == null) Load();
            bool beaten = current.Submit(KeyOfCatalogIndex(catalogIndex), score);
            if (beaten) Save();
            return beaten;
        }

        /// <summary>
        /// Copy the records of the stable store into the legacy array.
        ///
        /// The legacy array (<c>GManager.Hiscore</c>) is indexed by the
        /// position of the song in the catalog and is still what the select
        /// screen and the result screen read (they belong to another owner).
        /// The stable store is the truth; this method brings the legacy view
        /// in line with it, so the screen shows the record of the song it
        /// displays and not the record of another one.
        ///
        /// The legacy array is only a **view**: it is written from the store
        /// and never the other way round, so a song that moves in the catalog
        /// does not drag its record along.
        /// </summary>
        public static void SyncLegacyView()
        {
            if (current == null) Load();

            int[] legacy = GManager.Hiscore;
            if (legacy == null) return;

            SongData[] songs = GManager.ActiveSongs;
            int count = Math.Min(legacy.Length, songs == null ? 0 : songs.Length);

            for (int index = 0; index < count; index++)
            {
                ScoreKey key = KeyOf(songs[index], index);
                int best = current.Get(key);

                // The legacy slot of a cloud song is meaningless (the cloud
                // songs used to share the slots of the fixed array), so the
                // record of a cloud song is only written into the legacy
                // array while it sits at the slot the player just played.
                legacy[index] = best;
            }
        }

        /// <summary>
        /// Copy the legacy array into the stable store.
        ///
        /// The legacy array is what the result screen writes to (it is owned
        /// by another issue), so this method is the bridge: every slot is
        /// submitted under the **stable key** of the song that sits there.
        ///
        /// A slot that holds the record of a song that is not in the catalog
        /// any more is submitted under the key of the position it still
        /// occupies, so it stays reachable instead of being handed to another
        /// song.
        /// </summary>
        public static void SubmitLegacyView()
        {
            if (current == null) Load();

            int[] legacy = GManager.Hiscore;
            if (legacy == null) return;

            SongData[] songs = GManager.ActiveSongs;
            int count = Math.Min(legacy.Length, songs == null ? 0 : songs.Length);

            for (int index = 0; index < count; index++)
            {
                int score = legacy[index];
                if (score <= 0) continue;

                current.Submit(KeyOf(songs[index], index), score);
            }
        }
    }
}
