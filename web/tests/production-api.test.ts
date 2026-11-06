/**
 * Frozen interface verification against the deployed Worker.
 *
 * The responses are validated against the *frozen contract files*
 * (`contracts/openapi.v1.yaml`, `contracts/schemas/chart.v1.schema.json`),
 * never against a copy, so a drift between the deployed API and the contract
 * fails here.
 *
 * This suite talks to a deployed Worker, so it never runs by accident. It is
 * enabled explicitly:
 *
 *   FLASHBEAT_E2E=1           run the read checks against the deployed API
 *   FLASHBEAT_E2E_PUBLISH=1   additionally publish to the deployed database
 *   FLASHBEAT_API_BASE_URL    API base URL (default: the deployed worker)
 *   FLASHBEAT_ADMIN_TOKEN     staff token for the publish checks
 *   FLASHBEAT_D1_DATABASE_ID  D1 database to compare `catalogVersion` with
 *
 * The local counterpart of this suite is `api.test.ts`: it runs the same
 * Worker code against the local D1 and never touches a deployed resource.
 */
import assert from "node:assert/strict";
import { describe, it } from "node:test";

import { FLASHBEAT_D1_DATABASE_ID, countChartVersions } from "./helpers/d1.ts";
import {
  chartFixture,
  operationResponseValidator,
  publishRequestFixture,
} from "./helpers/frozen.ts";

const readEnabled = process.env.FLASHBEAT_E2E === "1";
const publishEnabled = readEnabled && process.env.FLASHBEAT_E2E_PUBLISH === "1";
const baseUrl = (
  process.env.FLASHBEAT_API_BASE_URL ?? "https://flashbeat-web.rebuild-up-up.workers.dev"
).replace(/\/+$/, "");
const adminToken = process.env.FLASHBEAT_ADMIN_TOKEN ?? "";
const databaseId = process.env.FLASHBEAT_D1_DATABASE_ID ?? FLASHBEAT_D1_DATABASE_ID;

/** The song of the fixture chart. */
const SONG_ID = "who";

const validateCatalog = operationResponseValidator("getCatalog", "200");
const validateChart = operationResponseValidator("getChart", "200");
const validatePublish = operationResponseValidator("publishSong", "200");

interface CatalogSong {
  id: string;
  chartVersion: number;
  noteCount: number;
}

interface Catalog {
  catalogVersion: number;
  songs: CatalogSong[];
}

async function fetchCatalog(): Promise<Catalog> {
  const response = await fetch(`${baseUrl}/api/v1/catalog`);
  assert.equal(response.status, 200);
  return (await response.json()) as Catalog;
}

async function publish(body: unknown, token: string): Promise<Response> {
  return fetch(`${baseUrl}/api/v1/admin/songs/${SONG_ID}`, {
    method: "PUT",
    headers: {
      "content-type": "application/json",
      authorization: `Bearer ${token}`,
    },
    body: JSON.stringify(body),
  });
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
});

describe("deployed publish API", { skip: !publishEnabled }, () => {
  it("rejects a wrong staff token with 401", async () => {
    const response = await publish(publishRequestFixture(), "not-the-staff-token");
    assert.equal(response.status, 401);
  });

  it("rejects an invalid payload with 422 and a diagnostic", async () => {
    const invalid = {
      ...publishRequestFixture(),
      // `maxBlock` must be 8 (contracts/schemas/chart.v1.schema.json).
      chart: { ...chartFixture(), maxBlock: 7 },
    };
    const response = await publish(invalid, adminToken);
    assert.equal(response.status, 422);

    const body: unknown = await response.json();
    const diagnostics = JSON.stringify(body);
    assert.ok(diagnostics.length > 2, "the 422 answer must carry a diagnostic");
  });

  it("publishes the fixture chart and counts up on the next publish", async () => {
    const before = await fetchCatalog();
    const previousVersion = before.songs.find((song) => song.id === SONG_ID)?.chartVersion ?? 0;

    const response = await publish(publishRequestFixture(), adminToken);
    assert.equal(response.status, 200);
    const published: unknown = await response.json();
    validatePublish(published);

    const body = published as { songId: string; chartVersion: number; catalogVersion: number };
    assert.equal(body.songId, SONG_ID);
    assert.equal(body.chartVersion, previousVersion + 1);
    assert.equal(body.catalogVersion, before.catalogVersion + 1);

    // The song is listed by the read API and its snapshot loads.
    const catalog = await fetchCatalog();
    const listed = catalog.songs.find((song) => song.id === SONG_ID);
    assert.equal(listed?.chartVersion, body.chartVersion);

    const chart = await fetch(`${baseUrl}/api/v1/charts/${SONG_ID}?version=${body.chartVersion}`);
    assert.equal(chart.status, 200);
    const chartBody: unknown = await chart.json();
    validateChart(chartBody);
    assert.deepEqual((chartBody as { chart: unknown }).chart, chartFixture());

    // Publishing the same song again adds a version, it never overwrites.
    const second = await publish(publishRequestFixture(), adminToken);
    assert.equal(second.status, 200);
    const secondBody = (await second.json()) as {
      songId: string;
      chartVersion: number;
      catalogVersion: number;
    };
    validatePublish(secondBody);
    assert.equal(secondBody.chartVersion, body.chartVersion + 1);
    assert.equal(secondBody.catalogVersion, body.catalogVersion + 1);

    // The previous snapshot is immutable and still loads.
    const first = await fetch(`${baseUrl}/api/v1/charts/${SONG_ID}?version=${body.chartVersion}`);
    assert.equal(first.status, 200);
  });
});
