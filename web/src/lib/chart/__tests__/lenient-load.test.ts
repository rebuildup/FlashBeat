import { describe, expect, it } from "vitest";

import { MIXED_LPB_OVERLAP_CHART } from "../fixtures";
import { importChartJson, importChartLenient } from "../import";
import { countErrors, countWarnings, mergeLoadWarnings } from "../issues";
import { validateChart } from "../schema";
import type { Chart } from "../types";

/**
 * Recovery path of a chart that was published before the semantic rules were
 * unified (issue #208).
 *
 * The fixture holds two overlapping holds of a mixed `LPB`: the line numbers
 * only touch, the beats overlap. Such a chart can be in the database, because
 * the publish API accepted it under the old rules. Before issue #208 the
 * editor refused it with "Published chart failed validation" and the
 * operator could not repair it.
 *
 * The rules themselves stay in `validate.ts` and are not touched here: the
 * publish API and the validator tests keep the strict mode. Only the *read*
 * paths are lenient now.
 */
describe("a published chart with a mixed LPB overlap", () => {
  it("is refused by the strict import", () => {
    const result = importChartJson(JSON.stringify(MIXED_LPB_OVERLAP_CHART));

    expect(result.ok).toBe(false);
    if (result.ok) {
      return;
    }
    expect(result.issues.map((issue) => issue.keyword)).toContain("overlap");
  });

  it("is read with a warning by the lenient import", () => {
    const result = importChartLenient(JSON.stringify(MIXED_LPB_OVERLAP_CHART));

    expect(result.ok).toBe(true);
    if (!result.ok) {
      return;
    }
    expect(result.issues?.map((issue) => issue.keyword)).toContain("warning.overlap");
    expect(countWarnings(result.issues ?? [])).toBe(1);
    // The chart is not rewritten: the operator repairs it in the editor.
    expect(result.chart.notes).toHaveLength(2);
  });

  it("keeps the warning in the live validation of the editor", () => {
    const loaded = importChartLenient(JSON.stringify(MIXED_LPB_OVERLAP_CHART));
    expect(loaded.ok).toBe(true);
    if (!loaded.ok) {
      return;
    }

    // The editor validates the chart it shows strictly, the publish API
    // refuses every violation.
    const live = validateChart(loaded.chart);
    expect(live.map((issue) => issue.keyword)).toContain("overlap");

    const merged = mergeLoadWarnings(live, loaded.issues ?? []);
    expect(merged.map((issue) => issue.keyword)).toContain("warning.overlap");
    expect(countWarnings(merged)).toBe(1);
    expect(countErrors(merged)).toBe(0);
  });

  it("drops the warning once the operator repairs the chart", () => {
    const repaired: Chart = {
      ...MIXED_LPB_OVERLAP_CHART,
      notes: [
        { type: 2, num: 0, endNum: 4, block: 0, LPB: 2 },
        // The beats 2.5 to 3 lie behind the first hold (the beats 0 to 2).
        { type: 2, num: 40, endNum: 48, block: 0, LPB: 16 },
      ],
    };

    const loaded = importChartLenient(JSON.stringify(MIXED_LPB_OVERLAP_CHART));
    expect(loaded.ok).toBe(true);
    if (!loaded.ok) {
      return;
    }

    const merged = mergeLoadWarnings(validateChart(repaired), loaded.issues ?? []);
    expect(merged).toEqual([]);
  });

  it("keeps a problem the operator creates an error", () => {
    // Nothing was loaded with a warning, so the live validation speaks for
    // itself: the overlap is an error and blocks the publish.
    const merged = mergeLoadWarnings(validateChart(MIXED_LPB_OVERLAP_CHART), []);

    expect(merged.map((issue) => issue.keyword)).toContain("overlap");
    expect(countErrors(merged)).toBe(1);
    expect(countWarnings(merged)).toBe(0);
  });
});
