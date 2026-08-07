using FlashBeat.Tests.Editor;
using NUnit.Framework;
using NoteEditor.DTO;
using NoteEditor.Model;
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
}