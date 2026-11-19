using System.Collections.Generic;

/// <summary>
/// Input-boundary validation for chart data (Issue #632).
///
/// <see cref="Data"/> is deserialized with <c>JsonUtility.FromJson</c>, which
/// performs no semantic checks: a note with <c>block</c> outside 0-7, an
/// unknown <c>type</c>, a non-positive <c>LPB</c> or a missing <c>notes</c>
/// array all parse "successfully" and used to flow into gameplay. An
/// out-of-range lane was silently skipped by <see cref="Judge.EnsureQueue"/>
/// while <c>noteNum</c> still counted it, so the play could never reach
/// judgmentsComplete (no full-combo bonus, and a 422 on submission).
///
/// The rules mirror the structural bounds of the publish contract
/// (<c>contracts/schemas/chart.v1.schema.json</c>) restricted to the fields
/// gameplay actually consumes. Publish-only semantic rules (hold length
/// limits, notes inside `durationSec`) stay on the publish side
/// (<c>web/src/lib/chart/validate.ts</c>): remote charts already passed them,
/// and the bundled charts are pinned by <c>ChartDataIntegrityTests</c>.
///
/// Issue #641 adds two gameplay-side rejections the schema cannot express:
///
///   - an empty <c>notes</c> array. A chartless song is a *missing* chart
///     (NotesManager gets null and plays without notes); an empty array
///     would set noteNum = 0, which is playable but can never reach
///     judgmentsComplete, leaving a score-0 submission path.
///   - a note on a lane already occupied by a HOLD. While a hold is held,
///     <see cref="JudgeQueue.TryPress"/> rejects the lane's input and
///     <see cref="JudgeQueue.TryPeekMiss"/> marks the pending head as Miss,
///     so any same-lane note whose start falls inside a hold's span is
///     unhittable. The comparison is done in beats (<c>num / LPB</c>), the
///     same normalization the publish-side check uses
///     (<c>web/src/lib/chart/validate.ts</c>): two notes that occupy the
///     same beats overlap no matter which LPB their line numbers were
///     written in. A note starting exactly at the hold's end is allowed —
///     releasing and pressing again at the same moment is playable.
/// </summary>
public static class ChartValidator
{
    /// <summary>Lane count of the playfield (same as Judge.LaneCount).</summary>
    public const int LaneCount = Judge.LaneCount;

    /// <summary>Largest chart the contract accepts (schema maxItems).</summary>
    public const int MaxNotes = 10000;

    /// <summary>BPM range of the contract (schema minimum/maximum).</summary>
    public const int MinBpm = 1;
    public const int MaxBpm = 600;

    /// <summary>LPB range of the contract (schema minimum/maximum).</summary>
    public const int MinLpb = 1;
    public const int MaxLpb = 192;

    /// <summary>
    /// Returns null when the chart is playable, otherwise a short reason
    /// naming the first violation (used in logs and the rejection message).
    /// </summary>
    public static string Validate(Data chart)
    {
        if (chart == null)
        {
            return "chart is null";
        }
        if (chart.notes == null)
        {
            return "notes is missing";
        }
        if (chart.notes.Length == 0)
        {
            // Issue #641: a chartless song has no chart asset at all
            // (NotesManager.ParseChart returns null). An empty notes array
            // is not "no chart" — it yields noteNum = 0, a playable state
            // whose judgmentsComplete can never become true.
            return "notes is empty";
        }
        if (chart.BPM < MinBpm || chart.BPM > MaxBpm)
        {
            return $"BPM {chart.BPM} is outside {MinBpm}-{MaxBpm}";
        }
        if (chart.notes.Length > MaxNotes)
        {
            return $"notes has {chart.notes.Length} entries (max {MaxNotes})";
        }

        for (int i = 0; i < chart.notes.Length; i++)
        {
            Note note = chart.notes[i];
            if (note == null)
            {
                return $"note {i} is null";
            }
            if (note.type != FlashBeat.Gameplay.LongNote.TYPE_TAP
                && note.type != FlashBeat.Gameplay.LongNote.TYPE_HOLD)
            {
                return $"note {i} has unknown type {note.type}";
            }
            if (note.num < 0)
            {
                return $"note {i} has negative num {note.num}";
            }
            if (note.block < 0 || note.block >= LaneCount)
            {
                return $"note {i} has out-of-range lane {note.block} (valid 0-{LaneCount - 1})";
            }
            if (note.LPB < MinLpb || note.LPB > MaxLpb)
            {
                return $"note {i} has LPB {note.LPB} outside {MinLpb}-{MaxLpb}";
            }
            if (note.type == FlashBeat.Gameplay.LongNote.TYPE_HOLD
                && note.endNum <= note.num)
            {
                return $"note {i} is a hold whose endNum {note.endNum} does not follow num {note.num}";
            }
        }

        return FindLaneHoldOverlap(chart.notes);
    }

    /// <summary>
    /// First same-lane note whose start beat falls inside a HOLD's span, or
    /// null. Runs after the per-note checks, so every note here has a valid
    /// lane, type and positive LPB.
    ///
    /// Per lane the notes and the holds are sorted by start beat and swept
    /// once (O(n log n) overall — the chart may hold MaxNotes entries, so a
    /// quadratic pair scan is not acceptable on the load path).
    /// </summary>
    static string FindLaneHoldOverlap(Note[] notes)
    {
        // Beat position of a chart line: num / LPB (the same normalization
        // NotesManager.NoteTimeSeconds uses — 60 / BPM * num / LPB seconds).
        // All comparisons are in beats, so notes written at different LPB
        // resolutions still overlap correctly (mirrors the publish-side
        // beat check in web/src/lib/chart/validate.ts).
        int count = notes.Length;
        var startBeat = new double[count];
        var endBeat = new double[count];
        var laneNotes = new List<int>[LaneCount];
        var laneHolds = new List<int>[LaneCount];
        for (int lane = 0; lane < LaneCount; lane++)
        {
            laneNotes[lane] = new List<int>();
            laneHolds[lane] = new List<int>();
        }
        for (int i = 0; i < count; i++)
        {
            Note note = notes[i];
            startBeat[i] = note.num / (double)note.LPB;
            endBeat[i] = note.type == FlashBeat.Gameplay.LongNote.TYPE_HOLD
                ? note.endNum / (double)note.LPB
                : 0.0;
            laneNotes[note.block].Add(i);
            if (note.type == FlashBeat.Gameplay.LongNote.TYPE_HOLD)
            {
                laneHolds[note.block].Add(i);
            }
        }

        for (int lane = 0; lane < LaneCount; lane++)
        {
            List<int> holds = laneHolds[lane];
            List<int> laneNoteList = laneNotes[lane];
            if (holds.Count == 0 || laneNoteList.Count < 2) continue;

            holds.Sort((a, b) => startBeat[a].CompareTo(startBeat[b]));
            laneNoteList.Sort((a, b) => startBeat[a].CompareTo(startBeat[b]));

            // A note at beat s is blocked by a hold with startBeat <= s <
            // endBeat (a note starting exactly at the hold's end touches it
            // and stays playable; one starting at the hold's start shares
            // the same press and cannot be hit). While sweeping the notes in
            // start order, every hold whose start is <= s has been added;
            // the two longest of them are tracked so the note's own hold
            // (when the note is a hold) can be excluded.
            int holdPos = 0;
            int bestHold = -1, secondHold = -1;
            double bestEnd = double.NegativeInfinity, secondEnd = double.NegativeInfinity;
            foreach (int noteIndex in laneNoteList)
            {
                double s = startBeat[noteIndex];
                while (holdPos < holds.Count && startBeat[holds[holdPos]] <= s)
                {
                    int h = holds[holdPos];
                    if (endBeat[h] > bestEnd)
                    {
                        secondEnd = bestEnd; secondHold = bestHold;
                        bestEnd = endBeat[h]; bestHold = h;
                    }
                    else if (endBeat[h] > secondEnd)
                    {
                        secondEnd = endBeat[h]; secondHold = h;
                    }
                    holdPos++;
                }

                int blocking = bestHold != noteIndex ? bestHold : secondHold;
                double blockingEnd = bestHold != noteIndex ? bestEnd : secondEnd;
                if (blocking >= 0 && s < blockingEnd)
                {
                    return $"note {noteIndex} on lane {lane} starts inside the hold of note {blocking} " +
                           $"(beats {startBeat[blocking]:0.###}-{endBeat[blocking]:0.###}) and cannot be hit";
                }
            }
        }

        return null;
    }
}
