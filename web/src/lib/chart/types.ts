/**
 * Legacy FlashBeat chart model.
 *
 * The shape is the one Unity reads from `Assets/Game/Resources/<song>.json` and
 * the one `contracts/schemas/chart.v1.schema.json` freezes. Nothing in the
 * editor may reshape it: fields are read and written verbatim, unknown
 * top-level fields are carried through untouched.
 */

/** Number of lanes of the FlashBeat keyboard. */
export const LANE_COUNT = 8;

/**
 * Note kind of a `ChartNote`.
 *
 * `TAP` is the only kind the charts shipped before issue #164 carry.
 * `HOLD` is the long note of issue #164: the key is pressed at the start of
 * the note and released at its end.
 */
export const NOTE_TYPE_TAP = 1;
export const NOTE_TYPE_HOLD = 2;

/**
 * A single note.
 *
 * `num` is a line index inside the note's own resolution and `LPB` is the
 * number of lines per beat, so the beat position of a note is `num / LPB`.
 * `Assets/Game/Scripts/Gameplay/NotesManager.cs` converts that to seconds with
 * `60 / BPM * num / LPB`.
 *
 * A hold (`type` = `NOTE_TYPE_HOLD`) carries `endNum`: the line index where
 * the hold ends. The hold covers the lines `(num, endNum]` at the resolution
 * of `LPB`, so it is `endNum - num` lines long. A tap ignores `endNum`.
 */
export interface ChartNote {
  /** Note kind: `NOTE_TYPE_TAP` (1) or `NOTE_TYPE_HOLD` (2). */
  type: number;
  /** Line index inside the note's own resolution. */
  num: number;
  /** Lane, `0` to `7`. */
  block: number;
  /** Lines per beat used by this note (`4` = 16ths, `8` = 32nds). */
  LPB: number;
  /** Line index where a hold ends. Only read for `NOTE_TYPE_HOLD`. */
  endNum?: number;
}

/** Lyrics block of a chart. Legacy `*_text.json` data was merged into it. */
export interface ChartLyrics {
  startTime?: number[];
  endTime?: number[];
  furigana?: string[];
  mondai?: string[];
  romaji?: string[];
  [key: string]: unknown;
}

/**
 * A whole chart.
 *
 * The index signature keeps unknown top-level fields (`videoId` in
 * `ニセモノ.json` for example) so import/export stays lossless.
 */
export interface Chart {
  name: string;
  maxBlock: number;
  BPM: number;
  offset: number;
  notes: ChartNote[];
  lyrics?: ChartLyrics;
  [key: string]: unknown;
}

/** A validation problem, already flattened for display in the editor UI. */
export interface ValidationIssue {
  /** JSON pointer of the offending value, empty for document level errors. */
  path: string;
  /** Human readable message. */
  message: string;
  /** Schema keyword that failed (`required`, `const`, `type`, ...). */
  keyword: string;
}
