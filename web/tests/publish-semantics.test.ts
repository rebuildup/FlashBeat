/**
 * Unit test of the semantic layer of the publish request (issue #16, #164).
 *
 * `semanticDiagnostics()` is the layer that the JSON schema cannot express:
 * the note kinds the player understands and the relation between the start
 * and the end of a Hold. Issue #164 added the Hold (type 2), so this test
 * pins that a Tap chart keeps passing, that a Hold chart is accepted, and
 * that a broken Hold is named instead of being silently published.
 */
import assert from "node:assert/strict";
import { describe, it } from "node:test";

import {
  NOTE_TYPE_HOLD,
  NOTE_TYPE_TAP,
  semanticDiagnostics,
  type SongPublishRequest,
} from "../routes/api/v1/_publish-model.ts";

/** A publish request with the given notes and a chart length of 90 seconds. */
function request(
  notes: unknown[],
  overrides: Partial<SongPublishRequest> = {},
): SongPublishRequest {
  return {
    title: "Hold Test",
    artist: "Tester",
    youtubeId: "8JXiXt0D6tw",
    level: 5,
    durationSec: 90,
    chart: {
      name: "Hold Test",
      maxBlock: 8,
      BPM: 120,
      offset: 0,
      notes,
    },
    ...overrides,
  };
}

/** The paths of the diagnostics, in the order they were reported. */
function paths(diagnostics: { path: string }[]): string[] {
  return diagnostics.map((diagnostic) => diagnostic.path);
}

describe("semanticDiagnostics", () => {
  it("accepts a Tap-only chart", () => {
    const diagnostics = semanticDiagnostics(
      request([
        { type: NOTE_TYPE_TAP, num: 4, block: 3, LPB: 4 },
        { type: NOTE_TYPE_TAP, num: 8, block: 4, LPB: 4 },
      ]),
    );

    assert.deepEqual(diagnostics, []);
  });

  it("accepts a chart that mixes Taps and Holds", () => {
    const diagnostics = semanticDiagnostics(
      request([
        { type: NOTE_TYPE_TAP, num: 4, block: 3, LPB: 4 },
        { type: NOTE_TYPE_HOLD, num: 8, block: 5, LPB: 4, endNum: 16 },
      ]),
    );

    assert.deepEqual(diagnostics, []);
  });

  it("accepts a Hold whose end is one line after its start", () => {
    const diagnostics = semanticDiagnostics(
      request([{ type: NOTE_TYPE_HOLD, num: 8, block: 0, LPB: 4, endNum: 9 }]),
    );

    assert.deepEqual(diagnostics, []);
  });

  it("rejects a Hold without an end line and names the field", () => {
    const diagnostics = semanticDiagnostics(
      request([{ type: NOTE_TYPE_HOLD, num: 8, block: 0, LPB: 4 }]),
    );

    assert.deepEqual(paths(diagnostics), ["/chart/notes/0/endNum"]);
    assert.match(diagnostics[0]?.message ?? "", /endNum/);
  });

  it("rejects a Hold whose end is not after its start", () => {
    const diagnostics = semanticDiagnostics(
      request([{ type: NOTE_TYPE_HOLD, num: 8, block: 0, LPB: 4, endNum: 8 }]),
    );

    assert.deepEqual(paths(diagnostics), ["/chart/notes/0/endNum"]);
    assert.match(diagnostics[0]?.message ?? "", /after the start of the hold/);
  });

  it("rejects a Hold whose end is before its start", () => {
    const diagnostics = semanticDiagnostics(
      request([{ type: NOTE_TYPE_HOLD, num: 8, block: 0, LPB: 4, endNum: 4 }]),
    );

    assert.deepEqual(paths(diagnostics), ["/chart/notes/0/endNum"]);
  });

  it("rejects an unknown note kind", () => {
    const diagnostics = semanticDiagnostics(
      request([{ type: 3, num: 8, block: 0, LPB: 4, endNum: 16 }]),
    );

    assert.deepEqual(paths(diagnostics), ["/chart/notes/0/type"]);
    assert.match(diagnostics[0]?.message ?? "", /Tap \(type 1\) or a Hold \(type 2\)/);
  });

  it("counts the end of a Hold against the chart length", () => {
    // BPM 120, LPB 4: endNum 7200 lands at 900s, beyond the 90s durationSec.
    // The request-level rule names the note, the shared semantic rule names
    // the field of the hold that reaches past the end (issue #191: both
    // layers answer, the operator sees both).
    const diagnostics = semanticDiagnostics(
      request([{ type: NOTE_TYPE_HOLD, num: 4, block: 0, LPB: 4, endNum: 7200 }]),
    );

    assert.deepEqual(paths(diagnostics), ["/chart/notes/0", "/chart/notes/0/endNum"]);
    assert.match(diagnostics[0]?.message ?? "", /durationSec/);
    assert.match(diagnostics[1]?.message ?? "", /chart length/);
  });

  it("ignores an end line on a Tap", () => {
    const diagnostics = semanticDiagnostics(
      request([{ type: NOTE_TYPE_TAP, num: 4, block: 0, LPB: 4, endNum: 2 }]),
    );

    assert.deepEqual(diagnostics, []);
  });

  it("names every broken Hold of a chart", () => {
    const diagnostics = semanticDiagnostics(
      request([
        { type: NOTE_TYPE_HOLD, num: 0, block: 0, LPB: 4 },
        { type: NOTE_TYPE_TAP, num: 4, block: 1, LPB: 4 },
        { type: NOTE_TYPE_HOLD, num: 8, block: 2, LPB: 4, endNum: 8 },
      ]),
    );

    assert.deepEqual(paths(diagnostics), ["/chart/notes/0/endNum", "/chart/notes/2/endNum"]);
  });
});
