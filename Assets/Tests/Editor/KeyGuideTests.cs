using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class KeyGuideTests
{
    private static readonly KeyCode[][] ExpectedLaneKeys = new KeyCode[][]
    {
        new KeyCode[] { KeyCode.Alpha1, KeyCode.Q, KeyCode.A, KeyCode.Z },
        new KeyCode[] { KeyCode.Alpha2, KeyCode.W, KeyCode.S, KeyCode.X },
        new KeyCode[] { KeyCode.Alpha3, KeyCode.E, KeyCode.D, KeyCode.C },
        new KeyCode[] { KeyCode.Alpha4, KeyCode.Alpha5, KeyCode.R, KeyCode.T, KeyCode.F, KeyCode.G, KeyCode.V, KeyCode.B },
        new KeyCode[] { KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Y, KeyCode.U, KeyCode.H, KeyCode.J, KeyCode.N, KeyCode.M },
        new KeyCode[] { KeyCode.Alpha8, KeyCode.I, KeyCode.K, KeyCode.Less, KeyCode.Comma },
        new KeyCode[] { KeyCode.Alpha9, KeyCode.O, KeyCode.L, KeyCode.Greater, KeyCode.Period },
        new KeyCode[] { KeyCode.Alpha0, KeyCode.Equals, KeyCode.Caret, KeyCode.Backslash, KeyCode.P, KeyCode.At, KeyCode.LeftBracket, KeyCode.Semicolon, KeyCode.Colon, KeyCode.RightBracket, KeyCode.Slash, KeyCode.Underscore, KeyCode.Minus, KeyCode.Asterisk, KeyCode.BackQuote }
    };

    [Test]
    public void TestLaneKeysHasEightLanes()
    {
        Assert.AreEqual(8, Judge.LaneKeys.Length);
    }

    [Test]
    public void TestLaneKeysMatchCanonicalDefinition()
    {
        for (int lane = 0; lane < ExpectedLaneKeys.Length; lane++)
        {
            Assert.AreEqual(ExpectedLaneKeys[lane], Judge.LaneKeys[lane],
                $"Lane {lane} key set diverges from the canonical definition.");
        }
    }

    [Test]
    public void TestLaneKeysAreDisjointAcrossLanes()
    {
        var seen = new HashSet<KeyCode>();
        for (int lane = 0; lane < Judge.LaneKeys.Length; lane++)
        {
            foreach (var key in Judge.LaneKeys[lane])
            {
                Assert.IsTrue(seen.Add(key), $"KeyCode {key} appears in more than one lane (lane {lane}).");
            }
        }
        Assert.AreEqual(8, Judge.LaneKeys.Length);
    }

    [Test]
    public void TestBuildLaneLabelContainsEveryKeyOfLane()
    {
        for (int lane = 0; lane < Judge.LaneKeys.Length; lane++)
        {
            string label = KeyGuideUI.BuildLaneLabel(lane);
            foreach (var key in Judge.LaneKeys[lane])
            {
                string name = KeyGuideUI.GetKeyDisplayName(key);
                Assert.IsTrue(label.Contains(name), $"Lane {lane} label '{label}' is missing key '{name}'.");
            }
        }
    }

    [Test]
    public void TestGetKeyDisplayNameCoversSymbolKeys()
    {
        Assert.AreEqual("-", KeyGuideUI.GetKeyDisplayName(KeyCode.Minus));
        Assert.AreEqual("^", KeyGuideUI.GetKeyDisplayName(KeyCode.Caret));
        Assert.AreEqual("@", KeyGuideUI.GetKeyDisplayName(KeyCode.At));
        Assert.AreEqual(":", KeyGuideUI.GetKeyDisplayName(KeyCode.Colon));
        Assert.AreEqual("0", KeyGuideUI.GetKeyDisplayName(KeyCode.Alpha0));
    }

    [Test]
    public void TestCreateGeneratesOneRowPerLaneFromJudgeLaneKeys()
    {
        var host = new GameObject("KeyGuideHost");
        try
        {
            KeyGuideUI guide = KeyGuideUI.Create(host);
            Assert.IsNotNull(guide);

            for (int lane = 0; lane < Judge.LaneKeys.Length; lane++)
            {
                string expected = KeyGuideUI.BuildLaneLabel(lane);
                string row = guide.GetRowLabel(lane);
                Assert.IsNotNull(row, $"Lane {lane} row is missing from the generated guide.");
                Assert.AreEqual(expected, row, $"Lane {lane} row must be generated from Judge.LaneKeys.");
            }
            Assert.IsNull(guide.GetRowLabel(Judge.LaneKeys.Length), "Guide must not have more rows than lanes.");
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }
}
