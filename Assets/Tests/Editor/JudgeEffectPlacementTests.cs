using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// The judgement effect (PERFECT / GREAT / BAD / MISS) has to appear on the
/// lane that was hit. With two notes on the same timestamp the first pending
/// note is a different lane, so the lane of the judged note decides.
/// </summary>
public class JudgeEffectPlacementTests
{
    [Test]
    public void EffectUsesTheLaneOfTheJudgedNote()
    {
        var lanes = new List<int> { 0, 4 };

        Assert.AreEqual(new Vector3(-3.5f, 0.76f, 0.15f), Judge.EffectPosition(lanes, 0));
        Assert.AreEqual(new Vector3(0.5f, 0.76f, 0.15f), Judge.EffectPosition(lanes, 1));
    }

    [Test]
    public void EffectOfASimultaneousPairLandsOnTwoDifferentLanes()
    {
        var lanes = new List<int> { 5, 2 };

        var first = Judge.EffectPosition(lanes, 0);
        var second = Judge.EffectPosition(lanes, 1);

        Assert.AreNotEqual(first, second, "A simultaneous press must show its effect on every lane.");
        Assert.AreEqual(1.5f, first.x, 0.001f);
        Assert.AreEqual(-1.5f, second.x, 0.001f);
    }

    [Test]
    public void EffectOfAMissFallsBackToTheFirstPendingLane()
    {
        var lanes = new List<int> { 6, 1 };

        Assert.AreEqual(new Vector3(2.5f, 0.76f, 0.15f), Judge.EffectPosition(lanes, 0));
    }

    [Test]
    public void EffectOfAnUnknownNoteDoesNotThrow()
    {
        Assert.AreEqual(new Vector3(-3.5f, 0.76f, 0.15f), Judge.EffectPosition(new List<int> { 3 }, 7));
        Assert.AreEqual(new Vector3(-3.5f, 0.76f, 0.15f), Judge.EffectPosition(null, 0));
    }
}
