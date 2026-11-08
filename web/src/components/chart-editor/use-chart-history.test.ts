import { describe, expect, it } from "vitest";
import { SAMPLE_CHART } from "@/lib/chart/fixtures";
import { addNote, createNote, moveNote } from "@/lib/chart/notes";
import {
  HISTORY_LIMIT,
  chartHistoryReducer,
  clampSelection,
  initialHistory,
} from "./use-chart-history";

/**
 * The Undo/Redo history of the chart editor (issue #216).
 *
 * An edit is one step; the selection travels with it, so undoing a delete
 * brings the note back selected.
 */
describe("chartHistoryReducer", () => {
  const withNote = addNote(SAMPLE_CHART, createNote(2, 8, 4));

  it("records one step per commit and restores the previous chart on undo", () => {
    let state = initialHistory(SAMPLE_CHART);
    state = chartHistoryReducer(state, { type: "commit", chart: withNote, selectedIndex: 53 });

    expect(state.present.chart).toBe(withNote);
    expect(state.past).toHaveLength(1);

    state = chartHistoryReducer(state, { type: "undo" });
    expect(state.present.chart).toBe(SAMPLE_CHART);
    expect(state.present.selectedIndex).toBeNull();
    expect(state.future).toHaveLength(1);
  });

  it("brings a deleted note back selected", () => {
    let state = initialHistory(SAMPLE_CHART);
    state = chartHistoryReducer(state, { type: "select", selectedIndex: 4 });
    state = chartHistoryReducer(state, {
      type: "commit",
      chart: { ...SAMPLE_CHART, notes: SAMPLE_CHART.notes.filter((_, index) => index !== 4) },
      selectedIndex: null,
    });

    expect(state.present.selectedIndex).toBeNull();

    state = chartHistoryReducer(state, { type: "undo" });
    expect(state.present.chart).toBe(SAMPLE_CHART);
    expect(state.present.selectedIndex).toBe(4);
  });

  it("replays an undone edit on redo", () => {
    let state = initialHistory(SAMPLE_CHART);
    const moved = moveNote(SAMPLE_CHART, 0, 7, 2, "off");
    state = chartHistoryReducer(state, { type: "commit", chart: moved, selectedIndex: 0 });
    state = chartHistoryReducer(state, { type: "undo" });
    state = chartHistoryReducer(state, { type: "redo" });

    expect(state.present.chart).toBe(moved);
    expect(state.present.selectedIndex).toBe(0);
    expect(state.future).toHaveLength(0);
  });

  it("drops the redo stack when a new edit is committed", () => {
    let state = initialHistory(SAMPLE_CHART);
    state = chartHistoryReducer(state, { type: "commit", chart: withNote, selectedIndex: 53 });
    state = chartHistoryReducer(state, { type: "undo" });
    expect(state.future).toHaveLength(1);

    state = chartHistoryReducer(state, {
      type: "commit",
      chart: { ...SAMPLE_CHART, name: "renamed" },
      selectedIndex: null,
    });
    expect(state.future).toHaveLength(0);
  });

  it("clears the history when a different chart is opened", () => {
    let state = initialHistory(SAMPLE_CHART);
    state = chartHistoryReducer(state, { type: "commit", chart: withNote, selectedIndex: 53 });
    state = chartHistoryReducer(state, { type: "reset", chart: withNote });

    expect(state.past).toHaveLength(0);
    expect(state.future).toHaveLength(0);
    expect(state.present).toEqual({ chart: withNote, selectedIndex: null });
  });

  it("does not record a step when nothing changed", () => {
    let state = initialHistory(SAMPLE_CHART);
    state = chartHistoryReducer(state, { type: "commit", chart: SAMPLE_CHART });
    expect(state.past).toHaveLength(0);
  });

  it("keeps the history bounded", () => {
    let state = initialHistory(SAMPLE_CHART);
    for (let index = 0; index < HISTORY_LIMIT + 5; index += 1) {
      state = chartHistoryReducer(state, {
        type: "commit",
        chart: { ...SAMPLE_CHART, name: `step-${index}` },
      });
    }
    expect(state.past).toHaveLength(HISTORY_LIMIT);
  });

  it("clamps a selection to the notes of the chart", () => {
    expect(clampSelection(SAMPLE_CHART, -1)).toBeNull();
    expect(clampSelection(SAMPLE_CHART, 0)).toBe(0);
    expect(clampSelection(SAMPLE_CHART, SAMPLE_CHART.notes.length)).toBeNull();
    expect(clampSelection(null, 0)).toBeNull();
  });
});
