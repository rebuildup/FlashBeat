/**
 * Fixture identity test (issue #638).
 *
 * `db/fixtures/who.chart.json` is the chart snapshot the seed publishes for
 * the "Who" song, and `db/seed.ts` stores its text verbatim so the published
 * chart is identical to the one the Unity player ships. The Unity source is
 * `Assets/Game/Resources/Who.json`; the fixture is that file minus the
 * `lyrics` block (lyrics are a runtime concern of the player, not part of
 * the published chart notes).
 *
 * Nothing enforced that invariant, so a drift between the two files would
 * silently publish a chart the Unity side no longer plays. This test fails
 * as soon as the notes — or any other chart field — diverge.
 */
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { describe, it } from "node:test";

const unityChartUrl = new URL("../../Assets/Game/Resources/Who.json", import.meta.url);
const fixtureUrl = new URL("../db/fixtures/who.chart.json", import.meta.url);

describe("who.chart.json fixture identity", () => {
  it("carries the chart of Assets/Game/Resources/Who.json, lyrics excluded", () => {
    const unityChart = JSON.parse(readFileSync(unityChartUrl, "utf-8")) as Record<string, unknown>;
    const fixture = JSON.parse(readFileSync(fixtureUrl, "utf-8")) as Record<string, unknown>;

    // The exclusion must be real: the Unity chart does carry a lyrics block
    // and the fixture must not.
    assert.ok("lyrics" in unityChart, "Assets/Game/Resources/Who.json has no lyrics block");
    assert.ok(!("lyrics" in fixture), "the fixture must not carry the lyrics block");

    const { lyrics: _lyrics, ...unityChartWithoutLyrics } = unityChart;
    assert.deepEqual(fixture, unityChartWithoutLyrics);
  });
});
