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
 * The real read endpoint is owned by #15. Until it is deployed the Web Studio
 * renders this fixture so the shell, the toolbar and every list state stay
 * demonstrable. Replace the body of {@link loadCatalog} with a `fetch` to
 * `/api/v1/catalog` once that endpoint is live.
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

export async function loadCatalog(mode: CatalogMode): Promise<SongSummary[]> {
  const { promise, resolve } = Promise.withResolvers<void>();
  setTimeout(resolve, 220);
  await promise;

  if (mode === "error") {
    throw new Error("GET /api/v1/catalog failed");
  }

  if (mode === "empty") {
    return [];
  }

  return FIXTURE;
}
