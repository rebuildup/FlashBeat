/**
 * Shared model of the per-account song APIs (issue #800).
 *
 * These routes are studio-internal: they are not part of the frozen Unity
 * contract (`contracts/openapi.v1.yaml`) and every handler resolves the
 * caller through the Better Auth session and `workspace_members`
 * (`_authz.ts`) — never through a client-supplied owner id.
 *
 * Ownership rules this module encodes:
 *
 *   - `songs.ownerWorkspaceId IS NULL` means **staff-managed**: the rows
 *     written before ownership existed (issue #799) and every song the
 *     staff token publishes. A user-scoped publish can never claim one —
 *     the id stays reserved for `PUT /api/v1/admin/songs/{songId}`.
 *   - A user-owned song id is global first-publisher-wins: the workspace
 *     that publishes it first owns it, and no other workspace can republish
 *     or overwrite it (the publish route answers 409/404 before writing).
 *   - `song_drafts.songId` is UNIQUE across all workspaces, so two
 *     workspaces cannot even reserve the same public id while drafting.
 *
 * Prefixed with `_` so Void does not turn it into a route.
 */
import { SONG_ID_PATTERN } from "./_read-model";
import {
  validatePublishRequest,
  type PublishDiagnostic,
  type SongPublishRequest,
} from "./_publish-model";
import type { Song, SongDraft } from "@schema";

/** Rights statuses a user publish may declare (stored on `songs`). */
export const RIGHTS_STATUSES = ["original", "licensed", "permission", "other"] as const;
export type RightsStatus = (typeof RIGHTS_STATUSES)[number];

export function isRightsStatus(value: unknown): value is RightsStatus {
  return typeof value === "string" && (RIGHTS_STATUSES as readonly string[]).includes(value);
}

/** YouTube video id of the contract: `^[A-Za-z0-9_-]{11}$`. */
const YOUTUBE_ID_PATTERN = /^[A-Za-z0-9_-]{11}$/;

/** Bounds of the rights fields (mirrored by `songs` column comments). */
export const MATERIAL_SOURCE_MAX = 200;
export const LICENSE_MAX = 500;

/**
 * Fields a draft create/save may carry. Every field is optional — a draft
 * is allowed to be incomplete; `POST …/publish` is where the contract
 * bounds become mandatory.
 */
export interface DraftPatch {
  songId?: string | null;
  title?: string;
  artist?: string;
  youtubeId?: string | null;
  level?: number | null;
  durationSec?: number | null;
  /** Parsed chart object; stored serialized in `chartJson`. */
  chart?: Record<string, unknown>;
  noteCount?: number;
  materialSource?: string | null;
  rightsStatus?: RightsStatus | null;
  license?: string | null;
}

export type DraftValidation =
  | { ok: true; patch: DraftPatch }
  | { ok: false; diagnostics: PublishDiagnostic[] };

function diagnostic(path: string, message: string): PublishDiagnostic {
  return { path, message };
}

/**
 * Validate the writable fields of a draft body. Unknown members are
 * ignored (the member-management routes do the same); a present-but-broken
 * field is a 422 diagnostic, never silently dropped.
 */
export function validateDraftBody(body: unknown): DraftValidation {
  if (typeof body !== "object" || body === null || Array.isArray(body)) {
    return {
      ok: false,
      diagnostics: [diagnostic("(body)", "must be a JSON object")],
    };
  }
  const fields = body as Record<string, unknown>;
  const diagnostics: PublishDiagnostic[] = [];
  const patch: DraftPatch = {};

  if ("songId" in fields) {
    const value = fields.songId;
    if (value === null) {
      patch.songId = null;
    } else if (typeof value === "string" && SONG_ID_PATTERN.test(value)) {
      patch.songId = value;
    } else {
      diagnostics.push(
        diagnostic(
          "songId",
          "must match ^[a-z0-9-]{3,64}$ (lowercase letters, digits and hyphens) or be null",
        ),
      );
    }
  }

  if ("title" in fields) {
    const value = fields.title;
    if (typeof value === "string" && value.length <= 160) {
      patch.title = value;
    } else {
      diagnostics.push(diagnostic("title", "must be a string of at most 160 characters"));
    }
  }

  if ("artist" in fields) {
    const value = fields.artist;
    if (typeof value === "string" && value.length <= 160) {
      patch.artist = value;
    } else {
      diagnostics.push(diagnostic("artist", "must be a string of at most 160 characters"));
    }
  }

  if ("youtubeId" in fields) {
    const value = fields.youtubeId;
    if (value === null || value === "") {
      patch.youtubeId = null;
    } else if (typeof value === "string" && YOUTUBE_ID_PATTERN.test(value)) {
      patch.youtubeId = value;
    } else {
      diagnostics.push(diagnostic("youtubeId", "must be an 11-character YouTube video id or null"));
    }
  }

  if ("level" in fields) {
    const value = fields.level;
    if (value === null) {
      patch.level = null;
    } else if (typeof value === "number" && Number.isInteger(value) && value >= 1 && value <= 99) {
      patch.level = value;
    } else {
      diagnostics.push(diagnostic("level", "must be an integer of 1-99 or null"));
    }
  }

  if ("durationSec" in fields) {
    const value = fields.durationSec;
    if (value === null) {
      patch.durationSec = null;
    } else if (typeof value === "number" && Number.isFinite(value) && value > 0 && value <= 1800) {
      patch.durationSec = value;
    } else {
      diagnostics.push(diagnostic("durationSec", "must be a number of 0-1800 or null"));
    }
  }

  if ("chart" in fields) {
    const value = fields.chart;
    if (typeof value === "object" && value !== null && !Array.isArray(value)) {
      patch.chart = value as Record<string, unknown>;
      const notes = (value as Record<string, unknown>).notes;
      patch.noteCount = Array.isArray(notes) ? notes.length : 0;
    } else {
      diagnostics.push(diagnostic("chart", "must be a JSON object (the legacy chart document)"));
    }
  }

  if ("materialSource" in fields) {
    const value = fields.materialSource;
    if (value === null || value === "") {
      patch.materialSource = null;
    } else if (
      typeof value === "string" &&
      value.trim().length >= 1 &&
      value.length <= MATERIAL_SOURCE_MAX
    ) {
      patch.materialSource = value.trim();
    } else {
      diagnostics.push(
        diagnostic(
          "materialSource",
          `must be a string of 1-${MATERIAL_SOURCE_MAX} characters or null`,
        ),
      );
    }
  }

  if ("rightsStatus" in fields) {
    const value = fields.rightsStatus;
    if (value === null || value === "") {
      patch.rightsStatus = null;
    } else if (isRightsStatus(value)) {
      patch.rightsStatus = value;
    } else {
      diagnostics.push(
        diagnostic(
          "rightsStatus",
          `must be one of ${RIGHTS_STATUSES.map((s) => `"${s}"`).join(", ")} or null`,
        ),
      );
    }
  }

  if ("license" in fields) {
    const value = fields.license;
    if (value === null || value === "") {
      patch.license = null;
    } else if (typeof value === "string" && value.length <= LICENSE_MAX) {
      patch.license = value;
    } else {
      diagnostics.push(
        diagnostic("license", `must be a string of at most ${LICENSE_MAX} characters or null`),
      );
    }
  }

  if (diagnostics.length > 0) {
    return { ok: false, diagnostics };
  }
  return { ok: true, patch };
}

/** The publish request plus the rights attestation (issue #800). */
export interface UserPublishRequest {
  request: SongPublishRequest;
  materialSource: string;
  rightsStatus: RightsStatus;
  license: string | null;
}

export type UserPublishValidation =
  | { ok: true; publish: UserPublishRequest }
  | { ok: false; diagnostics: PublishDiagnostic[] };

/**
 * Validate the body of `POST /api/v1/me/drafts/{draftId}/publish`.
 *
 * The song fields go through the exact same contract validation as the
 * staff publish route (`validatePublishRequest` + `semanticDiagnostics`),
 * so a user publish can never store a chart the staff flow would reject.
 * On top of that the rights attestation is mandatory: `rightsConfirmed`
 * must be literally `true` and the three rights fields must be present —
 * publishing without them is a 422, not a warning.
 */
export function validateUserPublishBody(body: unknown): UserPublishValidation {
  if (typeof body !== "object" || body === null || Array.isArray(body)) {
    return {
      ok: false,
      diagnostics: [diagnostic("(body)", "must be a JSON object")],
    };
  }
  const fields = body as Record<string, unknown>;

  // Reuse the frozen contract shape: pick exactly the members
  // `SongPublishRequest` declares so extra keys cannot smuggle fields in.
  const candidate = {
    title: fields.title,
    artist: fields.artist,
    youtubeId: fields.youtubeId,
    level: fields.level,
    durationSec: fields.durationSec,
    chart: fields.chart,
  };
  const structural = validatePublishRequestShallow(candidate);
  if (!structural.ok) {
    return structural;
  }

  const diagnostics: PublishDiagnostic[] = [];

  if (fields.rightsConfirmed !== true) {
    diagnostics.push(
      diagnostic(
        "rightsConfirmed",
        "must be true — publishing requires an explicit rights attestation",
      ),
    );
  }

  const materialSource = fields.materialSource;
  if (
    typeof materialSource !== "string" ||
    materialSource.trim().length < 1 ||
    materialSource.length > MATERIAL_SOURCE_MAX
  ) {
    diagnostics.push(
      diagnostic(
        "materialSource",
        `must be a string of 1-${MATERIAL_SOURCE_MAX} characters naming the material source`,
      ),
    );
  }

  const rightsStatus = fields.rightsStatus;
  if (!isRightsStatus(rightsStatus)) {
    diagnostics.push(
      diagnostic(
        "rightsStatus",
        `must be one of ${RIGHTS_STATUSES.map((s) => `"${s}"`).join(", ")}`,
      ),
    );
  }

  const license = fields.license;
  if (
    license !== undefined &&
    license !== null &&
    (typeof license !== "string" || license.length > LICENSE_MAX)
  ) {
    diagnostics.push(
      diagnostic("license", `must be a string of at most ${LICENSE_MAX} characters`),
    );
  }

  if (diagnostics.length > 0) {
    return { ok: false, diagnostics };
  }

  return {
    ok: true,
    publish: {
      request: structural.request,
      materialSource: (materialSource as string).trim(),
      rightsStatus: rightsStatus as RightsStatus,
      license: typeof license === "string" && license.length > 0 ? license : null,
    },
  };
}

function validatePublishRequestShallow(
  candidate: unknown,
): { ok: true; request: SongPublishRequest } | { ok: false; diagnostics: PublishDiagnostic[] } {
  return validatePublishRequest(candidate);
}

/* ------------------------------------------------------------------ */
/* Response shapes                                                     */
/* ------------------------------------------------------------------ */

/** One entry of `GET /api/v1/me/songs` (`songs` half). */
export interface MeSong {
  id: string;
  title: string;
  artist: string;
  youtubeId: string;
  level: number;
  durationSec: number;
  bpm: number;
  /** `chart_versions.version` the catalog currently serves. */
  chartVersion: number;
  /** `published` = live in the catalog; `unpublished` = archived. */
  state: "published" | "unpublished";
  archived: boolean;
  archivedAt: string | null;
  ownerWorkspaceId: string;
  ownerWorkspaceName: string;
  /** The caller's role inside the owning workspace. */
  role: string;
  canEdit: boolean;
  rightsStatus: string | null;
  materialSource: string | null;
  license: string | null;
}

/** One entry of `GET /api/v1/me/songs` (`drafts` half). */
export interface MeDraft {
  id: string;
  /** Intended public song id (`null` until the draft names one). */
  songId: string | null;
  title: string;
  artist: string;
  youtubeId: string | null;
  level: number | null;
  durationSec: number | null;
  noteCount: number;
  hasChart: boolean;
  /** `chart_versions.version` of the last publish; `null` = never. */
  publishedVersion: number | null;
  ownerWorkspaceId: string;
  ownerWorkspaceName: string;
  role: string;
  canEdit: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface MeSongListResponse {
  songs: MeSong[];
  drafts: MeDraft[];
}

export function toMeSong(
  row: Song,
  ownerWorkspaceName: string,
  role: string,
  canEdit: boolean,
): MeSong {
  return {
    id: row.id,
    title: row.title,
    artist: row.artist,
    youtubeId: row.youtubeId,
    level: row.level,
    durationSec: row.durationSec,
    bpm: row.bpm,
    chartVersion: row.currentVersion,
    state: row.archived === 1 ? "unpublished" : "published",
    archived: row.archived === 1,
    archivedAt: row.archivedAt,
    ownerWorkspaceId: row.ownerWorkspaceId ?? "",
    ownerWorkspaceName,
    role,
    canEdit,
    rightsStatus: row.rightsStatus,
    materialSource: row.materialSource,
    license: row.license,
  };
}

export function toMeDraft(
  row: SongDraft,
  ownerWorkspaceName: string,
  role: string,
  canEdit: boolean,
): MeDraft {
  return {
    id: row.id,
    songId: row.songId,
    title: row.title,
    artist: row.artist,
    youtubeId: row.youtubeId,
    level: row.level,
    durationSec: row.durationSec,
    noteCount: row.noteCount,
    hasChart: row.chartJson !== null,
    publishedVersion: row.publishedVersion,
    ownerWorkspaceId: row.ownerWorkspaceId,
    ownerWorkspaceName,
    role,
    canEdit,
    createdAt: row.createdAt,
    updatedAt: row.updatedAt,
  };
}
