using FlashBeat.Persistence;
using NUnit.Framework;

namespace FlashBeat.Tests.Editor
{
    /// <summary>
    /// The catalog and the best scores belong together (issue #189).
    ///
    /// These tests drive the real catalog (`GManager.ApplyRemoteCatalog`)
    /// and the real persistence (`ScorePersistence`), so they pin the
    /// behaviour and not an implementation detail. The scenarios of the
    /// issue report themselves are pinned in `ScoreStoreTests`; this fixture
    /// covers what surrounds them: the legacy view that the screens still
    /// read, and the rules for the save file (the legacy key is never
    /// written, the migration runs once).
    /// </summary>
    public class ScoreCatalogIntegrityTests
    {
        IScoreStorage previousStorage;
        MemoryScoreStorage storage;

        [SetUp]
        public void SetUp()
        {
            // A pristine, in-memory save file for every test.
            previousStorage = ScorePersistence.Storage;
            storage = new MemoryScoreStorage();
            ScorePersistence.Storage = storage;
            ScorePersistence.Unload();
            GManager.ClearRemoteCatalog();
            for (int i = 0; i < GManager.Hiscore.Length; i++)
            {
                GManager.Hiscore[i] = 0;
            }
        }

        [TearDown]
        public void TearDown()
        {
            ScorePersistence.Unload();
            GManager.ClearRemoteCatalog();
            for (int i = 0; i < GManager.Hiscore.Length; i++)
            {
                GManager.Hiscore[i] = 0;
            }
            ScorePersistence.Storage = previousStorage;
        }

        /// <summary>
        /// The cloud snapshot of the issue report: one song, `zzz`. The video
        /// id is one no built-in song has, so the cloud song is **appended**
        /// to the catalog (not merged into a local one) and its position
        /// depends on what else is published — the bug of the issue report.
        /// </summary>
        static SongData[] CatalogWith(string id, string title, int chartVersion)
        {
            return new[]
            {
                new SongData(0, "noSong", "no", "", 0, 0, 0, 0f),
                new SongData(1, title, "Tester", "vid-" + id, 120, 5, 100, 60f)
                {
                    isRemote = true,
                    remoteId = id,
                    chartVersion = chartVersion,
                },
            };
        }

        /// <summary>The index of a song of the merged catalog, by its cloud id.</summary>
        static int IndexOfRemote(SongData[] songs, string remoteId)
        {
            for (int i = 0; i < songs.Length; i++)
            {
                if (songs[i] != null && songs[i].remoteId == remoteId) return i;
            }
            return -1;
        }

        [Test]
        public void RemovingASongDoesNotMoveTheRecordOfTheOtherOnes()
        {
            GManager.ApplyRemoteCatalog(new[]
            {
                new SongData(0, "noSong", "no", "", 0, 0, 0, 0f),
                new SongData(1, "AAA", "Tester", "vid-aaa", 120, 5, 100, 60f)
                {
                    isRemote = true,
                    remoteId = "aaa",
                    chartVersion = 1,
                },
                new SongData(2, "ZZZ", "Tester", "vid-zzz", 120, 5, 100, 60f)
                {
                    isRemote = true,
                    remoteId = "zzz",
                    chartVersion = 1,
                },
            });

            int aaaIndex = IndexOfRemote(GManager.ActiveSongs, "aaa");
            int zzzIndex = IndexOfRemote(GManager.ActiveSongs, "zzz");
            ScorePersistence.SubmitForCatalogIndex(aaaIndex, 111111);
            ScorePersistence.SubmitForCatalogIndex(zzzIndex, 222222);

            // `aaa` is unpublished: the catalog shrinks and `zzz` moves up
            // into the slot `aaa` used to occupy.
            GManager.ApplyRemoteCatalog(CatalogWith("zzz", "ZZZ", 1));
            int zzzAfter = IndexOfRemote(GManager.ActiveSongs, "zzz");

            Assert.AreEqual(222222, ScorePersistence.BestOf(zzzAfter),
                "`zzz` must keep its record when another song disappears");
            Assert.AreEqual(111111, ScorePersistence.Current.Get(new ScoreKey("aaa", 1)),
                "the record of the removed song must not move to another song");
            Assert.AreNotEqual(111111, ScorePersistence.BestOf(zzzAfter),
                "`zzz` must not show the record of the removed song");
        }

        [Test]
        public void ANewChartVersionStartsFromZeroAndKeepsTheOldRecord()
        {
            GManager.ApplyRemoteCatalog(CatalogWith("who", "Who", 1));
            int index = IndexOfRemote(GManager.ActiveSongs, "who");
            ScorePersistence.SubmitForCatalogIndex(index, 800000);

            // The song is published again with a new chart.
            GManager.ApplyRemoteCatalog(CatalogWith("who", "Who", 2));
            int indexAfter = IndexOfRemote(GManager.ActiveSongs, "who");

            Assert.AreEqual(0, ScorePersistence.BestOf(indexAfter),
                "a new chart version is a new chart: its best score starts at 0");
            Assert.AreEqual(800000,
                ScorePersistence.Current.Get(new ScoreKey("who", 1)),
                "the record of the old chart version stays as history");
        }

        [Test]
        public void TheLegacyViewShowsTheRecordOfTheSongItDisplays()
        {
            GManager.ApplyRemoteCatalog(new[]
            {
                new SongData(0, "noSong", "no", "", 0, 0, 0, 0f),
                new SongData(1, "AAA", "Tester", "vid-aaa", 120, 5, 100, 60f)
                {
                    isRemote = true,
                    remoteId = "aaa",
                    chartVersion = 1,
                },
                new SongData(2, "ZZZ", "Tester", "vid-zzz", 120, 5, 100, 60f)
                {
                    isRemote = true,
                    remoteId = "zzz",
                    chartVersion = 1,
                },
            });

            int aaaIndex = IndexOfRemote(GManager.ActiveSongs, "aaa");
            int zzzIndex = IndexOfRemote(GManager.ActiveSongs, "zzz");
            ScorePersistence.SubmitForCatalogIndex(aaaIndex, 111111);
            ScorePersistence.SubmitForCatalogIndex(zzzIndex, 222222);

            // The catalog changes: the legacy view has to follow, so the
            // screen shows the record of the song it displays.
            GManager.ApplyRemoteCatalog(new[]
            {
                new SongData(0, "noSong", "no", "", 0, 0, 0, 0f),
                new SongData(1, "ZZZ", "Tester", "vid-zzz", 120, 5, 100, 60f)
                {
                    isRemote = true,
                    remoteId = "zzz",
                    chartVersion = 1,
                },
                new SongData(2, "AAA", "Tester", "vid-aaa", 120, 5, 100, 60f)
                {
                    isRemote = true,
                    remoteId = "aaa",
                    chartVersion = 1,
                },
            });

            int newZzzIndex = IndexOfRemote(GManager.ActiveSongs, "zzz");
            int newAaaIndex = IndexOfRemote(GManager.ActiveSongs, "aaa");

            Assert.AreEqual(222222, GManager.Hiscore[newZzzIndex],
                "the legacy slot of `zzz` must show the record of `zzz`");
            Assert.AreEqual(111111, GManager.Hiscore[newAaaIndex],
                "the legacy slot of `aaa` must show the record of `aaa`");
        }

        [Test]
        public void SavingDoesNotOverwriteTheLegacyHiscoreKey()
        {
            // A player upgrades from the old version: the legacy CSV is still
            // in the save data. The new format is written next to it, never
            // over it, so a rollback of this change still finds the old data.
            storage.SetString(ScoreStore.LegacyStorageKey, "0,900000,800000,700000");
            ScorePersistence.Load();

            GManager.ApplyRemoteCatalog(CatalogWith("zzz", "ZZZ", 1));
            int zzzIndex = IndexOfRemote(GManager.ActiveSongs, "zzz");
            ScorePersistence.SubmitForCatalogIndex(zzzIndex, 650000);

            Assert.AreEqual("0,900000,800000,700000",
                storage.GetString(ScoreStore.LegacyStorageKey, ""),
                "the legacy key is only read by the migration, never written");
            Assert.AreEqual(650000, ScorePersistence.BestOf(zzzIndex),
                "the new record is stored under its stable key");
        }

        [Test]
        public void TheLegacyDataIsMigratedOnlyOnce()
        {
            // The first load migrates the legacy CSV.
            storage.SetString(ScoreStore.LegacyStorageKey, "0,900000");
            ScorePersistence.Load();
            Assert.AreEqual(900000,
                ScorePersistence.Current.Get(new ScoreKey(ScoreKey.BuiltInSongKey(1), 0)),
                "the record of the built-in song is migrated");

            // The player resets that one record and it is saved.
            ScorePersistence.Current.Remove(new ScoreKey(ScoreKey.BuiltInSongKey(1), 0));
            ScorePersistence.Save();
            ScorePersistence.Unload();

            // The next load must not resurrect the old CSV value: the
            // migration runs once (the flag remembers that it ran).
            ScorePersistence.Load();
            Assert.AreEqual(0,
                ScorePersistence.Current.Get(new ScoreKey(ScoreKey.BuiltInSongKey(1), 0)),
                "the migration must not run a second time");
        }

        [Test]
        public void TheMigratedRecordsSurviveALaunchWithoutASave()
        {
            // The migration happens on the load of the select screen. A
            // player who quits there (no result screen, no save) must still
            // find the migrated records on the next launch.
            storage.SetString(ScoreStore.LegacyStorageKey, "0,900000");
            ScorePersistence.Load();
            ScorePersistence.Unload();

            ScorePersistence.Load();
            Assert.AreEqual(900000,
                ScorePersistence.Current.Get(new ScoreKey(ScoreKey.BuiltInSongKey(1), 0)),
                "the migrated records are persisted by the migration itself");
        }
    }
}
