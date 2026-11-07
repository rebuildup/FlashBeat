import type { SongSummary } from "./types";

/**
 * Catalog view states rendered by the catalog panel.
 *
 * `loading` / `ready` / `empty` / `error` are the four states the Web Studio
 * has to cover.
 */
export type CatalogMode = "loading" | "ready" | "empty" | "error";

/**
 * Load the published catalog.
 *
 * The read endpoint is served by the same worker as the studio, so the list
 * comes from `/api/v1/catalog` of the current origin — the songs the festival
 * build actually offers. `?state=loading|empty|error` keeps the review states
 * of the panel reachable without a backend.
 */
export async function loadCatalog(mode: CatalogMode): Promise<SongSummary[]> {
  if (mode === "error") {
    throw new Error("The catalog could not be loaded. Please check your connection and try again.");
  }

  if (mode === "empty") {
    return [];
  }

  const response = await fetch("/api/v1/catalog", {
    headers: { accept: "application/json" },
  });
  if (!response.ok) {
    throw new Error("The catalog request failed. Please try again later.");
  }

  const body: unknown = await response.json();
  return parseSongs(body);
}

/** A JSON object with unknown members, or null for everything else. */
function asObject(value: unknown): Record<string, unknown> | null {
  if (typeof value !== "object" || value === null || Array.isArray(value)) {
    return null;
  }
  return Object.fromEntries(Object.entries(value));
}

/**
 * Read the songs of a catalog answer.
 *
 * The API validates its answer against the frozen contract; the fields the
 * panel renders are narrowed here, so a broken answer is an error and not a
 * half filled table.
 */
function parseSongs(body: unknown): SongSummary[] {
  const catalog = asObject(body);
  if (catalog === null || !Array.isArray(catalog.songs)) {
    throw new Error("GET /api/v1/catalog answered no song list");
  }

  return catalog.songs.map((song) => {
    const entry = asObject(song);
    if (entry === null) {
      throw new Error("GET /api/v1/catalog answered a broken song");
    }

    const { id, title, artist, youtubeId, level, bpm, durationSec, chartVersion, noteCount } =
      entry;
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
      throw new Error("GET /api/v1/catalog answered a broken song");
    }

    return {
      id,
      title,
      artist,
      youtubeId,
      level,
      bpm,
      durationSec,
      chartVersion,
      noteCount,
    };
  });
}
