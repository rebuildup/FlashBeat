using System.IO;
using FlashBeat.Gameplay;
using FlashBeat.UI;
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
    public void TheMergedStreamIsTheAudioSourceWhenItIsReady()
    {
        Assert.AreEqual(PlaybackSource.StreamCombined,
            PlaybackRecovery.DecideAudioSource(combined: true, videoReady: true, audioReady: false, hasBundledWave: true));
    }

    [Test]
    public void TheSeparateTrackIsTheAudioSourceWhenThePictureIsMissing()
    {
        Assert.AreEqual(PlaybackSource.StreamSeparate,
            PlaybackRecovery.DecideAudioSource(combined: false, videoReady: false, audioReady: true, hasBundledWave: true));
    }

    [Test]
    public void TheBundledWaveIsOnlyAFallback()
    {
        Assert.AreEqual(PlaybackSource.BundledWave,
            PlaybackRecovery.DecideAudioSource(combined: false, videoReady: false, audioReady: false, hasBundledWave: true));
    }

    [Test]
    public void AMissingWavLeavesNoUndefinedAudioSource()
    {
        // ニセモノ (GManager.SongName[27]/[28]) has no
        // Assets/Game/Resources/Musics/ニセモノ.wav: the decision must end at
        // None instead of throwing, so the recovery can take the player back.
        Assert.DoesNotThrow(() =>
            PlaybackRecovery.DecideAudioSource(combined: false, videoReady: false, audioReady: false, hasBundledWave: false));
        Assert.AreEqual(PlaybackSource.None,
            PlaybackRecovery.DecideAudioSource(combined: false, videoReady: false, audioReady: false, hasBundledWave: false));
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

/// <summary>
/// The HUD hook of the recovery (issue #91): when the pipeline decides that the
/// song cannot be played (MusicManager.SelectReturnPending), the HUD has to show
/// the failure at once - not when its own 45s timeout runs out, because the
/// automatic return to the song list starts earlier than that.
/// </summary>
public class HudPlaybackFailureHookTests
{
    [Test]
    public void TheHudShowsTheFailureAsSoonAsThePipelineDecidesTheSongIsUnplayable()
    {
        // The load indicator is still active and the timeout is far away, but
        // the pipeline has already failed: the failure is shown immediately.
        Assert.AreEqual(HudPlayPhase.Failed,
            HudPlayState.Evaluate(videoError: false, playbackStarted: false, mediaLoading: true, loadingSeconds: 0f, pipelineFailed: true));
        Assert.AreEqual(HudPlayPhase.Failed,
            HudPlayState.Evaluate(videoError: false, playbackStarted: false, mediaLoading: false, loadingSeconds: 0f, pipelineFailed: true));
    }

    [Test]
    public void TheHudStaysQuietWhenThePlayerStartedTheSongAnyway()
    {
        // Space during the grace period: the song runs to the result screen on
        // the wall clock, the HUD does not nag about the failed preparation.
        Assert.AreEqual(HudPlayPhase.Hidden,
            HudPlayState.Evaluate(videoError: false, playbackStarted: true, mediaLoading: false, loadingSeconds: 0f, pipelineFailed: true));
    }

    [Test]
    public void ThePureOverloadWithoutPipelineFailureKeepsTheOldBehaviour()
    {
        Assert.AreEqual(HudPlayPhase.Preparing,
            HudPlayState.Evaluate(videoError: false, playbackStarted: false, mediaLoading: true, loadingSeconds: 0f, pipelineFailed: false));
        Assert.AreEqual(HudPlayPhase.Hidden,
            HudPlayState.Evaluate(videoError: false, playbackStarted: false, mediaLoading: false, loadingSeconds: 0f, pipelineFailed: false));
    }
}
