import { describe, expect, it } from "vitest";
import { importChartJson } from "../import";
import { beatToSeconds, noteBeat, noteTimeSec, secondsToBeat, snapBeat } from "../timing";
import { readRepositoryChart } from "./repository-charts";

/**
 * Timing of the legacy chart format.
 *
 * `Assets/Game/Scripts/Gameplay/NotesManager.cs` computes the hit time of a
 * note as `((60 / (BPM * LPB)) * LPB * num) / LPB`. The editor uses the same
 * formula, written as `60 / BPM * num / LPB`.
 */
function unityNoteTime(bpm: number, num: number, lpb: number): number {
  const kankaku = 60 / (bpm * lpb);
  const beatSec = kankaku * lpb;
  return (beatSec * num) / lpb;
}

describe("note timing", () => {
  it("matches the Unity formula for every note of a real chart", () => {
    const imported = importChartJson(readRepositoryChart("Chartreuse.json"));
    expect(imported.ok).toBe(true);
    if (!imported.ok) {
      return;
    }

    for (const note of imported.chart.notes) {
      const expected = unityNoteTime(imported.chart.BPM, note.num, note.LPB);
      expect(Math.abs(noteTimeSec(note, imported.chart.BPM) - expected)).toBeLessThan(1e-6);
    }
  });

  it("places a note at num / LPB beats", () => {
    expect(noteBeat({ type: 1, num: 5, block: 0, LPB: 4 })).toBe(1.25);
    expect(noteBeat({ type: 1, num: 5, block: 0, LPB: 8 })).toBe(0.625);
  });

  it("gives the same time for the same beat in both resolutions", () => {
    const coarse = { type: 1, num: 2, block: 0, LPB: 4 };
    const fine = { type: 1, num: 4, block: 0, LPB: 8 };

    expect(noteTimeSec(coarse, 128)).toBeCloseTo(noteTimeSec(fine, 128), 12);
  });

  it("converts between beats and seconds", () => {
    expect(beatToSeconds(1, 120)).toBe(0.5);
    expect(secondsToBeat(0.5, 120)).toBe(1);
    expect(beatToSeconds(4, 240)).toBe(1);
  });

  it("snaps a beat onto the grid", () => {
    expect(snapBeat(1.24, 4)).toBe(1.25);
    expect(snapBeat(1.3, 4)).toBe(1.25);
    expect(snapBeat(1.3, 8)).toBe(1.25);
    expect(snapBeat(1.3, 16)).toBe(1.3125);
  });
});
