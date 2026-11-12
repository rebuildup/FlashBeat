using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// The percentile helper of the frame-time probe (issue #381): the numbers in a
/// performance report are only comparable when the same definition is used,
/// so the edges (empty sample, single sample, p99 of a short window) are fixed
/// here.
/// </summary>
public class FrameTimeProbeTests
{
    [Test]
    public void EmptySampleHasNoPercentile()
    {
        Assert.AreEqual(0f, FrameTimeProbe.PercentileMs(null, 50f));
        Assert.AreEqual(0f, FrameTimeProbe.PercentileMs(new List<float>(), 99f));
    }

    [Test]
    public void SingleSampleIsEveryPercentile()
    {
        var samples = new List<float> { 0.016f };

        Assert.AreEqual(16f, FrameTimeProbe.PercentileMs(samples, 50f), 0.001f);
        Assert.AreEqual(16f, FrameTimeProbe.PercentileMs(samples, 99f), 0.001f);
    }

    [Test]
    public void PercentilesIndexIntoTheSortedWindow()
    {
        // 1..100 ms, one value per millisecond.
        var samples = new List<float>();
        for (int i = 1; i <= 100; i++) samples.Add(i / 1000f);

        Assert.AreEqual(50f, FrameTimeProbe.PercentileMs(samples, 50f), 0.001f);
        Assert.AreEqual(95f, FrameTimeProbe.PercentileMs(samples, 95f), 0.001f);
        Assert.AreEqual(99f, FrameTimeProbe.PercentileMs(samples, 99f), 0.001f);
    }

    [Test]
    public void SmallWindowsClampToTheWorstFrame()
    {
        var samples = new List<float> { 0.010f, 0.030f };

        Assert.AreEqual(10f, FrameTimeProbe.PercentileMs(samples, 1f), 0.001f);
        Assert.AreEqual(30f, FrameTimeProbe.PercentileMs(samples, 99f), 0.001f);
    }
}
