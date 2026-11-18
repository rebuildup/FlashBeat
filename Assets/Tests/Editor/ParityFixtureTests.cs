using System;
using System.IO;
using FlashBeat.Gameplay;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Unity ↔ Web parity contract tests (Issue #612).
///
/// Both implementations are verified against the same shared vectors in
/// `contracts/parity/`:
///
///   - score-vectors.json: the v1/v2/v3 formulas of `Scoring` /
///     `Judge.ComputeScore`, including the Issue #618 rule that the v3
///     full-combo bonus vests only when judgmentsComplete. The Web suite
///     (`web/tests/parity-score.test.ts`) checks the same file against
///     `computeUnityScore` / `computeUnityScoreV2` / `computeUnityScoreV3`.
///   - chart-timing.json: `NotesManager.NoteTimeSeconds` and
///     `LongNote.EndSeconds` against the float32 values the Web suite
///     reproduces with the same Math.fround operation order.
///
/// The fixtures are the single source of truth — nothing is generated or
/// copied — so a change on either side that breaks parity fails here.
///
/// Float note: the v1/v2 fixture values are the strict single-precision
/// results (`Math.fround` emulation, the contract the Web suite pins
/// exactly). The EditMode suite runs on the Unity Editor Mono JIT, which is
/// observed to keep double intermediates in some float expressions (a
/// last-ulp difference versus strict float32), so v1/v2 scores allow a ±1
/// point tolerance. The v3 scores are asserted **exactly**: since Issue
/// #631 both sides compute them in integer arithmetic, which has no
/// platform-dependent rounding (see contracts/parity/README.md).
/// </summary>
public class ParityFixtureTests
{
    private static string RepoFile(params string[] parts)
    {
        string[] all = new string[parts.Length + 2];
        all[0] = Application.dataPath;
        all[1] = "..";
        Array.Copy(parts, 0, all, 2, parts.Length);
        return Path.GetFullPath(Path.Combine(all));
    }

    // --- score vectors ----------------------------------------------------

    [Serializable]
    private class ScoreCase
    {
        public string name;
        public int perfect;
        public int great;
        public int bad;
        public int miss;
        public int maxCombo;
        public int comboMilestones;
        public int noteCount;
        public bool judgmentsComplete;
        public int scoreV1;
        public int scoreV2;
        public int scoreV3;
        public int scoreV3Settled;
    }

    [Serializable]
    private class ScoreFixture
    {
        public string format;
        public ScoreCase[] cases;
        // v3SubmissionChecks is consumed by the Web suite only (server-side
        // bound checks have no Unity counterpart); JsonUtility ignores it.
    }

    /// <summary>The v1 score exactly like the client computes it (Judge.ComputeScore).</summary>
    private static int ScoreV1(int perfect, int great, int bad, int noteCount)
    {
        float maxScore = noteCount * 5;
        float ratioScore = perfect * 5f + great * 3f + bad;
        return Judge.ComputeScore(ratioScore, maxScore);
    }

    private static void AssertScoreWithin(int expected, int actual, string label)
    {
        Assert.LessOrEqual(System.Math.Abs(expected - actual), 1,
            label + ": expected " + expected + " +/-1, got " + actual);
    }

    /// <summary>
    /// Time equality within float32/JIT slack (~1e-6 relative). A wrong
    /// formula (wrong operand order, beats vs seconds, missing LPB) differs
    /// by orders of magnitude more than this.
    /// </summary>
    private static void AssertTimeWithin(double expected, float actual, string label)
    {
        double tolerance = System.Math.Max(1e-7, System.Math.Abs(expected) * 1e-6);
        Assert.LessOrEqual(System.Math.Abs(expected - actual), tolerance,
            label + ": expected " + expected + ", got " + actual);
    }

    [Test]
    public void ScoreVectors_MatchEveryVersion()
    {
        string json = File.ReadAllText(RepoFile("contracts", "parity", "score-vectors.json"));
        ScoreFixture fixture = JsonUtility.FromJson<ScoreFixture>(json);
        Assert.IsNotNull(fixture, "score-vectors.json did not parse");
        Assert.IsNotNull(fixture.cases);
        Assert.Greater(fixture.cases.Length, 0);

        foreach (ScoreCase c in fixture.cases)
        {
            string label = "case '" + c.name + "'";
            // v1/v2: ±1 — a last-ulp float difference can flip the final
            // Floor() by one point (see the class comment); anything larger
            // is drift. v3 is exact integer arithmetic on both sides (Issue
            // #631), so it must match the fixture bit-for-bit.
            AssertScoreWithin(c.scoreV1, ScoreV1(c.perfect, c.great, c.bad, c.noteCount), label + " v1");
            AssertScoreWithin(c.scoreV2,
                Scoring.ScoreV2(c.perfect, c.great, c.bad, c.miss, c.maxCombo, c.noteCount),
                label + " v2");
            Assert.AreEqual(c.scoreV3,
                Scoring.ScoreV3(c.perfect, c.great, c.bad, c.miss, c.maxCombo,
                    c.comboMilestones, c.noteCount, c.judgmentsComplete),
                label + " v3 (judgmentsComplete=" + c.judgmentsComplete + ")");
            Assert.AreEqual(c.scoreV3Settled,
                Scoring.ScoreV3(c.perfect, c.great, c.bad, c.miss, c.maxCombo,
                    c.comboMilestones, c.noteCount, true),
                label + " v3 settled");
        }
    }

    [Test]
    public void ScoreVectors_IncompletePlayNeverVestsFinalBonus()
    {
        // Explicit Issue #618 check on top of the vector equality: for every
        // case with judgmentsComplete == false the live score must be the
        // settled score minus the 100-point full-combo bonus effect (or equal
        // when miss > 0 already zeroed it).
        string json = File.ReadAllText(RepoFile("contracts", "parity", "score-vectors.json"));
        ScoreFixture fixture = JsonUtility.FromJson<ScoreFixture>(json);
        foreach (ScoreCase c in fixture.cases)
        {
            if (c.judgmentsComplete || c.miss != 0)
            {
                continue;
            }

            Assert.Less(c.scoreV3, c.scoreV3Settled,
                "case '" + c.name + "': live in-progress score must exclude the final bonus");
        }
    }

    // --- chart timing vectors ----------------------------------------------

    [Serializable]
    private class TimingCase
    {
        public string name;
        public int bpm;
        public int num;
        public int lpb;
        /// <summary>Hold end line; 0 (absent) means a tap note.</summary>
        public int endNum;
        public double seconds;
        public double endSeconds;
    }

    [Serializable]
    private class TimingFixture
    {
        public string format;
        public TimingCase[] cases;
    }

    [Test]
    public void ChartTiming_MatchesEveryVector()
    {
        string json = File.ReadAllText(RepoFile("contracts", "parity", "chart-timing.json"));
        TimingFixture fixture = JsonUtility.FromJson<TimingFixture>(json);
        Assert.IsNotNull(fixture, "chart-timing.json did not parse");
        Assert.IsNotNull(fixture.cases);
        Assert.Greater(fixture.cases.Length, 0);

        foreach (TimingCase c in fixture.cases)
        {
            string label = "case '" + c.name + "'";
            var note = new Note { num = c.num, LPB = c.lpb, endNum = c.endNum };

            // The fixture pins the strict float32 result; the Editor JIT may
            // differ in the last ulp (see the class comment).
            AssertTimeWithin(c.seconds, NotesManager.NoteTimeSeconds(c.bpm, note), label);

            if (c.endNum > 0)
            {
                AssertTimeWithin(c.endSeconds, LongNote.EndSeconds(c.bpm, note),
                    label + " end");
            }
        }
    }
}
