using System.IO;
using FlashBeat.Gameplay;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Recovery contract of the media pipeline (issue #48/#31):
///
///   * a song without a sound track cannot be played - the player has to be
///     taken back to the song list instead of sitting in front of a silent song;
///   * the failure log names the stage, so it is visible where the playback
///     broke;
///   * the media cache lives in the player environment, the release build ships
///     no third party media.
/// </summary>
public class PlaybackRecoveryTests
{
    [Test]
    public void ASongWithoutSoundIsTakenBackToTheSongList()
    {
        Assert.IsTrue(PlaybackRecovery.NeedsSelectReturn(MediaFailure.MediaUnavailable, hasSound: false));
        Assert.IsTrue(PlaybackRecovery.NeedsSelectReturn(MediaFailure.VideoPrepareFailed, hasSound: false));
        Assert.IsTrue(PlaybackRecovery.NeedsSelectReturn(MediaFailure.AudioUnavailable, hasSound: false));
        Assert.IsTrue(PlaybackRecovery.NeedsSelectReturn(MediaFailure.AudioSilent, hasSound: false));
    }

    [Test]
    public void ASongWithSoundStaysPlayable()
    {
        Assert.IsFalse(PlaybackRecovery.NeedsSelectReturn(MediaFailure.MediaUnavailable, hasSound: true));
        Assert.IsFalse(PlaybackRecovery.NeedsSelectReturn(MediaFailure.VideoPrepareFailed, hasSound: true));
    }

    [Test]
    public void WithoutAFailureTheSongIsNeverTakenBack()
    {
        Assert.IsFalse(PlaybackRecovery.NeedsSelectReturn(MediaFailure.None, hasSound: false));
        Assert.IsFalse(PlaybackRecovery.NeedsSelectReturn(MediaFailure.None, hasSound: true));
    }

    [Test]
    public void TheFailureLogNamesTheStageAndTheReason()
    {
        string log = PlaybackRecovery.FailureLog(MediaStage.PreparingVideo, MediaFailure.VideoPrepareFailed, hasPicture: false);

        StringAssert.Contains("stage=preparing-video", log);
        StringAssert.Contains("could not be opened", log);
        StringAssert.Contains("picture=missing", log);
    }

    [Test]
    public void EveryStageHasAReadableName()
    {
        Assert.AreEqual("idle", PlaybackRecovery.StageName(MediaStage.Idle));
        Assert.AreEqual("resolving-stream", PlaybackRecovery.StageName(MediaStage.ResolvingStream));
        Assert.AreEqual("downloading-media", PlaybackRecovery.StageName(MediaStage.DownloadingMedia));
        Assert.AreEqual("preparing-video", PlaybackRecovery.StageName(MediaStage.PreparingVideo));
        Assert.AreEqual("preparing-audio", PlaybackRecovery.StageName(MediaStage.PreparingAudio));
        Assert.AreEqual("ready", PlaybackRecovery.StageName(MediaStage.Ready));
        Assert.AreEqual("playing", PlaybackRecovery.StageName(MediaStage.Playing));
        Assert.AreEqual("failed", PlaybackRecovery.StageName(MediaStage.Failed));
    }

    [Test]
    public void ThePlayerIsToldThatTheGameGoesBackToTheSongList()
    {
        string hint = PlaybackRecovery.StartHint(MediaFailure.AudioUnavailable);

        StringAssert.Contains("選曲画面", hint);
        StringAssert.Contains("no sound track", hint);
    }

    [Test]
    public void TheReturnToTheSongListIsBounded()
    {
        Assert.IsTrue(PlaybackRecovery.ShouldRetrySelectReturn(1));
        Assert.IsTrue(PlaybackRecovery.ShouldRetrySelectReturn(PlaybackRecovery.MaxSelectReturnAttempts - 1));
        Assert.IsFalse(PlaybackRecovery.ShouldRetrySelectReturn(PlaybackRecovery.MaxSelectReturnAttempts));
        Assert.IsFalse(PlaybackRecovery.ShouldRetrySelectReturn(PlaybackRecovery.MaxSelectReturnAttempts + 1));
    }

    [Test]
    public void TheReturnToTheSongListWaitsForTheMessage()
    {
        Assert.Greater(PlaybackRecovery.SelectReturnDelaySeconds, 1f,
            "the player needs a moment to read the message");
        Assert.Greater(PlaybackRecovery.SelectReturnRetrySeconds, 0f);
    }

    [Test]
    public void TheMediaCacheLivesInThePlayerEnvironment()
    {
        string root = YouTubeMediaCache.CacheRoot;

        StringAssert.StartsWith(Application.persistentDataPath, root,
            "the media cache must not live inside the build folder");
        StringAssert.Contains("FlashBeatMedia", root);
    }

    [Test]
    public void EverySongGetsItsOwnCacheDirectory()
    {
        string first = YouTubeMediaCache.CacheDirectoryFor("DeKLpgzh-qQ");
        string second = YouTubeMediaCache.CacheDirectoryFor("9O2VyUM5MlQ");

        Assert.AreNotEqual(first, second);
        StringAssert.StartsWith(YouTubeMediaCache.CacheRoot, first);
        StringAssert.Contains("DeKLpgzh-qQ", first);
    }
}
