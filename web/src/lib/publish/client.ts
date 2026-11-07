import type { PublishFetch, PublishResult, SongPublishRequest, SongPublishResponse } from "./types";

/**
 * Typed client of the publish route.
 *
 * The endpoint is `PUT /api/v1/admin/songs/{songId}` of
 * `contracts/openapi.v1.yaml`. The staff token travels as a bearer token in
 * the request header and is never part of the body, the URL or the bundle.
 */

/** Build the URL of the publish route. */
export function publishUrl(baseUrl: string, songId: string): string {
  const base = baseUrl.replace(/\/+$/, "");
  return `${base}/api/v1/admin/songs/${encodeURIComponent(songId)}`;
}

export interface PublishSongOptions {
  /** Origin of the API. Empty means the same origin. */
  baseUrl: string;
  /** Song id of the path parameter. */
  songId: string;
  /** Staff token, entered by the operator at runtime. */
  token: string;
  /** Body of the request. */
  request: SongPublishRequest;
  /** Transport. Defaults to the global `fetch`. */
  fetchImpl?: PublishFetch;
  signal?: AbortSignal;
}

/**
 * Publish a song.
 *
 * The result is a value, never an exception: the screen has to show the
 * difference between a rejected token, an invalid song and a missing
 * endpoint, and every one of those is an ordinary outcome.
 */
export async function publishSong(options: PublishSongOptions): Promise<PublishResult> {
  const { baseUrl, songId, token, request, signal } = options;

  if (token.trim().length === 0) {
    return {
      ok: false,
      failure: {
        code: "unauthorized",
        message: "Enter the staff token before publishing.",
      },
    };
  }

  const doFetch: PublishFetch = options.fetchImpl ?? ((input, init) => fetch(input, init));

  let response: Response;
  try {
    response = await doFetch(publishUrl(baseUrl, songId), {
      method: "PUT",
      headers: {
        Accept: "application/json",
        Authorization: `Bearer ${token}`,
        "Content-Type": "application/json",
      },
      body: JSON.stringify(request),
      signal,
    });
  } catch (error) {
    return {
      ok: false,
      failure: {
        code: "network",
        message: `The publish request did not reach the API: ${errorMessage(error)}`,
      },
    };
  }

  if (response.status === 200 || response.status === 201) {
    return readSuccess(response);
  }

  const details = await readFailureDetails(response);

  if (response.status === 401 || response.status === 403) {
    return {
      ok: false,
      failure: {
        code: "unauthorized",
        status: response.status,
        message: "The staff token was rejected. Check the token of the operator.",
        details,
      },
    };
  }

  if (response.status === 404) {
    return {
      ok: false,
      failure: {
        code: "not-found",
        status: response.status,
        message: "The publish endpoint answered 404. Check the API origin of the studio.",
        details,
      },
    };
  }

  if (response.status === 422) {
    return {
      ok: false,
      failure: {
        code: "invalid",
        status: response.status,
        message: "The API rejected the song. Fix the listed problems and publish again.",
        details,
      },
    };
  }

  return {
    ok: false,
    failure: {
      code: "server",
      status: response.status,
      message: `The publish endpoint answered ${response.status}.`,
      details,
    },
  };
}

async function readSuccess(response: Response): Promise<PublishResult> {
  let body: unknown;
  try {
    body = await response.json();
  } catch {
    return {
      ok: false,
      failure: {
        code: "malformed",
        status: response.status,
        message: "The publish endpoint answered without a readable body.",
      },
    };
  }

  const parsed = parsePublishResponse(body);
  if (parsed === null) {
    return {
      ok: false,
      failure: {
        code: "malformed",
        status: response.status,
        message: "The publish endpoint answered with an unexpected body.",
      },
    };
  }

  return { ok: true, response: parsed };
}

/**
 * Read the 200 body.
 *
 * The contract names the three fields, so anything else is a body the screen
 * cannot describe and is reported as malformed.
 */
export function parsePublishResponse(body: unknown): SongPublishResponse | null {
  if (body === null || typeof body !== "object" || Array.isArray(body)) {
    return null;
  }

  const record = body as Record<string, unknown>;
  const songId = record["songId"];
  const chartVersion = record["chartVersion"];
  const catalogVersion = record["catalogVersion"];

  if (typeof songId !== "string" || songId.length === 0) {
    return null;
  }
  if (typeof chartVersion !== "number" || !Number.isFinite(chartVersion)) {
    return null;
  }
  if (typeof catalogVersion !== "number" || !Number.isFinite(catalogVersion)) {
    return null;
  }

  return { songId, chartVersion, catalogVersion };
}

async function readFailureDetails(response: Response): Promise<string[]> {
  let text: string;
  try {
    text = await response.text();
  } catch {
    return [];
  }

  if (text.trim().length === 0) {
    return [];
  }

  try {
    return collectMessages(JSON.parse(text) as unknown);
  } catch {
    return [text.trim()];
  }
}

/**
 * Pull the human readable parts out of an error body.
 *
 * The contract does not freeze the shape of an error body, so the known keys
 * are read and anything else is ignored.
 */
function collectMessages(value: unknown): string[] {
  if (typeof value === "string") {
    return value.trim().length === 0 ? [] : [value.trim()];
  }

  if (Array.isArray(value)) {
    return value.flatMap((entry) => collectMessages(entry));
  }

  if (value !== null && typeof value === "object") {
    const record = value as Record<string, unknown>;
    const messages: string[] = [];

    for (const key of ["error", "message", "detail", "issues", "errors"]) {
      const found = record[key];
      if (found !== undefined) {
        messages.push(...collectMessages(found));
      }
    }

    return messages;
  }

  return [];
}

function errorMessage(error: unknown): string {
  if (error instanceof Error) {
    return error.message;
  }
  return String(error);
}

/** Confirm that the exact published snapshot is available before reporting success. */
export async function publishAndVerify(options: PublishSongOptions): Promise<PublishResult> {
  const result = await publishSong(options);
  if (!result.ok) return result;
  const request = options.fetchImpl ?? fetch;
  const base = options.baseUrl.replace(/\/+$/, "");
  try {
    if (result.response.songId !== options.songId)
      throw new Error("Published song identity differs from this draft.");
    const [catalogResponse, chartResponse] = await Promise.all([
      request(`${base}/api/v1/catalog`, { cache: "no-store", signal: options.signal }),
      request(
        `${base}/api/v1/charts/${encodeURIComponent(options.songId)}?version=${result.response.chartVersion}`,
        { cache: "no-store", signal: options.signal },
      ),
    ]);
    if (!catalogResponse.ok || !chartResponse.ok)
      throw new Error("Published data could not be read back.");
    const catalog = (await catalogResponse.json()) as {
      catalogVersion: number;
      songs?: Array<{
        id: string;
        chartVersion: number;
        title: string;
        artist: string;
        youtubeId: string;
        noteCount: number;
      }>;
    };
    const snapshot = (await chartResponse.json()) as {
      songId: string;
      chartVersion: number;
      chart: unknown;
    };
    const song = catalog.songs?.find((entry: { id: string }) => entry.id === options.songId);
    if (
      catalog.catalogVersion !== result.response.catalogVersion ||
      song?.chartVersion !== result.response.chartVersion ||
      snapshot.songId !== options.songId ||
      snapshot.chartVersion !== result.response.chartVersion ||
      !sameJson(snapshot.chart, options.request.chart) ||
      song.title !== options.request.title ||
      song.artist !== options.request.artist ||
      song.youtubeId !== options.request.youtubeId ||
      song.noteCount !== options.request.chart.notes.length
    )
      throw new Error("Published data does not match this draft.");
    return result;
  } catch {
    return {
      ok: false,
      failure: {
        code: "server",
        message:
          "The write may have succeeded, but persisted verification failed. Your draft is preserved. Check Catalog before publishing again.",
      },
    };
  }
}

function sameJson(left: unknown, right: unknown): boolean {
  if (left === right) return true;
  if (left === null || right === null || typeof left !== "object" || typeof right !== "object")
    return false;
  if (Array.isArray(left) !== Array.isArray(right)) return false;
  const a = Object.keys(left).sort();
  const b = Object.keys(right).sort();
  return (
    a.length === b.length &&
    a.every(
      (key, index) =>
        key === b[index] &&
        sameJson((left as Record<string, unknown>)[key], (right as Record<string, unknown>)[key]),
    )
  );
}
