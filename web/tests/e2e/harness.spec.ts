import { describe, expect, it } from "vitest";

import {
  buildPublishRequest,
  catalogStage,
  chartStage,
  chartSchemaIssues,
  leaderboardStage,
  makeFetchJson,
  pickSong,
  sameJson,
  schemaStage,
  writeStage,
  type Catalog,
  type ChartSnapshot,
} from "./lib/harness.ts";
import type { PublishFetch } from "../../src/lib/publish/types.ts";

const BASE = "https://example.test";

const SONG = {
  id: "e2e-song",
  title: "E2E Song",
  artist: "E2E Artist",
  youtubeId: "abcdefghijk",
  level: 5,
  bpm: 120,
  durationSec: 30,
  chartVersion: 3,
  noteCount: 2,
};

const CATALOG: Catalog = { catalogVersion: 5, songs: [SONG] };

const CHART = {
  name: "E2E Song",
  maxBlock: 8,
  BPM: 120,
  offset: 0,
  notes: [
    { type: 1, num: 0, block: 0, LPB: 1 },
    { type: 1, num: 4, block: 7, LPB: 1 },
  ],
};

const SNAPSHOT: ChartSnapshot = { songId: SONG.id, chartVersion: SONG.chartVersion, chart: CHART };

/** Route a mocked request by method + path, answering JSON. */
function mockFetch(
  routes: Array<{ method?: string; path: string; status: number; body: unknown }>,
): PublishFetch {
  return async (input, init) => {
    const url = new URL(String(input));
    const method = (init?.method ?? "GET").toUpperCase();
    const path = `${url.pathname}${url.search}`;
    const match = routes.find((route) => path === route.path && (route.method ?? "GET") === method);
    if (match === undefined) {
      return jsonResponse(404, { error: "no route" });
    }
    return jsonResponse(match.status, match.body);
  };
}

function jsonResponse(status: number, body: unknown): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}

describe("catalogStage", () => {
  it("passes a well-formed catalog and records version, songs and ids", async () => {
    const fetchJson = makeFetchJson(
      mockFetch([{ path: "/api/v1/catalog", status: 200, body: CATALOG }]),
    );
    const { entries, catalog } = await catalogStage(fetchJson, BASE);
    expect(entries.every((item) => item.pass)).toBe(true);
    expect(catalog).toEqual(CATALOG);
    expect(entries.map((item) => item.stage)).toContain("catalog.songIds.sortedUnique");
    expect(entries.find((item) => item.stage === "catalog.catalogVersion")).toMatchObject({
      actual: 5,
      pass: true,
    });
  });

  it("fails the status entry on a 500 and yields no catalog", async () => {
    const fetchJson = makeFetchJson(
      mockFetch([{ path: "/api/v1/catalog", status: 500, body: { error: "boom" } }]),
    );
    const { entries, catalog } = await catalogStage(fetchJson, BASE);
    expect(entries.find((item) => item.stage === "catalog.status")).toMatchObject({
      expected: 200,
      actual: 500,
      pass: false,
    });
    expect(catalog).toBeNull();
  });

  it("fails the shape entry on a malformed body", async () => {
    const fetchJson = makeFetchJson(
      mockFetch([{ path: "/api/v1/catalog", status: 200, body: { songs: "nope" } }]),
    );
    const { entries, catalog } = await catalogStage(fetchJson, BASE);
    expect(entries.find((item) => item.stage === "catalog.shape")?.pass).toBe(false);
    expect(catalog).toBeNull();
  });
});

describe("pickSong", () => {
  it("defaults to the first catalog entry", () => {
    const { song, entry } = pickSong(CATALOG, null);
    expect(song?.id).toBe("e2e-song");
    expect(entry.pass).toBe(true);
  });

  it("fails when the requested song id is absent", () => {
    const { song, entry } = pickSong(CATALOG, "missing-song");
    expect(song).toBeNull();
    expect(entry).toMatchObject({ expected: "missing-song", actual: null, pass: false });
  });
});

describe("chartStage", () => {
  it("passes identity, noteCount and youtubeId against the catalog entry", async () => {
    const fetchJson = makeFetchJson(
      mockFetch([{ path: "/api/v1/charts/e2e-song?version=3", status: 200, body: SNAPSHOT }]),
    );
    const { entries, snapshot } = await chartStage(fetchJson, BASE, SONG);
    expect(entries.every((item) => item.pass)).toBe(true);
    expect(snapshot).toEqual(SNAPSHOT);
  });

  it("fails when the snapshot identity differs", async () => {
    const fetchJson = makeFetchJson(
      mockFetch([
        {
          path: "/api/v1/charts/e2e-song?version=3",
          status: 200,
          body: { ...SNAPSHOT, songId: "other" },
        },
      ]),
    );
    const { entries } = await chartStage(fetchJson, BASE, SONG);
    expect(entries.find((item) => item.stage === "chart.songId")).toMatchObject({
      expected: "e2e-song",
      actual: "other",
      pass: false,
    });
  });

  it("fails when noteCount does not match chart.notes.length", async () => {
    const fetchJson = makeFetchJson(
      mockFetch([
        {
          path: "/api/v1/charts/e2e-song?version=3",
          status: 200,
          body: { ...SNAPSHOT, chart: { ...CHART, notes: CHART.notes.slice(0, 1) } },
        },
      ]),
    );
    const { entries } = await chartStage(fetchJson, BASE, SONG);
    expect(entries.find((item) => item.stage === "chart.noteCount")).toMatchObject({
      expected: 2,
      actual: 1,
      pass: false,
    });
  });

  it("fails a malformed catalog youtubeId", async () => {
    const fetchJson = makeFetchJson(
      mockFetch([{ path: "/api/v1/charts/e2e-song?version=3", status: 200, body: SNAPSHOT }]),
    );
    const { entries } = await chartStage(fetchJson, BASE, { ...SONG, youtubeId: "short" });
    expect(entries.find((item) => item.stage === "catalog.youtubeId")?.pass).toBe(false);
  });
});

describe("leaderboardStage", () => {
  const LEADERBOARD = {
    songId: SONG.id,
    chartVersion: SONG.chartVersion,
    entries: [
      { nickname: "First", score: 1000000 },
      { nickname: "Second", score: 960000 },
    ],
  };

  it("passes shape, size cap and ordering of a populated leaderboard", async () => {
    const fetchJson = makeFetchJson(
      mockFetch([
        { path: "/api/v1/leaderboards/e2e-song?version=3", status: 200, body: LEADERBOARD },
      ]),
    );
    const entries = await leaderboardStage(fetchJson, BASE, SONG);
    expect(entries.every((item) => item.pass)).toBe(true);
    expect(entries.map((item) => item.stage)).toEqual([
      "leaderboard.status",
      "leaderboard.shape",
      "leaderboard.order",
    ]);
  });

  it("passes an empty leaderboard", async () => {
    const fetchJson = makeFetchJson(
      mockFetch([
        {
          path: "/api/v1/leaderboards/e2e-song?version=3",
          status: 200,
          body: { songId: SONG.id, chartVersion: SONG.chartVersion, entries: [] },
        },
      ]),
    );
    const entries = await leaderboardStage(fetchJson, BASE, SONG);
    expect(entries.every((item) => item.pass)).toBe(true);
  });

  it("fails when the identity differs from the catalog entry", async () => {
    const fetchJson = makeFetchJson(
      mockFetch([
        {
          path: "/api/v1/leaderboards/e2e-song?version=3",
          status: 200,
          body: { ...LEADERBOARD, chartVersion: 2 },
        },
      ]),
    );
    const entries = await leaderboardStage(fetchJson, BASE, SONG);
    expect(entries.find((item) => item.stage === "leaderboard.shape")?.pass).toBe(false);
  });

  it("fails when more than 10 entries are returned", async () => {
    const entries = Array.from({ length: 11 }, (_, index) => ({
      nickname: `P${index}`,
      score: 1000 - index,
    }));
    const fetchJson = makeFetchJson(
      mockFetch([
        {
          path: "/api/v1/leaderboards/e2e-song?version=3",
          status: 200,
          body: { songId: SONG.id, chartVersion: SONG.chartVersion, entries },
        },
      ]),
    );
    const results = await leaderboardStage(fetchJson, BASE, SONG);
    expect(results.find((item) => item.stage === "leaderboard.shape")?.pass).toBe(false);
  });

  it("fails when the scores are not descending", async () => {
    const fetchJson = makeFetchJson(
      mockFetch([
        {
          path: "/api/v1/leaderboards/e2e-song?version=3",
          status: 200,
          body: {
            songId: SONG.id,
            chartVersion: SONG.chartVersion,
            entries: [
              { nickname: "Low", score: 10 },
              { nickname: "High", score: 999999 },
            ],
          },
        },
      ]),
    );
    const results = await leaderboardStage(fetchJson, BASE, SONG);
    expect(results.find((item) => item.stage === "leaderboard.order")?.pass).toBe(false);
  });

  it("fails on a non-200 answer", async () => {
    const fetchJson = makeFetchJson(
      mockFetch([{ path: "/api/v1/leaderboards/e2e-song?version=3", status: 404, body: null }]),
    );
    const results = await leaderboardStage(fetchJson, BASE, SONG);
    expect(results.find((item) => item.stage === "leaderboard.status")?.pass).toBe(false);
  });
});

describe("schemaStage", () => {
  it("passes a contract-valid chart", () => {
    const [item] = schemaStage(SNAPSHOT);
    expect(item.pass).toBe(true);
  });

  it("fails a chart that breaks the frozen schema", () => {
    const broken = { ...CHART, BPM: 0, notes: undefined };
    const [item] = schemaStage({ ...SNAPSHOT, chart: broken });
    expect(item.pass).toBe(false);
    expect(Array.isArray(item.actual)).toBe(true);
  });

  it("reports the Ajv paths of every offending value", () => {
    const issues = chartSchemaIssues({ ...CHART, BPM: 0, maxBlock: 9 });
    expect(issues).toHaveLength(2);
    expect(issues.map((issue) => issue.path).toSorted()).toEqual(["/BPM", "/maxBlock"]);
  });
});

describe("sameJson", () => {
  it("ignores key order but not array order", () => {
    expect(sameJson({ a: 1, b: [1, 2] }, { b: [1, 2], a: 1 })).toBe(true);
    expect(sameJson({ a: [1, 2] }, { a: [2, 1] })).toBe(false);
    expect(sameJson({ a: 1 }, { a: 2 })).toBe(false);
  });
});

describe("writeStage", () => {
  const request = buildPublishRequest(SONG, SNAPSHOT, (chart) => {
    chart["name"] = `${String(chart["name"])} [e2e #97]`;
  });

  it("publishes a modified copy, re-reads both endpoints and asserts +1 versions", async () => {
    const modified = { ...CHART, name: "E2E Song [e2e #97]" };
    const fetchImpl = mockFetch([
      {
        method: "PUT",
        path: "/api/v1/admin/songs/e2e-song",
        status: 200,
        body: { songId: "e2e-song", chartVersion: 4, catalogVersion: 6 },
      },
      {
        path: "/api/v1/catalog",
        status: 200,
        body: { catalogVersion: 6, songs: [{ ...SONG, chartVersion: 4, noteCount: 2 }] },
      },
      {
        path: "/api/v1/charts/e2e-song?version=4",
        status: 200,
        body: { songId: "e2e-song", chartVersion: 4, chart: modified },
      },
    ]);
    const entries = await writeStage({
      baseUrl: BASE,
      songId: "e2e-song",
      token: "secret",
      request,
      fetchImpl,
      catalogVersionBefore: 5,
      chartVersionBefore: 3,
    });
    expect(entries.map((item) => item.stage)).toEqual([
      "write.publishResponse",
      "write.catalogVersion",
      "write.catalogChartVersion",
      "write.catalogNoteCount",
      "write.snapshot.songId",
      "write.snapshot.chartVersion",
      "write.snapshot.exact",
    ]);
    expect(entries.every((item) => item.pass)).toBe(true);
  });

  it("stops after the publish entry when the publish fails", async () => {
    const fetchImpl = mockFetch([
      {
        method: "PUT",
        path: "/api/v1/admin/songs/e2e-song",
        status: 401,
        body: { error: "unauthorized" },
      },
    ]);
    const entries = await writeStage({
      baseUrl: BASE,
      songId: "e2e-song",
      token: "wrong",
      request,
      fetchImpl,
      catalogVersionBefore: 5,
      chartVersionBefore: 3,
    });
    expect(entries).toHaveLength(1);
    expect(entries[0]).toMatchObject({ stage: "write.publishResponse", pass: false });
  });

  it("fails the publish entry when the readback catalog did not bump", async () => {
    const fetchImpl = mockFetch([
      {
        method: "PUT",
        path: "/api/v1/admin/songs/e2e-song",
        status: 200,
        body: { songId: "e2e-song", chartVersion: 4, catalogVersion: 6 },
      },
      {
        path: "/api/v1/catalog",
        status: 200,
        body: { catalogVersion: 5, songs: [{ ...SONG, chartVersion: 4 }] },
      },
      {
        path: "/api/v1/charts/e2e-song?version=4",
        status: 200,
        body: { songId: "e2e-song", chartVersion: 4, chart: CHART },
      },
    ]);
    const entries = await writeStage({
      baseUrl: BASE,
      songId: "e2e-song",
      token: "secret",
      request,
      fetchImpl,
      catalogVersionBefore: 5,
      chartVersionBefore: 3,
    });
    // `publishAndVerify` verifies the readback itself and reports a failure,
    // so the write stage stops after the publish entry.
    expect(entries).toHaveLength(1);
    expect(entries[0]).toMatchObject({ stage: "write.publishResponse", pass: false });
  });
});
