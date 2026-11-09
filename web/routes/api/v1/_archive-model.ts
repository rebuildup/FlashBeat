/**
 * Shared model of the song archive (issue #151).
 *
 * The festival never deletes a song. "Removing" a song from the catalog means
 * archiving it: the row, its chart snapshots and its scores stay in the
 * database, the song only leaves the public catalog, and unarchiving brings it
 * back with its history intact. There is no `DELETE` route for a song, and
 * the model has no "delete" vocabulary at all.
 *
 * The helpers here are pure so the vitest suite can pin the request and
 * response shapes without a database.
 */

/** Body of `POST /api/v1/admin/songs/{songId}` (the archive route). */
export interface ArchiveRequest {
  /** `true` archives the song, `false` brings an archived song back. */
  archived: boolean;
}

/** One actionable problem of a rejected archive request. */
export interface ArchiveDiagnostic {
  /** JSON path of the offending member, e.g. `/archived`. */
  path: string;
  /** What is wrong and what a valid value looks like. */
  message: string;
}

/** Result of validating an archive request body. */
export type ArchiveValidation =
  | { ok: true; request: ArchiveRequest }
  | { ok: false; diagnostics: ArchiveDiagnostic[] };

/**
 * Validate the body of the archive route.
 *
 * The body must be an object with exactly one member, `archived`, and it must
 * be a boolean. A string `"true"` is rejected on purpose: the flag decides
 * whether a song disappears from the catalog, so an ambiguous value is an
 * error and not a guess.
 */
export function validateArchiveRequest(body: unknown): ArchiveValidation {
  if (typeof body !== "object" || body === null || Array.isArray(body)) {
    return {
      ok: false,
      diagnostics: [
        { path: "(body)", message: "must be a single JSON object" } satisfies ArchiveDiagnostic,
      ],
    };
  }

  const members = Object.entries(body as Record<string, unknown>);
  const diagnostics: ArchiveDiagnostic[] = [];

  for (const [key] of members) {
    if (key !== "archived") {
      diagnostics.push({
        path: `/${key}`,
        message: 'is not a member of the archive request (only "archived" is allowed)',
      });
    }
  }

  const archived = (body as Record<string, unknown>).archived;
  if (archived === undefined) {
    diagnostics.push({
      path: "/archived",
      message: "is required: true archives the song, false brings it back",
    });
  } else if (typeof archived !== "boolean") {
    diagnostics.push({
      path: "/archived",
      message: `must be a boolean, got ${typeof archived}`,
    });
  }

  if (diagnostics.length > 0) {
    return { ok: false, diagnostics };
  }
  return { ok: true, request: { archived: archived as boolean } };
}

/** One entry of the staff song list (`GET /api/v1/admin/songs`). */
export interface AdminSong {
  id: string;
  title: string;
  artist: string;
  youtubeId: string;
  level: number;
  bpm: number;
  durationSec: number;
  chartVersion: number;
  /** `true` when the song is archived and therefore out of the public catalog. */
  archived: boolean;
  /** ISO timestamp of the archive moment, `null` for a live song. */
  archivedAt: string | null;
  /** Number of score submissions of this song. */
  plays: number;
  /** Best score of this song, `null` when nobody played it. */
  bestScore: number | null;
}

/** Body of `GET /api/v1/admin/songs`. */
export interface AdminSongsResponse {
  /** Number of published chart snapshots (the same counter as the catalog). */
  catalogVersion: number;
  songs: AdminSong[];
}

/** Body of `POST /api/v1/admin/songs/{songId}` (the archive route). */
export interface ArchiveResponse {
  songId: string;
  archived: boolean;
  archivedAt: string | null;
  catalogVersion: number;
}

/**
 * Build the archive answer. `catalogVersion` is the same counter the public
 * catalog serves, so a client can tell whether the published charts changed.
 */
export function buildArchiveResponse(
  songId: string,
  archived: boolean,
  archivedAt: string | null,
  catalogVersion: number,
): ArchiveResponse {
  return { songId, archived, archivedAt, catalogVersion };
}
