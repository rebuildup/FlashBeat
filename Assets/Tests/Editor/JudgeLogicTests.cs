using NUnit.Framework;
using UnityEngine;

public class JudgeLogicTests
{
    [Test]
    public void TestCircularSongIndexOffset()
    {
        int total = 26;

        // Given song 1, offset -2 should wrap to 25
        int song1OffsetMinus2 = GetSongIndexAtOffset(1, -2, total);
        Assert.AreEqual(25, song1OffsetMinus2);

        // Given song 1, offset -1 should wrap to 26
        int song1OffsetMinus1 = GetSongIndexAtOffset(1, -1, total);
        Assert.AreEqual(26, song1OffsetMinus1);

        // Given song 1, offset 0 should be 1
        int song1Offset0 = GetSongIndexAtOffset(1, 0, total);
        Assert.AreEqual(1, song1Offset0);

        // Given song 26, offset +1 should wrap to 1
        int song26Offset1 = GetSongIndexAtOffset(26, 1, total);
        Assert.AreEqual(1, song26Offset1);
    }

    private int GetSongIndexAtOffset(int currentSongID, int offset, int total)
    {
        int zeroBased = (currentSongID - 1 + offset) % total;
        if (zeroBased < 0) zeroBased += total;
        return zeroBased + 1;
    }

    [Test]
    public void TestTimeLagCalculation()
    {
        float currentTime = 10.5f;
        float noteTime = 10.4f;
        float startTime = 0.0f;

        float lag = Mathf.Abs(currentTime - (noteTime + startTime));
        Assert.AreEqual(0.1f, lag, 0.001f);
    }
}
