import type { ChartNote } from "./types";

/**
 * Timing math of the legacy chart format.
 *
 * `Assets/Game/Scripts/Gameplay/NotesManager.cs` computes the hit time of a
 * note as `60 / BPM * num / LPB`, so a note sits at `num / LPB` beats. The
 * editor uses the same formula to place notes on the grid and to line the
 * playhead up with the YouTube preview.
 */

/** Beat position of a note. */
export function noteBeat(note: ChartNote): number {
  return note.num / note.LPB;
}

/** Hit time of a note in seconds, measured from the start of the song. */
export function noteTimeSec(note: ChartNote, bpm: number): number {
  return beatToSeconds(noteBeat(note), bpm);
}

/** Seconds of one beat at the given BPM. */
export function beatToSeconds(beat: number, bpm: number): number {
  return (60 / bpm) * beat;
}

/** Beat position of a point in time. */
export function secondsToBeat(seconds: number, bpm: number): number {
  return (seconds * bpm) / 60;
}

/** Snap a beat position onto the grid of the given resolution. */
export function snapBeat(beat: number, resolution: number): number {
  return Math.round(beat * resolution) / resolution;
}

/** Line index of a beat position in a grid of the given resolution. */
export function beatToLine(beat: number, resolution: number): number {
  return Math.round(beat * resolution);
}

/** Beat position of a line index in a grid of the given resolution. */
export function lineToBeat(line: number, resolution: number): number {
  return line / resolution;
}
