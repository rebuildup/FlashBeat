/**
 * BPM grid snapping (issue #180).
 *
 * The editor places notes on a grid that follows the tempo of the chart:
 * one beat lasts `60 / BPM` seconds, exactly like the Unity player computes
 * it (`Assets/Game/Scripts/Gameplay/NotesManager.cs`). A division says how
 * many of those steps fit into one beat: 1 is the whole beat, 2 the half
 * beat, 4 the quarter beat and so on up to 16.
 *
 * Everything in this file is a pure function. No state, no I/O, no React:
 * the grid can be reasoned about (and tested) without a canvas.
 *
 * The rounding is "nearest, ties go up" (half-up) and is computed on the
 * *index* of the step rather than on the time itself, so a value that sits
 * across a bar or a beat boundary lands on the same step it would land on
 * if it had been reached from the other side.
 */

/** The divisions the grid offers, from the coarsest to the finest. */
export const SNAP_DIVISIONS = [1, 2, 4, 8, 16] as const;

/** A division of the beat. */
export type SnapDivision = (typeof SNAP_DIVISIONS)[number];

/** The grid the operator picked: a division, or no grid at all. */
export type SnapSetting = SnapDivision | "off";

/** Every setting the toolbar offers, in the order it shows them. */
export const SNAP_SETTINGS: readonly SnapSetting[] = ["off", ...SNAP_DIVISIONS];

/** The grid the editor starts with: a quarter beat. */
/**
 * The grid the editor starts with.
 *
 * `off` keeps the behaviour the editor had before the grid was introduced:
 * the notes are placed on the lines-per-beat of the chart. The operator
 * picks a division when the notes shall sit on a coarser beat grid.
 */
export const DEFAULT_SNAP_DIVISION: SnapSetting = "off";

/**
 * Label of a setting, as the toolbar shows it.
 */
export function snapDivisionLabel(division: SnapSetting): string {
  return division === "off" ? "off" : `1/${division}`;
}

/**
 * The resolution (lines per beat) a division needs.
 *
 * A note carries its own resolution: its line index is counted in those
 * lines. A note of 4 lines per beat therefore has no line for the eighth
 * of a beat, so a 1/8 grid needs notes of 8 lines per beat and a 1/16 grid
 * needs 16. `off` needs no resolution at all and gives 1, so the caller
 * keeps the resolution it already had.
 */
export function snapResolution(division: SnapSetting): number {
  return isSnapEnabled(division) ? division : 1;
}

/**
 * True when the setting keeps the notes on a grid.
 *
 * The type guard narrows the setting to a division, so the callers can use
 * it without a second check.
 */
export function isSnapEnabled(division: SnapSetting): division is SnapDivision {
  return division !== "off";
}

/**
 * True when the BPM can be used for a grid.
 *
 * A chart without a tempo (0, a negative number, NaN) has no grid to snap
 * to, so the notes are placed freely instead of breaking the chart.
 */
export function isSnapableBpm(bpm: number): boolean {
  return Number.isFinite(bpm) && bpm > 0;
}

/**
 * Seconds of one beat at the given BPM.
 *
 * The same formula the Unity player uses, so the editor and the game agree
 * on where a beat is. A BPM that cannot carry a grid gives `0`.
 */
export function beatDuration(bpm: number): number {
  if (!isSnapableBpm(bpm)) {
    return 0;
  }
  return 60 / bpm;
}

/**
 * Seconds of one step of the given division.
 *
 * A division of 4 is a quarter beat, so its step lasts a quarter of a beat.
 */
export function snapDuration(bpm: number, division: SnapDivision): number {
  if (!isSnapableBpm(bpm) || division <= 0) {
    return 0;
  }
  return beatDuration(bpm) / division;
}

/**
 * Beat position of a point in time.
 *
 * This is `seconds * BPM / 60`, the inverse of `beatDuration`. It is
 * computed from the tempo, not from a rounded step count, so the value stays
 * exact for the whole song.
 */
export function timeToBeat(time: number, bpm: number): number {
  if (!isSnapableBpm(bpm) || !Number.isFinite(time)) {
    return 0;
  }
  return (time * bpm) / 60;
}

/**
 * Time of a beat position, in seconds from the start of the song.
 */
export function beatToTime(beat: number, bpm: number): number {
  if (!isSnapableBpm(bpm) || !Number.isFinite(beat)) {
    return 0;
  }
  return beatDuration(bpm) * beat;
}

/**
 * Index of the step a beat position falls on.
 *
 * The index is the step counted from the beginning of the song, so the
 * rounding is the same everywhere: the third beat of a 4/4 bar is step
 * `3 * division`, not a special case of its own.
 *
 * Ties go up: a value that sits exactly between two steps (0.5 of a step)
 * moves to the later one.
 */
export function snapStepIndex(beat: number, division: SnapDivision): number {
  if (!Number.isFinite(beat) || division <= 0) {
    return 0;
  }
  return Math.floor(beat * division + 0.5);
}

/**
 * Beat position of a step index.
 */
export function stepIndexToBeat(index: number, division: SnapDivision): number {
  if (!Number.isFinite(index) || division <= 0) {
    return 0;
  }
  return index / division;
}

/**
 * Snap a beat position onto the grid of the given division.
 *
 * `off` (or any division that is not a number) returns the beat unchanged,
 * so the caller can pass the setting straight through.
 */
export function snapBeat(beat: number, division: SnapSetting): number {
  if (!isSnapEnabled(division)) {
    return beat;
  }
  return stepIndexToBeat(snapStepIndex(beat, division), division);
}

/**
 * Snap a beat position onto the grid, but never onto the grid *before* it.
 *
 * The difference to `snapBeat` is the rounding at the lower bound: a beat
 * that sits just before a step moves to the next step instead of back to
 * the previous one. The end of a hold therefore never ends before the hold
 * starts.
 */
export function snapBeatExclusive(beat: number, division: SnapSetting): number {
  if (!isSnapEnabled(division)) {
    return beat;
  }

  const index = snapStepIndex(beat, division);
  const snapped = stepIndexToBeat(index, division);

  // The nearest step was behind the value: the next step is the one that
  // keeps the order.
  if (snapped < beat) {
    return stepIndexToBeat(index + 1, division);
  }

  return snapped;
}

/**
 * Snap a point in time onto the grid of the given division.
 *
 * The time is turned into a beat, snapped there and turned back into a
 * time. `off` returns the time unchanged.
 */
export function snapTime(time: number, bpm: number, division: SnapSetting): number {
  if (!isSnapEnabled(division) || !isSnapableBpm(bpm) || !Number.isFinite(time)) {
    return time;
  }
  return beatToTime(snapBeat(timeToBeat(time, bpm), division), bpm);
}

/**
 * Snap a point in time onto the grid, but never onto the grid *before* it.
 *
 * The difference to `snapTime` is the rounding at the lower bound: a time
 * that sits just before a step (for example the end of a hold that was
 * dragged a hair past the start) moves to the next step instead of back to
 * the previous one. The end of a hold therefore never ends before the hold
 * starts.
 */
export function snapTimeExclusive(time: number, bpm: number, division: SnapSetting): number {
  if (!isSnapEnabled(division) || !isSnapableBpm(bpm) || !Number.isFinite(time)) {
    return time;
  }
  return beatToTime(snapBeatExclusive(timeToBeat(time, bpm), division), bpm);
}

/**
 * Lines per step of the grid for a note of the given lines-per-beat.
 *
 * A note stores its position as a line index, the number of `lpb` lines is
 * one beat. A grid of 1/4 with 16 lines per beat is therefore a step of
 * four lines. When the division is finer than the lines of the note can
 * carry (a 1/16 grid on a note with 8 lines per beat), the step is a single
 * line: the note cannot be placed finer than its own lines.
 */
export function snapStepLines(lpb: number, division: SnapDivision): number {
  if (!Number.isFinite(lpb) || lpb <= 0 || division <= 0) {
    return 1;
  }
  if (lpb % division !== 0) {
    return 1;
  }
  return lpb / division;
}

/**
 * Snap a line index onto the grid of the given division.
 *
 * `off` keeps the line where it is (rounded to a whole line), which is what
 * the editor did before the grid was introduced.
 */
export function snapLine(line: number, lpb: number, division: SnapSetting): number {
  if (!isSnapEnabled(division)) {
    return Math.round(line);
  }
  const step = snapStepLines(lpb, division);
  return Math.round(line / step) * step;
}
