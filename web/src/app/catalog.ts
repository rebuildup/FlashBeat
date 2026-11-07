import { fetchCatalogSongs } from "@/lib/catalog/read";
import type { SongSummary } from "./types";

/**
 * Catalog view states rendered by the catalog panel.
 *
 * `loading` / `ready` / `empty` / `error` are the four states the Web Studio
 * has to cover before the real endpoint exists.
 */
export type CatalogMode = "loading" | "ready" | "empty" | "error";

/**
 * Local stand-in for `GET /api/v1/catalog`.
 *
 * The Web Studio reads the published songs of the real API first, so the
 * operator sees the same list as the game. This fixture is the fallback for
 * a studio that cannot reach the API, and it keeps the shell, the toolbar and
 * every list state demonstrable.
 */
const FIXTURE: SongSummary[] = [
  {
    id: "who",
    title: "Who",
    artist: "Azari",
    youtubeId: "dQw4w9WgXcQ",
    level: 5,
    bpm: 128,
    durationSec: 68,
    chartVersion: 1,
    noteCount: 412,
  },
  {
    id: "roast-ambrella",
    title: "ロストアンブレラ",
    artist: "稲葉曇",
    youtubeId: "x7cQ3mJSvFg",
    level: 8,
    bpm: 274,
    durationSec: 89,
    chartVersion: 1,
    noteCount: 688,
  },
  {
    id: "manimani",
    title: "まにまに",
    artist: "r-906",
    youtubeId: "ZbZSe6N_BXs",
    level: 13,
    bpm: 174,
    durationSec: 48,
    chartVersion: 2,
    noteCount: 731,
  },
  {
    id: "euthanasia",
    title: "エウタナシア",
    artist: "ど～ぱみん",
    youtubeId: "hTWKbfoikeg",
    level: 18,
    bpm: 127,
    durationSec: 90,
    chartVersion: 1,
    noteCount: 904,
  },
];

export async function loadCatalog(mode: CatalogMode, apiOrigin = ""): Promise<SongSummary[]> {
  const { promise, resolve } = Promise.withResolvers<void>();
  setTimeout(resolve, 220);
  await promise;

  if (mode === "error") {
    throw new Error("GET /api/v1/catalog failed");
  }

  if (mode === "empty") {
    return [];
  }

  // The published songs of the real API come first, so the operator sees the
  // same list as the game. The fixture is the fallback for an offline studio.
  const published = await fetchCatalogSongs({ baseUrl: apiOrigin });

  if (published !== null) {
    return published;
  }

  return FIXTURE;
}
