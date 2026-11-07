import { noteBeat } from "./timing";
import { NOTE_TYPE_HOLD, NOTE_TYPE_TAP, type Chart, type ChartNote } from "./types";

/**
 * Editing operations on a chart.
 *
 * Every operation returns a new chart object. The spread keeps the key order
 * of the original document, so exporting an edited chart still produces the
 * legacy layout Unity expects.
 */

/** A note that does not exist yet. */
export function createNote(block: number, line: number, resolution: number): ChartNote {
  return { type: NOTE_TYPE_TAP, num: line, block, LPB: resolution };
}

/** Append a note. */
export function addNote(chart: Chart, note: ChartNote): Chart {
  return { ...chart, notes: [...chart.notes, note] };
}

/** Remove the note at the given index. */
export function removeNote(chart: Chart, index: number): Chart {
  return { ...chart, notes: chart.notes.filter((_, noteIndex) => noteIndex !== index) };
}

/** Replace the note at the given index. */
export function updateNote(chart: Chart, index: number, note: ChartNote): Chart {
  return {
    ...chart,
    notes: chart.notes.map((existing, noteIndex) => (noteIndex === index ? note : existing)),
  };
}

/**
 * Move a note to another lane and beat.
 *
 * The note keeps its own resolution (`LPB`), the line index is recomputed in
 * that resolution so the note stays on the grid it was written for.
 */
export function moveNote(chart: Chart, index: number, block: number, beat: number): Chart {
  const note = chart.notes[index];
  if (!note) {
    return chart;
  }

  const line = Math.round(beat * note.LPB);
  return updateNote(chart, index, { ...note, block, num: line });
}

/** Replace the chart metadata. `offset` is a legacy field and stays untouched. */
export function updateChartMeta(
  chart: Chart,
  meta: { name?: string; BPM?: number; offset?: number },
): Chart {
  return { ...chart, ...meta };
}

/** Number of notes in the chart. */
export function noteCount(chart: Chart): number {
  return chart.notes.length;
}

/**
 * Number of holds in the chart.
 *
 * A hold counts as one note for the score (`maxScore = noteCount * 5`), so
 * the editor shows how many of them there are next to the note count.
 */
export function holdCount(chart: Chart): number {
  return chart.notes.filter((note) => note.type === NOTE_TYPE_HOLD).length;
}

/**
 * Move the end of a hold to another beat.
 *
 * The start of the hold stays where it is and the end is snapped onto the
 * grid of the note's own resolution, so a hold is never shorter than one
 * line and never ends before it starts.
 */
export function setHoldEnd(chart: Chart, index: number, endBeat: number): Chart {
  const note = chart.notes[index];
  if (!note || note.type !== NOTE_TYPE_HOLD) {
    return chart;
  }

  const endLine = Math.max(note.num + 1, Math.round(endBeat * note.LPB));
  return updateNote(chart, index, { ...note, endNum: endLine });
}

/**
 * Change the kind of a note.
 *
 * Turning a tap into a hold gives it the minimum length, so the operator can
 * drag the end of it right away. Turning a hold back into a tap drops the
 * end: a tap has no end.
 */
export function setNoteKind(chart: Chart, index: number, kind: number): Chart {
  const note = chart.notes[index];
  if (!note) {
    return chart;
  }

  if (kind === NOTE_TYPE_HOLD) {
    return updateNote(chart, index, {
      ...note,
      type: NOTE_TYPE_HOLD,
      endNum: note.num + 1,
    });
  }

  const { endNum: _endNum, ...withoutEnd } = note;
  return updateNote(chart, index, { ...withoutEnd, type: kind });
}

/** First and last beat covered by the notes. */
export function chartBeatRange(chart: Chart): { first: number; last: number } {
  if (chart.notes.length === 0) {
    return { first: 0, last: 0 };
  }

  let first = Number.POSITIVE_INFINITY;
  let last = Number.NEGATIVE_INFINITY;

  for (const note of chart.notes) {
    const beat = noteBeat(note);
    first = Math.min(first, beat);
    last = Math.max(last, beat);
  }

  return { first, last };
}

/**
 * Resolution used for new notes.
 *
 * The repository charts are written with 4 or 8 lines per beat; the most
 * common one is the natural default for the editor.
 */
export function dominantResolution(chart: Chart): number {
  const counter = new Map<number, number>();

  for (const note of chart.notes) {
    counter.set(note.LPB, (counter.get(note.LPB) ?? 0) + 1);
  }

  let best = 4;
  let bestCount = 0;

  for (const [resolution, count] of counter) {
    if (count > bestCount) {
      best = resolution;
      bestCount = count;
    }
  }

  return best;
}
