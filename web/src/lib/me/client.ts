/**
 * Typed client of the per-account song APIs (issue #800).
 *
 * Every route lives under `/api/v1/me/` and authenticates with the Better
 * Auth session cookie — same origin, no token in headers or the bundle.
 * A failure is a value, not an exception: `MeApiError` carries the HTTP
 * status and the server's diagnostics so the screen can tell "not
 * signed in" (401) apart from "not yours" (404) and "invalid" (422).
 */

export class MeApiError extends Error {
  readonly status: number;
  readonly diagnostics: { path: string; message: string }[];

  constructor(
    status: number,
    message: string,
    diagnostics: { path: string; message: string }[] = [],
  ) {
    super(message);
    this.name = "MeApiError";
    this.status = status;
    this.diagnostics = diagnostics;
  }
}

export interface MeUser {
  id: string;
  email: string;
  name: string;
  emailVerified: boolean;
}

export interface MeIdentity {
  user: MeUser;
  workspace: { id: string; role: string };
}

export interface MeSong {
  id: string;
  title: string;
  artist: string;
  youtubeId: string;
  level: number;
  durationSec: number;
  bpm: number;
  chartVersion: number;
  state: "published" | "unpublished";
  archived: boolean;
  archivedAt: string | null;
  ownerWorkspaceId: string;
  ownerWorkspaceName: string;
  role: string;
  canEdit: boolean;
  rightsStatus: string | null;
  materialSource: string | null;
  license: string | null;
}

export interface MeDraft {
  id: string;
  songId: string | null;
  title: string;
  artist: string;
  youtubeId: string | null;
  level: number | null;
  durationSec: number | null;
  noteCount: number;
  hasChart: boolean;
  publishedVersion: number | null;
  ownerWorkspaceId: string;
  ownerWorkspaceName: string;
  role: string;
  canEdit: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface MeDraftDetail extends MeDraft {
  createdByUserId: string;
  materialSource: string | null;
  rightsStatus: string | null;
  license: string | null;
  chart: unknown;
}

export interface MeSongList {
  songs: MeSong[];
  drafts: MeDraft[];
}

/** Body accepted by `POST /api/v1/me/drafts`. */
export interface CreateDraftBody {
  workspaceId?: string;
  songId?: string;
  title?: string;
  artist?: string;
  youtubeId?: string | null;
  level?: number | null;
  durationSec?: number | null;
  chart?: unknown;
  materialSource?: string | null;
  rightsStatus?: string | null;
  license?: string | null;
}

/** Partial update accepted by `PUT /api/v1/me/drafts/{id}`. */
export type SaveDraftBody = Partial<CreateDraftBody>;

/** Body of `POST /api/v1/me/drafts/{id}/publish`. */
export interface PublishDraftBody {
  title: string;
  artist: string;
  youtubeId: string;
  level: number;
  durationSec: number;
  chart: unknown;
  rightsConfirmed: true;
  materialSource: string;
  rightsStatus: string;
  license?: string | null;
}

export interface PublishDraftResponse {
  songId: string;
  chartVersion: number;
  catalogVersion: number;
  archived: boolean;
  archivedAt: string | null;
  draft: { id: string; publishedVersion: number };
}

export interface UnpublishResponse {
  songId: string;
  archived: boolean;
  archivedAt: string | null;
  catalogVersion: number;
}

type FetchLike = typeof fetch;

async function request<T>(
  path: string,
  init: { method?: string; body?: unknown; signal?: AbortSignal; fetchImpl?: FetchLike } = {},
): Promise<T> {
  const doFetch = init.fetchImpl ?? fetch;
  const headers: Record<string, string> = { Accept: "application/json" };
  let body: string | undefined;
  if (init.body !== undefined) {
    headers["Content-Type"] = "application/json";
    body = JSON.stringify(init.body);
  }
  let response: Response;
  try {
    response = await doFetch(path, {
      method: init.method ?? "GET",
      headers,
      body,
      signal: init.signal,
    });
  } catch (error) {
    throw new MeApiError(
      0,
      `APIに接続できませんでした: ${error instanceof Error ? error.message : String(error)}`,
    );
  }
  const text = await response.text();
  let parsed: unknown = null;
  if (text.length > 0) {
    try {
      parsed = JSON.parse(text);
    } catch {
      throw new MeApiError(response.status, "APIの応答を読み取れませんでした。");
    }
  }
  if (!response.ok) {
    const record = (parsed ?? {}) as Record<string, unknown>;
    const diagnostics = Array.isArray(record.diagnostics)
      ? (record.diagnostics as { path: string; message: string }[])
      : [];
    const message =
      typeof record.error === "string" && record.error.length > 0
        ? record.error
        : `APIが${response.status}を返しました。`;
    throw new MeApiError(response.status, message, diagnostics);
  }
  return parsed as T;
}

export function fetchMe(fetchImpl?: FetchLike): Promise<MeIdentity> {
  return request<MeIdentity>("/api/v1/me", { fetchImpl });
}

export function fetchMySongs(fetchImpl?: FetchLike): Promise<MeSongList> {
  return request<MeSongList>("/api/v1/me/songs", { fetchImpl });
}

export function fetchDraft(
  draftId: string,
  fetchImpl?: FetchLike,
): Promise<{ draft: MeDraftDetail }> {
  return request<{ draft: MeDraftDetail }>(`/api/v1/me/drafts/${encodeURIComponent(draftId)}`, {
    fetchImpl,
  });
}

export function createDraft(
  body: CreateDraftBody,
  fetchImpl?: FetchLike,
): Promise<{ draft: MeDraft }> {
  return request<{ draft: MeDraft }>("/api/v1/me/drafts", {
    method: "POST",
    body,
    fetchImpl,
  });
}

export function saveDraft(
  draftId: string,
  body: SaveDraftBody,
  fetchImpl?: FetchLike,
): Promise<{ draft: MeDraftDetail }> {
  return request<{ draft: MeDraftDetail }>(`/api/v1/me/drafts/${encodeURIComponent(draftId)}`, {
    method: "PUT",
    body,
    fetchImpl,
  });
}

export function publishDraft(
  draftId: string,
  body: PublishDraftBody,
  fetchImpl?: FetchLike,
): Promise<PublishDraftResponse> {
  return request<PublishDraftResponse>(`/api/v1/me/drafts/${encodeURIComponent(draftId)}/publish`, {
    method: "POST",
    body,
    fetchImpl,
  });
}

export function unpublishSong(songId: string, fetchImpl?: FetchLike): Promise<UnpublishResponse> {
  return request<UnpublishResponse>(`/api/v1/me/songs/${encodeURIComponent(songId)}/unpublish`, {
    method: "POST",
    fetchImpl,
  });
}
