import { expect, it } from "vitest";
import { fallbackSlug, freeAlternatives, slugifyTitle, suggestSongSlug } from "../slug";

it("reduces an ascii title to kebab-case", () => {
  expect(slugifyTitle("Who")).toBe("who");
  expect(slugifyTitle("Neon Genesis Ops!")).toBe("neon-genesis-ops");
  expect(slugifyTitle("  Already -- Slugged  ")).toBe("already-slugged");
});

it("returns an empty slug for Japanese-only titles", () => {
  expect(slugifyTitle("ニセモノ")).toBe("");
  expect(slugifyTitle("夜に駆ける")).toBe("");
});

it("keeps ascii inside mixed titles", () => {
  expect(slugifyTitle("Bling-Bang-Bang-Born")).toBe("bling-bang-bang-born");
  expect(slugifyTitle("アイドル (Idol)")).toBe("idol");
});

it("caps the slug at the 64 character contract limit", () => {
  const long = slugifyTitle("a".repeat(80));
  expect(long.length).toBe(64);
});

it("falls back to the video id when the title has no ascii", () => {
  expect(fallbackSlug("dQw4w9WgXcQ")).toBe("song-dqw4w9wgxcq");
});

it("accepts a free suggestion untouched", () => {
  const taken = new Set(["other", "yet-another"]);
  const suggestion = suggestSongSlug("Who", "dQw4w9WgXcQ", taken);
  expect(suggestion).toEqual({ suggestion: "who", taken: false, alternatives: [] });
});

it("reports a used id with numbered free alternatives", () => {
  const taken = new Set(["who", "who-2", "who-4"]);
  const suggestion = suggestSongSlug("Who", "dQw4w9WgXcQ", taken);
  expect(suggestion.taken).toBe(true);
  expect(suggestion.suggestion).toBe("who");
  expect(suggestion.alternatives).toEqual(["who-3", "who-5", "who-6"]);
});

it("skips a run of taken ids without producing duplicates", () => {
  const taken = new Set(["who", "who-2", "who-3"]);
  expect(freeAlternatives("who", taken, 3)).toEqual(["who-4", "who-5", "who-6"]);
});

it("keeps the suffix inside the 64 character limit", () => {
  const base = "a".repeat(64);
  const taken = new Set([base]);
  const [first] = freeAlternatives(base, taken, 1);
  expect(first.length).toBe(64);
  expect(first.endsWith("-2")).toBe(true);
  expect(taken.has(first)).toBe(false);
});

it("suggests nothing while there is neither ascii title nor video id", () => {
  expect(suggestSongSlug("ニセモノ", "", new Set())).toEqual({
    suggestion: "",
    taken: false,
    alternatives: [],
  });
});

it("uses the video id fallback for a Japanese title even when taken", () => {
  const taken = new Set(["song-dqw4w9wgxcq"]);
  const suggestion = suggestSongSlug("夜に駆ける", "dQw4w9WgXcQ", taken);
  expect(suggestion.taken).toBe(true);
  expect(suggestion.alternatives[0]).toBe("song-dqw4w9wgxcq-2");
});
