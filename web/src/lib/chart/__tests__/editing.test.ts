import { describe, expect, it } from "vitest";
import { exportChartJson } from "../export";
import { importChartJson } from "../import";
import { addNote, createNote, moveNote, removeNote, updateNote } from "../notes";
import { readRepositoryChart } from "./repository-charts";

/**
 * Editing a real chart and writing it back.
 *
 * The gate of issue #18: import a chart of the Unity project, edit notes, export
 * and re-import. The re-imported file must contain the edits and every
 * untouched note exactly as it was.
 */
describe("editing a real chart", () => {
  it("exports the edits and keeps every other note", () => {
    const imported = importChartJson(readRepositoryChart("Who.json"));
    expect(imported.ok).toBe(true);
    if (!imported.ok) {
      return;
    }

    const original = imported.chart;
    const originalNotes = original.notes;
    expect(originalNotes.length).toBe(139);

    // Edit 1: move the first note to lane 6 and to beat 2.5.
    const moved = moveNote(original, 0, 6, 2.5);
    expect(moved.notes[0]).toEqual({ type: 1, num: 10, block: 6, LPB: 4 });

    // Edit 2: delete the second note.
    const deleted = removeNote(moved, 1);
    expect(deleted.notes).toHaveLength(138);
    expect(
      deleted.notes.some(
        (note) => note.num === originalNotes[1].num && note.block === originalNotes[1].block,
      ),
    ).toBe(false);

    // Edit 3: add a new tap note on lane 3.
    const edited = addNote(deleted, createNote(3, 64, 4));
    expect(edited.notes).toHaveLength(139);

    const written = exportChartJson(edited);
    const reimported = importChartJson(written);
    expect(reimported.ok).toBe(true);
    if (!reimported.ok) {
      return;
    }

    const result = reimported.chart;

    expect(result.notes).toHaveLength(139);
    expect(result.notes[0]).toEqual({ type: 1, num: 10, block: 6, LPB: 4 });
    expect(result.notes[result.notes.length - 1]).toEqual({ type: 1, num: 64, block: 3, LPB: 4 });

    // Every note that was not edited keeps its lane, line and resolution.
    const untouched = result.notes.slice(1, 138);
    expect(untouched).toEqual(originalNotes.slice(2));
  });

  it("keeps the chart metadata and the lyrics when notes change", () => {
    const imported = importChartJson(readRepositoryChart("Who.json"));
    expect(imported.ok).toBe(true);
    if (!imported.ok) {
      return;
    }

    const edited = updateNote(imported.chart, 5, { ...imported.chart.notes[5], block: 0 });

    const written = exportChartJson(edited);
    const reimported = importChartJson(written);
    expect(reimported.ok).toBe(true);
    if (!reimported.ok) {
      return;
    }

    expect(reimported.chart.name).toBe(imported.chart.name);
    expect(reimported.chart.BPM).toBe(imported.chart.BPM);
    expect(reimported.chart.offset).toBe(imported.chart.offset);
    expect(reimported.chart.maxBlock).toBe(imported.chart.maxBlock);
    expect(reimported.chart.lyrics).toEqual(imported.chart.lyrics);
  });

  it("writes the file name Unity expects", () => {
    const imported = importChartJson(readRepositoryChart("Who.json"));
    expect(imported.ok).toBe(true);
    if (!imported.ok) {
      return;
    }

    expect(imported.chart.name).toBe("Who");
  });
});
