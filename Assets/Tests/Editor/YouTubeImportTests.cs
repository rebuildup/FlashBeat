using FlashBeat.Tests.Editor;
using NUnit.Framework;
using NoteEditor.DTO;
using NoteEditor.Model;
using NoteEditor.Presenter.YouTube;
using UnityEngine;

public class YouTubeImportTests
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
    public void EditDataSerializer_RoundtripsVideoId()
    {
        EditData.VideoId.Value = "dQw4w9WgXcQ";
        var json = EditDataSerializer.Serialize();
        EditDataSerializer.Deserialize(json);
        Assert.AreEqual("dQw4w9WgXcQ", EditData.VideoId.Value);
    }

    [Test]
    public void EditDataSerializer_HandlesMissingVideoId()
    {
        const string legacyJson = "{\"name\":\"test\",\"maxBlock\":8,\"BPM\":120,\"offset\":0,\"notes\":[]}";
        Assert.DoesNotThrow(() => EditDataSerializer.Deserialize(legacyJson));
        Assert.AreEqual("", EditData.VideoId.Value);
    }

    [Test]
    public void EditDataSerializer_HandlesEmptyVideoId()
    {
        const string emptyVideoIdJson = "{\"name\":\"test\",\"maxBlock\":8,\"BPM\":120,\"offset\":0,\"videoId\":\"\",\"notes\":[]}";
        EditDataSerializer.Deserialize(emptyVideoIdJson);
        Assert.AreEqual("", EditData.VideoId.Value);
    }

    [Test]
    public void VideoIdParser_ExtractsFromFullUrl()
    {
        Assert.IsTrue(YouTubeVideoIdParser.TryParse("https://www.youtube.com/watch?v=dQw4w9WgXcQ", out var id));
        Assert.AreEqual("dQw4w9WgXcQ", id);
    }

    [Test]
    public void VideoIdParser_ExtractsFromShortUrl()
    {
        Assert.IsTrue(YouTubeVideoIdParser.TryParse("https://youtu.be/dQw4w9WgXcQ", out var id));
        Assert.AreEqual("dQw4w9WgXcQ", id);
    }

    [Test]
    public void VideoIdParser_AcceptsRawId()
    {
        Assert.IsTrue(YouTubeVideoIdParser.TryParse("dQw4w9WgXcQ", out var id));
        Assert.AreEqual("dQw4w9WgXcQ", id);
    }

    [Test]
    public void VideoIdParser_RejectsEmpty()
    {
        Assert.IsFalse(YouTubeVideoIdParser.TryParse("", out var id));
        Assert.IsNull(id);
    }

    [Test]
    public void VideoIdParser_RejectsInvalid()
    {
        Assert.IsFalse(YouTubeVideoIdParser.TryParse("not a url", out var id));
        Assert.IsNull(id);
    }

    [Test]
    public void VideoIdParser_AcceptsUnderscoreAndHyphen()
    {
        Assert.IsTrue(YouTubeVideoIdParser.TryParse("_-abc-DEF_12", out var id));
        Assert.AreEqual("_-abc-DEF_1", id);
    }

    [Test]
    public void GManagerUpdater_AppendsEntryToSongName()
    {
        const string input =
            "public static readonly string[] SongName = { \"noSong\", \"AAA\", \"BBB\" };\n" +
            "public static readonly string[] Musician = { \"no\", \"x\", \"y\" };\n" +
            "public static readonly string[] SongURL = { \"v1\", \"v2\", \"v3\" };\n" +
            "public static readonly int[] SBPM = { 100, 120, 130 };\n" +
            "public static readonly int[] Slevel = { 10, 8, 9 };\n" +
            "public static readonly int[] Shit = { 100, 200, 300 };\n" +
            "public static readonly float[] SongLong = { 10000.0f, 80.0f, 90.0f };\n" +
            "public static readonly int[] Hiscore = new int[42];\n";
        var meta = new GManagerParallelArrayUpdater.SongMeta
        {
            Title = "NewSong",
            Musician = "artist",
            VideoId = "abcdefghijk",
            Bpm = 150,
            Level = 5,
            TotalHits = 250,
            Duration = 100.0f
        };

        var output = GManagerParallelArrayUpdater.ApplyEdits(input, meta);

        StringAssert.Contains("\"NewSong\"", output);
        StringAssert.Contains("\"artist\"", output);
        StringAssert.Contains("\"abcdefghijk\"", output);
        StringAssert.Contains("150", output);
        StringAssert.Contains("new int[43]", output);
    }

    [Test]
    public void GManagerUpdater_HandlesMultiLineArrays()
    {
        const string input =
            "public static readonly string[] SongName = {\n" +
            "    \"noSong\",\n" +
            "    \"AAA\"\n" +
            "};\n";
        var meta = new GManagerParallelArrayUpdater.SongMeta { Title = "Multi" };

        var output = GManagerParallelArrayUpdater.ApplyEdits(input, meta);
        StringAssert.Contains("\"Multi\"", output);
    }
}