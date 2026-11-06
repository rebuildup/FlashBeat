import { afterEach, expect, it, vi } from "vitest";
import { readPublishDraft, savePublishDraft } from "../draft";
import { SAMPLE_CHART } from "../../chart/fixtures";
const values = new Map<string, string>();
vi.stubGlobal("sessionStorage", { setItem: (key: string, value: string) => values.set(key, value), getItem: (key: string) => values.get(key) ?? null });
afterEach(() => values.clear());
it("retains edited chart through publish handoff without credentials", () => {
  const chart = { ...SAMPLE_CHART, BPM: 157 };
  const url = new URL(savePublishDraft({ songId: "who", version: "2", chart }), "https://example.test");
  const id = url.searchParams.get("draft")!;
  expect(readPublishDraft(id, "who", "2").BPM).toBe(157);
  expect(() => readPublishDraft(id, "other", "2")).toThrow("another song");
  expect(readPublishDraft(id, "who", "2").BPM).toBe(157);
});
it("does not replace missing drafts with sample data", () => {
  expect(() => readPublishDraft("00000000-0000-0000-0000-000000000000", "who", "2")).toThrow("unavailable");
});
