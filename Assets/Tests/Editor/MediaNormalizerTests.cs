using FlashBeat.Gameplay;
using NUnit.Framework;

/// <summary>
/// Issue #796: YouTube の progressive ストリーム (itag 18) など、映像
/// time_base が Unity VideoPlayer の時計を破壊する mp4 を検出・正規化する
/// MediaNormalizer の純粋な判定ロジックを固定する。
///
/// 実機で確認した事実:
///   - 壊れたファイル: 映像 time_base = 1/11988 (29.97fps, stts delta=400)
///     → songVideo.time が実時間の約 1/6 でしか進まず、音声も無音化した
///   - 直ったファイル: 同じ映像を timescale 29970 へ再エンコード
///     → 正常に実時間で再生された (pass=33 fail=0)
///   - 正常に再生できた既存キャッシュの timescale: 15360 / 30000
/// </summary>
public class MediaNormalizerTests
{
    [Test]
    public void NeedsNormalization_BelowThreshold_ReturnsTrue()
    {
        // 実際に壊れた Who (8JXiXt0D6tw) の time_base 分母。
        Assert.IsTrue(MediaNormalizer.NeedsNormalization(11988));
        Assert.IsTrue(MediaNormalizer.NeedsNormalization(1000));
        Assert.IsTrue(MediaNormalizer.NeedsNormalization(15359));
    }

    [Test]
    public void NeedsNormalization_AtOrAboveThreshold_ReturnsFalse()
    {
        // 実際に正常再生できた timescale。
        Assert.IsFalse(MediaNormalizer.NeedsNormalization(15360));
        Assert.IsFalse(MediaNormalizer.NeedsNormalization(29970));
        Assert.IsFalse(MediaNormalizer.NeedsNormalization(30000));
    }

    [Test]
    public void NeedsNormalization_InvalidValues_ReturnsFalse()
    {
        Assert.IsFalse(MediaNormalizer.NeedsNormalization(0));
        Assert.IsFalse(MediaNormalizer.NeedsNormalization(-1));
        Assert.IsFalse(MediaNormalizer.NeedsNormalization(-11988));
    }

    [Test]
    public void ComputeVideoTrackTimescale_PAL29_97_MatchesVerifiedFix()
    {
        // Who の実測値: avg_frame_rate=2997/100。
        // 検証済みの修正ファイルは -video_track_timescale 29970 だった。
        long t = MediaNormalizer.ComputeVideoTrackTimescale(2997, 100);
        Assert.AreEqual(29970, t);
        // 全フレームの duration が整数 tick になること (T*den/num)。
        Assert.AreEqual(0, (t * 100) % 2997);
    }

    [Test]
    public void ComputeVideoTrackTimescale_NTSC29_97_MatchesExistingFiles()
    {
        // 30000/1001 fps の動画が持つ標準的な timescale は 30000。
        long t = MediaNormalizer.ComputeVideoTrackTimescale(30000, 1001);
        Assert.AreEqual(30000, t);
        Assert.AreEqual(0, (t * 1001) % 30000);
    }

    [Test]
    public void ComputeVideoTrackTimescale_30fps_ProducesIntegerDelta()
    {
        long t = MediaNormalizer.ComputeVideoTrackTimescale(30, 1);
        Assert.Greater(t, 0);
        Assert.AreEqual(0, t % 30);
        // 1 フレームの tick 数 (t*1/30) が整数であること。
        Assert.AreEqual(0, t % 30);
    }

    [Test]
    public void ComputeVideoTrackTimescale_InvalidInput_ReturnsZero()
    {
        Assert.AreEqual(0, MediaNormalizer.ComputeVideoTrackTimescale(0, 100));
        Assert.AreEqual(0, MediaNormalizer.ComputeVideoTrackTimescale(2997, 0));
        Assert.AreEqual(0, MediaNormalizer.ComputeVideoTrackTimescale(-1, 100));
    }

    [Test]
    public void ParseTimeBaseDenominator_ReadsFfprobeOutput()
    {
        var output = "codec_type=video\n" +
                     "time_base=1/11988\n" +
                     "avg_frame_rate=2997/100\n" +
                     "r_frame_rate=2997/100\n" +
                     "codec_type=audio\n" +
                     "time_base=1/44100\n";
        Assert.AreEqual(11988, MediaNormalizer.ParseTimeBaseDenominator(output));
    }

    [Test]
    public void ParseTimeBaseDenominator_NoVideo_ReturnsZero()
    {
        // 音声のみファイル (m4a) は v:0 を選んでいるので出力が空になる。
        Assert.AreEqual(0, MediaNormalizer.ParseTimeBaseDenominator(""));
        Assert.AreEqual(0, MediaNormalizer.ParseTimeBaseDenominator(null));
        Assert.AreEqual(0, MediaNormalizer.ParseTimeBaseDenominator("codec_type=audio\ntime_base=1/44100\n"));
    }

    [Test]
    public void TryParseFrameRate_PrefersAvgFrameRate()
    {
        var output = "avg_frame_rate=2997/100\nr_frame_rate=30/1\n";
        int num, den;
        Assert.IsTrue(MediaNormalizer.TryParseFrameRate(output, out num, out den));
        Assert.AreEqual(2997, num);
        Assert.AreEqual(100, den);
    }

    [Test]
    public void TryParseFrameRate_FallsBackToRFrameRateWhenAvgIsZero()
    {
        var output = "avg_frame_rate=0/0\nr_frame_rate=30/1\n";
        int num, den;
        Assert.IsTrue(MediaNormalizer.TryParseFrameRate(output, out num, out den));
        Assert.AreEqual(30, num);
        Assert.AreEqual(1, den);
    }

    [Test]
    public void TryParseFrameRate_SingleNumber_TreatedAsIntegerFps()
    {
        var output = "avg_frame_rate=30\n";
        int num, den;
        Assert.IsTrue(MediaNormalizer.TryParseFrameRate(output, out num, out den));
        Assert.AreEqual(30, num);
        Assert.AreEqual(1, den);
    }

    [Test]
    public void TryParseFrameRate_Invalid_ReturnsFalse()
    {
        int num, den;
        Assert.IsFalse(MediaNormalizer.TryParseFrameRate("", out num, out den));
        Assert.IsFalse(MediaNormalizer.TryParseFrameRate("avg_frame_rate=N/A\n", out num, out den));
        Assert.IsFalse(MediaNormalizer.TryParseFrameRate("avg_frame_rate=0/0\n", out num, out den));
    }
}
