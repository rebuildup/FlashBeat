using NUnit.Framework;
using UnityEngine;

public class SongDataTests
{
    [Test]
    public void TestSongsArrayNotEmpty()
    {
        Assert.Greater(GManager.Songs.Length, 0, "Songs array should not be empty.");
    }

    [Test]
    public void TestSongsIndexMatchesId()
    {
        for (int i = 0; i < GManager.Songs.Length; i++)
        {
            Assert.AreEqual(i, GManager.Songs[i].id, $"Songs[{i}].id should be {i}.");
        }
    }

    [Test]
    public void TestSongsIndexZeroIsPlaceholder()
    {
        Assert.AreEqual("noSong", GManager.Songs[0].title, "Songs[0] should be the noSong placeholder.");
    }

    [Test]
    public void TestSongIdOneTitleUnchanged()
    {
        Assert.AreEqual("ロストアンブレラ", GManager.Songs[1].title, "Songs[1].title must match prior SongName[1].");
    }

    [Test]
    public void TestGetSongValidAndInvalidId()
    {
        SongData song = GManager.GetSong(1);
        Assert.IsNotNull(song, "Song ID 1 should exist.");
        Assert.AreEqual("ロストアンブレラ", song.title);

        Assert.IsNull(GManager.GetSong(-1), "Negative song ID should return null.");
        Assert.IsNull(GManager.GetSong(999), "Out-of-bounds song ID should return null.");
    }

    [Test]
    public void TestTotalSongDerivedFromSongsLength()
    {
        Assert.AreEqual(GManager.Songs.Length - 1, GManager.totalSong, "totalSong should equal Songs.Length - 1 (excluding noSong placeholder).");
    }

    [Test]
    public void TestVideoIdMigrationMatchesPriorSongUrl()
    {
        Assert.AreEqual(GManager.SongURL[1], GManager.Songs[1].videoId, "Songs[1].videoId must equal SongURL[1].");
        Assert.AreEqual(GManager.SongURL[10], GManager.Songs[10].videoId, "Songs[10].videoId must equal SongURL[10].");
        Assert.AreEqual(GManager.SongURL[26], GManager.Songs[26].videoId, "Songs[26].videoId must equal SongURL[26].");
    }

    [Test]
    public void TestResetSession()
    {
        GManager.perfect = 10;
        GManager.great = 5;
        GManager.bad = 2;
        GManager.miss = 1;
        GManager.score = 500000;
        GManager.combo = 15;

        GManager.ResetSession();

        Assert.AreEqual(0, GManager.perfect);
        Assert.AreEqual(0, GManager.great);
        Assert.AreEqual(0, GManager.bad);
        Assert.AreEqual(0, GManager.miss);
        Assert.AreEqual(0, GManager.score);
        Assert.AreEqual(0, GManager.combo);
    }
}
