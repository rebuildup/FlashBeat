using System.IO;
using System.Linq;
using NUnit.Framework;
using NoteEditor.Presenter.FlashBeatSong;

namespace FlashBeat.Tests.Editor
{
    public class FlashBeatSongLoaderTests
    {
        string tempDir;

        [SetUp]
        public void SetUp()
        {
            tempDir = Path.Combine(Path.GetTempPath(), "flashbeat_songloader_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }

        [Test]
        public void EnumerateJsonFiles_ExcludesTextJson()
        {
            File.WriteAllText(Path.Combine(tempDir, "AAA.json"), "{}");
            File.WriteAllText(Path.Combine(tempDir, "AAA_text.json"), "{}");
            File.WriteAllText(Path.Combine(tempDir, "BBB.json"), "{}");

            var result = FlashBeatSongLoader.EnumerateJsonFilesForTest(tempDir);

            CollectionAssert.AreEquivalent(new[] { "AAA.json", "BBB.json" },
                result.Select(Path.GetFileName).ToArray());
        }

        [Test]
        public void EnumerateJsonFiles_ExcludesE2EProbeSong()
        {
            File.WriteAllText(Path.Combine(tempDir, "Song1.json"), "{}");
            File.WriteAllText(Path.Combine(tempDir, "E2EProbeSong.json"), "{}");

            var result = FlashBeatSongLoader.EnumerateJsonFilesForTest(tempDir);

            CollectionAssert.AreEquivalent(new[] { "Song1.json" },
                result.Select(Path.GetFileName).ToArray());
        }

        [Test]
        public void EnumerateJsonFiles_ReturnsEmptyForEmptyDir()
        {
            var result = FlashBeatSongLoader.EnumerateJsonFilesForTest(tempDir);
            Assert.IsEmpty(result);
        }

        [Test]
        public void EnumerateJsonFiles_ReturnsEmptyForMissingDir()
        {
            var result = FlashBeatSongLoader.EnumerateJsonFilesForTest(Path.Combine(tempDir, "nonexistent"));
            Assert.IsEmpty(result);
        }
    }
}
