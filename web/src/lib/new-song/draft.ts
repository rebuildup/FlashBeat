import type { Chart } from "../chart/types";

/**
 * Local draft of the new-song flow.
 *
 * The whole flow (song info + chart + step position) lives in
 * `sessionStorage`, so a reload in the middle of the exhibition booth work
 * loses nothing. Like the publish draft it never leaves the browser tab and
 * holds no credentials.
 */

export const NEW_SONG_DRAFT_KEY = "flashbeat-new-song-draft";

export const NEW_SONG_STEPS = [1, 2, 3] as const;
export type NewSongStep = (typeof NEW_SONG_STEPS)[number];

/** The metadata the operator edits in step 1. Shape of `SongMetadataDraft`. */
export interface NewSongInfo {
  /** A YouTube URL or a bare video id, as pasted. */
  youtube: string;
  title: string;
  artist: string;
  songId: string;
  /** `NaN` while the field is empty or not a number. */
  level: number;
  /** `NaN` while the field is empty or not a number. */
  durationSec: number;
}

export interface NewSongDraft {
  step: NewSongStep;
  /** Highest step reached, so the stepper can navigate back and forward. */
  maxStep: NewSongStep;
  info: NewSongInfo;
  chart: Chart | null;
  savedAt: string;
}

export const EMPTY_NEW_SONG_INFO: NewSongInfo = {
  youtube: "",
  title: "",
  artist: "",
  songId: "",
  level: Number.NaN,
  durationSec: Number.NaN,
};

export function emptyNewSongDraft(): NewSongDraft {
  return { step: 1, maxStep: 1, info: { ...EMPTY_NEW_SONG_INFO }, chart: null, savedAt: "" };
}

/** Store the draft. Called after every change of the flow. */
export function saveNewSongDraft(draft: NewSongDraft): void {
  const recordable: NewSongDraft = { ...draft, savedAt: new Date().toISOString() };
  sessionStorage.setItem(NEW_SONG_DRAFT_KEY, JSON.stringify(recordable));
}

/** Read the draft, or `null` when there is none or it is unusable. */
export function loadNewSongDraft(): NewSongDraft | null {
  const raw = sessionStorage.getItem(NEW_SONG_DRAFT_KEY);
  if (raw === null) {
    return null;
  }

  let value: unknown;
  try {
    value = JSON.parse(raw);
  } catch {
    return null;
  }
  return sanitizeDraft(value);
}

/** Drop the draft. Called after a successful publish. */
export function clearNewSongDraft(): void {
  sessionStorage.removeItem(NEW_SONG_DRAFT_KEY);
}

function asRecord(value: unknown): Record<string, unknown> | null {
  return value !== null && typeof value === "object" && !Array.isArray(value)
    ? (value as Record<string, unknown>)
    : null;
}

function asText(value: unknown): string {
  return typeof value === "string" ? value : "";
}

/**
 * `NaN` is serialized as `null` by `JSON.stringify`, so a stored number may
 * come back as `null`; both mean "the field is empty".
 */
function asNumber(value: unknown): number {
  return typeof value === "number" && Number.isFinite(value) ? value : Number.NaN;
}

function sanitizeChart(value: unknown): Chart | null {
  const chart = asRecord(value);
  if (chart === null || !Array.isArray(chart["notes"])) {
    return null;
  }
  return value as Chart;
}

function sanitizeDraft(value: unknown): NewSongDraft | null {
  const record = asRecord(value);
  if (record === null) {
    return null;
  }

  const info = asRecord(record["info"]);
  if (info === null) {
    return null;
  }

  const step = record["step"];
  const maxStep = record["maxStep"];
  const isStep = (candidate: unknown): candidate is NewSongStep =>
    typeof candidate === "number" && (NEW_SONG_STEPS as readonly number[]).includes(candidate);

  return {
    step: isStep(step) ? step : 1,
    maxStep: isStep(maxStep) ? maxStep : isStep(step) ? step : 1,
    info: {
      youtube: asText(info["youtube"]),
      title: asText(info["title"]),
      artist: asText(info["artist"]),
      songId: asText(info["songId"]),
      level: asNumber(info["level"]),
      durationSec: asNumber(info["durationSec"]),
    },
    chart: sanitizeChart(record["chart"]),
    savedAt: asText(record["savedAt"]),
  };
}
