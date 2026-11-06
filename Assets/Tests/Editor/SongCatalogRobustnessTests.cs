using System.Collections.Generic;
using NUnit.Framework;

namespace FlashBeat.Tests.Editor
{
    // Catalog integrity (#95): the parallel arrays are read fail-soft (a future
    // editing mistake must not throw IndexOutOfRange), the duplicate ニセモノ
    // placeholder rows stay in the catalog but are marked unplayable, and the
    // cloud noteCount is carried through the merge.
    public class SongCatalogRobustnessTests
    {
        static int IndexOf(SongData[] songs, string title)
        {
            for (int i = 0; i < songs.Length; i++)
            {
                if (songs[i] != null && songs[i].title == title) return i;
            }
            return -1;
        }

        [TearDown]
        public void ResetTheCatalog()
        {
            GManager.ClearRemoteCatalog();
        }

        // --- fail-soft parallel array reads -----------------------------------

        [Test]
        public void ParallelValue_ReturnsTheValueInsideTheArray()
        {
            Assert.AreEqual(8, GManager.ParallelValue(new[] { 7, 8, 9 }, 1, "SBPM"));
            Assert.AreEqual(2.5f, GManager.ParallelValue(new[] { 2.5f, 3.5f }, 0, "SongLong"), 0.001f);
            Assert.AreEqual("x", GManager.ParallelValue(new[] { "x" }, 0, "Musician"));
        }

        [Test]
        public void ParallelValue_FailsSoftForAShorterArray()
        {
            // The catalog has 29 songs; a numeric array with 28 entries must
            // not throw and must hand the song a fallback value instead.
            Assert.DoesNotThrow(() => GManager.ParallelValue(new[] { 7, 8, 9 }, 3, "SBPM"));
            Assert.AreEqual(0, GManager.ParallelValue(new[] { 7, 8, 9 }, 3, "SBPM"));
            Assert.AreEqual(0f, GManager.ParallelValue((float[])null, 0, "SongLong"), 0.001f);
            Assert.AreEqual("", GManager.ParallelValue((string[])null, 0, "Musician"));
        }

        [Test]
        public void ParallelValue_FailsSoftForANegativeIndex()
        {
            Assert.AreEqual(0, GManager.ParallelValue(new[] { 1, 2 }, -1, "SBPM"));
        }

        [Test]
        public void TheBuiltCatalogCarriesTheRealArrayValues()
        {
            // With the arrays in lockstep every value has to arrive intact -
            // the fail-soft reads must not silently blank out real songs.
            for (int i = 1; i < GManager.Songs.Length; i++)
            {
                Assert.AreEqual(GManager.SongName[i], GManager.Songs[i].title);
                Assert.AreEqual(GManager.Musician[i], GManager.Songs[i].musician);
                Assert.AreEqual(GManager.SongURL[i], GManager.Songs[i].videoId);
                Assert.AreEqual(GManager.SBPM[i], GManager.Songs[i].bpm);
                Assert.AreEqual(GManager.Slevel[i], GManager.Songs[i].level);
                Assert.AreEqual(GManager.Shit[i], GManager.Songs[i].totalHits);
                Assert.AreEqual(GManager.SongLong[i], GManager.Songs[i].duration);
            }
        }

        // --- unplayable placeholder rows ---------------------------------------

        [Test]
        public void TheDuplicateNisemonoRowsStayInTheCatalogButAreUnplayable()
        {
            // The rows must not be deleted: the indices of Songs and of the
            // parallel arrays must not shift.
            Assert.AreEqual("ニセモノ", GManager.SongName[27]);
            Assert.AreEqual("ニセモノ", GManager.SongName[28]);
            Assert.AreEqual(29, GManager.Songs.Length);

            Assert.IsTrue(GManager.Songs[27].unplayable, "SongName[27] must be marked unplayable");
            Assert.IsTrue(GManager.Songs[28].unplayable, "SongName[28] must be marked unplayable");
            Assert.IsTrue(GManager.Songs[0].unplayable, "the noSong placeholder has no chart either");
        }

        [Test]
        public void TheRealSongsStayPlayable()
        {
            var unplayable = new List<string>();
            for (int i = 1; i < GManager.Songs.Length; i++)
            {
                if (!GManager.Songs[i].unplayable) continue;
                if (GManager.SongName[i] != "ニセモノ") unplayable.Add($"{i}:{GManager.SongName[i]}");
            }
            Assert.IsEmpty(unplayable, "songs wrongly marked unplayable: " + string.Join(", ", unplayable));
        }

        // --- cloud noteCount carried through the merge -------------------------

        [Test]
        public void ParseCatalogCarriesTheCloudNoteCount()
        {
            string json = "{\"songs\":[{\"id\":\"note-count\",\"title\":\"ノート数\",\"artist\":\"作曲者\"," +
                          "\"youtubeId\":\"dQw4w9WgXcQ\",\"chartVersion\":2,\"noteCount\":321}]}";

            SongData[] songs = CloudCatalog.ParseCatalog(json);

            Assert.IsNotNull(songs);
            Assert.AreEqual(321, songs[1].noteCount, "the cloud noteCount must be kept on the SongData");
            Assert.AreEqual(2, songs[1].chartVersion, "the cloud chartVersion must be kept on the SongData");
        }

        [Test]
        public void TheMergeCarriesTheCloudNoteCountIntoEveryMergedSong()
        {
            var local = new[]
            {
                new SongData(0, "noSong", "no", "", 0, 0, 0, 0f),
                new SongData(1, "ロストアンブレラ", "稲葉曇", "DeKLpgzh-qQ", 274, 8, 268, 89f),
            };
            var remote = new[]
            {
                new SongData(0, "noSong", "no", "", 0, 0, 0, 0f),
                new SongData(1, "ロストアンブレラ", "稲葉曇", "DeKLpgzh-qQ", 274, 8, 0, 89f)
                {
                    isRemote = true,
                    remoteId = "lost-umbrella",
                    chartVersion = 4,
                    noteCount = 268,
                },
                new SongData(2, "未知の曲", "未知", "abcdefghijk", 120, 3, 0, 60f)
                {
                    isRemote = true,
                    remoteId = "unknown-song",
                    chartVersion = 2,
                    noteCount = 77,
                },
            };

            SongData[] merged = GManager.Merge(local, remote);

            Assert.AreEqual(3, merged.Length, "local songs stay, the unknown cloud song is appended");

            int umbrella = IndexOf(merged, "ロストアンブレラ");
            Assert.GreaterOrEqual(umbrella, 0);
            Assert.AreEqual(268, merged[umbrella].noteCount, "the cloud noteCount is carried into the merged song");
            Assert.AreEqual(4, merged[umbrella].chartVersion);

            int added = IndexOf(merged, "未知の曲");
            Assert.GreaterOrEqual(added, 0);
            Assert.AreEqual(77, merged[added].noteCount, "an appended cloud song keeps its noteCount");
        }

        [Test]
        public void TheMergeWithoutACloudNoteCountKeepsTheLocalValue()
        {
            var local = new[]
            {
                new SongData(0, "noSong", "no", "", 0, 0, 0, 0f),
                new SongData(1, "ロストアンブレラ", "稲葉曇", "DeKLpgzh-qQ", 274, 8, 268, 89f),
            };
            var remote = new[]
            {
                new SongData(0, "noSong", "no", "", 0, 0, 0, 0f),
                new SongData(1, "ロストアンブレラ", "稲葉曇", "DeKLpgzh-qQ", 274, 8, 0, 89f)
                {
                    isRemote = true,
                    remoteId = "lost-umbrella",
                    chartVersion = 4,
                },
            };

            SongData[] merged = GManager.Merge(local, remote);

            Assert.AreEqual(268, merged[1].noteCount, "a cloud entry without noteCount falls back to the local value");
        }

        [Test]
        public void TheMergedCatalogHasNoDuplicatePlayableVideoIds()
        {
            // A cloud entry that lists a song again under the same videoId must
            // not produce a second row. (The local catalog has one duplicated
            // videoId pair: the unplayable ニセモノ rows.)
            var remote = new[]
            {
                new SongData(0, "noSong", "no", "", 0, 0, 0, 0f),
                new SongData(1, "ロストアンブレラ", "稲葉曇", "DeKLpgzh-qQ", 274, 8, 268, 89f)
                {
                    isRemote = true,
                    remoteId = "lost-umbrella",
                    chartVersion = 4,
                    noteCount = 268,
                },
                new SongData(2, "ロストアンブレラ再掲", "稲葉曇", "DeKLpgzh-qQ", 274, 8, 268, 89f)
                {
                    isRemote = true,
                    remoteId = "lost-umbrella-again",
                    chartVersion = 4,
                    noteCount = 268,
                },
            };

            SongData[] merged = GManager.Merge(GManager.Songs, remote);

            Assert.AreEqual(GManager.Songs.Length, merged.Length,
                "a cloud song the local data knows must overlay it, not be appended again");

            var seen = new HashSet<string>();
            foreach (var song in merged)
            {
                if (song == null || song.unplayable || string.IsNullOrEmpty(song.videoId)) continue;
                Assert.IsTrue(seen.Add(song.videoId), $"duplicate videoId {song.videoId} in the merged catalog");
            }
        }

        [Test]
        public void TheCatalogKeepsStableIdsThroughTheMerge()
        {
            var remote = new[]
            {
                new SongData(0, "noSong", "no", "", 0, 0, 0, 0f),
                new SongData(1, "ロストアンブレラ", "稲葉曇", "DeKLpgzh-qQ", 274, 8, 268, 89f)
                {
                    isRemote = true,
                    remoteId = "lost-umbrella",
                    chartVersion = 4,
                    noteCount = 268,
                },
            };

            SongData[] merged = GManager.Merge(GManager.Songs, remote);

            Assert.AreEqual(GManager.Songs.Length, merged.Length);
            for (int i = 0; i < merged.Length; i++)
            {
                Assert.AreEqual(i, merged[i].id, $"merged[{i}].id must stay the catalog index");
            }
        }
    }
}
