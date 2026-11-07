import type { SongSummary } from "./types";

/**
 * Pure catalog view logic (issue #70): filter, sort and format.
 *
 * Kept free of DOM so the vitest suite (src glob test files) can pin the
 * ordering and filtering contract the panel renders.
 */

export type LevelFilter = "all" | "low" | "mid" | "high";

/** Sort keys over the fields the catalog API actually returns. */
export type SortKey = "catalog" | "title" | "level" | "bpm" | "notes" | "length";

export const SORT_OPTIONS: ReadonlyArray<{ label: string; value: SortKey }> = [
  { label: "Catalog", value: "catalog" },
  { label: "Title A–Z", value: "title" },
  { label: "Level", value: "level" },
  { label: "BPM", value: "bpm" },
  { label: "Notes", value: "notes" },
  { label: "Length", value: "length" },
];

export function isSortKey(value: string | null): value is SortKey {
  return SORT_OPTIONS.some((option) => option.value === value);
}

export function matchesLevel(level: number, filter: LevelFilter): boolean {
  if (filter === "all") return true;
  if (filter === "low") return level < 10;
  if (filter === "mid") return level >= 10 && level < 20;
  return level >= 20;
}

/**
 * Filter and sort a catalog page.
 *
 * `catalog` keeps the API order (the order charts were published); every other
 * key sorts by that field, numerics descending, title ascending, with the
 * title as deterministic tie-break.
 */
export function filterAndSort(
  songs: readonly SongSummary[],
  query: string,
  level: LevelFilter,
  sort: SortKey,
): SongSummary[] {
  const needle = query.trim().toLowerCase();
  const visible = songs.filter(
    (song) =>
      (needle.length === 0 ||
        song.title.toLowerCase().includes(needle) ||
        song.artist.toLowerCase().includes(needle)) &&
      matchesLevel(song.level, level),
  );

  const byTitle = (a: SongSummary, b: SongSummary) =>
    a.title.localeCompare(b.title, "ja") || a.id.localeCompare(b.id);

  const sorted = [...visible];
  if (sort === "title") {
    sorted.sort(byTitle);
  } else if (sort !== "catalog") {
    const key: (song: SongSummary) => number =
      sort === "level"
        ? (song) => song.level
        : sort === "bpm"
          ? (song) => song.bpm
          : sort === "notes"
            ? (song) => song.noteCount
            : (song) => song.durationSec;
    sorted.sort((a, b) => key(b) - key(a) || byTitle(a, b));
  }
  return sorted;
}

/** `durationSec` as `m:ss` (`69` → `1:09`); never shows a 0-padded hour. */
export function formatDuration(durationSec: number): string {
  const minutes = Math.floor(durationSec / 60);
  const seconds = durationSec % 60;
  return `${minutes}:${String(seconds).padStart(2, "0")}`;
}
