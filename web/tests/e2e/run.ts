#!/usr/bin/env node
/**
 * Production E2E harness CLI (issue #97, refs #41).
 *
 * Verifies the production chain stage by stage with real values against
 * `https://flashbeat.rebuildup.dev`:
 *
 *   1. `GET /api/v1/catalog` — record `catalogVersion`, songs, ids.
 *   2. `GET /api/v1/charts/{id}?version=N` — identity `songId`/`chartVersion`,
 *      `youtubeId`, `noteCount === chart.notes.length` against the catalog.
 *   3. Chart snapshot validated against `contracts/schemas/chart.v1.schema.json`.
 *   4. OPTIONAL write stage, only with
 *      `--write --song-id <id> --token-env FLASHBEAT_ADMIN_TOKEN`:
 *      `PUT /api/v1/admin/songs/{id}` with a modified copy, then re-GET the
 *      catalog and the chart and assert `chartVersion + 1`, `catalogVersion + 1`
 *      and exact snapshot equality (via `publishAndVerify`).
 *
 * Default mode is read-only. The token is read from the environment named by
 * `--token-env` and is never printed. The JSON report goes to stdout and to
 * `last-report.json` next to this file (gitignored).
 *
 * Usage:
 *   node tests/e2e/run.ts [--base-url <url>] [--song-id <id>]
 *   node tests/e2e/run.ts --write --song-id <id> [--token-env <name>]
 */
import { writeFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

import {
  buildPublishRequest,
  catalogStage,
  chartStage,
  leaderboardStage,
  makeFetchJson,
  pickSong,
  schemaStage,
  writeStage,
  type Catalog,
  type CatalogSong,
  type StageEntry,
} from "./lib/harness.ts";

const DEFAULT_BASE_URL = "https://flashbeat.rebuildup.dev";

interface Args {
  baseUrl: string;
  write: boolean;
  songId: string | null;
  tokenEnv: string;
}

function parseArgs(argv: Array<string>): Args {
  const args: Args = {
    baseUrl: DEFAULT_BASE_URL,
    write: false,
    songId: null,
    tokenEnv: "FLASHBEAT_ADMIN_TOKEN",
  };
  for (let index = 0; index < argv.length; index += 1) {
    const flag = argv[index];
    if (flag === "--write") {
      args.write = true;
    } else if (flag === "--base-url" || flag === "--song-id" || flag === "--token-env") {
      const value = argv[index + 1];
      if (value === undefined) {
        fail(`missing value for ${flag}`);
      }
      index += 1;
      if (flag === "--base-url") args.baseUrl = value.replace(/\/+$/, "");
      if (flag === "--song-id") args.songId = value;
      if (flag === "--token-env") args.tokenEnv = value;
    } else {
      fail(`unknown flag: ${flag}`);
    }
  }
  return args;
}

function fail(message: string): never {
  console.error(`e2e: ${message}`);
  process.exit(2);
}

const args = parseArgs(process.argv.slice(2));
const fetchJson = makeFetchJson((input, init) => fetch(input, init));

const stages: Array<StageEntry> = [];
let catalog: Catalog | null = null;

// Stage 1: catalog.
const catalogResult = await catalogStage(fetchJson, args.baseUrl);
stages.push(...catalogResult.entries);
catalog = catalogResult.catalog;

// Stage 2 + 3: chart of one song, then the frozen chart schema.
let snapshot = null;
let pickedSong: CatalogSong | null = null;
if (catalog !== null) {
  const picked = pickSong(catalog, args.songId);
  stages.push(picked.entry);
  pickedSong = picked.song;
  if (picked.song !== null) {
    const chartResult = await chartStage(fetchJson, args.baseUrl, picked.song);
    stages.push(...chartResult.entries);
    snapshot = chartResult.snapshot;
  }
}
if (snapshot !== null) {
  stages.push(...schemaStage(snapshot));
}

// Stage 3.5: leaderboard of the same song (read-only).
if (pickedSong !== null) {
  stages.push(...(await leaderboardStage(fetchJson, args.baseUrl, pickedSong)));
}

// Stage 4: optional write. Refuses to run without the explicit flags.
if (args.write) {
  if (args.songId === null) {
    fail("--write requires --song-id <id>");
  }
  const token = process.env[args.tokenEnv] ?? "";
  if (token.trim().length === 0) {
    fail(`--write requires a non-empty ${args.tokenEnv} environment variable`);
  }
  if (catalog === null || snapshot === null) {
    fail("--write needs a successful catalog and chart read first");
  }
  const song = catalog.songs.find((candidate) => candidate.id === args.songId) ?? null;
  if (song === null) {
    fail(`song id ${args.songId} is not in the catalog`);
  }
  const request = buildPublishRequest(song, snapshot, (chart) => {
    // The modified copy: an e2e marker in the legacy chart display name.
    chart["name"] = `${String(chart["name"] ?? "")} [e2e #97]`;
  });
  stages.push(
    ...(await writeStage({
      baseUrl: args.baseUrl,
      songId: args.songId,
      token,
      request,
      fetchImpl: (input, init) => fetch(input, init),
      catalogVersionBefore: catalog.catalogVersion,
      chartVersionBefore: song.chartVersion,
    })),
  );
}

const passed = stages.filter((stage) => stage.pass).length;
const report = {
  generatedAt: new Date().toISOString(),
  baseUrl: args.baseUrl,
  mode: args.write ? "write" : "read-only",
  summary: { total: stages.length, passed, failed: stages.length - passed },
  stages,
};

const json = `${JSON.stringify(report, null, 2)}\n`;
process.stdout.write(json);
writeFileSync(fileURLToPath(new URL("./last-report.json", import.meta.url)), json, "utf8");
process.exit(report.summary.failed === 0 ? 0 : 1);
