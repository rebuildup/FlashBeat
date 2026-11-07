import { useSyncExternalStore } from "react";
import { DEFAULT_SNAP_DIVISION, type SnapSetting } from "@/lib/chart/snap";

/**
 * The grid the notes are placed on, shared by the toolbar, the grid and the
 * editor.
 *
 * The toolbar picks the division and the grid places the notes on it. The
 * three live in the same screen, so they talk through this tiny store
 * instead of through a prop that would have to travel through the editor.
 *
 * The store is a plain module with a listener list, so it works with
 * `useSyncExternalStore` (React 18 and later) and can be read from a test
 * without a component.
 */

/** The division the grid uses right now. */
let currentDivision: SnapSetting = DEFAULT_SNAP_DIVISION;

/** The listeners that want to know when the division changes. */
const listeners = new Set<() => void>();

/** The division the notes are placed on. */
export function getSnapDivision(): SnapSetting {
  return currentDivision;
}

/** Pick another division. The listeners are told about the change. */
export function setSnapDivision(division: SnapSetting): void {
  if (division === currentDivision) {
    return;
  }
  currentDivision = division;
  for (const listener of listeners) {
    listener();
  }
}

/** Subscribe to the changes of the division. */
export function subscribeToSnapDivision(listener: () => void): () => void {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}

/**
 * The division the notes are placed on, as a React value.
 *
 * The toolbar, the grid and the editor read the same store, so a change of
 * the division is seen by all of them at once.
 */
export function useSnapDivision(): SnapSetting {
  return useSyncExternalStore(subscribeToSnapDivision, getSnapDivision, getSnapDivision);
}
