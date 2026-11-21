/**
 * Local seed for the festival D1 database.
 *
 * `void db seed` resets the local database, re-applies every migration and then
 * runs this module. It inserts one synthetic fixture song together with its
 * chart snapshot.
 *
 * The chart snapshot is stored as the *raw* bytes of
 * `db/fixtures/seed.chart.json`, a byte-identical copy of the rights-cleared
 * contract chart `contracts/charts/seed-chart.json` (issue #823: the Unity
 * project ships no local song charts anymore — charts come from the cloud
 * catalog). Storing the file text verbatim keeps the seeded chart identical
 * to the contract corpus.
 */
import { readFileSync } from "node:fs";
import Ajv2020 from "ajv/dist/2020.js";
import { defineSeed } from "void/seed";
import { count } from "void/db";
import type * as schemaModule from "./schema";

const chartSchemaUrl = new URL("../../contracts/schemas/chart.v1.schema.json", import.meta.url);
const chartFixtureUrl = new URL("./fixtures/seed.chart.json", import.meta.url);
const songFixtureUrl = new URL("./fixtures/seed.song.json", import.meta.url);

export type SongFixture = {
  id: string;
  title: string;
  artist: string;
  youtubeId: string;
  bpm: number;
  level: number;
  durationSec: number;
  currentVersion: number;
  /** Archive flag: 0 = live, 1 = archived (issue #151). */
  archived: number;
  /** Archive moment, `null` while the song is live (issue #151). */
  archivedAt: string | null;
};

/**
 * Validate the chart fixture against the frozen contract
 * (`contracts/schemas/chart.v1.schema.json`). The seed fails loudly instead of
 * publishing a chart the API contract would reject.
 */
export function assertChartMatchesContract(chartText: string): Record<string, unknown> {
  const schema = JSON.parse(readFileSync(chartSchemaUrl, "utf-8")) as Record<string, unknown>;
  const chart = JSON.parse(chartText) as Record<string, unknown>;
  const ajv = new Ajv2020({ strict: false, allErrors: true });
  const validate = ajv.compile(schema);
  if (!validate(chart)) {
    throw new Error(
      `chart fixture does not satisfy contracts/schemas/chart.v1.schema.json: ${ajv.errorsText(validate.errors)}`,
    );
  }
  return chart;
}

/** Read the fixtures from disk. Exported so tests can assert the same data. */
export function readFixtures(): {
  song: SongFixture;
  chartText: string;
  chart: Record<string, unknown>;
} {
  const chartText = readFileSync(chartFixtureUrl, "utf-8");
  const song = JSON.parse(readFileSync(songFixtureUrl, "utf-8")) as SongFixture;
  const chart = assertChartMatchesContract(chartText);
  return { song, chartText, chart };
}

export default defineSeed<typeof schemaModule>(async ({ db, schema, dialect }) => {
  if (dialect !== "sqlite") {
    throw new Error(`seed: expected the sqlite (D1) dialect, got "${dialect}"`);
  }

  const { song, chartText, chart } = readFixtures();

  // The chart and the song row describe the same song; a mismatch means one
  // of the two fixtures drifted away from `Assets/Resources/Who.json`.
  if (chart.name !== song.title) {
    throw new Error(
      `seed: chart name "${String(chart.name)}" does not match song title "${song.title}"`,
    );
  }
  if (chart.BPM !== song.bpm) {
    throw new Error(`seed: chart BPM "${String(chart.BPM)}" does not match song bpm "${song.bpm}"`);
  }

  const notes = chart.notes as unknown[];
  const noteCount = notes.length;

  await db.insert(schema.songs).values(song);
  await db.insert(schema.chartVersions).values({
    songId: song.id,
    version: song.currentVersion,
    json: chartText,
    noteCount,
  });

  // Issue #656: the catalog epoch starts at the seeded chart count so a
  // fresh local D1 reports the same catalogVersion range the read API
  // served before the epoch existed.
  const [chartTotal] = await db.select({ value: count() }).from(schema.chartVersions);
  await db
    .insert(schema.catalogMeta)
    .values({ id: 1, epoch: chartTotal.value })
    .onConflictDoUpdate({ target: schema.catalogMeta.id, set: { epoch: chartTotal.value } });
});
