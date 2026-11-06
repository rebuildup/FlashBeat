using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// The song list of the select screen must not depend on the cloud API: the
/// original data of all songs stays playable, and the cloud catalog only
/// updates the songs it knows (chart version, chart id and song info).
/// </summary>
public class CatalogMergeTests
{
    /// <summary>The cloud snapshot of the production catalog (2026-11-07).</summary>
    static SongData[] RemoteSnapshot()
    {
        return new[]
        {
            new SongData(0, "noSong", "no", "", 0, 0, 0, 0f),
            new SongData(1, "ロストアンブレラ", "稲葉曇", "DeKLpgzh-qQ", 274, 8, 268, 89f)
            {
                isRemote = true,
                remoteId = "lost-umbrella",
                chartVersion = 1,
            },
            new SongData(2, "Who", "Azari", "8JXiXt0D6tw", 128, 5, 139, 68f)
            {
                isRemote = true,
                remoteId = "who",
                chartVersion = 9,
            },
        };
    }

    static int IndexOf(SongData[] songs, string title)
    {
        for (int i = 0; i < songs.Length; i++)
        {
            if (songs[i] != null && songs[i].title == title) return i;
        }
        return -1;
    }

    [TearDown]
    public void ResetTheCatalog()
    {
        GManager.ClearRemoteCatalog();
    }

    [Test]
    public void EverySongOfTheOriginalDataStaysPlayable()
    {
        var merged = GManager.Merge(GManager.Songs, RemoteSnapshot());

        Assert.AreEqual(GManager.Songs.Length, merged.Length,
            "the cloud snapshot must not remove a song of the original data");

        foreach (var song in GManager.Songs)
        {
            Assert.GreaterOrEqual(IndexOf(merged, song.title), 0, $"'{song.title}' is missing in the merged catalog");
        }
    }

    [Test]
    public void TheSongsTheCloudKnowsBecomeTheCloudVersion()
    {
        var merged = GManager.Merge(GManager.Songs, RemoteSnapshot());

        int umbrella = IndexOf(merged, "ロストアンブレラ");
        Assert.GreaterOrEqual(umbrella, 0);
        Assert.IsTrue(merged[umbrella].isRemote, "the cloud song has to be marked as remote");
        Assert.AreEqual("lost-umbrella", merged[umbrella].remoteId);
        Assert.AreEqual(1, merged[umbrella].chartVersion);
        Assert.AreEqual(89f, merged[umbrella].duration, 0.01f);

        int who = IndexOf(merged, "Who");
        Assert.GreaterOrEqual(who, 0);
        Assert.IsTrue(merged[who].isRemote);
        Assert.AreEqual("who", merged[who].remoteId);
        Assert.AreEqual(9, merged[who].chartVersion);
    }

    [Test]
    public void TheSongsTheCloudDoesNotKnowKeepTheirLocalData()
    {
        var merged = GManager.Merge(GManager.Songs, RemoteSnapshot());

        int mania = IndexOf(merged, "まにまに");
        Assert.GreaterOrEqual(mania, 0);
        Assert.IsFalse(merged[mania].isRemote, "a song that is not published in the cloud stays local");
        Assert.AreEqual(GManager.Songs[mania].videoId, merged[mania].videoId);
        Assert.AreEqual(GManager.Songs[mania].duration, merged[mania].duration, 0.01f);
    }

    [Test]
    public void ACloudSongTheOriginalDataDoesNotKnowIsAdded()
    {
        var remote = new[]
        {
            new SongData(0, "noSong", "no", "", 0, 0, 0, 0f),
            new SongData(1, "新しい曲", "新しいアーティスト", "abcdefghijk", 120, 3, 42, 60f)
            {
                isRemote = true,
                remoteId = "new-song",
                chartVersion = 3,
            },
        };

        var merged = GManager.Merge(GManager.Songs, remote);

        Assert.AreEqual(GManager.Songs.Length + 1, merged.Length);
        int added = IndexOf(merged, "新しい曲");
        Assert.GreaterOrEqual(added, 0);
        Assert.IsTrue(merged[added].isRemote);
        Assert.AreEqual("new-song", merged[added].remoteId);
    }

    [Test]
    public void TheCatalogWithoutACloudSnapshotKeepsEverySong()
    {
        GManager.ClearRemoteCatalog();

        Assert.AreEqual(GManager.Songs.Length, GManager.ActiveSongs.Length);
        Assert.IsFalse(GManager.HasRemoteCatalog);
    }

    [Test]
    public void TheActiveCatalogIsTheMergedOneWhileTheCloudIsReachable()
    {
        GManager.ApplyRemoteCatalog(RemoteSnapshot());

        Assert.IsTrue(GManager.HasRemoteCatalog);
        Assert.AreEqual(GManager.Songs.Length, GManager.ActiveSongs.Length);
        Assert.IsTrue(GManager.ActiveSongs[IndexOf(GManager.ActiveSongs, "Who")].isRemote);
        Assert.IsFalse(GManager.ActiveSongs[IndexOf(GManager.ActiveSongs, "まにまに")].isRemote);
    }
}
