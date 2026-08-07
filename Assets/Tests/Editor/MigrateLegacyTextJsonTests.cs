using System.IO;
using NUnit.Framework;
using NoteEditor.DTO;
using NoteEditor.Editor;

namespace FlashBeat.Tests.Editor
{
    public class MigrateLegacyTextJsonTests
    {
        string tempDir;

        [SetUp]
        public void SetUp()
        {
            tempDir = Path.Combine(Path.GetTempPath(), "flashbeat_migrate_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }

        [Test]
        public void Migrate_MergesTextIntoChartAndDeletesTextFile()
        {
            var chartJson = "{\"name\":\"AAA\",\"maxBlock\":8,\"BPM\":120,\"offset\":0,\"notes\":[]}";
            var textJson = "{\"StartTime\":[1.0,2.0],\"Furigana\":[\"a\",\"b\"]," +
                           "\"Mondai\":[\"c\",\"d\"],\"romaji\":[\"e\",\"f\"]," +
                           "\"EndTime\":[1.5,2.5]}";
            File.WriteAllText(Path.Combine(tempDir, "AAA.json"), chartJson);
            File.WriteAllText(Path.Combine(tempDir, "AAA_text.json"), textJson);

            MigrateLegacyTextJson.MigrateForTest(tempDir);

            Assert.IsFalse(File.Exists(Path.Combine(tempDir, "AAA_text.json")));
            Assert.IsTrue(File.Exists(Path.Combine(tempDir, "AAA.json")));

            var merged = UnityEngine.JsonUtility.FromJson<MusicDTO.EditData>(
                File.ReadAllText(Path.Combine(tempDir, "AAA.json")));
            Assert.IsNotNull(merged.lyrics);
            Assert.AreEqual(2, merged.lyrics.mondai.Length);
            Assert.AreEqual("c", merged.lyrics.mondai[0]);
        }

        [Test]
        public void Migrate_SkipsOrphanTextFile()
        {
            File.WriteAllText(Path.Combine(tempDir, "Orphan_text.json"), "{}");
            Assert.DoesNotThrow(() => MigrateLegacyTextJson.MigrateForTest(tempDir));
            Assert.IsTrue(File.Exists(Path.Combine(tempDir, "Orphan_text.json")));
        }

        [Test]
        public void Migrate_IsIdempotent()
        {
            var alreadyMerged = "{\"name\":\"X\",\"maxBlock\":8,\"BPM\":120,\"offset\":0,\"notes\":[]," +
                                "\"lyrics\":{\"startTime\":[1],\"mondai\":[\"a\"]}}";
            File.WriteAllText(Path.Combine(tempDir, "X.json"), alreadyMerged);
            File.WriteAllText(Path.Combine(tempDir, "X_text.json"),
                "{\"StartTime\":[99],\"Mondai\":[\"different\"]}");

            MigrateLegacyTextJson.MigrateForTest(tempDir);

            var chart = UnityEngine.JsonUtility.FromJson<MusicDTO.EditData>(
                File.ReadAllText(Path.Combine(tempDir, "X.json")));
            Assert.AreEqual("a", chart.lyrics.mondai[0], "Existing lyrics should not be overwritten");
        }
    }
}
