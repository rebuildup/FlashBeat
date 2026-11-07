import { describe, expect, it } from "vitest";

import type { Chart } from "../types";
import { MAX_HOLD_BEATS, MIN_HOLD_BEATS, validateChartSemantics } from "../validate";

/**
 * Regression tests of the shared semantic validator itself (issue #191).
 *
 * `chart-validation-parity.test.ts` pins that the Web studio and the publish
 * API answer the *same* way; these tests pin *what* the shared answer is, so
 * a wrong rule cannot slip through as an agreement:
 *
 *   - overlapping holds of a mixed `LPB` are detected (the old editor compared
 *     raw line numbers and missed them),
 *   - holds of a mixed `LPB` that do not overlap stay false-positive free,
 *   - two holds that only touch are accepted (boundary contact is playable),
 *   - the minimum and the maximum hold length are judged at their exact bounds.
 */

/** Wrap a note list into a chart. */
function chartOf(notes: Chart["notes"]): Chart {
  return {
    name: "Semantics",
    maxBlock: 8,
    BPM: 120,
    offset: 0,
    notes,
  };
}

describe("overlapping holds of a mixed LPB", () => {
  it("refuses two holds that share the same beats of a lane", () => {
    // Both holds cover beats 1-2 of lane 0: lines 4-8 at LPB 4 and lines
    // 8-16 at LPB 8. The raw line comparison of the old editor saw no
    // overlap here (issue #191, case 2).
    const issues = validateChartSemantics(
      chartOf([
        { type: 2, num: 4, endNum: 8, block: 0, LPB: 4 },
        { type: 2, num: 8, endNum: 16, block: 0, LPB: 8 },
      ]),
    );

    expect(issues).toHaveLength(1);
    expect(issues[0]?.path).toBe("/notes/1/num");
    expect(issues[0]?.keyword).toBe("overlap");
  });

  it("accepts holds of a mixed LPB whose beats do not overlap", () => {
    // Beats 0-1 (LPB 4) and beats 2-3 (LPB 8): no shared beat.
    const issues = validateChartSemantics(
      chartOf([
        { type: 2, num: 0, endNum: 4, block: 0, LPB: 4 },
        { type: 2, num: 16, endNum: 24, block: 0, LPB: 8 },
      ]),
    );

    expect(issues).toEqual([]);
  });
});

describe("holds that only touch", () => {
  it("accepts a boundary contact of the same resolution", () => {
    // The second hold starts exactly where the first one ends (beat 2).
    const issues = validateChartSemantics(
      chartOf([
        { type: 2, num: 0, endNum: 8, block: 0, LPB: 4 },
        { type: 2, num: 8, endNum: 16, block: 0, LPB: 4 },
      ]),
    );

    expect(issues).toEqual([]);
  });

  it("accepts a boundary contact across resolutions", () => {
    // Beats 0-2 (LPB 4) and beats 2-3 (LPB 8): they touch at beat 2.
    const issues = validateChartSemantics(
      chartOf([
        { type: 2, num: 0, endNum: 8, block: 0, LPB: 4 },
        { type: 2, num: 16, endNum: 24, block: 0, LPB: 8 },
      ]),
    );

    expect(issues).toEqual([]);
  });
});

describe("the hold length bounds", () => {
  it("accepts a hold of exactly the minimum length", () => {
    // One line at LPB 4 is exactly MIN_HOLD_BEATS.
    const issues = validateChartSemantics(
      chartOf([{ type: 2, num: 0, endNum: 1, block: 0, LPB: 4 }]),
    );

    expect(issues).toEqual([]);
  });

  it("refuses a hold below the minimum length", () => {
    // One line at LPB 8 is only half of MIN_HOLD_BEATS.
    const issues = validateChartSemantics(
      chartOf([{ type: 2, num: 0, endNum: 1, block: 0, LPB: 8 }]),
    );

    expect(issues).toHaveLength(1);
    expect(issues[0]?.path).toBe("/notes/0/endNum");
    expect(issues[0]?.keyword).toBe("minimum");
    expect(issues[0]?.message).toContain(`${MIN_HOLD_BEATS} beats`);
  });

  it("accepts a hold of exactly the maximum length", () => {
    // 64 beats at LPB 4 are 256 lines.
    const issues = validateChartSemantics(
      chartOf([{ type: 2, num: 0, endNum: 4 * MAX_HOLD_BEATS, block: 0, LPB: 4 }]),
    );

    expect(issues).toEqual([]);
  });

  it("refuses a hold above the maximum length", () => {
    const issues = validateChartSemantics(
      chartOf([{ type: 2, num: 0, endNum: 4 * MAX_HOLD_BEATS + 4, block: 0, LPB: 4 }]),
    );

    expect(issues).toHaveLength(1);
    expect(issues[0]?.path).toBe("/notes/0/endNum");
    expect(issues[0]?.keyword).toBe("maximum");
    expect(issues[0]?.message).toContain(`${MAX_HOLD_BEATS} beats`);
  });
});
