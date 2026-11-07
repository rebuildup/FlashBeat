/**
 * Reading the published catalog.
 *
 * `GET /api/v1/catalog` is the public route of the contract. The Web Studio
 * shows the songs that are really published, so the operator sees the same
 * list as the game.
 */

/** One entry of the catalog (`SongSummary` in the contract). */
export interface CatalogSong {
  id: string;
  title: string;
  artist: string;
  youtubeId: string;
  level: number;
  bpm: number;
  durationSec: number;
  chartVersion: number;
  noteCount: number;
}

export type CatalogFetch = (input: string, init?: RequestInit) => Promise<Response>;

export interface CatalogReadOptions {
  /** Origin of the API. Empty means the same origin as the studio. */
  baseUrl?: string;
  /** Transport used for the request. */
  fetchImpl?: CatalogFetch;
  /** Timeout of the request in milliseconds. Defaults to `5000`. */
  timeoutMs?: number;
}

/**
 * Read the songs of a catalog body.
 *
 * The body is only accepted when every song has the fields of the contract.
 * A body that does not match is `null`, so the caller can fall back instead
 * of showing a broken list.
 */
export function parseCatalogSongs(value: unknown): CatalogSong[] | null {
  if (value === null || typeof value !== "object" || Array.isArray(value)) {
    return null;
  }

  const songs = (value as Record<string, unknown>)["songs"];
  if (!Array.isArray(songs)) {
    return null;
  }

  const parsed: CatalogSong[] = [];

  for (const entry of songs) {
    if (entry === null || typeof entry !== "object" || Array.isArray(entry)) {
      return null;
    }

    const song = entry as Record<string, unknown>;
    const id = song["id"];
    const title = song["title"];
    const artist = song["artist"];
    const youtubeId = song["youtubeId"];
    const level = song["level"];
    const bpm = song["bpm"];
    const durationSec = song["durationSec"];
    const chartVersion = song["chartVersion"];
    const noteCount = song["noteCount"];

    if (
      typeof id !== "string" ||
      typeof title !== "string" ||
      typeof artist !== "string" ||
      typeof youtubeId !== "string" ||
      typeof level !== "number" ||
      typeof bpm !== "number" ||
      typeof durationSec !== "number" ||
      typeof chartVersion !== "number" ||
      typeof noteCount !== "number"
    ) {
      return null;
    }

    parsed.push({
      id,
      title,
      artist,
      youtubeId,
      level,
      bpm,
      durationSec,
      chartVersion,
      noteCount,
    });
  }

  return parsed;
}

/**
 * Fetch the published songs.
 *
 * `null` means the API could not be read: the caller decides what to show
 * instead. An empty list is a valid answer and means that nothing is
 * published yet.
 */
export async function fetchCatalogSongs(
  options: CatalogReadOptions = {},
): Promise<CatalogSong[] | null> {
  const doFetch: CatalogFetch = options.fetchImpl ?? ((input, init) => fetch(input, init));
  const base = (options.baseUrl ?? "").replace(/\/+$/, "");

  try {
    const response = await doFetch(`${base}/api/v1/catalog`, {
      method: "GET",
      headers: { Accept: "application/json" },
      signal: AbortSignal.timeout(options.timeoutMs ?? 5000),
    });

    if (!response.ok) {
      return null;
    }

    return parseCatalogSongs((await response.json()) as unknown);
  } catch {
    return null;
  }
}
