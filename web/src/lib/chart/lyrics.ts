import type { Chart, ChartLyrics } from "./types";

/**
 * Lyrics editing model.
 *
 * The Unity charts store lyrics as five parallel arrays (`startTime`,
 * `endTime`, `furigana`, `mondai`, `romaji`) inside the chart document. The
 * arrays are not guaranteed to have the same length and some legacy charts
 * (`Who`) even store `endTime < startTime` for every line. The runtime
 * (`Assets/Game/Scripts/UI/LyricsTimeline.cs`) copes with both: it pairs the
 * arrays up to the shorter one and derives the end of a broken line from the
 * next line's start (or four seconds when there is no next line).
 *
 * This module mirrors those rules so the web editor shows the same timeline
 * the game will play, while keeping the raw arrays lossless:
 *
 * - Reading never rewrites a chart. `buildLyricsArrays` returns the original
 *   `lyrics` object untouched as long as nothing was edited.
 * - A legacy interval (`end <= start`) is preserved verbatim when the
 *   operator only edits the text of that line. Unity already renders it.
 * - A line whose times were edited is normalized on save: an invalid end is
 *   replaced with the next line's start or `start + 4s`.
 * - Arrays are normalized to one length on save, because the contract and the
 *   Unity importer expect five parallel arrays of the same entry count.
 */

/** Seconds a line stays on screen when it has no usable end time. */
export const DEFAULT_LINE_DURATION = 4;

/** Intervals shorter than this are treated as zero length. */
const TIME_EPSILON = 0.0005;

/** Keys of the lyrics block owned by this editor. */
const KNOWN_KEYS = ["startTime", "endTime", "furigana", "mondai", "romaji"] as const;

/** One lyric line, independent of the parallel array layout. */
export interface LyricRow {
  /** Stable identity for React keys; unique inside one document. */
  id: number;
  /** Raw start time in seconds, `null` when the source array has no entry. */
  startTime: number | null;
  /** Raw end time in seconds, `null` when the source array has no entry. */
  endTime: number | null;
  /** Text shown in the game (empty lines hide the lyric panel). */
  mondai: string;
  /** Reading shown above the text. */
  furigana: string;
  /** Romaji shown by the typing scene. */
  romaji: string;
  /**
   * True once the operator edited the times of this line. Only these lines
   * are repaired on save, so legacy charts survive a text-only edit.
   */
  timesDirty: boolean;
}

/** Length of each source array, kept for the "arrays are ragged" warning. */
export interface LyricArrayLengths {
  startTime: number;
  endTime: number;
  furigana: number;
  mondai: number;
  romaji: number;
}

/** The editable lyrics document. */
export interface LyricsDocument {
  rows: LyricRow[];
  /** Original lyrics block; returned verbatim while nothing is dirty. */
  source?: ChartLyrics;
  lengths: LyricArrayLengths;
  /** True when any field was edited. */
  dirty: boolean;
  /** Next row id to hand out. */
  nextId: number;
}

/** Effective interval of a row, after the Unity repair rules. */
export interface LyricInterval {
  /** `null` when the line has no start time. */
  start: number | null;
  /** `null` when the line has no start time. */
  end: number | null;
  /** True when `endTime` was unusable and the end was derived. */
  derived: boolean;
}

/** Inline problems of one row, already worded for the UI. */
export interface LyricRowIssues {
  startTime?: string;
  endTime?: string;
}

/** Machine-readable checks for the panel header. */
export interface LyricsSummary {
  lines: number;
  /** Rows whose source `endTime` is not after `startTime`. */
  legacyIntervals: number;
  /** Edited rows whose end will be derived on save. */
  repairedIntervals: number;
  /** Rows without a start time (they cannot be displayed). */
  missingStart: number;
  /** Rows that start before the previous line ends. */
  overlaps: number;
  /** True when the five source arrays have different lengths. */
  arrayLengthMismatch: boolean;
  lengths: LyricArrayLengths;
}

/** A document level problem shown above the line list. */
export interface LyricsIssue {
  severity: "error" | "warning";
  message: string;
}

const EMPTY_LENGTHS: LyricArrayLengths = {
  startTime: 0,
  endTime: 0,
  furigana: 0,
  mondai: 0,
  romaji: 0,
};

function readNumberArray(value: unknown): unknown[] {
  // Positions are kept even for a malformed entry so the parallel arrays stay
  // aligned; `toTime` turns the entry into `null`.
  return Array.isArray(value) ? value : [];
}

function readStringArray(value: unknown): string[] {
  return Array.isArray(value) ? value.map((entry) => (typeof entry === "string" ? entry : "")) : [];
}

/** Finite number or `null`; the source arrays may carry `null`/strings. */
function toTime(value: unknown): number | null {
  return typeof value === "number" && Number.isFinite(value) ? value : null;
}

/** Round to milliseconds so repeated edits do not drift. */
export function roundTime(seconds: number): number {
  return Math.round(seconds * 1000) / 1000;
}

/**
 * Read a chart's lyrics into rows.
 *
 * The row count is the longest of the five arrays, so text that exists without
 * a time is still visible to the operator instead of being silently dropped.
 */
export function readLyrics(source?: ChartLyrics): LyricsDocument {
  if (!source) {
    return {
      rows: [],
      source: undefined,
      lengths: { ...EMPTY_LENGTHS },
      dirty: false,
      nextId: 0,
    };
  }

  const startTimes = readNumberArray(source.startTime);
  const endTimes = readNumberArray(source.endTime);
  const furigana = readStringArray(source.furigana);
  const mondai = readStringArray(source.mondai);
  const romaji = readStringArray(source.romaji);

  const lengths: LyricArrayLengths = {
    startTime: startTimes.length,
    endTime: endTimes.length,
    furigana: furigana.length,
    mondai: mondai.length,
    romaji: romaji.length,
  };

  const count = Math.max(
    lengths.startTime,
    lengths.endTime,
    lengths.furigana,
    lengths.mondai,
    lengths.romaji,
  );

  const rows: LyricRow[] = [];
  for (let index = 0; index < count; index += 1) {
    rows.push({
      id: index,
      startTime: toTime(startTimes[index]),
      endTime: toTime(endTimes[index]),
      mondai: mondai[index] ?? "",
      furigana: furigana[index] ?? "",
      romaji: romaji[index] ?? "",
      timesDirty: false,
    });
  }

  return {
    rows,
    source,
    lengths,
    dirty: false,
    nextId: count,
  };
}

/** End used by the runtime when `endTime` is missing or not after the start. */
function deriveEnd(start: number, nextStart: number | null): number {
  let end = start + DEFAULT_LINE_DURATION;
  if (nextStart !== null && nextStart > start && nextStart < end) {
    end = nextStart;
  }
  return roundTime(end);
}

/** Effective display interval of a row, matching `LyricsTimeline.Build`. */
export function rowInterval(document: LyricsDocument, index: number): LyricInterval {
  const row = document.rows[index];
  if (!row || row.startTime === null) {
    return { start: null, end: null, derived: false };
  }

  const nextStart = document.rows[index + 1]?.startTime ?? null;
  if (row.endTime !== null && row.endTime > row.startTime + TIME_EPSILON) {
    return { start: row.startTime, end: row.endTime, derived: false };
  }

  return { start: row.startTime, end: deriveEnd(row.startTime, nextStart), derived: true };
}

/** Row shown at `mediaTime`, mirroring `LyricsTimeline.IndexAt`. */
export function activeLyricIndex(document: LyricsDocument, mediaTime: number): number | null {
  let found: number | null = null;
  let latestStart = Number.NEGATIVE_INFINITY;

  for (let index = 0; index < document.rows.length; index += 1) {
    const interval = rowInterval(document, index);
    if (interval.start === null || interval.end === null) {
      continue;
    }
    if (mediaTime < interval.start || mediaTime >= interval.end) {
      continue;
    }
    if (interval.start >= latestStart) {
      latestStart = interval.start;
      found = index;
    }
  }

  return found;
}

/** Source key order is kept so an export stays close to the original file. */
function orderedLyrics(
  source: ChartLyrics | undefined,
  arrays: Record<(typeof KNOWN_KEYS)[number], unknown>,
): ChartLyrics {
  if (!source) {
    return arrays as unknown as ChartLyrics;
  }

  const ordered: Record<string, unknown> = {};
  for (const key of Object.keys(source)) {
    ordered[key] = Object.prototype.hasOwnProperty.call(arrays, key) ? arrays[key as keyof typeof arrays] : source[key];
  }
  for (const key of KNOWN_KEYS) {
    if (!Object.prototype.hasOwnProperty.call(ordered, key)) {
      ordered[key] = arrays[key];
    }
  }
  return ordered as ChartLyrics;
}

/**
 * Serialize the document back to the legacy parallel arrays.
 *
 * Returns the source object verbatim while the document is clean. Once it is
 * dirty every array is written with one entry per row: a missing number gets
 * the previous line's end (or zero) for the start and the same derived end the
 * runtime would use.
 */
export function buildLyricsArrays(document: LyricsDocument): ChartLyrics {
  if (!document.dirty && document.source) {
    return document.source;
  }

  const startTimes: number[] = [];
  const endTimes: number[] = [];
  const furigana: string[] = [];
  const mondai: string[] = [];
  const romaji: string[] = [];

  let previousEnd: number | null = null;

  for (let index = 0; index < document.rows.length; index += 1) {
    const row = document.rows[index];
    const nextStart = document.rows[index + 1]?.startTime ?? null;

    const start = row.startTime ?? previousEnd ?? 0;
    let end: number;

    if (row.endTime !== null && row.endTime > start + TIME_EPSILON) {
      end = row.endTime;
    } else if (row.timesDirty || row.endTime === null) {
      // Edited interval (or a missing entry) is repaired exactly like the
      // runtime would repair it.
      end = deriveEnd(start, nextStart);
    } else {
      // Legacy interval: keep the raw value so `Who` stays byte identical
      // when only its text is edited.
      end = row.endTime;
    }

    // Raw source numbers are written back untouched; only derived values
    // (a missing entry, an edited interval) are rounded to milliseconds.
    startTimes.push(row.startTime !== null ? row.startTime : roundTime(start));
    endTimes.push(end);
    furigana.push(row.furigana);
    mondai.push(row.mondai);
    romaji.push(row.romaji);
    previousEnd = end;
  }

  return orderedLyrics(document.source, {
    startTime: startTimes,
    endTime: endTimes,
    furigana,
    mondai,
    romaji,
  });
}

/** Apply the lyrics document to a chart, keeping every other field. */
export function chartWithLyrics(chart: Chart, document: LyricsDocument): Chart {
  return { ...chart, lyrics: buildLyricsArrays(document) };
}

function hasOwn<T extends object>(value: T, key: PropertyKey): boolean {
  return Object.prototype.hasOwnProperty.call(value, key);
}

/** Fields the operator may change on a row. */
export type LyricRowPatch = Partial<
  Pick<LyricRow, "startTime" | "endTime" | "mondai" | "furigana" | "romaji">
>;

/** Replace fields of one row. Time edits mark the row for normalization. */
export function updateLyricRow(
  document: LyricsDocument,
  id: number,
  patch: LyricRowPatch,
): LyricsDocument {
  const timesEdited = hasOwn(patch, "startTime") || hasOwn(patch, "endTime");
  const normalized: LyricRowPatch = { ...patch };
  if (hasOwn(patch, "startTime") && typeof patch.startTime === "number") {
    normalized.startTime = roundTime(patch.startTime);
  }
  if (hasOwn(patch, "endTime") && typeof patch.endTime === "number") {
    normalized.endTime = roundTime(patch.endTime);
  }

  let changed = false;

  const rows = document.rows.map((row) => {
    if (row.id !== id) {
      return row;
    }
    changed = true;
    return {
      ...row,
      ...normalized,
      timesDirty: row.timesDirty || timesEdited,
    };
  });

  if (!changed) {
    return document;
  }

  return { ...document, rows, dirty: true };
}

/**
 * Set the start of a row (for example "use the playhead").
 *
 * The end is kept when it is still after the new start; otherwise it is
 * derived, so the shortcut never produces an interval the game cannot show.
 */
export function setLyricStart(
  document: LyricsDocument,
  id: number,
  startTime: number,
): LyricsDocument {
  const index = document.rows.findIndex((row) => row.id === id);
  if (index < 0) {
    return document;
  }

  const start = roundTime(Math.max(0, startTime));
  const row = document.rows[index];
  const nextStart = document.rows[index + 1]?.startTime ?? null;
  const end = row.endTime !== null && row.endTime > start + TIME_EPSILON
    ? row.endTime
    : deriveEnd(start, nextStart);

  return updateLyricRow(document, id, { startTime: start, endTime: end });
}

/** Set the end of a row; the value is validated by `rowIssues`. */
export function setLyricEnd(document: LyricsDocument, id: number, endTime: number): LyricsDocument {
  return updateLyricRow(document, id, { endTime: roundTime(Math.max(0, endTime)) });
}

/** Insert an empty line after `afterId` (or at the end). */
export function addLyricRow(document: LyricsDocument, afterId?: number): LyricsDocument {
  const afterIndex = afterId === undefined
    ? document.rows.length - 1
    : document.rows.findIndex((row) => row.id === afterId);

  const insertAt = afterIndex < 0 ? document.rows.length : afterIndex + 1;
  const previous = afterIndex >= 0 ? rowInterval(document, afterIndex) : null;
  const start = previous?.end ?? 0;
  const nextStart = document.rows[insertAt]?.startTime ?? null;

  const row: LyricRow = {
    id: document.nextId,
    startTime: roundTime(start),
    endTime: deriveEnd(start, nextStart),
    mondai: "",
    furigana: "",
    romaji: "",
    timesDirty: true,
  };

  const rows = [...document.rows.slice(0, insertAt), row, ...document.rows.slice(insertAt)];

  return {
    ...document,
    rows,
    dirty: true,
    nextId: document.nextId + 1,
  };
}

/** Remove one row. */
export function removeLyricRow(document: LyricsDocument, id: number): LyricsDocument {
  const rows = document.rows.filter((row) => row.id !== id);
  if (rows.length === document.rows.length) {
    return document;
  }
  return { ...document, rows, dirty: true };
}

/**
 * Swap the text of a row with its neighbour.
 *
 * The time slots do not move: the list stays chronological, which is what the
 * runtime assumes. This is "move this line's content earlier/later", not a
 * free reorder of the timeline.
 */
export function moveLyricRow(
  document: LyricsDocument,
  id: number,
  direction: -1 | 1,
): LyricsDocument {
  const index = document.rows.findIndex((row) => row.id === id);
  const target = index + direction;
  if (index < 0 || target < 0 || target >= document.rows.length) {
    return document;
  }

  const rows = [...document.rows];
  const current = rows[index];
  const neighbour = rows[target];

  rows[index] = {
    ...current,
    mondai: neighbour.mondai,
    furigana: neighbour.furigana,
    romaji: neighbour.romaji,
  };
  rows[target] = {
    ...neighbour,
    mondai: current.mondai,
    furigana: current.furigana,
    romaji: current.romaji,
  };

  return { ...document, rows, dirty: true };
}

/** Sort rows by start time (stable for equal times, missing starts last). */
export function sortLyricRows(document: LyricsDocument): LyricsDocument {
  const rows = document.rows
    .map((row, index) => ({ row, index }))
    .sort((a, b) => {
      const startA = a.row.startTime ?? Number.POSITIVE_INFINITY;
      const startB = b.row.startTime ?? Number.POSITIVE_INFINITY;
      if (startA !== startB) {
        return startA - startB;
      }
      return a.index - b.index;
    })
    .map((entry) => entry.row);

  return { ...document, rows, dirty: true };
}

/** Inline problems of one row. */
export function rowIssues(document: LyricsDocument, index: number): LyricRowIssues {
  const row = document.rows[index];
  if (!row) {
    return {};
  }

  const issues: LyricRowIssues = {};

  if (row.startTime === null) {
    issues.startTime = "Start time is required";
  } else if (row.startTime < 0) {
    issues.startTime = "Start must be 0 or more";
  }

  if (row.endTime !== null && row.endTime < 0) {
    issues.endTime = "End must be 0 or more";
  } else if (
    row.timesDirty &&
    row.endTime !== null &&
    row.startTime !== null &&
    row.endTime <= row.startTime + TIME_EPSILON
  ) {
    issues.endTime = "End must be after start";
  }

  return issues;
}

/** Counts used by the panel header and the document level issues. */
export function summarizeLyrics(document: LyricsDocument): LyricsSummary {
  let legacyIntervals = 0;
  let repairedIntervals = 0;
  let missingStart = 0;
  let overlaps = 0;

  for (let index = 0; index < document.rows.length; index += 1) {
    const row = document.rows[index];

    if (row.startTime === null) {
      missingStart += 1;
    }

    if (row.endTime !== null && row.startTime !== null) {
      const invalid = row.endTime <= row.startTime + TIME_EPSILON;
      if (invalid) {
        if (row.timesDirty) {
          repairedIntervals += 1;
        } else {
          legacyIntervals += 1;
        }
      }
    }

    if (row.timesDirty && row.endTime === null) {
      repairedIntervals += 1;
    }

    if (index > 0 && row.startTime !== null) {
      const previous = rowInterval(document, index - 1);
      if (previous.end !== null && row.startTime < previous.end - TIME_EPSILON) {
        overlaps += 1;
      }
    }
  }

  const distinctLengths = new Set(Object.values(document.lengths));
  const arrayLengthMismatch = distinctLengths.size > 1;

  return {
    lines: document.rows.length,
    legacyIntervals,
    repairedIntervals,
    missingStart,
    overlaps,
    arrayLengthMismatch,
    lengths: document.lengths,
  };
}

/** Document level issues, most important first. */
export function lyricsIssues(document: LyricsDocument): LyricsIssue[] {
  const summary = summarizeLyrics(document);
  const issues: LyricsIssue[] = [];

  if (summary.missingStart > 0) {
    issues.push({
      severity: "error",
      message:
        `${summary.missingStart} line(s) have no start time. ` +
        "Set a start before exporting the chart.",
    });
  }

  if (summary.repairedIntervals > 0) {
    issues.push({
      severity: "warning",
      message:
        `${summary.repairedIntervals} edited line(s) have end <= start and are repaired on save. ` +
        "Saving writes the next line's start (or start + 4s) as the end.",
    });
  }

  if (summary.legacyIntervals > 0) {
    issues.push({
      severity: "warning",
      message:
        `${summary.legacyIntervals} legacy line(s) end at or before they start. ` +
        "Unity derives their end from the next line; the raw values are kept unless you edit the times.",
    });
  }

  if (summary.arrayLengthMismatch) {
    const lengths = summary.lengths;
    const detail =
      `startTime ${lengths.startTime} / endTime ${lengths.endTime} / ` +
      `furigana ${lengths.furigana} / mondai ${lengths.mondai} / romaji ${lengths.romaji}`;
    issues.push({
      severity: "warning",
      message:
        `The lyrics arrays have different lengths (${detail}). ` +
        `Editing the lyrics writes ${summary.lines} entries into every array.`,
    });
  }

  if (summary.overlaps > 0) {
    issues.push({
      severity: "warning",
      message:
        `${summary.overlaps} line(s) overlap the previous line. ` +
        "Unity shows the line that starts last.",
    });
  }

  return issues;
}

/** True when the document can be exported without losing a line. */
export function canSaveLyrics(document: LyricsDocument): boolean {
  return summarizeLyrics(document).missingStart === 0;
}

/** Seconds formatted as `m:ss.SS` for readouts. */
export function formatTimecode(seconds: number): string {
  const rounded = Math.max(0, roundTime(seconds));
  const minutes = Math.floor(rounded / 60);
  const rest = rounded - minutes * 60;
  return `${minutes}:${rest.toFixed(2).padStart(5, "0")}`;
}

/** Full-width digits and punctuation produced by a Japanese IME. */
function normalizeTimeInput(text: string): string {
  return text
    .replace(/[０-９]/g, (char) => String.fromCharCode(char.charCodeAt(0) - 0xfee0))
    .replace(/[：]/g, ":")
    .replace(/[．。]/g, ".")
    .replace(/[−–—]/g, "-")
    .replace(/\s+/g, "");
}

/**
 * Parse a time field.
 *
 * `null` means "empty" (the operator cleared the field), `undefined` means
 * "not a time". Accepted forms: `12`, `12.5`, `0:12.5`, `1:02:03.250`, with
 * full-width digits and colons from a Japanese IME.
 */
export function parseTimeInput(text: string): number | null | undefined {
  const normalized = normalizeTimeInput(text);
  if (normalized === "") {
    return null;
  }
  if (normalized.startsWith("-") || normalized.includes("e") || normalized.includes("E")) {
    return undefined;
  }

  const parts = normalized.split(":");
  if (parts.length === 0 || parts.length > 3) {
    return undefined;
  }

  const last = parts[parts.length - 1];
  if (!/^\d+(?:\.\d+)?$/.test(last)) {
    return undefined;
  }

  let total = 0;
  for (let index = 0; index < parts.length - 1; index += 1) {
    const part = parts[index];
    if (!/^\d+$/.test(part)) {
      return undefined;
    }
    const value = Number.parseInt(part, 10);
    if (parts.length === 3 && value >= 60) {
      // `h:mm:ss`: minutes must look like minutes.
      return undefined;
    }
    total = total * 60 + value;
  }

  const seconds = Number.parseFloat(last);
  if (!Number.isFinite(seconds)) {
    return undefined;
  }
  if (parts.length > 1 && seconds >= 60) {
    return undefined;
  }
  total = total * 60 + seconds;

  if (!Number.isFinite(total) || total < 0) {
    return undefined;
  }
  return roundTime(total);
}

/** Field value for a time; empty string when the time is not set. */
export function formatTimeInput(value: number | null): string {
  if (value === null || !Number.isFinite(value)) {
    return "";
  }
  return String(roundTime(value));
}
