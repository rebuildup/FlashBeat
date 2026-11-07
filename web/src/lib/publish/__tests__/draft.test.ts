import { afterEach, expect, it, vi } from "vitest";
import { readPublishDraft, savePublishDraft } from "../draft";
import { SAMPLE_CHART } from "../../chart/fixtures";
const values = new Map<string, string>();
vi.stubGlobal("sessionStorage", {
  setItem: (key: string, value: string) => values.set(key, value),
  getItem: (key: string) => values.get(key) ?? null,
});
afterEach(() => values.clear());
it("retains edited chart through publish handoff without credentials", () => {
  const chart = { ...SAMPLE_CHART, BPM: 157 };
  const url = new URL(
    savePublishDraft({ songId: "who", version: "2", chart }),
    "https://example.test",
  );
  const id = url.searchParams.get("draft")!;
  expect(readPublishDraft(id, "who", "2").BPM).toBe(157);
  expect(() => readPublishDraft(id, "other", "2")).toThrow("another song");
  expect(readPublishDraft(id, "who", "2").BPM).toBe(157);
});
it("does not replace missing drafts with sample data", () => {
  expect(() => readPublishDraft("00000000-0000-0000-0000-000000000000", "who", "2")).toThrow(
    "unavailable",
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
