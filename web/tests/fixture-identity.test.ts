/**
 * Fixture identity test (issue #638, updated for #823/#794).
 *
 * `db/fixtures/seed.chart.json` is the chart snapshot the seed publishes for
 * the fixture song. Since issue #823 the Unity project ships no local song
 * charts — charts come from the cloud catalog — so the canonical copy is the
 * rights-cleared synthetic chart `contracts/charts/seed-chart.json`.
 *
 * The fixture must stay identical to that canonical file so the seeded local
 * D1 never drifts from the contract corpus. The chart carries no `lyrics`
 * block (lyrics are a runtime concern delivered inside cloud charts).
 */
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { describe, it } from "node:test";

const canonicalChartUrl = new URL("../../contracts/charts/seed-chart.json", import.meta.url);
const fixtureUrl = new URL("../db/fixtures/seed.chart.json", import.meta.url);

describe("seed.chart.json fixture identity", () => {
  it("carries the contract chart contracts/charts/seed-chart.json verbatim", () => {
    const canonical = JSON.parse(readFileSync(canonicalChartUrl, "utf-8")) as Record<
      string,
      unknown
    >;
    const fixture = JSON.parse(readFileSync(fixtureUrl, "utf-8")) as Record<string, unknown>;

    assert.ok(!("lyrics" in fixture), "the fixture must not carry the lyrics block");
    assert.ok(!("lyrics" in canonical), "the canonical chart must not carry lyrics");
    assert.deepEqual(fixture, canonical);
  });
});
