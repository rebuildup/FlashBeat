import { validateChart } from "./schema";
import type { Chart, ValidationIssue } from "./types";

/** Result of reading a chart file. */
export type ImportResult = { ok: true; chart: Chart } | { ok: false; issues: ValidationIssue[] };

/**
 * Read a legacy FlashBeat chart.
 *
 * The JSON is parsed as-is and validated against the frozen contract. The
 * parsed object is kept untouched, so unknown top-level fields (`videoId`)
 * and the original key order survive the round trip.
 */
export function importChartJson(text: string): ImportResult {
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

  const issues = validateChart(parsed);
  if (issues.length > 0) {
    return { ok: false, issues };
  }

  return { ok: true, chart: parsed as Chart };
}
