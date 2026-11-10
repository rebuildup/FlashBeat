import type { SongSummary } from "./types";

/**
 * Catalog view states rendered by the catalog panel.
 *
 * `loading` / `ready` / `empty` / `error` are the four states the Web Studio
 * has to cover.
 */
export type CatalogMode = "loading" | "ready" | "empty" | "error";

/** 読み込みのオプション。中断シグナルを API 呼び出しまで引き回す。 */
export interface LoadCatalogOptions {
  /** 中断したい要求のシグナル。新しいリクエストが始まったら古い方を abort する。 */
  signal?: AbortSignal;
}

/**
 * Load the published catalog.
 *
 * The read endpoint is served by the same worker as the studio, so the list
 * comes from `/api/v1/catalog` of the current origin — the songs the festival
 * build actually offers. `?state=loading|empty|error` keeps the review states
 * of the panel reachable without a backend.
 *
 * `options.signal` で要求を中断できる。abort された要求は失敗ではなく
 * 「この応答はもう要らない」扱いなので、呼び出し側でエラー表示にしない。
 */
export async function loadCatalog(
  mode: CatalogMode,
  options: LoadCatalogOptions = {},
): Promise<SongSummary[]> {
  if (mode === "error") {
    throw new Error("GET /api/v1/catalog failed");
  }

  if (mode === "empty") {
    return [];
  }

  const response = await fetch("/api/v1/catalog", {
    headers: { accept: "application/json" },
    signal: options.signal,
  });
  if (!response.ok) {
    throw new Error(`GET /api/v1/catalog answered ${response.status}`);
  }

  const body: unknown = await response.json();
  return parseSongs(body);
}

/**
 * 失敗が「中断」かどうか。
 *
 * abort は失敗ではないので、UI は「カタログを取得できません」とは表示しない。
 * `fetch` は中断時に `AbortError` の `DOMException` を投げるが、
 * 実装によって別の形になることもあるので `name` だけを見る。
 */
export function isAbortError(error: unknown): boolean {
  return (
    typeof error === "object" &&
    error !== null &&
    (error as { name?: unknown }).name === "AbortError"
  );
}

/** A JSON object with unknown members, or null for everything else. */
function asObject(value: unknown): Record<string, unknown> | null {
  if (typeof value !== "object" || value === null || Array.isArray(value)) {
    return null;
  }
  return Object.fromEntries(Object.entries(value));
}

/**
 * Read the songs of a catalog answer.
 *
 * The API validates its answer against the frozen contract; the fields the
 * panel renders are narrowed here, so a broken answer is an error and not a
 * half filled table.
 *
 * 表示する曲リスト（`songs`）だけが表に出る契約。`catalogVersion` は
 * カタログ全体の版で、このクライアントは使わないので読み飛ばす。
 */
export function parseSongs(body: unknown): SongSummary[] {
  const catalog = asObject(body);
  if (catalog === null || !Array.isArray(catalog.songs)) {
    throw new Error("GET /api/v1/catalog answered no song list");
  }

  return catalog.songs.map((song) => {
    const entry = asObject(song);
    if (entry === null) {
      throw new Error("GET /api/v1/catalog answered a broken song");
    }

    const { id, title, artist, youtubeId, level, bpm, durationSec, chartVersion, noteCount } =
      entry;
    if (
      typeof id !== "string" ||
      typeof title !== "string" ||
      typeof artist !== "string" ||
      typeof youtubeId !== "string" ||
      typeof level !== "number" ||
      typeof bpm !== "number" ||
      typeof durationSec !== "number" ||
      typeof chartVersion !== "number" ||
      typeof noteCount !== "number"
    ) {
      throw new Error("GET /api/v1/catalog answered a broken song");
    }

    return {
      id,
      title,
      artist,
      youtubeId,
      level,
      bpm,
      durationSec,
      chartVersion,
      noteCount,
    };
  });
}
