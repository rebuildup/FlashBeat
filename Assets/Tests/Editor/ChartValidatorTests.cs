using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Input-boundary validation of chart data (Issue #632).
///
/// <see cref="ChartValidator"/> runs on every chart the game loads —
/// bundled <c>Resources</c> charts and remote published charts alike —
/// before a single note reaches the judge. These tests pin both sides of
/// the contract: every violation the publish schema rejects is rejected
/// here, and the charts that actually ship still pass.
/// </summary>
public class ChartValidatorTests
{
    static Data ValidChart()
    {
        return new Data
        {
            BPM = 120,
            notes = new[]
            {
                new Note { type = 1, num = 0, block = 0, LPB = 4 },
                new Note { type = 1, num = 4, block = 7, LPB = 4 },
                new Note { type = 2, num = 8, endNum = 12, block = 3, LPB = 4 },
            },
        };
    }

    [Test]
    public void AValidChartPasses()
    {
        Assert.IsNull(ChartValidator.Validate(ValidChart()));
    }

    [Test]
    public void AnEmptyNoteListIsRejected()
    {
        // Issue #641: a chartless song has no chart asset at all (null). An
        // empty notes array used to pass and produced noteNum = 0 — a
        // playable state whose judgmentsComplete can never become true.
        var chart = ValidChart();
        chart.notes = new Note[0];
        string reason = ChartValidator.Validate(chart);
        Assert.IsNotNull(reason);
        StringAssert.Contains("empty", reason);
    }

    [Test]
    public void ANullChartIsRejected()
    {
        Assert.IsNotNull(ChartValidator.Validate(null));
    }

    [Test]
    public void AMissingNotesArrayIsRejected()
    {
        var chart = ValidChart();
        chart.notes = null;
        Assert.IsNotNull(ChartValidator.Validate(chart));
    }

    [Test]
    public void AnOutOfRangeLaneIsRejected()
    {
        // Issue #632: block 8 used to be skipped silently by Judge.EnsureQueue
        // while noteNum still counted it, leaving judgmentsComplete false.
        foreach (int block in new[] { -1, 8, 100 })
        {
            var chart = ValidChart();
            chart.notes[0].block = block;
            string reason = ChartValidator.Validate(chart);
            Assert.IsNotNull(reason, $"block {block} must be rejected");
            StringAssert.Contains("lane", reason);
        }
    }

    [Test]
    public void AnUnknownNoteTypeIsRejected()
    {
        var chart = ValidChart();
        chart.notes[0].type = 3;
        Assert.IsNotNull(ChartValidator.Validate(chart));
    }

    [Test]
    public void ANonPositiveResolutionIsRejected()
    {
        var chart = ValidChart();
        chart.notes[0].LPB = 0;
        Assert.IsNotNull(ChartValidator.Validate(chart));

        chart = ValidChart();
        chart.notes[0].LPB = 193;
        Assert.IsNotNull(ChartValidator.Validate(chart));
    }

    [Test]
    public void ANegativeLineNumberIsRejected()
    {
        var chart = ValidChart();
        chart.notes[0].num = -4;
        Assert.IsNotNull(ChartValidator.Validate(chart));
    }

    [Test]
    public void AHoldThatDoesNotFollowItsStartIsRejected()
    {
        var chart = ValidChart();
        chart.notes[2].endNum = chart.notes[2].num;
        Assert.IsNotNull(ChartValidator.Validate(chart));
    }

    [Test]
    public void ATapInsideAHoldOnTheSameLaneIsRejected()
    {
        // Issue #641: while a hold is held, TryPress rejects the lane, so a
        // same-lane note inside the hold's span is unhittable.
        var chart = ValidChart();
        chart.notes = new[]
        {
            new Note { type = 2, num = 0, endNum = 16, block = 2, LPB = 4 },   // beats 0-4
            new Note { type = 1, num = 8, block = 2, LPB = 4 },              // beat 2: inside
        };
        string reason = ChartValidator.Validate(chart);
        Assert.IsNotNull(reason);
        StringAssert.Contains("inside the hold", reason);
    }

    [Test]
    public void AHeldNoteInsideAnotherHoldOnTheSameLaneIsRejected()
    {
        var chart = ValidChart();
        chart.notes = new[]
        {
            new Note { type = 2, num = 0, endNum = 16, block = 2, LPB = 4 },  // beats 0-4
            new Note { type = 2, num = 8, endNum = 20, block = 2, LPB = 4 }, // beats 2-5: overlaps
        };
        Assert.IsNotNull(ChartValidator.Validate(chart));
    }

    [Test]
    public void ANoteInsideAHoldOnADifferentLaneIsAccepted()
    {
        var chart = ValidChart();
        chart.notes = new[]
        {
            new Note { type = 2, num = 0, endNum = 16, block = 2, LPB = 4 },  // beats 0-4 on lane 2
            new Note { type = 1, num = 8, block = 5, LPB = 4 },              // beat 2 on lane 5
        };
        Assert.IsNull(ChartValidator.Validate(chart));
    }

    [Test]
    public void ANoteStartingExactlyAtTheHoldEndIsAccepted()
    {
        // Touching is playable: the player releases and presses again at the
        // same moment (same rule as the publish-side beat check).
        var chart = ValidChart();
        chart.notes = new[]
        {
            new Note { type = 2, num = 0, endNum = 16, block = 2, LPB = 4 },  // beats 0-4
            new Note { type = 1, num = 16, block = 2, LPB = 4 },             // beat 4: touches the end
        };
        Assert.IsNull(ChartValidator.Validate(chart));
    }

    [Test]
    public void OverlapIsDetectedAcrossDifferentLpbResolutions()
    {
        // The publish-side check compares beats, not raw line numbers: a note
        // written at LPB 8 inside a hold written at LPB 4 still overlaps.
        var chart = ValidChart();
        chart.notes = new[]
        {
            new Note { type = 2, num = 0, endNum = 32, block = 2, LPB = 8 },  // beats 0-4
            new Note { type = 1, num = 20, block = 2, LPB = 8 },             // beat 2.5: inside
        };
        Assert.IsNotNull(ChartValidator.Validate(chart));
    }

    [Test]
    public void SequentialHoldsOnTheSameLaneAreAccepted()
    {
        var chart = ValidChart();
        chart.notes = new[]
        {
            new Note { type = 2, num = 0, endNum = 16, block = 2, LPB = 4 },  // beats 0-4
            new Note { type = 2, num = 16, endNum = 32, block = 2, LPB = 4 }, // beats 4-8: touches
            new Note { type = 1, num = 40, block = 2, LPB = 4 },             // beat 10: after
        };
        Assert.IsNull(ChartValidator.Validate(chart));
    }

    [Test]
    public void AnOutOfRangeBpmIsRejected()
    {
        var chart = ValidChart();
        chart.BPM = 0;
        Assert.IsNotNull(ChartValidator.Validate(chart));

        chart = ValidChart();
        chart.BPM = 601;
        Assert.IsNotNull(ChartValidator.Validate(chart));
    }

    [Test]
    public void EveryBundledChartPassesTheValidator()
    {
        // The shipping charts must never hit the rejection path — if one
        // does, the song becomes unplayable at the exhibition.
        var seen = new HashSet<string>();
        var rejected = new List<string>();
        foreach (var song in GManager.SongName)
        {
            if (!seen.Add(song) || song == "noSong") continue;
            // ニセモノ is the unplayable cloud-catalog placeholder (#95): its
            // chart exists but carries zero notes, and Issue #641 makes an
            // empty notes array a rejection. It is never selectable, so it
            // is outside the "shipping charts" contract of this test.
            if (song == "ニセモノ") continue;
            var asset = Resources.Load<TextAsset>(song);
            if (asset == null) continue;
            var chart = JsonUtility.FromJson<Data>(asset.text);
            string reason = ChartValidator.Validate(chart);
            if (reason != null) rejected.Add($"{song}: {reason}");
        }
        Assert.IsEmpty(rejected, "bundled charts rejected by the validator: " + string.Join(", ", rejected));
    }
}
