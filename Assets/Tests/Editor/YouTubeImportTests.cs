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
}