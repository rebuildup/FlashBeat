/**
 * Read-only access to the FlashBeat D1 database for the interface
 * verification.
 *
 * The check exists because `catalogVersion` is defined as
 * `COUNT(chart_versions)`: the API and the database have to agree, and the
 * database is the only place where the real number can be read. The official
 * `cf` CLI is used (never `wrangler`); the database is addressed by its id,
 * because `cf d1 query` does not accept a database name.
 *
 * The command inherits the environment, so it needs `CLOUDFLARE_API_TOKEN` and
 * `CLOUDFLARE_ACCOUNT_ID` — inject them with `script/with-secrets`.
 */
import { spawnSync } from "node:child_process";

/** The single FlashBeat D1 database. */
export const FLASHBEAT_D1_DATABASE_ID = "7de6fd23-dd7b-4e21-bddd-b28b33ee0462";

/**
 * `COUNT(chart_versions)` of the given database.
 *
 * @param databaseId D1 database id, e.g. `FLASHBEAT_D1_DATABASE_ID`
 */
export function countChartVersions(databaseId: string): number {
  const result = spawnSync(
    "cf",
    ["d1", "query", databaseId, "--sql", "SELECT COUNT(*) AS c FROM chart_versions"],
    { encoding: "utf-8" },
  );
  if (result.status !== 0) {
    throw new Error(`cf d1 query failed (${result.status}): ${result.stderr.trim()}`);
  }
  const payload: unknown = JSON.parse(result.stdout);
  if (!Array.isArray(payload) || payload.length === 0) {
    throw new Error(`cf d1 query returned no result: ${result.stdout}`);
  }
  const first = payload[0];
  if (typeof first !== "object" || first === null || !("results" in first)) {
    throw new Error(`cf d1 query returned an unexpected payload: ${result.stdout}`);
  }
  const rows = first.results;
  if (!Array.isArray(rows) || rows.length === 0) {
    throw new Error(`cf d1 query returned no rows: ${result.stdout}`);
  }
  const row = rows[0];
  if (typeof row !== "object" || row === null || !("c" in row) || typeof row.c !== "number") {
    throw new Error(`cf d1 query returned an unexpected row: ${result.stdout}`);
  }
  return row.c;
}
