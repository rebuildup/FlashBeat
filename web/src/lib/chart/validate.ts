import { holdEndLine, isHold } from "./long-note";
import type { Chart, ChartNote, ValidationIssue } from "./types";

/**
 * Semantic validation of a chart.
 *
 * The contract schema (`contracts/schemas/chart.v1.schema.json`) can express
 * the shape of a note, but not the relation between its fields: a hold that
 * ends before it starts, two holds that fight over the same lane and a hold
 * that reaches past the end of the song are all well formed JSON and still
 * unplayable.
 *
 * These checks run before a chart is published, so a broken hold never
 * reaches the players. Every issue carries the JSON pointer of the offending
 * note, exactly like the schema issues do.
 */

/** A hold must be at least this long, in beats. */
export const MIN_HOLD_BEATS = 0.25;

/**
 * Longest hold in beats.
 *
 * A hold longer than this cannot be held by a human and would block the lane
 * for the whole song, so it is refused before the publication.
 */
export const MAX_HOLD_BEATS = 64;

/** Options of the semantic validation. */
export interface SemanticValidationOptions {
  /**
   * Length of the song in seconds. When it is given, a hold that ends after
   * it is refused. The publish API knows the duration, the editor does not.
   */
  durationSec?: number;
}

/** JSON pointer of a note inside the chart. */
function notePath(index: number): string {
  return `/notes/${index}`;
}

/** Check one note and collect the problems it has. */
function validateNote(
  note: ChartNote,
  index: number,
  chartBpm: number,
  options: SemanticValidationOptions,
): ValidationIssue[] {
  const issues: ValidationIssue[] = [];

  if (!isHold(note)) {
    return issues;
  }

  const endLine = holdEndLine(note);

  if (note.endNum === undefined) {
    issues.push({
      path: `${notePath(index)}/endNum`,
      message: "must have required property 'endNum'",
      keyword: "required",
    });
    return issues;
  }

  if (endLine <= note.num) {
    issues.push({
      path: `${notePath(index)}/endNum`,
      message: `must be after the start of the hold (num ${note.num}), got ${endLine}`,
      keyword: "minimum",
    });
    return issues;
  }

  const beats = (endLine - note.num) / note.LPB;

  if (beats < MIN_HOLD_BEATS) {
    issues.push({
      path: `${notePath(index)}/endNum`,
      message: `a hold must last at least ${MIN_HOLD_BEATS} beats, got ${beats}`,
      keyword: "minimum",
    });
  }

  // The length of the song is checked before the generic maximum, because it
  // is the reason the hold is too long. The operator shortens the hold or
  // lengthens the song - not the other way around.
  const durationSec = options.durationSec;
  if (durationSec !== undefined && durationSec > 0) {
    const endSec = (60 / chartBpm) * (endLine / note.LPB);
    if (endSec > durationSec) {
      issues.push({
        path: `${notePath(index)}/endNum`,
        message: `the hold ends at ${endSec.toFixed(3)}s, beyond the ${durationSec}s chart length`,
        keyword: "maximum",
      });
      return issues;
    }
  }

  if (beats > MAX_HOLD_BEATS) {
    issues.push({
      path: `${notePath(index)}/endNum`,
      message: `a hold must not last longer than ${MAX_HOLD_BEATS} beats, got ${beats}`,
      keyword: "maximum",
    });
  }

  return issues;
}

/**
 * Validate the relations between the notes of a chart.
 *
 * Returns every problem instead of the first one, so the operator sees the
 * complete list at once.
 */
export function validateChartSemantics(
  chart: Chart,
  options: SemanticValidationOptions = {},
): ValidationIssue[] {
  const issues: ValidationIssue[] = [];
  const notes = Array.isArray(chart.notes) ? chart.notes : [];

  notes.forEach((note, index) => {
    issues.push(...validateNote(note, index, chart.BPM, options));
  });

  // Two holds of the same lane may not fight over the key.
  const holds = notes.filter(isHold);
  for (const [index, hold] of holds.entries()) {
    for (const other of holds.slice(index + 1)) {
      if (hold.block !== other.block) {
        continue;
      }
      const holdEnd = holdEndLine(hold);
      const otherEnd = holdEndLine(other);
      const overlaps = hold.num < otherEnd && other.num < holdEnd;
      if (!overlaps) {
        continue;
      }
      issues.push({
        path: `${notePath(notes.indexOf(other))}/num`,
        message: `overlaps another hold on lane ${other.block} (lines ${hold.num}-${holdEnd} and ${other.num}-${otherEnd})`,
        keyword: "overlap",
      });
    }
  }

  return issues;
}
