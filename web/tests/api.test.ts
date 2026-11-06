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

const webRoot = fileURLToPath(new URL("..", import.meta.url));
const voidCli = fileURLToPath(new URL("../node_modules/void/dist/cli/cli.mjs", import.meta.url));
const vpCli = fileURLToPath(new URL("../node_modules/vite-plus/bin/vp", import.meta.url));

const port = 20000 + (process.pid % 20000);
const baseUrl = `http://127.0.0.1:${port}`;

const validateCatalog = responseValidator("catalog");
const validateChart = responseValidator("charts");
const validatePublish = responseValidator("publish", "put");

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

    server = spawn(process.execPath, [vpCli, "dev", "--port", String(port), "--strictPort"], {
      cwd: webRoot,
      detached: true,
      env: { ...process.env, FLASHBEAT_ADMIN_TOKEN: TEST_ADMIN_TOKEN },
      // The server output goes to a file instead of a pipe: the vite and
      // workerd processes inherit the pipe and would keep the test runner
      // waiting for the end of the stream.
      stdio: ["ignore", logFd, logFd],
    });

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
        "a non-Tap note",
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
});
