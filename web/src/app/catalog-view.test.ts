import { describe, expect, it } from "vitest";
import {
  filterAndSort,
  formatDuration,
  isSortKey,
  matchesLevel,
  type SortKey,
} from "./catalog-view";
import type { SongSummary } from "./types";

function song(overrides: Partial<SongSummary> & { id: string }): SongSummary {
  return {
    title: overrides.id,
    artist: "Artist",
    youtubeId: "dQw4w9WgXcQ",
    level: 10,
    bpm: 120,
    durationSec: 60,
    chartVersion: 1,
    noteCount: 100,
    ...overrides,
  };
}

const catalog: SongSummary[] = [
  song({
    id: "4th-smile",
    title: "4th smile",
    artist: "LeaF",
    level: 5,
    bpm: 140,
    durationSec: 69,
    noteCount: 105,
  }),
  song({
    id: "agon-beats",
    title: "阿吽のビーツ",
    artist: "羽生まゐご",
    level: 25,
    bpm: 206,
    durationSec: 67,
    noteCount: 262,
  }),
  song({
    id: "bibibibi",
    title: "ビビビビ",
    artist: "フロクロ",
    level: 15,
    bpm: 175,
    durationSec: 74,
    noteCount: 184,
  }),
  song({
    id: "calamity",
    title: "Calamity",
    artist: "LeaF",
    level: 7,
    bpm: 200,
    durationSec: 52,
    noteCount: 111,
  }),
];

describe("matchesLevel", () => {
  it("splits the bands at the published level edges", () => {
    expect(matchesLevel(5, "low")).toBe(true);
    expect(matchesLevel(9, "low")).toBe(true);
    expect(matchesLevel(10, "low")).toBe(false);
    expect(matchesLevel(10, "mid")).toBe(true);
    expect(matchesLevel(19, "mid")).toBe(true);
    expect(matchesLevel(20, "mid")).toBe(false);
    expect(matchesLevel(25, "high")).toBe(true);
    expect(matchesLevel(25, "all")).toBe(true);
  });
});

describe("filterAndSort", () => {
  it("matches the query against title and artist, case-insensitive", () => {
    const byLeaf = filterAndSort(catalog, "leaf", "all", "catalog");
    expect(byLeaf.map((song) => song.id)).toEqual(["4th-smile", "calamity"]);
    const byAun = filterAndSort(catalog, "阿吽", "all", "catalog");
    expect(byAun.map((song) => song.id)).toEqual(["agon-beats"]);
  });

  it("combines query and level filter", () => {
    const both = filterAndSort(catalog, "LeaF", "high", "catalog");
    expect(both).toEqual([]);
    const lowLeaF = filterAndSort(catalog, "LeaF", "low", "catalog");
    expect(lowLeaF.map((song) => song.id)).toEqual(["4th-smile", "calamity"]);
  });

  it("keeps the catalog order for the catalog key", () => {
    expect(filterAndSort(catalog, "", "all", "catalog").map((song) => song.id)).toEqual([
      "4th-smile",
      "agon-beats",
      "bibibibi",
      "calamity",
    ]);
  });

  it("sorts ASCII titles ascending", () => {
    const ascii = [
      song({ id: "c", title: "Calamity" }),
      song({ id: "a", title: "4th smile" }),
      song({ id: "b", title: "Bibibibi" }),
    ];
    expect(filterAndSort(ascii, "", "all", "title").map((song) => song.id)).toEqual([
      "a",
      "b",
      "c",
    ]);
  });

  it("sorts Japanese titles deterministically", () => {
    const first = filterAndSort(catalog, "", "all", "title").map((song) => song.id);
    const second = filterAndSort(catalog, "", "all", "title").map((song) => song.id);
    expect(first).toEqual(second);
    expect(first).toHaveLength(4);
  });

  it("sorts level, bpm, notes and length descending with a title tie-break", () => {
    expect(filterAndSort(catalog, "", "all", "level").map((song) => song.id)).toEqual([
      "agon-beats",
      "bibibibi",
      "calamity",
      "4th-smile",
    ]);
    expect(filterAndSort(catalog, "", "all", "bpm")[0].id).toBe("agon-beats");
    expect(filterAndSort(catalog, "", "all", "notes")[0].id).toBe("agon-beats");
    expect(filterAndSort(catalog, "", "all", "length").map((song) => song.id)).toEqual([
      "bibibibi",
      "4th-smile",
      "agon-beats",
      "calamity",
    ]);
  });

  it("does not mutate the input catalog", () => {
    filterAndSort(catalog, "", "all", "level");
    expect(catalog[0].id).toBe("4th-smile");
  });
});

describe("formatDuration", () => {
  it("renders m:ss", () => {
    expect(formatDuration(52)).toBe("0:52");
    expect(formatDuration(69)).toBe("1:09");
    expect(formatDuration(604)).toBe("10:04");
  });
});

describe("isSortKey", () => {
  it("accepts only published sort keys", () => {
    for (const key of ["catalog", "title", "level", "bpm", "notes", "length"] as SortKey[]) {
      expect(isSortKey(key)).toBe(true);
    }
    expect(isSortKey("recent")).toBe(false);
    expect(isSortKey(null)).toBe(false);
  });
});
