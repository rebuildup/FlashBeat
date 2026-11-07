import { validateChart } from "./schema";
import type { SemanticStrictness } from "./validate";
import type { Chart, ValidationIssue } from "./types";

/**
 * Result of reading a chart file.
 *
 * `issues` holds the semantic violations that were tolerated in `lenient`
 * mode. It is only present when the chart could be read despite them.
 */
export type ImportResult =
  | { ok: true; chart: Chart; issues?: ValidationIssue[] }
  | { ok: false; issues: ValidationIssue[] };

/**
 * Read a legacy FlashBeat chart.
 *
 * The JSON is parsed as-is and validated against the frozen contract. The
 * parsed object is kept untouched, so unknown top-level fields (`videoId`)
 * and the original key order survive the round trip.
 *
 * `strictness` decides how a chart whose relations are broken is treated
 * (issue #191, requirement R7):
 *
 * - `strict` (the default) refuses it. This is what the editor and the
 *   publish API use.
 * - `lenient` accepts it and returns the violations as warnings
 *   (`issues` is filled, `ok` stays `true`). This is the recovery path for a
 *   chart that was published before the rules were unified: it can be read
 *   back and repaired instead of being dropped.
 */
export function importChartJson(
  text: string,
  strictness: SemanticStrictness = "strict",
): ImportResult {
  let parsed: unknown;

  try {
    parsed = JSON.parse(text);
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    return {
      ok: false,
      issues: [{ path: "", message: `Invalid JSON: ${message}`, keyword: "parse" }],
    };
  }

  const issues = validateChart(parsed, {}, strictness);
  if (strictness === "strict" && issues.length > 0) {
    return { ok: false, issues };
  }

  if (issues.length > 0) {
    return { ok: true, chart: parsed as Chart, issues };
  }

  return { ok: true, chart: parsed as Chart };
}

/**
 * Read a chart that the editor opens: a file the operator picked or a
 * published chart of a song session.
 *
 * The read is **lenient**: a chart whose *relations* are broken is loaded and
 * the violations are returned as warnings, so a chart that was published
 * before the semantic rules were unified can be read back and repaired
 * instead of being dropped (issue #208). The structural problems are still
 * errors — a chart that does not match the contract cannot be repaired by
 * editing a hold.
 *
 * The publish path stays strict: `routes/api/v1/_publish-model.ts` refuses
 * every violation, and the validator tests pin that.
 */
export function importChartLenient(text: string): ImportResult {
  return importChartJson(text, "lenient");
}
