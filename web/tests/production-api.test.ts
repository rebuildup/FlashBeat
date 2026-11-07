/**
 * Frozen interface verification against the deployed Worker.
 *
 * The responses are validated against the *frozen contract files*
 * (`contracts/openapi.v1.yaml`, `contracts/schemas/chart.v1.schema.json`),
 * never against a copy, so a drift between the deployed API and the contract
 * fails here.
 *
 * The publish checks go through the client of the Web Studio
 * (`src/lib/publish/client.ts`), so the request the studio sends is the
 * request that is verified here.
 *
 * This suite talks to a deployed Worker, so it never runs by accident. It is
 * enabled explicitly:
 *
 *   FLASHBEAT_E2E=1           run the read checks against the deployed API
 *   FLASHBEAT_E2E_PUBLISH=1   additionally publish to the deployed database
 *   FLASHBEAT_API_BASE_URL    API base URL (default: the deployed worker)
 *   FLASHBEAT_ADMIN_TOKEN     staff token for the publish checks
 *   FLASHBEAT_E2E_SONG_ID     song the publish checks write (default: `who`)
 *   FLASHBEAT_D1_DATABASE_ID  D1 database to compare `catalogVersion` with
 *
 * The local counterpart of this suite is `api.test.ts`: it runs the same
 * Worker code against the local D1 and never touches a deployed resource.
 */
import assert from "node:assert/strict";
import { describe, it } from "node:test";

import { publishSong } from "../src/lib/publish/client.ts";
import type { SongPublishRequest } from "../src/lib/publish/types.ts";
import { FLASHBEAT_D1_DATABASE_ID, countChartVersions } from "./helpers/d1.ts";
import {
  chartFixture,
  operationResponseValidator,
  publishRequestFixture,
} from "./helpers/frozen.ts";

const readEnabled = process.env.FLASHBEAT_E2E === "1";
const publishEnabled = readEnabled && process.env.FLASHBEAT_E2E_PUBLISH === "1";
const baseUrl = (process.env.FLASHBEAT_API_BASE_URL ?? "https://flashbeat.rebuildup.dev").replace(
  /\/+$/,
  "",
);
const adminToken = process.env.FLASHBEAT_ADMIN_TOKEN ?? "";
const databaseId = process.env.FLASHBEAT_D1_DATABASE_ID ?? FLASHBEAT_D1_DATABASE_ID;

/** The song the publish checks write. */
const SONG_ID = process.env.FLASHBEAT_E2E_SONG_ID ?? "who";

const validateCatalog = operationResponseValidator("getCatalog", "200");
const validateChart = operationResponseValidator("getChart", "200");
const validatePublish = operationResponseValidator("publishSong", "200");

interface CatalogSong {
  id: string;
  title: string;
  artist: string;
  youtubeId: string;
  level: number;
  bpm: number;
  durationSec: number;
  chartVersion: number;
  noteCount: number;
}

interface Catalog {
  catalogVersion: number;
  songs: CatalogSong[];
}

/** A JSON object with unknown members, or null for everything else. */
function asObject(value: unknown): Record<string, unknown> | null {
  if (typeof value !== "object" || value === null || Array.isArray(value)) {
    return null;
  }
  return Object.fromEntries(Object.entries(value));
}

async function fetchCatalog(): Promise<Catalog> {
  const response = await fetch(`${baseUrl}/api/v1/catalog`);
  assert.equal(response.status, 200);
  return (await response.json()) as Catalog;
}

/** Publish with the client of the Web Studio. */
function publish(songId: string, request: SongPublishRequest, token: string) {
  return publishSong({ baseUrl, songId, token, request });
}

describe("deployed read API", { skip: !readEnabled }, () => {
  it("answers the catalog with the contract shape and without caching", async () => {
    const response = await fetch(`${baseUrl}/api/v1/catalog`);
    assert.equal(response.status, 200);
    assert.match(response.headers.get("cache-control") ?? "", /no-store/);

    const body: unknown = await response.json();
    validateCatalog(body);

    const catalog = body as Catalog;
    assert.ok(Number.isInteger(catalog.catalogVersion));
    assert.ok(catalog.catalogVersion >= 0);
    // The list order is stable: the Unity client polls it every few seconds.
    assert.deepEqual(
      catalog.songs.map((song) => song.id),
      catalog.songs.map((song) => song.id).toSorted(),
    );
  });

  it("counts every published chart snapshot in the catalog version", async () => {
    const catalog = await fetchCatalog();
    const publishedSnapshots = countChartVersions(databaseId);
    assert.equal(catalog.catalogVersion, publishedSnapshots);
  });

  it("lists only songs whose current version is published", async () => {
    const catalog = await fetchCatalog();
    for (const song of catalog.songs) {
      const response = await fetch(
        `${baseUrl}/api/v1/charts/${song.id}?version=${song.chartVersion}`,
      );
      assert.equal(response.status, 200, `${song.id} v${song.chartVersion} must load`);
      const body: unknown = await response.json();
      validateChart(body);
    }
  });

  it("rejects a missing or invalid version with 400", async () => {
    for (const query of ["", "?version=0", "?version=-1", "?version=1.5", "?version=abc"]) {
      const response = await fetch(`${baseUrl}/api/v1/charts/${SONG_ID}${query}`);
      assert.equal(response.status, 400, `expected 400 for "${query}"`);
    }
  });

  it("answers 404 for an unknown song and for an unknown version", async () => {
    const unknownSong = await fetch(`${baseUrl}/api/v1/charts/no-such-song?version=1`);
    assert.equal(unknownSong.status, 404);

    const catalog = await fetchCatalog();
    const published = catalog.songs.find((song) => song.id === SONG_ID);
    const unknownVersion = await fetch(
      `${baseUrl}/api/v1/charts/${SONG_ID}?version=${(published?.chartVersion ?? 0) + 1000}`,
    );
    assert.equal(unknownVersion.status, 404);
  });

  it("answers every catalog entry the way the Unity client reads it", async () => {
    // `Assets/Game/Scripts/Songs/CloudCatalog.cs` keeps an entry only when the
    // song id matches `^[a-z0-9-]{3,64}$`, the title is not empty and the
    // video id matches `^[A-Za-z0-9_-]{11}$`; the chart version is pinned
    // from the catalog and the snapshot is fetched with it.
    const catalog = await fetchCatalog();
    for (const song of catalog.songs) {
      assert.match(song.id, /^[a-z0-9-]{3,64}$/, `${song.id} is not a usable song id`);
      assert.ok(song.title.trim().length > 0, `${song.id} has no title`);
      assert.match(song.youtubeId, /^[A-Za-z0-9_-]{11}$/, `${song.id} has no usable video id`);
      assert.ok(song.chartVersion >= 1, `${song.id} has no published chart version`);
      assert.ok(song.noteCount >= 1, `${song.id} has no notes`);
    }
  });

  it("serves the chart snapshot in the shape the Unity player loads", async () => {
    // The Unity chart classes (`Data` / `Note` in
    // `Assets/Game/Scripts/Gameplay/NotesManager.cs`) read `name`,
    // `maxBlock`, `BPM`, `offset` and the notes `type`, `num`, `block` and
    // `LPB`; the client needs them on every note.
    const catalog = await fetchCatalog();
    for (const song of catalog.songs) {
      const response = await fetch(
        `${baseUrl}/api/v1/charts/${song.id}?version=${song.chartVersion}`,
      );
      assert.equal(response.status, 200);
      const raw: unknown = await response.json();
      const body = asObject(raw);
      assert.ok(body !== null, `${song.id} must answer a JSON object`);
      assert.equal(body.songId, song.id);
      assert.equal(body.chartVersion, song.chartVersion);

      const chart = asObject(body.chart);
      assert.ok(chart !== null, `${song.id} must answer a chart object`);
      assert.equal(typeof chart.name, "string");
      assert.equal(typeof chart.maxBlock, "number");
      assert.equal(typeof chart.BPM, "number");
      assert.equal(typeof chart.offset, "number");
      assert.ok(Array.isArray(chart.notes));
      assert.equal(chart.notes.length, song.noteCount);

      for (const note of chart.notes as unknown[]) {
        const fields = asObject(note);
        assert.ok(fields !== null, `${song.id}: note is not an object`);
        for (const field of ["type", "num", "block", "LPB"]) {
          assert.equal(typeof fields[field], "number", `${song.id}: note misses ${field}`);
        }
      }
    }
  });
});

describe("deployed publish API", { skip: !publishEnabled }, () => {
  it("rejects a wrong staff token with 401", async () => {
    const result = await publish(SONG_ID, publishRequestFixture(), "not-the-staff-token");
    assert.equal(result.ok, false);
    if (result.ok) return;
    assert.equal(result.failure.code, "unauthorized");
    assert.equal(result.failure.status, 401);
  });

  it("rejects an invalid payload with 422 and a diagnostic", async () => {
    const invalid: SongPublishRequest = {
      ...publishRequestFixture(),
      // `maxBlock` must stay within 1..8 (contracts/schemas/chart.v1.schema.json);
      // legacy Unity charts carry 5 lanes, so 8 is not the only valid value.
      chart: { ...publishRequestFixture().chart, maxBlock: 9 },
    };
    const result = await publish(SONG_ID, invalid, adminToken);
    assert.equal(result.ok, false);
    if (result.ok) return;
    assert.equal(result.failure.code, "invalid");
    assert.equal(result.failure.status, 422);
    assert.ok((result.failure.details ?? []).length > 0, "the 422 answer must carry a diagnostic");
  });

  it("publishes the fixture chart and counts up on the next publish", async () => {
    const before = await fetchCatalog();
    const previousVersion = before.songs.find((song) => song.id === SONG_ID)?.chartVersion ?? 0;

    const first = await publish(SONG_ID, publishRequestFixture(), adminToken);
    assert.equal(first.ok, true, "the studio client must publish into the deployed API");
    if (!first.ok) return;
    assert.equal(first.response.songId, SONG_ID);
    assert.equal(first.response.chartVersion, previousVersion + 1);
    assert.equal(first.response.catalogVersion, before.catalogVersion + 1);

    // The song is listed by the read API and its snapshot loads.
    const catalog = await fetchCatalog();
    const listed = catalog.songs.find((song) => song.id === SONG_ID);
    assert.equal(listed?.chartVersion, first.response.chartVersion);

    const chart = await fetch(
      `${baseUrl}/api/v1/charts/${SONG_ID}?version=${first.response.chartVersion}`,
    );
    assert.equal(chart.status, 200);
    const chartBody: unknown = await chart.json();
    validateChart(chartBody);
    const chartObject = asObject(chartBody);
    assert.ok(chartObject !== null);
    assert.deepEqual(chartObject.chart, publishRequestFixture().chart);

    // Publishing the same song again adds a version, it never overwrites.
    const second = await publish(SONG_ID, publishRequestFixture(), adminToken);
    assert.equal(second.ok, true);
    if (!second.ok) return;
    assert.equal(second.response.chartVersion, first.response.chartVersion + 1);
    assert.equal(second.response.catalogVersion, first.response.catalogVersion + 1);

    // The previous snapshot is immutable and still loads.
    const snapshot = await fetch(
      `${baseUrl}/api/v1/charts/${SONG_ID}?version=${first.response.chartVersion}`,
    );
    assert.equal(snapshot.status, 200);
  });
});
