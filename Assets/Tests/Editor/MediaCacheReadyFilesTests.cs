using System;
using System.IO;
using FlashBeat.Gameplay;
using NUnit.Framework;

/// <summary>
/// The "already downloaded" lookup that lets a run skip the yt-dlp format probe
/// (issue #377):
///
///   * a merged file is enough on its own - picture and sound come from it;
///   * a picture file alone is not, because the plan that needs the sound file
///     can only be known from the format probe;
///   * only completed entries count (the marker has to match the file size),
///     so an interrupted download is fetched again instead of played (#184);
///   * the lookup is the only place that decides what a reusable cache entry is.
/// </summary>
public class MediaCacheReadyFilesTests
{
    string videoId;
    string dir;

    [SetUp]
    public void SetUp()
    {
        videoId = "ready-" + Guid.NewGuid().ToString("N");
        dir = YouTubeMediaCache.CacheDirectoryFor(videoId);
        Directory.CreateDirectory(dir);
    }

    [TearDown]
    public void TearDown()
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
    }

    void WriteCacheFile(string fileName, int bytes, bool marked)
    {
        var path = Path.Combine(dir, fileName);
        File.WriteAllBytes(path, new byte[bytes]);
        if (marked)
        {
            File.WriteAllText(YouTubeMediaCache.DoneMarkerPath(path), bytes.ToString());
        }
    }

    [Test]
    public void CompletedMergedFileIsAReadySet()
    {
        WriteCacheFile("merged.mp4", 4096, marked: true);

        ReadyMedia media;
        Assert.IsTrue(YouTubeMediaCache.TryGetReadyFiles(videoId, out media));
        Assert.IsTrue(media.Combined, "the merged file carries picture and sound");
        Assert.IsFalse(media.HasAudio, "a merged file has no separate sound file");
        StringAssert.EndsWith("merged.mp4", media.VideoPath);
    }

    [Test]
    public void CompletedVideoAndAudioPairIsAReadySet()
    {
        WriteCacheFile("video.mp4", 2048, marked: true);
        WriteCacheFile("audio.m4a", 512, marked: true);

        ReadyMedia media;
        Assert.IsTrue(YouTubeMediaCache.TryGetReadyFiles(videoId, out media));
        Assert.IsFalse(media.Combined);
        Assert.IsTrue(media.HasAudio, "the split plan needs both files");
        StringAssert.EndsWith("video.mp4", media.VideoPath);
        StringAssert.EndsWith("audio.m4a", media.AudioPath);
    }

    [Test]
    public void PictureFileWithoutSoundFileIsNotAReadySet()
    {
        // Without the format probe the plan is unknown: a picture-only file may
        // mean "no ffmpeg" of an earlier run or "the merged download failed".
        // Adopting it would silently drop the YouTube sound.
        WriteCacheFile("video.mp4", 2048, marked: true);

        ReadyMedia media;
        Assert.IsFalse(YouTubeMediaCache.TryGetReadyFiles(videoId, out media));
        Assert.IsNull(media);
    }

    [Test]
    public void UnmarkedMergedFileIsNotAReadySet()
    {
        WriteCacheFile("merged.mp4", 4096, marked: false);

        ReadyMedia media;
        Assert.IsFalse(YouTubeMediaCache.TryGetReadyFiles(videoId, out media));
    }

    [Test]
    public void StaleMarkerSizeIsNotAReadySet()
    {
        WriteCacheFile("merged.mp4", 4096, marked: true);
        File.WriteAllText(
            YouTubeMediaCache.DoneMarkerPath(Path.Combine(dir, "merged.mp4")),
            "1024");

        ReadyMedia media;
        Assert.IsFalse(YouTubeMediaCache.TryGetReadyFiles(videoId, out media),
            "a file whose size no longer matches the marker is not complete");
    }

    [Test]
    public void EmptyVideoIdHasNoReadySet()
    {
        ReadyMedia media;
        Assert.IsFalse(YouTubeMediaCache.TryGetReadyFiles("", out media));
        Assert.IsFalse(YouTubeMediaCache.TryGetReadyFiles(null, out media));
    }

    [Test]
    public void InvalidateDropsTheReadySet()
    {
        WriteCacheFile("merged.mp4", 4096, marked: true);

        ReadyMedia media;
        Assert.IsTrue(YouTubeMediaCache.TryGetReadyFiles(videoId, out media));

        // What MusicManager does when the player cannot open the file (#184):
        // the next run has to resolve formats and fetch again.
        YouTubeMediaCache.InvalidateProducedFile(media.VideoPath);

        Assert.IsFalse(YouTubeMediaCache.TryGetReadyFiles(videoId, out media));
    }
}
