import { snapLine, type SnapSetting } from "./snap";
import { noteBeat } from "./timing";
import { NOTE_TYPE_HOLD, NOTE_TYPE_TAP, type ChartNote } from "./types";

/**
 * Long note (HOLD) logic.
 *
 * A hold is a note that is pressed down at its start and released at its end.
 * The data model is the legacy one: a hold is an ordinary note object whose
 * `type` is `NOTE_TYPE_HOLD` and whose `endNum` holds the line index of the
 * end. Nothing else about the chart format changes, so the tap charts of the
 * Unity project keep their exact bytes.
 *
 * The timing uses the very formula the Unity player uses
 * (`Assets/Game/Scripts/Gameplay/NotesManager.cs`):
 *
 *     seconds = 60 / BPM * line / LPB
 *
 * so the Web editor and the game agree on where a hold starts and ends.
 */

/**
 * Shortest hold in lines.
 *
 * A hold must cover at least one line, otherwise the player would have to
 * press and release a key inside the same frame. One line is the smallest
 * unit the grid can show, and it is the unit the resize handle snaps to.
 */
export const MIN_HOLD_LINES = 1;

/** True when the note is a hold. */
export function isHold(note: ChartNote): boolean {
  return note.type === NOTE_TYPE_HOLD;
}

/** True when the note is a tap. */
export function isTap(note: ChartNote): boolean {
  return note.type === NOTE_TYPE_TAP;
}

/**
 * Line index where a hold ends.
 *
 * A hold without `endNum` (a chart that was hand edited) is treated as a
 * hold of the minimum length, so the editor never crashes on it and the
 * validator reports the missing field.
 */
export function holdEndLine(note: ChartNote): number {
  return note.endNum ?? note.num + MIN_HOLD_LINES;
}

/**
 * Length of a hold in lines.
 *
 * Never negative: a hold that ends before it starts is broken, and the
 * validator reports it. The editor still needs a sane number to draw, so it
 * gets the minimum length.
 */
export function holdLengthLines(note: ChartNote): number {
  return Math.max(MIN_HOLD_LINES, holdEndLine(note) - note.num);
}

/** Length of a hold in beats. */
export function holdLengthBeats(note: ChartNote): number {
  return holdLengthLines(note) / note.LPB;
}

/** Beat position where the hold starts. */
export function holdStartBeat(note: ChartNote): number {
  return noteBeat(note);
}

/** Beat position where the hold ends. */
export function holdEndBeat(note: ChartNote): number {
  return holdEndLine(note) / note.LPB;
}

/** Time the hold starts, in seconds from the start of the song. */
export function holdStartSec(note: ChartNote, bpm: number): number {
  return (60 / bpm) * holdStartBeat(note);
}

/** Time the hold ends, in seconds from the start of the song. */
export function holdEndSec(note: ChartNote, bpm: number): number {
  return (60 / bpm) * holdEndBeat(note);
}

/** Length of the hold in seconds. */
export function holdLengthSec(note: ChartNote, bpm: number): number {
  return holdEndSec(note, bpm) - holdStartSec(note, bpm);
}

/**
 * Build a hold that covers the given beats.
 *
 * `endBeat` is the beat where the hold ends; it is snapped onto the grid of
 * the resolution and clamped so that the hold is at least `MIN_HOLD_LINES`
 * long and never starts before the beginning of the chart.
 */
export function createHold(
  block: number,
  startBeat: number,
  endBeat: number,
  resolution: number,
  division: SnapSetting = "off",
): ChartNote {
  const startLine = Math.max(0, snapLine(startBeat * resolution, resolution, division));
  const endLine = Math.max(
    startLine + MIN_HOLD_LINES,
    snapLine(endBeat * resolution, resolution, division),
  );

  return {
    type: NOTE_TYPE_HOLD,
    num: startLine,
    block,
    LPB: resolution,
    endNum: endLine,
  };
}

/**
 * Move the end of a hold to another beat.
 *
 * The start stays where it is and the end is snapped onto the grid, so a
 * hold is never shorter than one line and never ends before it starts.
 */
export function setHoldEndBeat(
  note: ChartNote,
  endBeat: number,
  division: SnapSetting = "off",
): ChartNote {
  const endLine = Math.max(
    note.num + MIN_HOLD_LINES,
    snapLine(endBeat * note.LPB, note.LPB, division),
  );
  return { ...note, endNum: endLine };
}

/**
 * Length of a hold in lines, snapped so that a hold always covers whole
 * lines of its own resolution.
 */
export function snapHoldEndLine(
  note: ChartNote,
  endBeat: number,
  division: SnapSetting = "off",
): number {
  return Math.max(
    note.num + MIN_HOLD_LINES,
    snapLine(endBeat * note.LPB, note.LPB, division),
  );
}

/**
 * True when two holds of the same lane overlap.
 *
 * The lanes are played independently, so a hold of lane 3 never conflicts
 * with a hold of lane 4. Two holds of the same lane may touch (the second
 * one starts where the first one ends) but may not overlap.
 */
export function holdsOverlap(first: ChartNote, second: ChartNote): boolean {
  if (first.block !== second.block) {
    return false;
  }

  const firstStart = first.num;
  const firstEnd = holdEndLine(first);
  const secondStart = second.num;
  const secondEnd = holdEndLine(second);

  return firstStart < secondEnd && secondStart < firstEnd;
}

/**
 * Holds of the chart that overlap another hold of the same lane.
 *
 * The result is one entry per offending hold, so the validator can name the
 * exact note the operator has to fix.
 */
export function findOverlappingHolds(notes: ChartNote[]): ChartNote[] {
  const holds = notes.filter(isHold);
  const overlapping = new Set<ChartNote>();

  for (const [index, hold] of holds.entries()) {
    for (const [otherIndex, other] of holds.entries()) {
      if (index === otherIndex) {
        continue;
      }
      if (holdsOverlap(hold, other)) {
        overlapping.add(hold);
        overlapping.add(other);
      }
    }
  }

  return [...overlapping];
}
