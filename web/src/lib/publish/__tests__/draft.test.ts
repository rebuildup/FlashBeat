import { afterEach, expect, it, vi } from "vitest";
import { readPublishDraft, savePublishDraft } from "../draft";
import { SAMPLE_CHART } from "../../chart/fixtures";
const values = new Map<string, string>();
vi.stubGlobal("sessionStorage", {
  get length() {
    return values.size;
  },
  key: (index: number) => [...values.keys()][index] ?? null,
  setItem: (key: string, value: string) => values.set(key, value),
  getItem: (key: string) => values.get(key) ?? null,
  removeItem: (key: string) => values.delete(key),
});
afterEach(() => {
  values.clear();
  vi.useRealTimers();
});
it("retains edited chart through publish handoff without credentials", () => {
  const chart = { ...SAMPLE_CHART, BPM: 157 };
  const url = new URL(
    savePublishDraft({ songId: "who", version: "2", chart }),
    "https://example.test",
  );
  const id = url.searchParams.get("draft")!;
  expect(readPublishDraft(id, "who", "2").BPM).toBe(157);
  expect(() => readPublishDraft(id, "other", "2")).toThrow("別の曲");
  expect(readPublishDraft(id, "who", "2").BPM).toBe(157);
});
it("does not replace missing drafts with sample data", () => {
  expect(() => readPublishDraft("00000000-0000-0000-0000-000000000000", "who", "2")).toThrow(
    "利用できません",
  );
});
it("hands a failed publish back to the editor with the chart draft kept", () => {
  const chart = { ...SAMPLE_CHART, BPM: 190 };
  const url = new URL(
    savePublishDraft({ songId: "who", version: "2", chart }, "editor"),
    "https://example.test",
  );
  expect(url.pathname).toBe("/chart");
  const id = url.searchParams.get("draft")!;
  expect(url.searchParams.get("songId")).toBe("who");
  expect(url.searchParams.get("version")).toBe("2");
  expect(readPublishDraft(id, "who", "2").BPM).toBe(190);
});
it("keeps only the newest drafts when the storage fills up", () => {
  vi.useFakeTimers();
  const ids = Array.from({ length: 8 }, (_, index) => {
    vi.setSystemTime(new Date(2026, 0, 1, 0, 0, index));
    const url = new URL(
      savePublishDraft({ songId: null, version: null, chart: SAMPLE_CHART }),
      "https://example.test",
    );
    return url.searchParams.get("draft")!;
  });
  vi.useRealTimers();

  expect(values.size).toBe(5);
  // The oldest drafts are gone; the newest handoff still resolves.
  expect(() => readPublishDraft(ids[0], null, null)).toThrow("利用できません");
  expect(() => readPublishDraft(ids[2], null, null)).toThrow("利用できません");
  expect(readPublishDraft(ids[7], null, null).BPM).toBe(SAMPLE_CHART.BPM);
});
it("keeps the publish handoff URL for new drafts without a song", () => {
  const url = new URL(
    savePublishDraft({ songId: null, version: null, chart: SAMPLE_CHART }, "editor"),
    "https://example.test",
  );
  expect(url.pathname).toBe("/chart");
  expect(url.searchParams.has("songId")).toBe(false);
  expect(url.searchParams.has("version")).toBe(false);
  expect(
    new URL(
      savePublishDraft({ songId: null, version: null, chart: SAMPLE_CHART }),
      "https://example.test",
    ).pathname,
  ).toBe("/publish");
});
