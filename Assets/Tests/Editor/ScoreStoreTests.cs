using System.Collections.Generic;
using FlashBeat.Persistence;
using NUnit.Framework;

namespace FlashBeat.Tests.Editor
{
    /// <summary>
    /// The best scores of the player (issue #189).
    ///
    /// The scores used to live in a fixed array that was indexed by the
    /// position of the song in the catalog. The catalog is dynamic, so a song
    /// that was added shifted the positions and the record of one song ended
    /// up on another one.
    ///
    /// These tests pin the new model: a record belongs to a stable song id
    /// and a chart version, and neither adding, reordering nor removing a
    /// song moves it.
    /// </summary>
    public class ScoreStoreTests
    {
        IScoreStorage previousStorage;
        MemoryScoreStorage storage;

        [SetUp]
        public void SetUp()
        {
            // A pristine, in-memory save file for every test: the run must
            // not read the save data of the player, and the tests must not
            // leak their scores into each other.
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

        // --- the key --------------------------------------------------------

        [Test]
        public void TheKeyOfABuiltInSongDoesNotDependOnItsPositionInTheCatalog()
        {
            var song = new SongData(7, "ロストアンブレラ", "稲葉曇", "DeKLpgzh-qQ", 274, 8, 268, 89f);

            ScoreKey atSeven = ScorePersistence.KeyOf(song, 7);
            ScoreKey atForty = ScorePersistence.KeyOf(song, 40);

            Assert.AreEqual(atSeven.songId, atForty.songId,
                "the stable id of a song must not depend on its position");
            Assert.AreEqual(atSeven.chartVersion, atForty.chartVersion);
            Assert.AreEqual(atSeven, atForty);
        }

        [Test]
        public void TheKeyOfACloudSongIsItsCloudId()
        {
            var song = new SongData(3, "Who", "Azari", "8JXiXt0D6tw", 128, 5, 139, 68f)
            {
                isRemote = true,
                remoteId = "who",
                chartVersion = 3,
            };

            ScoreKey key = ScorePersistence.KeyOf(song, 3);

            Assert.AreEqual("who", key.songId);
            Assert.AreEqual(3, key.chartVersion);
        }

        [Test]
        public void TheKeyOfACloudSongCarriesTheChartVersion()
        {
            var song = new SongData(3, "Who", "Azari", "8JXiXt0D6tw", 128, 5, 139, 68f)
            {
                isRemote = true,
                remoteId = "who",
                chartVersion = 4,
            };

            ScoreKey before = ScorePersistence.KeyOf(song, 3);
            song.chartVersion = 5;
            ScoreKey after = ScorePersistence.KeyOf(song, 3);

            Assert.AreNotEqual(before, after,
                "a new chart version must be a new key: the record of the old chart must not carry over");
        }

        [Test]
        public void AKeyIsWrittenAndReadBackWithoutLosingAnything()
        {
            var key = new ScoreKey("who", 12);

            ScoreKey parsed;
            bool ok = ScoreKey.TryParse(key.ToStorageString(), out parsed);

            Assert.IsTrue(ok);
            Assert.AreEqual(key, parsed);
        }

        [Test]
        public void ABrokenKeyIsNotReadAsAKey()
        {
            ScoreKey key;
            Assert.IsFalse(ScoreKey.TryParse("", out key), "an empty text is not a key");
            Assert.IsFalse(ScoreKey.TryParse("who", out key), "a text without a version is not a key");
            Assert.IsFalse(ScoreKey.TryParse("who@abc", out key), "a non numeric version is not a key");
            Assert.IsFalse(ScoreKey.TryParse("who@-1", out key), "a negative version is not a key");
        }

        // --- the store -------------------------------------------------------

        [Test]
        public void TheStoreKeepsTheBestScoreOfEveryKey()
        {
            var store = new ScoreStore();
            var first = new ScoreKey("who", 1);
            var second = new ScoreKey("lost-umbrella", 1);

            Assert.IsTrue(store.Submit(first, 500000));
            Assert.IsFalse(store.Submit(first, 400000), "a lower score must not beat the record");
            Assert.IsTrue(store.Submit(second, 600000));

            Assert.AreEqual(500000, store.Get(first));
            Assert.AreEqual(600000, store.Get(second));
            Assert.AreEqual(2, store.RecordedSongCount);
        }

        [Test]
        public void AnUnknownKeyHasNoRecord()
        {
            var store = new ScoreStore();

            Assert.AreEqual(0, store.Get(new ScoreKey("nothing", 1)));
        }

        [Test]
        public void TheStoreSurvivesASaveAndLoad()
        {
            var store = new ScoreStore();
            store.Submit(new ScoreKey("who", 2), 750000);
            store.Submit(new ScoreKey("lost-umbrella", 1), 320000);

            ScoreStore loaded = ScoreStore.Deserialize(store.Serialize());

            Assert.AreEqual(750000, loaded.Get(new ScoreKey("who", 2)));
            Assert.AreEqual(320000, loaded.Get(new ScoreKey("lost-umbrella", 1)));
        }

        [Test]
        public void TheUnmappedLegacySlotsSurviveASaveAndLoad()
        {
            var store = new ScoreStore();
            store.KeepUnmappedLegacySlot(4);
            store.KeepUnmappedLegacySlot(5);

            ScoreStore loaded = ScoreStore.Deserialize(store.Serialize());

            Assert.AreEqual(2, loaded.UnmappedLegacySlots.Count,
                "the unmapped slots are kept so that the data is not silently lost");
            CollectionAssert.AreEquivalent(new[] { 4, 5 }, loaded.UnmappedLegacySlots);
        }

        [Test]
        public void ABrokenSaveFileDoesNotCostTheOtherRecords()
        {
            var store = new ScoreStore();
            store.Submit(new ScoreKey("who", 2), 750000);

            string text = store.Serialize() + "broken-line-without-a-separator\n";

            ScoreStore loaded = ScoreStore.Deserialize(text);

            Assert.AreEqual(750000, loaded.Get(new ScoreKey("who", 2)),
                "a broken line must not cost the records of the other lines");
        }

        [Test]
        public void EverySongOfTheCatalogHasItsOwnRecord()
        {
            var store = new ScoreStore();
            var keys = new List<ScoreKey>();
            for (int i = 1; i <= 60; i++)
            {
                var key = new ScoreKey("song-" + i, 1);
                keys.Add(key);
                store.Submit(key, 100000 + i);
            }

            Assert.AreEqual(60, store.RecordedSongCount);
            for (int i = 0; i < keys.Count; i++)
            {
                Assert.AreEqual(100000 + i + 1, store.Get(keys[i]));
            }
        }

        // --- the migration ---------------------------------------------------

        [Test]
        public void TheLegacyCsvIsMigratedForTheBuiltInSongs()
        {
            var store = new ScoreStore();

            // Slot 1 = ロストアンブレラ, slot 2 = まにまに, slot 3 = Who.
            LegacyScoreMigration.Migrate(store, "0,900000,800000,700000", 4);

            Assert.AreEqual(900000, store.Get(new ScoreKey(ScoreKey.BuiltInSongKey(1), 0)));
            Assert.AreEqual(800000, store.Get(new ScoreKey(ScoreKey.BuiltInSongKey(2), 0)));
            Assert.AreEqual(700000, store.Get(new ScoreKey(ScoreKey.BuiltInSongKey(3), 0)));
        }

        [Test]
        public void TheLegacyCsvDoesNotGuessWhichRemoteSongAnUnknownSlotBelongedTo()
        {
            var store = new ScoreStore();

            // Slots 0-3 are the built-in songs, slot 4 used to hold a cloud
            // song. Which one is unknown, so the slot must not be handed to a
            // song: that is exactly the bug this issue is about.
            LegacyScoreMigration.Migrate(store, "0,900000,800000,700000,650000", 4);

            Assert.AreEqual(0, store.Get(new ScoreKey(ScoreKey.BuiltInSongKey(4), 0)),
                "the slot past the built-in table must not become the record of a built-in song");
            Assert.AreEqual(0, store.Get(new ScoreKey("who", 0)),
                "the slot of an unknown cloud song must not be handed to a cloud song either");
            Assert.Contains(4, (System.Collections.ICollection)new List<int>(store.UnmappedLegacySlots),
                "the unmapped slot must be kept so that the data is not silently lost");
        }

        [Test]
        public void TheLegacyCsvKeepsTheSlotsThatAreNotMapped()
        {
            var store = new ScoreStore();

            LegacyScoreMigration.Migrate(store, "0,100,0,0,200,300", 4);

            System.Collections.Generic.List<int> unmapped =
                new System.Collections.Generic.List<int>(store.UnmappedLegacySlots);
            Assert.AreEqual(2, unmapped.Count);
            Assert.Contains(4, unmapped);
            Assert.Contains(5, unmapped);
        }

        [Test]
        public void TheLegacyCsvWithABrokenElementMigratesTheOtherElements()
        {
            var store = new ScoreStore();

            LegacyScoreMigration.Migrate(store, "0,900000,broken,700000", 4);

            Assert.AreEqual(900000, store.Get(new ScoreKey(ScoreKey.BuiltInSongKey(1), 0)));
            Assert.AreEqual(0, store.Get(new ScoreKey(ScoreKey.BuiltInSongKey(2), 0)));
            Assert.AreEqual(700000, store.Get(new ScoreKey(ScoreKey.BuiltInSongKey(3), 0)));
        }

        [Test]
        public void TheLegacyCsvWithMoreElementsThanTheTableKeepsTheRest()
        {
            var store = new ScoreStore();

            LegacyScoreMigration.Migrate(store, "0,100,200,300,400,500,600", 4);

            Assert.AreEqual(100, store.Get(new ScoreKey(ScoreKey.BuiltInSongKey(1), 0)));
            Assert.AreEqual(3, store.UnmappedLegacySlots.Count);
        }

        [Test]
        public void TheMigrationIsIdempotent()
        {
            var store = new ScoreStore();
            LegacyScoreMigration.Migrate(store, "0,900000,800000", 3);

            // A newer record must not be overwritten by the migration.
            store.Submit(new ScoreKey(ScoreKey.BuiltInSongKey(1), 0), 950000);
            LegacyScoreMigration.Migrate(store, "0,900000,800000", 3);

            Assert.AreEqual(950000, store.Get(new ScoreKey(ScoreKey.BuiltInSongKey(1), 0)));
            Assert.AreEqual(800000, store.Get(new ScoreKey(ScoreKey.BuiltInSongKey(2), 0)));
        }

        // --- the catalog (the scenarios of the issue) ------------------------

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
        public void AddingASongBeforeAnExistingOneKeepsTheExistingBestScores()
        {
            // The catalog of the issue report: only `zzz` is published.
            GManager.ApplyRemoteCatalog(CatalogWith("zzz", "ZZZ", 1));
            int zzzIndex = IndexOfRemote(GManager.ActiveSongs, "zzz");
            Assert.Greater(zzzIndex, 0, "the cloud song must be in the catalog");

            // The player beats `zzz`.
            ScorePersistence.SubmitForCatalogIndex(zzzIndex, 900000);

            // A second song is published whose id sorts **before** `zzz`. The
            // API orders the songs by id, so `aaa` comes first, and `zzz`
            // (appended at the end) moves to the next slot.
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

            int newAaaIndex = IndexOfRemote(GManager.ActiveSongs, "aaa");
            int newZzzIndex = IndexOfRemote(GManager.ActiveSongs, "zzz");

            Assert.AreEqual(900000, ScorePersistence.BestOf(newZzzIndex),
                "the record of `zzz` must stay on `zzz`");
            Assert.AreEqual(0, ScorePersistence.BestOf(newAaaIndex),
                "`aaa` has never been played: it must not inherit the record of `zzz`");
        }

        [Test]
        public void TheBestScoreOfEverySongSurvivesMoreSongsThanTheOldTable()
        {
            // The old table had 45 slots, and every song past it shared slot
            // 0. The new model has no limit.
            const int songCount = 60;

            var catalog = new List<SongData>();
            catalog.Add(new SongData(0, "noSong", "no", "", 0, 0, 0, 0f));
            for (int i = 1; i <= songCount; i++)
            {
                catalog.Add(new SongData(i, "Song " + i, "Tester", "vid-" + i, 120, 5, 100, 60f)
                {
                    isRemote = true,
                    remoteId = "song-" + i,
                    chartVersion = 1,
                });
            }
            GManager.ApplyRemoteCatalog(catalog.ToArray());

            for (int i = 1; i <= songCount; i++)
            {
                int index = IndexOfRemote(GManager.ActiveSongs, "song-" + i);
                Assert.Greater(index, 0, "song-" + i + " must be in the catalog");
                ScorePersistence.SubmitForCatalogIndex(index, 100000 + i);
            }

            for (int i = 1; i <= songCount; i++)
            {
                int index = IndexOfRemote(GManager.ActiveSongs, "song-" + i);
                Assert.AreEqual(100000 + i, ScorePersistence.BestOf(index),
                    "song-" + i + " must keep its own record");
            }
        }
    }
}
