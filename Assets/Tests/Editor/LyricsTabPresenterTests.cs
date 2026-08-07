using NUnit.Framework;
using NoteEditor.Model;
using NoteEditor.Presenter.Lyrics;

namespace FlashBeat.Tests.Editor
{
    public class LyricsTabPresenterTests
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
        public void AddLyricAtTime_AppendsToAllArrays()
        {
            LyricsTabPresenter.AddLyricAtTime(startTime: 5.0f, endTime: 6.5f,
                mondai: "漢字", furigana: "かんじ", romaji: "kanji");

            Assert.AreEqual(1, EditData.Lyrics.StartTime.Value.Length);
            Assert.AreEqual(5.0f, EditData.Lyrics.StartTime.Value[0]);
            Assert.AreEqual("漢字", EditData.Lyrics.Mondai.Value[0]);
            Assert.AreEqual("かんじ", EditData.Lyrics.Furigana.Value[0]);
            Assert.AreEqual("kanji", EditData.Lyrics.Romaji.Value[0]);
            Assert.AreEqual(6.5f, EditData.Lyrics.EndTime.Value[0]);
        }

        [Test]
        public void RemoveLyricAt_RemovesFromAllArrays()
        {
            LyricsTabPresenter.AddLyricAtTime(1, 2, "a", "f", "r");
            LyricsTabPresenter.AddLyricAtTime(3, 4, "b", "g", "s");
            LyricsTabPresenter.AddLyricAtTime(5, 6, "c", "h", "t");

            LyricsTabPresenter.RemoveLyricAt(1);

            Assert.AreEqual(2, EditData.Lyrics.Mondai.Value.Length);
            Assert.AreEqual("a", EditData.Lyrics.Mondai.Value[0]);
            Assert.AreEqual("c", EditData.Lyrics.Mondai.Value[1]);
        }

        [Test]
        public void UpdateLyricAt_ReplacesFields()
        {
            LyricsTabPresenter.AddLyricAtTime(1, 2, "old", "f", "r");
            LyricsTabPresenter.UpdateLyricAt(0, startTime: 1.5f, endTime: 2.5f,
                mondai: "new", furigana: "n", romaji: "n2");

            Assert.AreEqual(1, EditData.Lyrics.Mondai.Value.Length);
            Assert.AreEqual("new", EditData.Lyrics.Mondai.Value[0]);
            Assert.AreEqual(1.5f, EditData.Lyrics.StartTime.Value[0]);
            Assert.AreEqual(2.5f, EditData.Lyrics.EndTime.Value[0]);
        }
    }
}