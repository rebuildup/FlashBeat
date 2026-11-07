import { holdEndLine, isHold } from "./long-note.ts";
import type { Chart, ChartNote, ValidationIssue } from "./types.ts";

/**
 * Semantic validation of a chart — **the single source of truth** (issue #191).
 *
 * The contract schema (`contracts/schemas/chart.v1.schema.json`) can express
 * the shape of a note, but not the relation between its fields: a hold that
 * ends before it starts, two holds that fight over the same lane and a hold
 * that reaches past the end of the song are all well formed JSON and still
 * unplayable.
 *
 * This module holds every rule that the JSON schema cannot express, and it is
 * used by **both** consumers:
 *
 *   - the Web studio (editor + import): `schema.ts` → `validateChart`
 *   - the publish API (Worker): `routes/api/v1/_publish-model.ts` →
 *     `semanticDiagnostics`
 *
 * Before issue #191 the two sides had their own rules, so a chart the API
 * accepted could be rejected by the editor ("Published chart failed
 * validation"), and the editor's overlap check compared **raw line numbers**
 * instead of beats, which missed overlapping holds whose `LPB` differs.
 *
 * The module is deliberately free of any runtime dependency (no DOM, no
 * filesystem, no network), so the same function runs in the browser, in the
 * Worker and in Node.
 *
 * All positions are normalized to **beats** (`num / LPB`) before they are
 * compared, because the actual playing time of a note is
 * `60 / BPM * num / LPB` (`Assets/Game/Scripts/Gameplay/NotesManager.cs`).
 * Two holds of the same lane that occupy the same beats overlap no matter
 * which resolution their line numbers were written in.
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

/**
 * Smallest gap between two holds of the same lane, in beats.
 *
 * Two holds may touch (the second one starts exactly where the first one
 * ends): the player releases the key and presses it again in the same
 * moment, which is playable. Anything that overlaps is refused.
 */
export const HOLD_TOUCH_EPSILON_BEATS = 1e-9;

/** Options of the semantic validation. */
export interface SemanticValidationOptions {
  /**
   * Length of the song in seconds. When it is given, a hold that ends after
   * it is refused. The publish API knows the duration, the editor does not.
   */
  durationSec?: number;
}

/**
 * How strict the semantic validation is.
 *
 * - `strict` (the default) refuses every semantic violation. The editor and
 *   the publish API use it.
 * - `lenient` reports the semantic violations as **warnings** instead of
 *   errors, so a chart that was published before the rules were unified can
 *   still be read back and repaired (issue #191, requirement R7). The chart
 *   is never silently dropped or rewritten.
 */
export type SemanticStrictness = "strict" | "lenient";

/** JSON pointer of a note inside the chart. */
function notePath(index: number): string {
  return `/notes/${index}`;
}

/**
 * Beat position of a line index at the resolution of a note.
 *
 * `LPB` is validated to be a positive number so that a broken note can never
 * produce `Infinity` or `NaN` (which would silently pass every comparison).
 */
function lineToBeats(line: number, lpb: number): number {
  if (!Number.isFinite(line) || !Number.isFinite(lpb) || lpb <= 0) {
    return Number.NaN;
  }
  return line / lpb;
}

/** Beat position where a hold starts. */
function holdStartBeats(note: ChartNote): number {
  return lineToBeats(note.num, note.LPB);
}

/** Beat position where a hold ends. */
function holdEndBeats(note: ChartNote): number {
  return lineToBeats(holdEndLine(note), note.LPB);
}

/** Length of a hold in beats. */
function holdBeats(note: ChartNote): number {
  return holdEndBeats(note) - holdStartBeats(note);
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

  const beats = holdBeats(note);

  // A broken `LPB` cannot be normalized to beats, so the length rules do not
  // apply. The JSON schema already rejects `LPB < 1`.
  if (!Number.isFinite(beats)) {
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
  if (durationSec !== undefined && durationSec > 0 && Number.isFinite(chartBpm) && chartBpm > 0) {
    const endSec = (60 / chartBpm) * holdEndBeats(note);
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
  //
  // The comparison is done in **beats**, not in raw line numbers: the notes
  // of a chart may use different resolutions (`LPB`), and two holds that
  // occupy the same beats overlap even when their line numbers differ.
  // Issue #191 fixed exactly this: the raw comparison missed holds that
  // overlapped across resolutions.
  const holds = notes.map((note, index) => ({ note, index })).filter((entry) => isHold(entry.note));
  for (const [position, hold] of holds.entries()) {
    const holdStart = holdStartBeats(hold.note);
    const holdEnd = holdEndBeats(hold.note);
    if (!Number.isFinite(holdStart) || !Number.isFinite(holdEnd)) {
      continue;
    }
    for (const other of holds.slice(position + 1)) {
      if (hold.note.block !== other.note.block) {
        continue;
      }
      const otherStart = holdStartBeats(other.note);
      const otherEnd = holdEndBeats(other.note);
      if (!Number.isFinite(otherStart) || !Number.isFinite(otherEnd)) {
        continue;
      }
      const overlaps =
        holdStart < otherEnd - HOLD_TOUCH_EPSILON_BEATS &&
        otherStart < holdEnd - HOLD_TOUCH_EPSILON_BEATS;
      if (!overlaps) {
        continue;
      }
      issues.push({
        path: `${notePath(other.index)}/num`,
        message: `overlaps another hold on lane ${other.note.block} (beats ${holdStart}-${holdEnd} and ${otherStart}-${otherEnd})`,
        keyword: "overlap",
      });
    }
  }

  return issues;
}

/**
 * Semantic validation of a chart, with the strictness of the caller.
 *
 * `lenient` keeps every issue but marks it as a warning (`keyword` gets the
 * `warning.` prefix), so a chart that was published before the rules were
 * unified can still be opened and repaired instead of being dropped
 * (issue #191, requirement R7).
 */
export function validateChartSemanticsWithStrictness(
  chart: Chart,
  strictness: SemanticStrictness = "strict",
  options: SemanticValidationOptions = {},
): ValidationIssue[] {
  const issues = validateChartSemantics(chart, options);
  if (strictness === "strict") {
    return issues;
  }
  return issues.map((issue) => ({
    ...issue,
    keyword: `warning.${issue.keyword}`,
  }));
}
