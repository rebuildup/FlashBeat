import { importChartJson } from "../chart/import";
import type { Chart } from "../chart/types";

export const DRAFT_PREFIX = "flashbeat-publish-draft:";
/**
 * Every publish roundtrip stores a full chart JSON, so the drafts are
 * bounded: only the newest few survive. `sessionStorage` dies with the tab,
 * the cap only keeps a long editing session from filling the quota.
 */
const MAX_PUBLISH_DRAFTS = 5;

export interface PublishDraft {
  songId: string | null;
  version: string | null;
  chart: Chart;
}

/** `savedAt` orders the garbage collection; readers ignore it. */
interface StoredPublishDraft extends PublishDraft {
  savedAt?: number;
}

/**
 * Store a chart draft and return the URL that hands it over.
 *
 * The default target is the publish screen. `target: "editor"` returns a
 * `/chart` URL instead, so a failed publish can send the operator back to the
 * editor without losing the chart they were about to publish. Both targets
 * share the storage and the `readPublishDraft` reader.
 */
export function savePublishDraft(
  draft: PublishDraft,
  target: "publish" | "editor" = "publish",
): string {
  const id = crypto.randomUUID();
  const stored: StoredPublishDraft = { ...draft, savedAt: Date.now() };
  sessionStorage.setItem(DRAFT_PREFIX + id, JSON.stringify(stored));
  try {
    prunePublishDrafts(DRAFT_PREFIX + id);
  } catch {
    // The collection is best-effort: a storage quirk must not lose the draft
    // that was just saved.
  }
  const query = new URLSearchParams({ draft: id });
  if (draft.songId !== null) query.set("songId", draft.songId);
  if (draft.version !== null) query.set("version", draft.version);
  return `/${target === "editor" ? "chart" : "publish"}?${query}`;
}

/**
 * Drop the oldest drafts beyond `MAX_PUBLISH_DRAFTS`.
 *
 * `retainedKey` is the draft just written: it always survives, so at most
 * `MAX_PUBLISH_DRAFTS - 1` other entries are kept next to it.
 */
function prunePublishDrafts(retainedKey: string): void {
  const entries: { key: string; savedAt: number }[] = [];
  for (let index = 0; index < sessionStorage.length; index += 1) {
    const key = sessionStorage.key(index);
    if (key === null || key === retainedKey || !key.startsWith(DRAFT_PREFIX)) continue;
    entries.push({ key, savedAt: draftSavedAt(sessionStorage.getItem(key)) });
  }

  entries.sort((left, right) => right.savedAt - left.savedAt);
  for (const entry of entries.slice(MAX_PUBLISH_DRAFTS - 1)) {
    sessionStorage.removeItem(entry.key);
  }
}

function draftSavedAt(raw: string | null): number {
  if (raw === null) return 0;
  try {
    const savedAt = (JSON.parse(raw) as { savedAt?: unknown }).savedAt;
    return typeof savedAt === "number" && Number.isFinite(savedAt) ? savedAt : 0;
  } catch {
    return 0;
  }
}

export function readPublishDraft(id: string, songId: string | null, version: string | null): Chart {
  if (!/^[a-f0-9-]{36}$/.test(id)) throw new Error("ローカル下書きの識別子が不正です。");
  const raw = sessionStorage.getItem(DRAFT_PREFIX + id);
  if (!raw)
    throw new Error("このローカル下書きは利用できません。同じタブでエディタに戻ってください。");
  const value = JSON.parse(raw) as PublishDraft;
  if (value.songId !== songId || value.version !== version)
    throw new Error("ローカル下書きは別の曲またはバージョンのものです。");
  const imported = importChartJson(JSON.stringify(value.chart));
  if (!imported.ok)
    throw new Error("ローカル下書きの譜面が検証に失敗しました。エディタに戻ってください。");
  return imported.chart;
}
