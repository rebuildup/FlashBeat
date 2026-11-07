/**
 * Integration test of the festival read API (issue #15).
 *
 * The test runs the real Worker (Void dev server) against the real local D1
 * database, so it covers routing, the SQL of the handlers, the response shapes
 * and the HTTP semantics together. The responses are validated against the
 * frozen contract (`contracts/openapi.v1.yaml`).
 *
 * The local database is seeded with the real "Who" chart and two extra rows
 * that make the version handling observable:
 *
 *   - `zz-second-song` v1 — a second published song (catalog order)
 *   - `who` v2 — a second chart version of the same song (exact version)
 *
 * The database is reset to the seed state afterwards.
 */
import assert from "node:assert/strict";
import { execFileSync, spawn, type ChildProcess } from "node:child_process";
import { once } from "node:events";
import { mkdtempSync, openSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { fileURLToPath } from "node:url";
import { after, before, describe, it } from "node:test";

import { chartFixture, chartFixtureNoteCount, responseValidator } from "./helpers/contract.ts";
// The shared semantic validator of the studio (issue #191). The API and the
// studio use the same function, so the round-trip of a published chart can be
// checked with the very code the studio runs.
import { validateChartSemantics } from "../src/lib/chart/validate.ts";
import type { Chart } from "../src/lib/chart/types.ts";

const webRoot = fileURLToPath(new URL("..", import.meta.url));
const voidCli = fileURLToPath(new URL("../node_modules/void/dist/cli/cli.mjs", import.meta.url));
const vpCli = fileURLToPath(new URL("../node_modules/vite-plus/bin/vp", import.meta.url));

const port = 20000 + (process.pid % 20000);
const baseUrl = `http://127.0.0.1:${port}`;

const validateCatalog = responseValidator("catalog");
const validateChart = responseValidator("charts");
const validatePublish = responseValidator("publish", "put");
const validateScoreResponse = responseValidator("scores", "post", "201");
const validateLeaderboard = responseValidator("leaderboards");

/**
 * The staff token the dev server runs with. It is injected through the
 * environment of the spawned server (the same path a real deployment uses:
 * Worker secret), never into any bundle.
 */
const TEST_ADMIN_TOKEN = "test-staff-token-0123456789abcdef";

/** A minimal chart that satisfies `contracts/schemas/chart.v1.schema.json`. */
const SECOND_CHART = JSON.stringify({
  name: "Second Song",
  maxBlock: 8,
  BPM: 120,
  offset: 0,
  notes: [{ type: 1, num: 1, block: 0, LPB: 4 }],
});

/** A second, deliberately different chart version of the seeded song. */
const SECOND_WHO_CHART = JSON.stringify({
  name: "Who",
  maxBlock: 8,
  BPM: 128,
  offset: 5,
  notes: [{ type: 1, num: 1, block: 0, LPB: 4 }],
});

const FIXTURE_SQL = `
INSERT INTO songs (id, title, artist, youtubeId, bpm, level, durationSec, currentVersion)
VALUES ('zz-second-song', 'Second Song', 'Tester', '8JXiXt0D6tw', 120, 7, 90, 1);
INSERT INTO chart_versions (songId, version, json, noteCount)
VALUES ('zz-second-song', 1, '${SECOND_CHART}', 1);
INSERT INTO chart_versions (songId, version, json, noteCount)
VALUES ('who', 2, '${SECOND_WHO_CHART}', 1);
`;

let server: ChildProcess | undefined;
let serverLogPath = "";
let tempDir: string;

function runVoid(args: string[]): void {
  execFileSync(process.execPath, [voidCli, ...args], { cwd: webRoot, stdio: "pipe" });
}

/**
 * Wait for the dev server to answer. The poll interval is a real timer
 * because the condition (a listening socket) is owned by another process and
 * has no event to await.
 */
async function waitForServer(timeoutMs: number): Promise<void> {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    try {
      const response = await fetch(`${baseUrl}/api/v1/catalog`);
      if (response.status === 200) {
        return;
      }
    } catch {
      // The server is not listening yet.
    }
    const { promise, resolve } = Promise.withResolvers<void>();
    setTimeout(resolve, 250);
    await promise;
  }
  throw new Error(`dev server did not become ready on ${baseUrl}:\n${readServerLog()}`);
}

/** Output of the dev server, used when the server does not come up. */
function readServerLog(): string {
  if (!serverLogPath) {
    return "";
  }
  try {
    return readFileSync(serverLogPath, "utf-8");
  } catch {
    return "";
  }
}

/**
 * Stop the dev server and wait until the process is gone.
 *
 * The dev server runs in its own process group (`detached`), because it spawns
 * vite and workerd as children of its own. Killing the group stops all of
 * them; a single `child.kill()` would leave the grandchildren running and
 * hold the test runner open.
 */
async function stopServer(child: ChildProcess): Promise<void> {
  if (child.exitCode !== null) {
    return;
  }
  const exited = once(child, "exit");
  killGroup(child, "SIGTERM");
  const timedOut = Symbol("timeout");
  const result = await Promise.race([exited, delay(5_000, timedOut)]);
  if (result === timedOut) {
    killGroup(child, "SIGKILL");
    await once(child, "exit");
  }
}

/** Send a signal to the whole process group of the dev server. */
function killGroup(child: ChildProcess, signal: NodeJS.Signals): void {
  if (child.pid === undefined) {
    child.kill(signal);
    return;
  }
  try {
    process.kill(-child.pid, signal);
  } catch {
    child.kill(signal);
  }
}

/** Resolve after `ms`, or with `fallback` when the time is up. */
function delay(ms: number, fallback: symbol): Promise<symbol> {
  const { promise, resolve } = Promise.withResolvers<symbol>();
  setTimeout(() => resolve(fallback), ms);
  return promise;
}

before(
  async () => {
    // Baseline: the seeded song only. `void db seed` resets the local D1 and
    // re-applies the migrations.
    runVoid(["db", "seed"]);

    tempDir = mkdtempSync(join(tmpdir(), "flashbeat-read-api-"));
    const sqlFile = join(tempDir, "fixtures.sql");
    writeFileSync(sqlFile, FIXTURE_SQL, "utf-8");
    runVoid(["db", "execute", "--file", sqlFile]);

    serverLogPath = join(tempDir, "dev-server.log");
    const logFd = openSync(serverLogPath, "w");

    // `--host 127.0.0.1` pins the listening socket: `vp dev` otherwise binds
    // whatever `localhost` resolves to (observed: IPv6-only `::1`), while the
    // test URLs and the readiness poll below use 127.0.0.1.
    server = spawn(
      process.execPath,
      [vpCli, "dev", "--port", String(port), "--strictPort", "--host", "127.0.0.1"],
      {
        cwd: webRoot,
        detached: true,
        env: { ...process.env, FLASHBEAT_ADMIN_TOKEN: TEST_ADMIN_TOKEN },
        // The server output goes to a file instead of a pipe: the vite and
        // workerd processes inherit the pipe and would keep the test runner
        // waiting for the end of the stream.
        stdio: ["ignore", logFd, logFd],
      },
    );

    await waitForServer(180_000);
  },
  { timeout: 240_000 },
);

after(async () => {
  if (server) {
    await stopServer(server);
  }
  runVoid(["db", "seed"]);
  rmSync(tempDir, { recursive: true, force: true });
});

describe("GET /api/v1/catalog", () => {
  it("lists the published songs in a stable order", async () => {
    const response = await fetch(`${baseUrl}/api/v1/catalog`);
    assert.equal(response.status, 200);
    assert.match(response.headers.get("cache-control") ?? "", /no-store/);

    const body = (await response.json()) as {
      catalogVersion: number;
      songs: { id: string; chartVersion: number; noteCount: number }[];
    };
    validateCatalog(body);

    assert.deepEqual(
      body.songs.map((song) => song.id),
      ["who", "zz-second-song"],
    );
  });

  it("exposes the published chart version and the note count of every song", async () => {
    const response = await fetch(`${baseUrl}/api/v1/catalog`);
    const body = (await response.json()) as {
      songs: { id: string; chartVersion: number; noteCount: number }[];
    };

    const who = body.songs.find((song) => song.id === "who");
    const second = body.songs.find((song) => song.id === "zz-second-song");
    assert.equal(who?.chartVersion, 1);
    assert.equal(who?.noteCount, chartFixtureNoteCount());
    assert.equal(second?.chartVersion, 1);
    assert.equal(second?.noteCount, 1);
  });

  it("counts every published chart snapshot in the catalog version", async () => {
    const response = await fetch(`${baseUrl}/api/v1/catalog`);
    const body = (await response.json()) as { catalogVersion: number };

    // who v1, who v2 and zz-second-song v1 are published.
    assert.equal(body.catalogVersion, 3);
  });
});

describe("GET /api/v1/charts/{songId}", () => {
  it("returns the requested chart version", async () => {
    const response = await fetch(`${baseUrl}/api/v1/charts/who?version=1`);
    assert.equal(response.status, 200);
    assert.match(response.headers.get("cache-control") ?? "", /no-store/);

    const body = (await response.json()) as {
      songId: string;
      chartVersion: number;
      chart: unknown;
    };
    validateChart(body);
    assert.equal(body.songId, "who");
    assert.equal(body.chartVersion, 1);
    assert.deepEqual(body.chart, chartFixture());
  });

  it("returns the exact version instead of the newest one", async () => {
    const response = await fetch(`${baseUrl}/api/v1/charts/who?version=2`);
    assert.equal(response.status, 200);

    const body = (await response.json()) as { chartVersion: number; chart: { notes: unknown[] } };
    validateChart(body);
    assert.equal(body.chartVersion, 2);
    assert.equal(body.chart.notes.length, 1);
  });

  it("answers 404 for a version that does not exist", async () => {
    const response = await fetch(`${baseUrl}/api/v1/charts/who?version=3`);
    assert.equal(response.status, 404);
  });

  it("answers 404 for an unknown song", async () => {
    const response = await fetch(`${baseUrl}/api/v1/charts/no-such-song?version=1`);
    assert.equal(response.status, 404);
  });

  it("answers 404 for a song id that cannot exist", async () => {
    const response = await fetch(`${baseUrl}/api/v1/charts/WHO?version=1`);
    assert.equal(response.status, 404);
  });

  it("answers 400 when the version is missing or not a positive integer", async () => {
    for (const query of ["", "?version=0", "?version=-1", "?version=1.5", "?version=abc"]) {
      const response = await fetch(`${baseUrl}/api/v1/charts/who${query}`);
      assert.equal(response.status, 400, `expected 400 for "${query}"`);
    }
  });
});

describe("PUT /api/v1/admin/songs/{songId}", () => {
  /** A valid publish body for `songId`, built from the given notes. */
  function publishBody(songId: string, notes: unknown[]): string {
    return JSON.stringify({
      title: `Publish Test ${songId}`,
      artist: "Tester",
      youtubeId: "8JXiXt0D6tw",
      level: 5,
      durationSec: 90,
      chart: {
        name: `Publish Test ${songId}`,
        maxBlock: 8,
        BPM: 120,
        offset: 0,
        notes,
      },
    });
  }

  function publish(songId: string, body: string, token?: string): Promise<Response> {
    return fetch(`${baseUrl}/api/v1/admin/songs/${songId}`, {
      method: "PUT",
      headers: {
        "content-type": "application/json",
        ...(token === undefined ? {} : { authorization: `Bearer ${token}` }),
      },
      body,
    });
  }

  async function catalogVersion(): Promise<number> {
    const response = await fetch(`${baseUrl}/api/v1/catalog`);
    const body = (await response.json()) as { catalogVersion: number };
    return body.catalogVersion;
  }

  it("publishes a new song and returns the new versions", async () => {
    const before = await catalogVersion();
    const response = await publish(
      "publish-new-song",
      publishBody("publish-new-song", [{ type: 1, num: 4, block: 3, LPB: 4 }]),
      TEST_ADMIN_TOKEN,
    );
    assert.equal(response.status, 200);

    const body = (await response.json()) as {
      songId: string;
      chartVersion: number;
      catalogVersion: number;
    };
    validatePublish(body);
    assert.equal(body.songId, "publish-new-song");
    assert.equal(body.chartVersion, 1);
    assert.equal(body.catalogVersion, before + 1);

    // The published song is listed by the read API and its snapshot loads.
    const catalog = await (await fetch(`${baseUrl}/api/v1/catalog`)).json();
    const listed = (catalog as { songs: { id: string; chartVersion: number }[] }).songs.find(
      (song) => song.id === "publish-new-song",
    );
    assert.equal(listed?.chartVersion, 1);

    const chart = await fetch(`${baseUrl}/api/v1/charts/publish-new-song?version=1`);
    assert.equal(chart.status, 200);
  });

  it("increments the chart version when the same song is published again", async () => {
    const before = await catalogVersion();
    const response = await publish(
      "publish-new-song",
      publishBody("publish-new-song", [
        { type: 1, num: 4, block: 3, LPB: 4 },
        { type: 1, num: 8, block: 4, LPB: 4 },
      ]),
      TEST_ADMIN_TOKEN,
    );
    assert.equal(response.status, 200);

    const body = (await response.json()) as {
      songId: string;
      chartVersion: number;
      catalogVersion: number;
    };
    validatePublish(body);
    assert.equal(body.songId, "publish-new-song");
    assert.equal(body.chartVersion, 2);
    assert.equal(body.catalogVersion, before + 1);

    // Snapshots are immutable: the previous version still loads exactly.
    const first = await fetch(`${baseUrl}/api/v1/charts/publish-new-song?version=1`);
    assert.equal(first.status, 200);
    const firstBody = (await first.json()) as { chart: { notes: unknown[] } };
    assert.equal(firstBody.chart.notes.length, 1);
  });

  it("answers 401 for a wrong token and publishes nothing", async () => {
    const before = await catalogVersion();
    for (const token of [undefined, "Bearer wrong-token", TEST_ADMIN_TOKEN.slice(0, -1)]) {
      const response = await publish(
        "publish-unauthorized-song",
        publishBody("publish-unauthorized-song", [{ type: 1, num: 1, block: 0, LPB: 4 }]),
        token,
      );
      assert.equal(response.status, 401, `expected 401 for token ${String(token)}`);
    }
    assert.equal(await catalogVersion(), before);
  });

  it("answers 422 with actionable diagnostics for invalid data and publishes nothing", async () => {
    const before = await catalogVersion();
    const whoChart = await (await fetch(`${baseUrl}/api/v1/charts/who?version=1`)).json();

    const invalidCharts: [string, Record<string, unknown>][] = [
      // Schema-level violations.
      [
        "a note outside the 8 lanes",
        {
          name: "X",
          maxBlock: 8,
          BPM: 120,
          offset: 0,
          notes: [{ type: 1, num: 1, block: 8, LPB: 4 }],
        },
      ],
      [
        // The contract requires `endNum` for a Hold (type 2).
        "a Hold without an end line",
        {
          name: "X",
          maxBlock: 8,
          BPM: 120,
          offset: 0,
          notes: [{ type: 2, num: 1, block: 0, LPB: 4 }],
        },
      ],
      [
        "a non-positive beat position",
        {
          name: "X",
          maxBlock: 8,
          BPM: 120,
          offset: 0,
          notes: [{ type: 1, num: -1, block: 0, LPB: 4 }],
        },
      ],
      [
        "a BPM outside the supported range",
        {
          name: "X",
          maxBlock: 8,
          BPM: 601,
          offset: 0,
          notes: [{ type: 1, num: 1, block: 0, LPB: 4 }],
        },
      ],
      ["an empty note list", { name: "X", maxBlock: 8, BPM: 120, offset: 0, notes: [] }],
      // Request-level violations.
      [
        "a level outside the supported range",
        {
          name: "X",
          maxBlock: 8,
          BPM: 120,
          offset: 0,
          notes: [{ type: 1, num: 1, block: 0, LPB: 4 }],
        },
      ],
    ];
    for (const [label, chart] of invalidCharts) {
      const level = label.includes("level") ? 100 : 5;
      const body = JSON.stringify({
        title: "Publish Invalid",
        artist: "Tester",
        youtubeId: "8JXiXt0D6tw",
        level,
        durationSec: 90,
        chart,
      });
      const response = await publish("publish-invalid-song", body, TEST_ADMIN_TOKEN);
      assert.equal(response.status, 422, `expected 422 for ${label}`);
      const rejected = (await response.json()) as {
        error: string;
        diagnostics: { path: string; message: string }[];
      };
      assert.ok(rejected.diagnostics.length >= 1, `expected diagnostics for ${label}`);
      assert.ok(
        rejected.diagnostics.every(
          (diagnostic) =>
            typeof diagnostic.path === "string" && typeof diagnostic.message === "string",
        ),
        `expected actionable diagnostics for ${label}`,
      );
    }

    // Nothing was written: the catalog version is unchanged and the already
    // published chart is byte-identical.
    assert.equal(await catalogVersion(), before);
    assert.deepEqual(
      await (await fetch(`${baseUrl}/api/v1/charts/who?version=1`)).json(),
      whoChart,
    );
  });

  it("rejects a note that lands beyond the chart length", async () => {
    // BPM 120, LPB 4: num 7200 lands at 900s, beyond the 90s durationSec.
    const response = await publish(
      "publish-too-long-song",
      publishBody("publish-too-long-song", [{ type: 1, num: 7200, block: 0, LPB: 4 }]),
      TEST_ADMIN_TOKEN,
    );
    assert.equal(response.status, 422);
    const body = (await response.json()) as { diagnostics: { message: string }[] };
    assert.match(body.diagnostics[0]?.message ?? "", /durationSec/);
  });

  it("publishes a chart with a Hold and serves it back unchanged (issue #164)", async () => {
    const before = await catalogVersion();
    const holdNotes = [
      { type: 1, num: 4, block: 0, LPB: 4 },
      { type: 2, num: 8, block: 3, LPB: 4, endNum: 16 },
      { type: 2, num: 24, block: 5, LPB: 4, endNum: 25 },
    ];
    const response = await publish(
      "publish-hold-song",
      publishBody("publish-hold-song", holdNotes),
      TEST_ADMIN_TOKEN,
    );
    assert.equal(response.status, 200);

    const body = (await response.json()) as {
      songId: string;
      chartVersion: number;
      catalogVersion: number;
    };
    validatePublish(body);
    assert.equal(body.songId, "publish-hold-song");
    assert.equal(body.chartVersion, 1);
    assert.equal(body.catalogVersion, before + 1);

    // The chart is served back exactly as it was published: the Hold keeps
    // its kind, its end line and its lane.
    const chart = await fetch(`${baseUrl}/api/v1/charts/publish-hold-song?version=1`);
    assert.equal(chart.status, 200);
    const served = (await chart.json()) as { chart: { notes: unknown[] } };
    assert.deepEqual(served.chart.notes, holdNotes);

    // The song is listed by the catalog with its note count.
    const catalog = (await (await fetch(`${baseUrl}/api/v1/catalog`)).json()) as {
      songs: { id: string; noteCount: number }[];
    };
    const listed = catalog.songs.find((song) => song.id === "publish-hold-song");
    assert.equal(listed?.noteCount, holdNotes.length);
  });

  it("rejects a Hold without an end line and publishes nothing (issue #164)", async () => {
    const before = await catalogVersion();
    const response = await publish(
      "publish-broken-hold-song",
      publishBody("publish-broken-hold-song", [{ type: 2, num: 8, block: 3, LPB: 4 }]),
      TEST_ADMIN_TOKEN,
    );
    assert.equal(response.status, 422);
    const body = (await response.json()) as { diagnostics: { path: string; message: string }[] };
    assert.ok(body.diagnostics.length >= 1, "expected a diagnostic for the missing end line");
    assert.ok(
      body.diagnostics.some((diagnostic) => diagnostic.message.includes("endNum")),
      `expected an endNum diagnostic, got ${JSON.stringify(body.diagnostics)}`,
    );
    assert.equal(await catalogVersion(), before);
  });

  it("rejects a Hold that does not end after it starts and publishes nothing (issue #164)", async () => {
    const before = await catalogVersion();
    const response = await publish(
      "publish-backwards-hold-song",
      publishBody("publish-backwards-hold-song", [
        { type: 2, num: 8, block: 3, LPB: 4, endNum: 8 },
      ]),
      TEST_ADMIN_TOKEN,
    );
    assert.equal(response.status, 422);
    const body = (await response.json()) as { diagnostics: { path: string; message: string }[] };
    assert.ok(
      body.diagnostics.some((diagnostic) => diagnostic.path.endsWith("/endNum")),
      `expected an endNum diagnostic, got ${JSON.stringify(body.diagnostics)}`,
    );
    assert.equal(await catalogVersion(), before);
  });

  it("rejects a Hold that ends beyond the chart length (issue #164)", async () => {
    // BPM 120, LPB 4: endNum 7200 lands at 900s, beyond the 90s durationSec.
    const response = await publish(
      "publish-long-hold-song",
      publishBody("publish-long-hold-song", [{ type: 2, num: 4, block: 0, LPB: 4, endNum: 7200 }]),
      TEST_ADMIN_TOKEN,
    );
    assert.equal(response.status, 422);
    const body = (await response.json()) as { diagnostics: { message: string }[] };
    assert.match(body.diagnostics[0]?.message ?? "", /durationSec/);
  });

  it("rejects two Holds that fight over the same lane (issue #191)", async () => {
    // The chart of the issue report: the API used to accept it, the editor
    // used to refuse it, so a published chart could not be edited any more.
    // Now both sides refuse it.
    const before = await catalogVersion();
    const response = await publish(
      "publish-overlapping-hold-song",
      publishBody("publish-overlapping-hold-song", [
        { type: 2, num: 0, endNum: 8, block: 0, LPB: 4 },
        { type: 2, num: 4, endNum: 12, block: 0, LPB: 4 },
      ]),
      TEST_ADMIN_TOKEN,
    );
    assert.equal(response.status, 422);
    const body = (await response.json()) as { diagnostics: { path: string; message: string }[] };
    assert.ok(
      body.diagnostics.some((diagnostic) => diagnostic.path === "/chart/notes/1/num"),
      `expected an overlap diagnostic, got ${JSON.stringify(body.diagnostics)}`,
    );
    assert.equal(await catalogVersion(), before);
  });

  it("rejects two Holds that fight over the same lane with a mixed LPB (issue #191)", async () => {
    // The second case of the issue report: the raw line numbers do not
    // overlap, but the beats do (both holds cover beat 1 to 2 of lane 0).
    const before = await catalogVersion();
    const response = await publish(
      "publish-mixed-lpb-hold-song",
      publishBody("publish-mixed-lpb-hold-song", [
        { type: 2, num: 4, endNum: 8, block: 0, LPB: 4 },
        { type: 2, num: 8, endNum: 16, block: 0, LPB: 8 },
      ]),
      TEST_ADMIN_TOKEN,
    );
    assert.equal(response.status, 422);
    const body = (await response.json()) as { diagnostics: { path: string; message: string }[] };
    assert.ok(
      body.diagnostics.some((diagnostic) => diagnostic.path === "/chart/notes/1/num"),
      `expected an overlap diagnostic, got ${JSON.stringify(body.diagnostics)}`,
    );
    assert.equal(await catalogVersion(), before);
  });

  it("serves a published chart back in a shape the Web studio can read (issue #191)", async () => {
    // The round-trip invariant of issue #191: a chart the API accepted can be
    // read back by the studio. Before the fix the studio refused charts the
    // API had published ("Published chart failed validation").
    const notes = [
      { type: 1, num: 4, block: 0, LPB: 4 },
      { type: 2, num: 8, block: 3, LPB: 4, endNum: 16 },
      { type: 2, num: 24, block: 5, LPB: 4, endNum: 25 },
      { type: 1, num: 40, block: 7, LPB: 8 },
    ];
    const response = await publish(
      "publish-round-trip-song",
      publishBody("publish-round-trip-song", notes),
      TEST_ADMIN_TOKEN,
    );
    assert.equal(response.status, 200);

    const chart = await fetch(`${baseUrl}/api/v1/charts/publish-round-trip-song?version=1`);
    assert.equal(chart.status, 200);
    const served = (await chart.json()) as { chart: unknown };

    // The very same validator the studio uses accepts the served chart.
    const issues = validateChartSemantics(served.chart as Chart, { durationSec: 90 });
    assert.deepEqual(issues, []);
  });
});

describe("POST /api/v1/scores", () => {
  /** A valid submission body for the given judgment counts. */
  function submission(
    songId: string,
    chartVersion: number,
    nickname: string,
    counts: { perfect?: number; great?: number; bad?: number; miss?: number },
    extra: { maxCombo?: number; score?: number } = {},
  ): string {
    return JSON.stringify({
      songId,
      chartVersion,
      nickname,
      perfect: counts.perfect ?? 0,
      great: counts.great ?? 0,
      bad: counts.bad ?? 0,
      miss: counts.miss ?? 0,
      ...extra,
    });
  }

  async function submit(body: string): Promise<Response> {
    return fetch(`${baseUrl}/api/v1/scores`, {
      method: "POST",
      headers: { "content-type": "application/json" },
      body,
    });
  }

  it("stores a submission and returns the server-computed score and rank", async () => {
    // zz-second-song v1 has 1 note: a perfect hit scores 1000000.
    const first = await submit(
      submission("zz-second-song", 1, "Player One", { perfect: 1 }, { score: 1000000 }),
    );
    assert.equal(first.status, 201);
    const firstBody = (await first.json()) as { score: number; rank: number };
    validateScoreResponse(firstBody);
    assert.equal(firstBody.score, 1000000);
    assert.equal(firstBody.rank, 1);

    // A tie shares the rank of the earlier submission.
    const second = await submit(submission("zz-second-song", 1, "Player Two", { perfect: 1 }));
    assert.equal(second.status, 201);
    assert.equal(((await second.json()) as { rank: number }).rank, 1);

    // A miss-only play scores 0 and ranks behind both.
    const third = await submit(submission("zz-second-song", 1, "Player Three", { miss: 1 }));
    assert.equal(third.status, 201);
    const thirdBody = (await third.json()) as { score: number; rank: number };
    assert.equal(thirdBody.score, 0);
    assert.equal(thirdBody.rank, 3);
  });

  it("accepts the current and older chart versions of a published song", async () => {
    // `who` v2 is an older snapshot of the published song (noteCount 1).
    const older = await submit(submission("who", 2, "OldVersion", { perfect: 1 }));
    assert.equal(older.status, 201);
    validateScoreResponse((await older.json()) as { score: number; rank: number });
  });

  it("answers 422 for an unknown song or chart version", async () => {
    for (const body of [
      submission("no-such-song", 1, "Unknown", { perfect: 1 }),
      submission("WHO", 1, "UpperCase", { perfect: 1 }),
      submission("who", 999, "UnknownVersion", { perfect: 1 }),
    ]) {
      const response = await submit(body);
      assert.equal(response.status, 422, `expected 422 for ${body}`);
      const rejected = (await response.json()) as { diagnostics: { path: string }[] };
      assert.ok(rejected.diagnostics.length >= 1);
    }
  });

  it("answers 422 when the judgment sum differs from the note count", async () => {
    // The seeded Who chart v1 has 139 notes.
    const response = await submit(submission("who", 1, "WrongSum", { perfect: 138 }));
    assert.equal(response.status, 422);
    const rejected = (await response.json()) as { diagnostics: { message: string }[] };
    assert.match(rejected.diagnostics[0]?.message ?? "", /139/);
  });

  it("answers 422 when the client score disagrees with the Unity formula", async () => {
    // 1 perfect note of 1 scores 1000000; the client claims 999999.
    const response = await submit(
      submission("zz-second-song", 1, "Cheater", { perfect: 1 }, { score: 999999 }),
    );
    assert.equal(response.status, 422);
    const rejected = (await response.json()) as {
      error: string;
      diagnostics: { message: string }[];
    };
    assert.equal(rejected.error, "score mismatch");
    assert.match(rejected.diagnostics[0]?.message ?? "", /1000000/);
  });

  it("answers 422 for a combo above the note count", async () => {
    const response = await submit(
      submission("zz-second-song", 1, "ComboCheater", { perfect: 1 }, { maxCombo: 2 }),
    );
    assert.equal(response.status, 422);
    const rejected = (await response.json()) as { diagnostics: { path: string }[] };
    assert.equal(rejected.diagnostics[0]?.path, "/maxCombo");
  });

  it("enforces the nickname rules", async () => {
    for (const [label, nickname] of [
      ["13 characters", "abcdefghijklm"],
      ["empty after trim", "   "],
      ["control character", "a\u0000b"],
    ] as const) {
      const response = await submit(submission("zz-second-song", 1, nickname, { perfect: 1 }));
      assert.equal(response.status, 422, `expected 422 for ${label}`);
      const rejected = (await response.json()) as { diagnostics: { path: string }[] };
      assert.equal(rejected.diagnostics[0]?.path, "/nickname", label);
    }
  });

  it("answers 422 for a body that is not valid JSON or breaks the schema", async () => {
    const notJson = await submit("{not json");
    assert.equal(notJson.status, 422);

    const missingField = await submit('{"songId":"zz-second-song"}');
    assert.equal(missingField.status, 422);

    const negative = await submit(
      submission("zz-second-song", 1, "Negative", { perfect: -1, miss: 2 }),
    );
    assert.equal(negative.status, 422);
  });

  it("rejects a duplicate identical submission within 10 seconds with 429", async () => {
    const body = submission("who", 2, "RateLimited", { perfect: 1 });
    const first = await submit(body);
    assert.equal(first.status, 201);

    const duplicate = await submit(body);
    assert.equal(duplicate.status, 429);
    const retryAfter = duplicate.headers.get("retry-after");
    assert.ok(retryAfter !== null && Number(retryAfter) >= 1, `Retry-After: ${retryAfter}`);

    // A different nickname or score is not a duplicate.
    const otherNickname = await submit(submission("who", 2, "RateLimit", { perfect: 1 }));
    assert.equal(otherNickname.status, 201);
  });
});

describe("GET /api/v1/leaderboards/{songId}", () => {
  /** Publish a fresh 10-note song for the leaderboard tests. */
  async function publishLeaderboardSong(songId: string): Promise<void> {
    const notes = Array.from({ length: 10 }, (_, index) => ({
      type: 1,
      num: index + 1,
      block: index % 8,
      LPB: 4,
    }));
    const response = await fetch(`${baseUrl}/api/v1/admin/songs/${songId}`, {
      method: "PUT",
      headers: {
        "content-type": "application/json",
        authorization: `Bearer ${TEST_ADMIN_TOKEN}`,
      },
      body: JSON.stringify({
        title: `Leaderboard ${songId}`,
        artist: "Tester",
        youtubeId: "8JXiXt0D6tw",
        level: 5,
        durationSec: 90,
        chart: { name: songId, maxBlock: 8, BPM: 120, offset: 0, notes },
      }),
    });
    assert.equal(response.status, 200);
  }

  async function submitScore(
    songId: string,
    nickname: string,
    counts: { perfect: number; great: number; bad: number; miss: number },
  ): Promise<void> {
    const response = await fetch(`${baseUrl}/api/v1/scores`, {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({ songId, chartVersion: 1, nickname, ...counts }),
    });
    assert.equal(response.status, 201);
  }

  it("returns an empty entries list for a song without scores", async () => {
    const response = await fetch(`${baseUrl}/api/v1/leaderboards/who?version=1`);
    assert.equal(response.status, 200);
    assert.match(response.headers.get("cache-control") ?? "", /no-store/);

    const body = (await response.json()) as {
      songId: string;
      chartVersion: number;
      entries: unknown[];
    };
    validateLeaderboard(body);
    assert.equal(body.songId, "who");
    assert.equal(body.chartVersion, 1);
    assert.deepEqual(body.entries, []);
  });

  it("returns the top 10 in score order with ties earliest first", async () => {
    await publishLeaderboardSong("lb-order-song");
    // 10 notes: maxScore 50. Perfect play = 1000000, 9 perfect + 1 great
    // = 48/50 = 960000, 9 perfect + 1 bad = 46/50 = 920000, all miss = 0.
    await submitScore("lb-order-song", "Late Perfect", { perfect: 10, great: 0, bad: 0, miss: 0 });
    await submitScore("lb-order-song", "Early Perf", { perfect: 10, great: 0, bad: 0, miss: 0 });
    await submitScore("lb-order-song", "Great Player", { perfect: 9, great: 1, bad: 0, miss: 0 });
    await submitScore("lb-order-song", "Bad Player", { perfect: 9, great: 0, bad: 1, miss: 0 });
    for (let index = 0; index < 8; index++) {
      await submitScore("lb-order-song", `Zero ${index}`, {
        perfect: 0,
        great: 0,
        bad: 0,
        miss: 10,
      });
    }

    const response = await fetch(`${baseUrl}/api/v1/leaderboards/lb-order-song?version=1`);
    assert.equal(response.status, 200);
    const body = (await response.json()) as {
      songId: string;
      chartVersion: number;
      entries: { nickname: string; score: number }[];
    };
    validateLeaderboard(body);
    assert.equal(body.songId, "lb-order-song");
    assert.equal(body.chartVersion, 1);

    // 12 submissions, only the top 10 are returned...
    assert.equal(body.entries.length, 10);
    assert.equal(body.entries[0]?.score, 1000000);
    assert.equal(body.entries[1]?.score, 1000000);
    assert.equal(body.entries[2]?.score, 960000);
    assert.equal(body.entries[3]?.score, 920000);

    // ...the tie is broken by earliest submission, and the two lowest zeros
    // are cut.
    assert.deepEqual(
      body.entries.map((entry) => entry.nickname),
      [
        "Late Perfect",
        "Early Perf",
        "Great Player",
        "Bad Player",
        "Zero 0",
        "Zero 1",
        "Zero 2",
        "Zero 3",
        "Zero 4",
        "Zero 5",
      ],
    );
  });

  it("answers 404 for an unknown song or version and 400 for a bad version", async () => {
    const unknownSong = await fetch(`${baseUrl}/api/v1/leaderboards/no-such-song?version=1`);
    assert.equal(unknownSong.status, 404);

    const unknownVersion = await fetch(`${baseUrl}/api/v1/leaderboards/who?version=999`);
    assert.equal(unknownVersion.status, 404);

    const upperCase = await fetch(`${baseUrl}/api/v1/leaderboards/WHO?version=1`);
    assert.equal(upperCase.status, 404);

    for (const query of ["", "?version=0", "?version=-1", "?version=abc"]) {
      const response = await fetch(`${baseUrl}/api/v1/leaderboards/who${query}`);
      assert.equal(response.status, 400, `expected 400 for "${query}"`);
    }
  });
});
