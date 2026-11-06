import { describe, expect, it } from "vitest";
import { importChartJson } from "../import";
import { validateChart } from "../schema";

/**
 * Contract validation and the issues the editor shows to the user.
 */
const VALID_CHART = {
  name: "Test",
  maxBlock: 8,
  BPM: 120,
  offset: 5,
  notes: [{ type: 1, num: 4, block: 3, LPB: 4 }],
};

describe("chart validation", () => {
  it("accepts a valid chart", () => {
    expect(validateChart(VALID_CHART)).toEqual([]);
  });

  it("accepts unknown top level fields", () => {
    expect(validateChart({ ...VALID_CHART, videoId: "abc" })).toEqual([]);
  });

  it("accepts unknown fields inside the lyrics block", () => {
    expect(validateChart({ ...VALID_CHART, lyrics: { mondai: ["a"], custom: [1] } })).toEqual([]);
  });

  it("points at a missing required field", () => {
    const { BPM: _bpm, ...withoutBpm } = VALID_CHART;

    const issues = validateChart(withoutBpm);

    expect(issues).toHaveLength(1);
    expect(issues[0].keyword).toBe("required");
    expect(issues[0].path).toBe("/BPM");
  });

  it("points at the lane of an out of range note", () => {
    const issues = validateChart({
      ...VALID_CHART,
      notes: [{ type: 1, num: 4, block: 9, LPB: 4 }],
    });

    expect(issues).toHaveLength(1);
    expect(issues[0].keyword).toBe("maximum");
    expect(issues[0].path).toBe("/notes/0/block");
  });

  it("reports a lane count the game does not have", () => {
    const issues = validateChart({ ...VALID_CHART, maxBlock: 9 });

    expect(issues).toHaveLength(1);
    expect(issues[0].keyword).toBe("maximum");
    expect(issues[0].path).toBe("/maxBlock");
  });

  it("reports every problem of a broken chart at once", () => {
    const issues = validateChart({
      ...VALID_CHART,
      BPM: 0,
      notes: [
        { type: 1, num: 4, block: 9, LPB: 4 },
        { type: 2, num: 4, block: 1, LPB: 4 },
      ],
    });

    expect(issues.length).toBeGreaterThanOrEqual(3);
    expect(issues.map((issue) => issue.path)).toContain("/notes/0/block");
    expect(issues.map((issue) => issue.path)).toContain("/notes/1/type");
  });

  it("accepts a chart without notes", () => {
    // The legacy charts of the Unity project carry empty note lists, so the
    // contract has to accept them.
    expect(validateChart({ ...VALID_CHART, notes: [] })).toEqual([]);
  });

  it("accepts a chart with fewer than eight lanes", () => {
    // `ニセモノ.json` carries five lanes.
    expect(validateChart({ ...VALID_CHART, maxBlock: 5 })).toEqual([]);
  });

  it("refuses a chart with more lanes than the game has", () => {
    const issues = validateChart({ ...VALID_CHART, maxBlock: 9 });

    expect(issues).toHaveLength(1);
    expect(issues[0].keyword).toBe("maximum");
    expect(issues[0].path).toBe("/maxBlock");
  });
});

describe("importing broken files", () => {
  it("reports invalid JSON", () => {
    const result = importChartJson("{ not json");

    expect(result.ok).toBe(false);
    if (result.ok) {
      return;
    }

    expect(result.issues).toHaveLength(1);
    expect(result.issues[0].keyword).toBe("parse");
  });

  it("reports the problems of an invalid chart", () => {
    const result = importChartJson(JSON.stringify({ name: "x" }));

    expect(result.ok).toBe(false);
    if (result.ok) {
      return;
    }

    expect(result.issues.length).toBeGreaterThanOrEqual(3);
  });
});
