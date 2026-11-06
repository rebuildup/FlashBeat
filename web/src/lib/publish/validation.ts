import { parseYouTubeId } from "@/lib/chart/youtube";
import type { SongPublishRequest } from "./types";

/**
 * Metadata rules of the publish endpoint.
 *
 * Every rule here is a copy of a constraint in `contracts/openapi.v1.yaml`.
 * The operator sees the problems before the request leaves the browser, so a
 * rejected publish never costs a round trip.
 */

/** `songId` pattern of the contract. */
export const SONG_ID_PATTERN = /^[a-z0-9-]{3,64}$/;

/** `youtubeId` pattern of the contract. */
export const YOUTUBE_ID_PATTERN = /^[A-Za-z0-9_-]{11}$/;

/** `title` length limit of the contract. */
export const MAX_TITLE_LENGTH = 160;

/** `artist` length limit of the contract. */
export const MAX_ARTIST_LENGTH = 160;

/** `durationSec` upper bound of the contract. */
export const MAX_DURATION_SEC = 1800;

/** `level` bounds of the contract. */
export const MIN_LEVEL = 1;
export const MAX_LEVEL = 99;

/**
 * The metadata the operator edits.
 *
 * `durationSec` is `NaN` while the field is empty or holds something that is
 * not a number, so the validation can tell "not filled in" from "out of range".
 */
export interface SongMetadataDraft {
  songId: string;
  title: string;
  artist: string;
  /** A YouTube URL or a bare video id. It is normalized when the request is built. */
  youtube: string;
  level: number;
  durationSec: number;
}

export type SongMetadataField = keyof SongMetadataDraft;

export interface SongMetadataIssue {
  field: SongMetadataField;
  message: string;
}

export function validateSongId(value: string): string | undefined {
  const trimmed = value.trim();
  if (trimmed.length === 0) {
    return "Enter the song id.";
  }
  if (!SONG_ID_PATTERN.test(trimmed)) {
    return "Use 3 to 64 lowercase letters, digits or dashes.";
  }
  return undefined;
}

export function validateTitle(value: string): string | undefined {
  const trimmed = value.trim();
  if (trimmed.length === 0) {
    return "Enter the title.";
  }
  if (trimmed.length > MAX_TITLE_LENGTH) {
    return `The title must be ${MAX_TITLE_LENGTH} characters or fewer.`;
  }
  return undefined;
}

export function validateArtist(value: string): string | undefined {
  if (value.trim().length > MAX_ARTIST_LENGTH) {
    return `The artist must be ${MAX_ARTIST_LENGTH} characters or fewer.`;
  }
  return undefined;
}

export function validateYoutube(value: string): string | undefined {
  if (value.trim().length === 0) {
    return "Enter the YouTube video.";
  }
  if (parseYouTubeId(value) === null) {
    return "Enter a YouTube URL or an 11 character video id.";
  }
  return undefined;
}

export function validateLevel(value: number): string | undefined {
  if (!Number.isInteger(value)) {
    return "The level must be a whole number.";
  }
  if (value < MIN_LEVEL || value > MAX_LEVEL) {
    return `The level must be between ${MIN_LEVEL} and ${MAX_LEVEL}.`;
  }
  return undefined;
}

export function validateDuration(value: number): string | undefined {
  if (!Number.isFinite(value)) {
    return "Enter the duration in seconds.";
  }
  if (value <= 0) {
    return "The duration must be longer than zero.";
  }
  if (value > MAX_DURATION_SEC) {
    return `The duration must be ${MAX_DURATION_SEC} seconds or fewer.`;
  }
  return undefined;
}

/**
 * Every metadata problem of a draft.
 *
 * The list is complete instead of first failure only, so the form can mark
 * all broken fields at once.
 */
export function validateSongMetadata(draft: SongMetadataDraft): SongMetadataIssue[] {
  const issues: SongMetadataIssue[] = [];

  const songId = validateSongId(draft.songId);
  if (songId) {
    issues.push({ field: "songId", message: songId });
  }

  const title = validateTitle(draft.title);
  if (title) {
    issues.push({ field: "title", message: title });
  }

  const artist = validateArtist(draft.artist);
  if (artist) {
    issues.push({ field: "artist", message: artist });
  }

  const youtube = validateYoutube(draft.youtube);
  if (youtube) {
    issues.push({ field: "youtube", message: youtube });
  }

  const level = validateLevel(draft.level);
  if (level) {
    issues.push({ field: "level", message: level });
  }

  const duration = validateDuration(draft.durationSec);
  if (duration) {
    issues.push({ field: "durationSec", message: duration });
  }

  return issues;
}

/**
 * Build the request body of the publish route.
 *
 * The video field accepts a URL, so the id is extracted here. The draft is
 * validated first: a request with a broken field never leaves the browser.
 */
export function buildPublishRequest(
  draft: SongMetadataDraft,
  chart: SongPublishRequest["chart"],
): { ok: true; request: SongPublishRequest } | { ok: false; issues: SongMetadataIssue[] } {
  const issues = validateSongMetadata(draft);
  if (issues.length > 0) {
    return { ok: false, issues };
  }

  const youtubeId = parseYouTubeId(draft.youtube);
  if (youtubeId === null) {
    return {
      ok: false,
      issues: [{ field: "youtube", message: "Enter a YouTube URL or an 11 character video id." }],
    };
  }

  return {
    ok: true,
    request: {
      title: draft.title.trim(),
      artist: draft.artist.trim(),
      youtubeId,
      level: draft.level,
      durationSec: draft.durationSec,
      chart,
    },
  };
}
