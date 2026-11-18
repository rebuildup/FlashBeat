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
/// limits, same-lane hold overlap, notes inside `durationSec`) stay on the
/// publish side (<c>web/src/lib/chart/validate.ts</c>): remote charts already
/// passed them, and the bundled charts are pinned by
/// <c>ChartDataIntegrityTests</c>. An empty note list stays valid — the
/// contract explicitly allows chartless songs.
/// </summary>
public static class ChartValidator
{
    /// <summary>Lane count of the playfield (same as Judge.LaneCount).</summary>
    public const int LaneCount = FlashBeat.Gameplay.Judge.LaneCount;

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

        return null;
    }
}
