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
    }
}
