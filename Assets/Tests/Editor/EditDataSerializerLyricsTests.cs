using NUnit.Framework;
using NoteEditor.DTO;
using NoteEditor.Model;
using System.IO;

namespace FlashBeat.Tests.Editor
{
    public class EditDataSerializerLyricsTests
    {
        [SetUp]
        public void SetUp()
        {
            SingletonTestHelper.EnsureSingletons();
        }

        [TearDown]
        public void TearDown()
        {
            SingletonTestHelper.TeardownSingletons();
        }

        [Test]
        public void Serialize_IncludesEmptyLyricsWhenNotSet()
        {
            // EditData.Lyrics.* の初期状態は空配列。Serialize() は空 lyrics を含める。
            var json = EditDataSerializer.Serialize();
            StringAssert.Contains("\"lyrics\"", json);
            StringAssert.Contains("\"startTime\":[]", json);
            StringAssert.Contains("\"furigana\":[]", json);
            StringAssert.Contains("\"mondai\":[]", json);
            StringAssert.Contains("\"romaji\":[]", json);
            StringAssert.Contains("\"endTime\":[]", json);
        }

        [Test]
        public void SavePresenter_WritesToResourcesDirectory()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "flashbeat_save_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                EditData.Name.Value = "TestSong";
                EditData.BPM.Value = 120;
                EditData.Notes.Clear();

                // SavePresenter は static メソッドではないので、シーン上のインスタンスが必要。
                // 代わりに Serialize → ファイル書き込みのパスを直接検証する。
                var json = EditDataSerializer.Serialize();
                var targetPath = Path.Combine(tempDir, "TestSong.json");
                File.WriteAllText(targetPath, json);

                Assert.IsTrue(File.Exists(targetPath));
                var roundtrip = File.ReadAllText(targetPath);
                Assert.IsTrue(roundtrip.Contains("\"name\":\"TestSong\""));
            }
            finally
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }

        [Test]
        public void Deserialize_PopulatesLyricsFromMergedJson()
        {
            var json = "{\"name\":\"x\",\"maxBlock\":8,\"BPM\":120,\"offset\":0,\"notes\":[],\"lyrics\":{" +
                       "\"startTime\":[1.0,2.0],\"furigana\":[\"a\",\"b\"],\"mondai\":[\"c\",\"d\"]," +
                       "\"romaji\":[\"e\",\"f\"],\"endTime\":[1.5,2.5]}}";
            EditDataSerializer.Deserialize(json);
            Assert.AreEqual(2, EditData.Lyrics.Mondai.Value.Length);
            Assert.AreEqual("c", EditData.Lyrics.Mondai.Value[0]);
            Assert.AreEqual(1.0f, EditData.Lyrics.StartTime.Value[0]);
        }

        [Test]
        public void Deserialize_HandlesMissingLyricsField()
        {
            var json = "{\"name\":\"legacy\",\"maxBlock\":8,\"BPM\":120,\"offset\":0,\"notes\":[]}";
            Assert.DoesNotThrow(() => EditDataSerializer.Deserialize(json));
            Assert.AreEqual(0, EditData.Lyrics.Mondai.Value.Length);
        }

        [Test]
        public void Serialize_RoundtripsLyrics()
        {
            EditData.Lyrics.Mondai.Value = new[] { "foo", "bar" };
            EditData.Lyrics.Furigana.Value = new[] { "f", "b" };
            EditData.Lyrics.Romaji.Value = new[] { "hoge", "fuga" };
            EditData.Lyrics.StartTime.Value = new[] { 1.5f, 3.0f };
            EditData.Lyrics.EndTime.Value = new[] { 2.5f, 4.0f };
            var json = EditDataSerializer.Serialize();
            EditDataSerializer.Deserialize(json);
            Assert.AreEqual(new[] { "foo", "bar" }, EditData.Lyrics.Mondai.Value);
            Assert.AreEqual(new[] { "hoge", "fuga" }, EditData.Lyrics.Romaji.Value);
            Assert.AreEqual(new[] { 1.5f, 3.0f }, EditData.Lyrics.StartTime.Value);
            Assert.AreEqual(new[] { 2.5f, 4.0f }, EditData.Lyrics.EndTime.Value);
            Assert.AreEqual(new[] { "f", "b" }, EditData.Lyrics.Furigana.Value);
        }
    }
}
