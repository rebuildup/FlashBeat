import { afterEach, expect, it, vi } from "vitest";
import { SAMPLE_CHART } from "../../chart/fixtures";
import type { NewSongDraft } from "../draft";
import {
  clearNewSongDraft,
  emptyNewSongDraft,
  loadNewSongDraft,
  NEW_SONG_DRAFT_KEY,
  saveNewSongDraft,
} from "../draft";

const values = new Map<string, string>();
vi.stubGlobal("sessionStorage", {
  setItem: (key: string, value: string) => values.set(key, value),
  getItem: (key: string) => values.get(key) ?? null,
  removeItem: (key: string) => values.delete(key),
});
afterEach(() => values.clear());

function draftWith(overrides: Partial<NewSongDraft>): NewSongDraft {
  return { ...emptyNewSongDraft(), ...overrides };
}

it("round-trips the whole flow through sessionStorage", () => {
  const draft = draftWith({
    step: 2,
    maxStep: 3,
    info: { youtube: "https://youtu.be/dQw4w9WgXcQ?si=x", title: "Who", artist: "Azari", songId: "who", level: 7, durationSec: 68 },
    chart: { ...SAMPLE_CHART, BPM: 157 },
  });
  saveNewSongDraft(draft);

  const loaded = loadNewSongDraft();
  expect(loaded).not.toBeNull();
  expect(loaded?.step).toBe(2);
  expect(loaded?.maxStep).toBe(3);
  expect(loaded?.info).toEqual(draft.info);
  expect(loaded?.chart?.BPM).toBe(157);
  expect(loaded?.savedAt).not.toBe("");
});

it("survives a page reload in the middle of the chart step", () => {
  const draft = draftWith({ step: 2, chart: SAMPLE_CHART });
  saveNewSongDraft(draft);
  values.set(NEW_SONG_DRAFT_KEY, values.get(NEW_SONG_DRAFT_KEY) ?? "");

  const loaded = loadNewSongDraft();
  expect(loaded?.step).toBe(2);
  expect(loaded?.chart?.notes.length).toBe(SAMPLE_CHART.notes.length);
});

it("turns the JSON null of an empty number field back into NaN", () => {
  saveNewSongDraft(draftWith({ info: { youtube: "", title: "", artist: "", songId: "", level: Number.NaN, durationSec: Number.NaN } }));
  const loaded = loadNewSongDraft();
  expect(loaded?.info.level).toBeNaN();
  expect(loaded?.info.durationSec).toBeNaN();
});

it("returns null for corrupt JSON instead of throwing", () => {
  values.set(NEW_SONG_DRAFT_KEY, "{not json");
  expect(loadNewSongDraft()).toBeNull();
});

it("returns null when the stored shape is not a draft", () => {
  values.set(NEW_SONG_DRAFT_KEY, JSON.stringify({ hello: "world" }));
  expect(loadNewSongDraft()).toBeNull();
});

it("keeps the info and drops only a broken chart", () => {
  values.set(NEW_SONG_DRAFT_KEY, JSON.stringify({ step: 2, maxStep: 2, info: { title: "Who" }, chart: { broken: true }, savedAt: "" }));
  const loaded = loadNewSongDraft();
  expect(loaded?.info.title).toBe("Who");
  expect(loaded?.chart).toBeNull();
});

it("clear removes the draft", () => {
  saveNewSongDraft(emptyNewSongDraft());
  expect(loadNewSongDraft()).not.toBeNull();
  clearNewSongDraft();
  expect(loadNewSongDraft()).toBeNull();
});
