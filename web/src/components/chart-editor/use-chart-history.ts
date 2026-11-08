import { useCallback, useReducer } from "react";
import type { Chart } from "@/lib/chart/types";

/**
 * Undo/redo of the chart editor.
 *
 * Every edit of the chart is a step the operator can take back with the
 * Undo button or Ctrl/Cmd+Z (issue #216). The selection travels with the
 * step: an undone delete brings the note back *and* selects it again, so
 * the operator sees what was restored.
 *
 * The reducer is exported so the history can be tested without a component.
 * `chart` is treated as an immutable value by `lib/chart/notes.ts`; a step
 * is recorded only when the chart object actually changes.
 */

/** One point in the edit history: the chart and what was selected. */
export interface EditorSnapshot {
  chart: Chart | null;
  selectedIndex: number | null;
}

export interface ChartHistoryState {
  /** Older states, oldest first. */
  past: EditorSnapshot[];
  present: EditorSnapshot;
  /** States that were undone, newest first. */
  future: EditorSnapshot[];
}

export type ChartHistoryAction =
  | {
      type: "commit";
      chart: Chart | null;
      /** The selection after the edit; the current one when left out. */
      selectedIndex?: number | null;
    }
  | { type: "select"; selectedIndex: number | null }
  | { type: "undo" }
  | { type: "redo" }
  | { type: "reset"; chart: Chart | null; selectedIndex?: number | null };

/** Upper bound of the steps that are kept. */
export const HISTORY_LIMIT = 100;

/** Keep the selection inside the notes of the chart. */
export function clampSelection(chart: Chart | null, index: number | null): number | null {
  if (index === null || !chart || index < 0 || index >= chart.notes.length) {
    return null;
  }
  return index;
}

function snapshot(chart: Chart | null, selectedIndex: number | null): EditorSnapshot {
  return { chart, selectedIndex: clampSelection(chart, selectedIndex) };
}

export function chartHistoryReducer(
  state: ChartHistoryState,
  action: ChartHistoryAction,
): ChartHistoryState {
  switch (action.type) {
    case "commit": {
      const nextSelection =
        action.selectedIndex === undefined ? state.present.selectedIndex : action.selectedIndex;
      const next = snapshot(action.chart, nextSelection);

      if (
        next.chart === state.present.chart &&
        next.selectedIndex === state.present.selectedIndex
      ) {
        return state;
      }

      return {
        past: [...state.past, state.present].slice(-HISTORY_LIMIT),
        present: next,
        future: [],
      };
    }

    case "select": {
      const selectedIndex = clampSelection(state.present.chart, action.selectedIndex);
      if (selectedIndex === state.present.selectedIndex) {
        return state;
      }
      return {
        ...state,
        present: { ...state.present, selectedIndex },
      };
    }

    case "undo": {
      const previous = state.past.at(-1);
      if (!previous) {
        return state;
      }
      return {
        past: state.past.slice(0, -1),
        present: previous,
        future: [state.present, ...state.future],
      };
    }

    case "redo": {
      const [next, ...rest] = state.future;
      if (!next) {
        return state;
      }
      return {
        past: [...state.past, state.present].slice(-HISTORY_LIMIT),
        present: next,
        future: rest,
      };
    }

    case "reset":
      return {
        past: [],
        present: snapshot(action.chart, action.selectedIndex ?? null),
        future: [],
      };
  }
}

/** The empty history for a chart the editor opens with. */
export function initialHistory(chart: Chart | null): ChartHistoryState {
  return { past: [], present: { chart, selectedIndex: null }, future: [] };
}

export interface ChartHistory {
  chart: Chart | null;
  selectedIndex: number | null;
  canUndo: boolean;
  canRedo: boolean;
  /** Record an edit. The previous chart and selection become the undo step. */
  commit(chart: Chart | null, selectedIndex?: number | null): void;
  /** Change the selection without adding a step. */
  select(selectedIndex: number | null): void;
  undo(): void;
  redo(): void;
  /** Drop the history (a different chart was imported or opened). */
  reset(chart: Chart | null): void;
}

export function useChartHistory(initialChart: Chart | null): ChartHistory {
  const [state, dispatch] = useReducer(chartHistoryReducer, initialChart, initialHistory);

  const commit = useCallback((chart: Chart | null, selectedIndex?: number | null) => {
    dispatch({ type: "commit", chart, selectedIndex });
  }, []);

  const select = useCallback((selectedIndex: number | null) => {
    dispatch({ type: "select", selectedIndex });
  }, []);

  const undo = useCallback(() => {
    dispatch({ type: "undo" });
  }, []);

  const redo = useCallback(() => {
    dispatch({ type: "redo" });
  }, []);

  const reset = useCallback((chart: Chart | null) => {
    dispatch({ type: "reset", chart });
  }, []);

  return {
    chart: state.present.chart,
    selectedIndex: state.present.selectedIndex,
    canUndo: state.past.length > 0,
    canRedo: state.future.length > 0,
    commit,
    select,
    undo,
    redo,
    reset,
  };
}
