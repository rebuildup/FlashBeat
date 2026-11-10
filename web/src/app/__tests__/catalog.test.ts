import { afterEach, describe, expect, it, vi } from "vitest";
import { isAbortError, loadCatalog, parseSongs } from "../catalog";
import type { SongSummary } from "../types";

/**
 * Issue #68 — カタログクライアントの契約テスト。
 *
 * `loadCatalog` と `parseSongs` は API と UI の契約なので、
 * 形が壊れた応答は「半分の埋まった表」ではなく必ずエラーになる。
 * ネットワークには出ない: `fetch` は差し替えた fake だけを使う。
 */

/** 契約 (`contracts/openapi.v1.yaml` の `SongSummary`) どおりの 1 曲。 */
const contractSong = {
  id: "who",
  title: "Who",
  artist: "Azari",
  youtubeId: "8JXiXt0D6tw",
  level: 5,
  bpm: 128,
  durationSec: 68,
  chartVersion: 1,
  noteCount: 139,
};

/** API の答え (200)。`catalogVersion` はカタログ全体の版。 */
const contractAnswer = {
  catalogVersion: 38,
  songs: [contractSong],
};

afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

/** 成功する `fetch` の fake。 */
function stubJsonResponse(body: unknown, init: { ok?: boolean; status?: number } = {}) {
  const fetchMock = vi.fn(async (_url: string, _options?: RequestInit) => ({
    ok: init.ok ?? true,
    status: init.status ?? 200,
    json: async () => body,
  }));
  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
}

describe("loadCatalog", () => {
  it("GET /api/v1/catalog の答えを曲リストに読み替える", async () => {
    const fetchMock = stubJsonResponse(contractAnswer);

    const songs = await loadCatalog("loading");

    expect(songs).toEqual([contractSong]);
    expect(fetchMock).toHaveBeenCalledTimes(1);
    const [url, options] = fetchMock.mock.calls[0];
    const headers = options?.headers as Record<string, string> | undefined;
    expect(url).toBe("/api/v1/catalog");
    expect(headers?.accept).toBe("application/json");
  });

  it("キャッシュを使わない (サーバと同じ no-store)", async () => {
    const fetchMock = stubJsonResponse(contractAnswer);

    await loadCatalog("loading");

    const [, options] = fetchMock.mock.calls[0];
    expect(options?.cache).toBe("no-store");
  });

  it("曲リストが空の答えでも成功する", async () => {
    stubJsonResponse({ catalogVersion: 0, songs: [] });

    await expect(loadCatalog("loading")).resolves.toEqual([]);
  });

  it("サーバーがエラーを返したら例外になる", async () => {
    stubJsonResponse({ error: "boom" }, { ok: false, status: 500 });

    await expect(loadCatalog("loading")).rejects.toThrow(/answered 500/);
  });

  it("見つからない答え (404) でも例外になる", async () => {
    stubJsonResponse({ error: "not found" }, { ok: false, status: 404 });

    await expect(loadCatalog("loading")).rejects.toThrow(/answered 404/);
  });

  it("ネットワークの失敗はそのまま例外になる", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async () => {
        throw new TypeError("Failed to fetch");
      }),
    );

    await expect(loadCatalog("loading")).rejects.toThrow(/Failed to fetch/);
  });

  it("JSON として壊れた本文は例外になる", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async () => ({
        ok: true,
        status: 200,
        json: async () => {
          throw new SyntaxError("Unexpected token < in JSON at position 0");
        },
      })),
    );

    await expect(loadCatalog("loading")).rejects.toThrow(/Unexpected token/);
  });

  it("契約に無い本文は例外になる", async () => {
    stubJsonResponse({ catalogVersion: 1 });

    await expect(loadCatalog("loading")).rejects.toThrow(/no song list/);
  });

  it("壊れた曲を含む答えでも例外になる", async () => {
    stubJsonResponse({ catalogVersion: 1, songs: [{ ...contractSong, level: "五" }] });

    await expect(loadCatalog("loading")).rejects.toThrow(/broken song/);
  });

  it("シグナルを渡すと fetch に引き回される", async () => {
    const fetchMock = stubJsonResponse(contractAnswer);
    const controller = new AbortController();

    await loadCatalog("loading", { signal: controller.signal });

    const [, options] = fetchMock.mock.calls[0];
    expect(options?.signal).toBe(controller.signal);
  });

  it("中断の例外と失敗の例外を判別できる", () => {
    expect(isAbortError(new DOMException("aborted", "AbortError"))).toBe(true);
    expect(isAbortError(new Error("boom"))).toBe(false);
    expect(isAbortError(undefined)).toBe(false);
  });
});

describe("parseSongs", () => {
  it("契約どおりの曲リストを読み取る", () => {
    const songs = parseSongs(contractAnswer);

    expect(songs).toHaveLength(1);
    expect(songs[0]).toEqual(contractSong);
  });

  it("契約のフィールドだけを返す", () => {
    const songs = parseSongs({
      catalogVersion: 38,
      songs: [{ ...contractSong, extra: "ignored" }],
    });

    expect(Object.keys(songs[0] ?? {}).sort()).toEqual(
      [
        "artist",
        "bpm",
        "chartVersion",
        "durationSec",
        "id",
        "level",
        "noteCount",
        "title",
        "youtubeId",
      ].sort(),
    );
  });

  it("空の曲リストは空の配列になる", () => {
    expect(parseSongs({ catalogVersion: 0, songs: [] })).toEqual([]);
  });

  it("曲リストが配列でないときは例外になる", () => {
    expect(() => parseSongs({ songs: "who" })).toThrow(/no song list/);
    expect(() => parseSongs({ songs: { id: "who" } })).toThrow(/no song list/);
    expect(() => parseSongs(null)).toThrow(/no song list/);
    expect(() => parseSongs([])).toThrow(/no song list/);
  });

  it("フィールドが足りない曲があるときは例外になる", () => {
    const missing = { ...contractSong } as Partial<typeof contractSong>;
    delete missing.noteCount;

    expect(() => parseSongs({ catalogVersion: 1, songs: [missing] })).toThrow(/broken song/);
  });

  it("型が違うフィールドがあるときは例外になる", () => {
    const wrongTypes: [keyof SongSummary, unknown][] = [
      ["id", 1],
      ["title", null],
      ["artist", ["Azari"]],
      ["youtubeId", 8],
      ["level", "5"],
      ["bpm", "128"],
      ["durationSec", "68"],
      ["chartVersion", "1"],
      ["noteCount", null],
    ];

    for (const [key, value] of wrongTypes) {
      const broken = { ...contractSong, [key]: value };
      expect(() => parseSongs({ catalogVersion: 1, songs: [broken] })).toThrow(/broken song/);
    }
  });

  it("曲そのものがオブジェクトでないときは例外になる", () => {
    expect(() => parseSongs({ catalogVersion: 1, songs: ["who"] })).toThrow(/broken song/);
    expect(() => parseSongs({ catalogVersion: 1, songs: [null] })).toThrow(/broken song/);
  });
});
