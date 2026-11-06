import { importChartJson } from "../chart/import";
import type { Chart } from "../chart/types";

export const DRAFT_PREFIX = "flashbeat-publish-draft:";
export interface PublishDraft { songId: string | null; version: string | null; chart: Chart }
export function savePublishDraft(draft: PublishDraft): string {
  const id = crypto.randomUUID();
  sessionStorage.setItem(DRAFT_PREFIX + id, JSON.stringify(draft));
  const query = new URLSearchParams({ draft: id });
  if (draft.songId !== null) query.set("songId", draft.songId);
  if (draft.version !== null) query.set("version", draft.version);
  return `/publish?${query}`;
}
export function readPublishDraft(id: string, songId: string | null, version: string | null): Chart {
  if (!/^[a-f0-9-]{36}$/.test(id)) throw new Error("Invalid local draft identifier.");
  const raw = sessionStorage.getItem(DRAFT_PREFIX + id);
  if (!raw) throw new Error("This local draft is unavailable. Return to the editor in the same tab.");
  const value = JSON.parse(raw) as PublishDraft;
  if (value.songId !== songId || value.version !== version) throw new Error("Local draft belongs to another song or version.");
  const imported = importChartJson(JSON.stringify(value.chart));
  if (!imported.ok) throw new Error("Local draft failed chart validation. Return to the editor.");
  return imported.chart;
}
