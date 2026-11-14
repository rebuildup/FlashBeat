using System.Collections.Generic;
using FlashBeat.Gameplay;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Timing contract of the media pipeline: the judgement has to follow the media
/// clock, otherwise the notes drift away from the picture and the sound.
/// </summary>
public class MediaTimelineTests
{
    [Test]
    public void JudgeLagMatchesMediaLagWhenStartTimeComesFromTheMediaClock()
    {
        float wallClock = 120.5f;
        float mediaTime = 17.25f;
        float noteTime = 17.4f;

        float startTime = MediaTimeline.JudgeStartTime(wallClock, mediaTime);
        float judgeLag = MediaTimeline.JudgeLag(wallClock, startTime, noteTime);

        Assert.AreEqual(MediaTimeline.MediaLag(mediaTime, noteTime), judgeLag, 0.0001f);
        Assert.AreEqual(0.15f, judgeLag, 0.0001f);
    }

    [Test]
    public void NoteIsJudgedPerfectlyWhenTheMediaReachesTheNoteTime()
    {
        float wallClock = 300f;
        float mediaTime = 42.0f;
        float noteTime = 42.0f;

        float startTime = MediaTimeline.JudgeStartTime(wallClock, mediaTime);
        float judgeLag = MediaTimeline.JudgeLag(wallClock, startTime, noteTime);

        Assert.AreEqual(0f, judgeLag, 0.0001f);
        Assert.Less(judgeLag, 0.10f, "a note that the media just reached has to be judged Perfect");
    }

    [Test]
    public void StalledMediaDoesNotAccumulateJudgementError()
    {
        float noteTime = 12.0f;

        // Playback runs, then the media stalls for two seconds while the game keeps
        // rendering frames. The start time is recomputed from the media clock, so
        // the note is still judged against the media position.
        float wallClockBeforeStall = 100.0f;
        float mediaTime = 12.0f;
        float startTimeBeforeStall = MediaTimeline.JudgeStartTime(wallClockBeforeStall, mediaTime);

        float wallClockAfterStall = 102.0f;
        float mediaTimeAfterStall = 12.0f;
        float startTimeAfterStall = MediaTimeline.JudgeStartTime(wallClockAfterStall, mediaTimeAfterStall);

        float lagAfterStall = MediaTimeline.JudgeLag(wallClockAfterStall, startTimeAfterStall, noteTime);
        float frozenClockLag = MediaTimeline.JudgeLag(wallClockAfterStall, startTimeBeforeStall, noteTime);

        Assert.AreEqual(0f, lagAfterStall, 0.0001f, "the judgement must follow the stalled media clock");
        Assert.Greater(frozenClockLag, 1.9f, "a frozen start time would drift by the stall length");
    }

    [Test]
    public void MediaTimeIsRecoveredFromTheJudgementClock()
    {
        float wallClock = 512.75f;
        float mediaTime = 63.5f;

        float startTime = MediaTimeline.JudgeStartTime(wallClock, mediaTime);

        Assert.AreEqual(mediaTime, MediaTimeline.MediaTimeAt(wallClock, startTime), 0.0001f);
    }

    [Test]
    public void SongEndsOneSecondBeforeTheChartDuration()
    {
        Assert.IsFalse(MediaTimeline.HasSongEnded(46.9f, 48f));
        Assert.IsTrue(MediaTimeline.HasSongEnded(47.0f, 48f));
        Assert.IsTrue(MediaTimeline.HasSongEnded(47.5f, 48f));
    }

    /// <summary>
    /// PR #387 レビュー指摘: 本番 (MusicManager) の終端判定はリードアウトを
    /// 差し引かない。1 秒引くと Result への遷移要求が曲長の 1 秒前に出て、
    /// 最後の 1 秒が表示されないまま暗転する。フェードの位置は
    /// ChartTiming.FadeOut が持つ別の関心事。
    /// </summary>
    [Test]
    public void ProductionEndConditionUsesTheFullDuration()
    {
        const float duration = 48f;

        Assert.IsFalse(MediaTimeline.HasSongEnded(47.9f, duration, leadOutSeconds: 0f),
            "曲長の 1 秒前では終わらない");
        Assert.IsTrue(MediaTimeline.HasSongEnded(48f, duration, leadOutSeconds: 0f),
            "曲長に達したら終わる");
        Assert.IsTrue(MediaTimeline.HasSongEnded(48.5f, duration, leadOutSeconds: 0f));
    }

    /// <summary>同じく本番の、実メディア時計が無いときの終端。</summary>
    [Test]
    public void ProductionFallbackEndConditionUsesTheFullDuration()
    {
        Assert.AreEqual(48f, MediaTimeline.FallbackSongEnd(48f, leadOutSeconds: 0f), 0.0001f,
            "本番は曲長そのものを終端にする");
        Assert.AreEqual(47f, MediaTimeline.FallbackSongEnd(48f), 0.0001f,
            "既定の引数は従来どおり (既存の呼び出しを壊さない)");
    }
}

/// <summary>
/// The format selection has to survive YouTube dropping the progressive itag 18/22
/// (verified with `yt-dlp -F` for the FlashBeat song list on 2026-11-06).
/// </summary>
public class YouTubeFormatPlanTests
{
    // Real `yt-dlp -F` output of https://www.youtube.com/watch?v=9O2VyUM5MlQ
    // (まにまに). No progressive format (18/22) is offered any more.
    const string AdaptiveOnlyFormatList = @"ID  EXT   RESOLUTION FPS CH |   FILESIZE  TBR PROTO | VCODEC         VBR ACODEC      ABR ASR MORE INFO
-------------------------------------------------------------------------------------------------------------------
sb3 mhtml 48x27        0    |                 mhtml | images                                 storyboard
233 mp4   audio only        |                 m3u8  | audio only         unknown             [ja] Default, low
140 m4a   audio only    2   |  4.15MiB  129k https | audio only   mp4a.40.2  129k 44k [ja] medium, m4a_dash
139 m4a   audio only    2   |  1.57MiB   49k https | audio only   mp4a.40.5   49k 22k [ja] low, m4a_dash
134 mp4   640x360     30    |  857.48KiB   26k https | avc1.4D401E   26k video only          360p, mp4_dash
136 mp4   1280x720    30    |  2.06MiB    64k https | avc1.4D401F   64k video only          720p, mp4_dash
137 mp4   1920x1080   30    |  3.55MiB   100k https | avc1.640028  100k video only          1080p, mp4_dash
";

    // A video that still offers the progressive format (H.264 + AAC in one mp4).
    const string ProgressiveFormatList = @"ID  EXT   RESOLUTION FPS CH |   FILESIZE  TBR PROTO | VCODEC         VBR ACODEC      ABR ASR MORE INFO
18  mp4   640x360     30    |  10.00MiB   1k https | avc1.4D401E  700k mp4a.40.2   96k 44k
134 mp4   640x360     30    |  857.48KiB   26k https | avc1.4D401E   26k video only          360p, mp4_dash
140 m4a   audio only    2   |  4.15MiB  129k https | audio only   mp4a.40.2  129k 44k [ja] medium, m4a_dash
";

    [Test]
    public void ParserReadsCodecsAndResolutionOfEveryRow()
    {
        var formats = YouTubeStreamResolver.ParseFormats(AdaptiveOnlyFormatList);

        var video = formats.Find(f => f.Id == "134");
        Assert.IsNotNull(video);
        Assert.AreEqual("mp4", video.Ext);
        Assert.AreEqual("640x360", video.Resolution);
        Assert.AreEqual("avc1.4D401E", video.Vcodec);
        Assert.AreEqual("video only", video.Acodec);
        Assert.IsTrue(video.HasVideo);
        Assert.IsFalse(video.HasAudio);
        Assert.IsFalse(video.IsCombined);

        var audio = formats.Find(f => f.Id == "140");
        Assert.IsNotNull(audio);
        Assert.AreEqual("m4a", audio.Ext);
        Assert.AreEqual("audio only", audio.Vcodec);
        Assert.AreEqual("mp4a.40.2", audio.Acodec);
        Assert.IsFalse(audio.HasVideo);
        Assert.IsTrue(audio.HasAudio);
    }

    [Test]
    public void AdaptiveOnlyVideoIsMergedWithItsAudioTrack()
    {
        var formats = YouTubeStreamResolver.ParseFormats(AdaptiveOnlyFormatList);
        var plan = YouTubeMediaCache.SelectPlan(formats, canMerge: true);

        // Unity's VideoPlayer cannot open an audio-only m4a, so both streams are
        // muxed into one mp4 - that is the only way the YouTube sound is heard.
        Assert.IsTrue(plan.Combined);
        Assert.IsFalse(plan.HasAudioFile);
        Assert.AreEqual("136+140", plan.VideoSelector, "the largest H.264 mp4 up to 720p plus the best AAC track");
        Assert.AreEqual("136", plan.FallbackVideoSelector, "the picture-only fallback if the merge fails");
    }

    [Test]
    public void WithoutFfmpegTheStreamsStaySplit()
    {
        var formats = YouTubeStreamResolver.ParseFormats(AdaptiveOnlyFormatList);
        var plan = YouTubeMediaCache.SelectPlan(formats, canMerge: false);

        Assert.IsFalse(plan.Combined);
        Assert.IsTrue(plan.HasAudioFile);
        Assert.AreEqual("136", plan.VideoSelector, "the largest H.264 mp4 up to 720p is used");
        Assert.AreEqual("140", plan.AudioSelector, "the highest bitrate AAC track is used");
    }

    [Test]
    public void ProgressiveVideoIsDownloadedAsOneFileWithSound()
    {
        var formats = YouTubeStreamResolver.ParseFormats(ProgressiveFormatList);
        var plan = YouTubeMediaCache.SelectPlan(formats);

        Assert.IsTrue(plan.Combined);
        Assert.IsFalse(plan.HasAudioFile);
        Assert.AreEqual("18", plan.VideoSelector);
    }

    [Test]
    public void UnknownFormatListFallsBackToWorkingSelectors()
    {
        var plan = YouTubeMediaCache.SelectPlan(new List<YtDlpFormat>());

        Assert.IsFalse(plan.Combined);
        Assert.AreEqual("bv[ext=mp4][height<=720]", plan.VideoSelector);
        Assert.AreEqual("ba[ext=m4a]", plan.AudioSelector);
    }

    [Test]
    public void AMergedPlanIsCachedUnderTheMergedPrefixAndAPictureOnlyPlanUnderTheVideoPrefix()
    {
        // The game (MusicManager) and the pre-cache tool (MediaCacheTools) have
        // to store the picture file under the same prefix: a merged file under
        // the video prefix would be found again as if it were picture-only.
        var merged = YouTubeMediaCache.SelectPlan(YouTubeStreamResolver.ParseFormats(AdaptiveOnlyFormatList), canMerge: true);
        var split = YouTubeMediaCache.SelectPlan(YouTubeStreamResolver.ParseFormats(AdaptiveOnlyFormatList), canMerge: false);

        Assert.AreEqual(YouTubeMediaCache.MergedFilePrefix, YouTubeMediaCache.PrefixForPlan(merged));
        Assert.AreEqual(YouTubeMediaCache.VideoFilePrefix, YouTubeMediaCache.PrefixForPlan(split));
        Assert.AreNotEqual(YouTubeMediaCache.PrefixForPlan(merged), YouTubeMediaCache.PrefixForPlan(split));
    }

    [Test]
    public void ResumeReSeekOnlyWhenThePictureClockDriftedFromTheFrozenTime()
    {
        // Issue #382: ポーズ中にメディア時計が固定位置からずれたら再開時に
        // シークし直す。閾値内の誤差 (シーク往復の丸めなど) では動かさない。
        Assert.IsTrue(MediaTimeline.ShouldReSeekMedia(1.0f, 0.0f));
        Assert.IsTrue(MediaTimeline.ShouldReSeekMedia(0.0f, 0.5f));
        Assert.IsFalse(MediaTimeline.ShouldReSeekMedia(0.05f, 0.0f));
        Assert.IsFalse(MediaTimeline.ShouldReSeekMedia(10.0f, 10.05f));
        Assert.IsFalse(MediaTimeline.ShouldReSeekMedia(3.0f, 3.0f));
    }
}
