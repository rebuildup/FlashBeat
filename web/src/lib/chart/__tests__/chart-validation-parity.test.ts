/**
 * Parity between the Web studio and the publish API (issue #191).
 *
 * The two sides used to have their own semantic rules, so the same chart
 * could be accepted by the API and rejected by the editor ("Published chart
 * failed validation"), and the editor's overlap check compared raw line
 * numbers instead of beats and missed overlapping holds whose `LPB` differed.
 *
 * This test runs **the same fixtures** through both sides and pins that they
 * answer the same way.
 */
import { describe, expect, it } from "vitest";

import { validateChartSemantics } from "../validate";
import { importChartJson } from "../import";
import type { Chart } from "../types";

/**
 * The publish API's semantic layer. It lives in `routes/api/v1/_publish-model.ts`
 * and is imported here on purpose: the point of the test is that the two
 * implementations cannot drift apart.
 */
import {
  semanticDiagnostics,
  sharedSemanticDiagnostics,
  type SongPublishRequest,
} from "../../../../routes/api/v1/_publish-model.ts";

/** A note of the legacy chart format. */
interface Note {
  type: number;
  num: number;
  block: number;
  LPB: number;
  endNum?: number;
}

/** One fixture: a chart plus the length of the song it belongs to. */
interface Fixture {
  label: string;
  chart: Chart;
  durationSec: number;
}

/** Wrap a note list into a chart. */
function chartOf(notes: Note[], overrides: Partial<Chart> = {}): Chart {
  return {
    name: "Parity",
    maxBlock: 8,
    BPM: 120,
    offset: 0,
    notes: notes as Chart["notes"],
    ...overrides,
  };
}

/** Wrap a chart into a publish request. */
function requestOf(fixture: Fixture): SongPublishRequest {
  return {
    title: "Parity",
    artist: "Tester",
    youtubeId: "8JXiXt0D6tw",
    level: 5,
    durationSec: fixture.durationSec,
    chart: fixture.chart as unknown as Record<string, unknown>,
  };
}

/**
 * The verdict of the Web side: the JSON pointers of the semantic issues.
 *
 * Only the **semantic** layer is compared. The structural layer (the JSON
 * schema) is not part of the comparison: the API validates the *request*
 * against the frozen contract with a different engine (`@cfworker/json-schema`
 * instead of Ajv, because the Workers runtime forbids `new Function`), and
 * the two engines report a missing property differently (Ajv names the
 * parent object as well). Only the *semantic* layer is shared, and that is
 * exactly what issue #191 is about.
 */
function webVerdict(fixture: Fixture): string[] {
  return validateChartSemantics(fixture.chart, { durationSec: fixture.durationSec })
    .map((issue) => issue.path)
    .sort();
}

/**
 * The verdict of the API side: the JSON pointers of the **shared** semantic
 * diagnostics.
 *
 * `sharedSemanticDiagnostics` is the part of the publish validation that the
 * studio shares (the semantic layer of the chart validation). The
 * request-level rules of `semanticDiagnostics` (the level range, the empty
 * chart, the note kinds, the lane range) belong to the publish *request* and
 * have no counterpart in the editor, which edits a chart and not a request.
 */
function apiVerdict(fixture: Fixture): string[] {
  return sharedSemanticDiagnostics(requestOf(fixture))
    .map((diagnostic) => diagnostic.path)
    .sort();
}

/**
 * The verdict of the whole publish validation, request rules included.
 *
 * It is used for the round-trip check: a chart the API refuses can never
 * reach the database, so the studio never has to read it back.
 */
function apiFullVerdict(fixture: Fixture): string[] {
  return semanticDiagnostics(requestOf(fixture))
    .map((diagnostic) => diagnostic.path)
    .sort();
}

/**
 * The fixtures both sides have to judge the same way.
 *
 * They cover the two cases of issue #191 (the API accepts what the editor
 * rejects, and the editor misses an overlap across resolutions) plus the
 * boundary cases: the minimum and the maximum hold length, a hold that only
 * touches another hold, and a hold of a different resolution that does not
 * overlap.
 */
const FIXTURES: Fixture[] = [
  {
    label: "a plain Tap chart",
    chart: chartOf([{ type: 1, num: 4, block: 0, LPB: 4 }]),
    durationSec: 90,
  },
  {
    label: "a Tap and a Hold",
    chart: chartOf([
      { type: 1, num: 4, block: 3, LPB: 4 },
      { type: 2, num: 8, block: 5, LPB: 4, endNum: 16 },
    ]),
    durationSec: 90,
  },
  {
    // Issue #191, case 1: the API accepted this chart, the editor refused it
    // ("overlaps another hold on lane 0").
    label: "two overlapping Holds of the same lane",
    chart: chartOf([
      { type: 2, num: 0, endNum: 8, block: 0, LPB: 4 },
      { type: 2, num: 4, endNum: 12, block: 0, LPB: 4 },
    ]),
    durationSec: 10,
  },
  {
    // Issue #191, case 2: the editor missed this overlap, because it
    // compared the raw line numbers instead of the beats.
    label: "two overlapping Holds with a mixed LPB",
    chart: chartOf([
      { type: 2, num: 4, endNum: 8, block: 0, LPB: 4 },
      { type: 2, num: 8, endNum: 16, block: 0, LPB: 8 },
    ]),
    durationSec: 90,
  },
  {
    // The same beats, but on a different lane: no conflict.
    label: "two overlapping Holds on different lanes",
    chart: chartOf([
      { type: 2, num: 0, endNum: 8, block: 0, LPB: 4 },
      { type: 2, num: 4, endNum: 12, block: 1, LPB: 4 },
    ]),
    durationSec: 90,
  },
  {
    // Boundary contact: the second hold starts exactly where the first one
    // ends. The player releases and presses again, which is playable.
    label: "two Holds that only touch",
    chart: chartOf([
      { type: 2, num: 0, endNum: 8, block: 0, LPB: 4 },
      { type: 2, num: 8, endNum: 16, block: 0, LPB: 4 },
    ]),
    durationSec: 90,
  },
  {
    // A different resolution, but the beats do not overlap.
    label: "two Holds of a mixed LPB that do not overlap",
    chart: chartOf([
      { type: 2, num: 0, endNum: 4, block: 0, LPB: 4 },
      { type: 2, num: 8, endNum: 16, block: 0, LPB: 8 },
    ]),
    durationSec: 90,
  },
  {
    // The shortest hold that is still allowed (0.25 beats = 1 line at LPB 4).
    label: "a Hold of exactly the minimum length",
    chart: chartOf([{ type: 2, num: 0, endNum: 1, block: 0, LPB: 4 }]),
    durationSec: 90,
  },
  {
    label: "a Hold below the minimum length",
    chart: chartOf([
      { type: 2, num: 0, endNum: 1, block: 0, LPB: 8 },
      { type: 2, num: 8, endNum: 17, block: 1, LPB: 8 },
    ]),
    durationSec: 90,
  },
  {
    // 64 beats at LPB 4 is 256 lines.
    label: "a Hold of exactly the maximum length",
    chart: chartOf([{ type: 2, num: 0, endNum: 256, block: 0, LPB: 4 }]),
    durationSec: 200,
  },
  {
    label: "a Hold above the maximum length",
    chart: chartOf([{ type: 2, num: 0, endNum: 260, block: 0, LPB: 4 }]),
    durationSec: 200,
  },
  {
    label: "a Hold without an end line",
    chart: chartOf([{ type: 2, num: 8, block: 0, LPB: 4 }]),
    durationSec: 90,
  },
  {
    label: "a Hold whose end is not after its start",
    chart: chartOf([{ type: 2, num: 8, block: 0, LPB: 4, endNum: 8 }]),
    durationSec: 90,
  },
  {
    label: "a Hold that ends before it starts",
    chart: chartOf([{ type: 2, num: 8, block: 0, LPB: 4, endNum: 4 }]),
    durationSec: 90,
  },
  {
    label: "a Hold that ends past the length of the song",
    chart: chartOf([{ type: 2, num: 0, endNum: 720, block: 0, LPB: 4 }]),
    durationSec: 10,
  },
];

describe("the Web studio and the publish API judge a chart the same way", () => {
  for (const fixture of FIXTURES) {
    it(`agrees on ${fixture.label}`, () => {
      const web = webVerdict(fixture);
      const api = apiVerdict(fixture);

      // The API answers in request-relative paths (`/chart/notes/…`), the
      // editor in chart-relative ones (`/notes/…`). They describe the same
      // offending item.
      expect(api).toEqual(web.map((path) => `/chart${path}`));
    });
  }

  it("refuses the overlapping Holds of the issue report on both sides", () => {
    const fixture = FIXTURES[2]!;

    expect(webVerdict(fixture)).toContain("/notes/1/num");
    expect(apiVerdict(fixture)).toContain("/chart/notes/1/num");
  });

  it("refuses the mixed LPB overlap of the issue report on both sides", () => {
    const fixture = FIXTURES[3]!;

    expect(webVerdict(fixture)).toContain("/notes/1/num");
    expect(apiVerdict(fixture)).toContain("/chart/notes/1/num");
  });

  it("accepts a chart both sides accept", () => {
    const fixture = FIXTURES[0]!;

    expect(webVerdict(fixture)).toEqual([]);
    expect(apiVerdict(fixture)).toEqual([]);
  });
});

describe("a chart the API accepted can be read back in the Web studio", () => {
  for (const fixture of FIXTURES) {
    it(`round-trips ${fixture.label}`, () => {
      const accepted = apiFullVerdict(fixture).length === 0;
      const imported = importChartJson(JSON.stringify(fixture.chart));

      expect(imported.ok).toBe(accepted);
    });
  }

  it("reads a chart that was published before the rules were unified", () => {
    // The chart of the issue report: the API used to accept it, the editor
    // used to refuse it. It is still readable in the lenient mode, so it can
    // be repaired instead of being dropped (issue #191, requirement R7).
    const chart = chartOf([
      { type: 2, num: 0, endNum: 8, block: 0, LPB: 4 },
      { type: 2, num: 4, endNum: 12, block: 0, LPB: 4 },
    ]);

    const strict = importChartJson(JSON.stringify(chart));
    expect(strict.ok).toBe(false);

    const lenient = importChartJson(JSON.stringify(chart), "lenient");
    expect(lenient.ok).toBe(true);
    if (!lenient.ok) {
      return;
    }
    expect(lenient.issues?.map((issue) => issue.keyword)).toContain("warning.overlap");
    // The chart itself is not rewritten: the offending notes are still there.
    expect(lenient.chart.notes).toHaveLength(2);
  });
});
