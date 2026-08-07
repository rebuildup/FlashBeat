using NUnit.Framework;
using NoteEditor.DTO;
using NoteEditor.Model;

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
    }
}
