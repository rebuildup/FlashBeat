/**
 * Unity ↔ Web chart-timing parity contract test (Issue #612).
 *
 * Both sides verify the same shared vectors of
 * `contracts/parity/chart-timing.json`:
 *
 *   - `seconds` / `endSeconds` are the float32 results of the Unity
 *     operation order (`NotesManager.NoteTimeSeconds`,
 *     `LongNote.EndSeconds`). The Web side reproduces them exactly with the
 *     same `Math.fround` step order; `noteTimeSec` / `holdEndSec` of
 *     `src/lib/chart` are the double-precision equivalents and must agree
 *     within float32 rounding.
 *   - The Unity EditMode suite (`Assets/Tests/Editor/ParityFixtureTests.cs`)
 *     checks the same file against the C# implementation.
 */
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { describe, it } from "node:test";

import { holdEndSec } from "../src/lib/chart/long-note.ts";
import { noteTimeSec } from "../src/lib/chart/timing.ts";
import { NOTE_TYPE_HOLD, NOTE_TYPE_TAP, type ChartNote } from "../src/lib/chart/types.ts";

interface TimingCase {
  name: string;
  bpm: number;
  num: number;
  lpb: number;
  endNum?: number;
  seconds: number;
  endSeconds?: number;
}

const fixture = JSON.parse(
  readFileSync(new URL("../../contracts/parity/chart-timing.json", import.meta.url), "utf8"),
) as { cases: TimingCase[] };

const fround = Math.fround;

/**
 * `NotesManager.NoteTimeSeconds` in single precision:
 * `kankaku = 60 / (bpm * LPB)`, `beatSec = kankaku * LPB`,
 * `time = beatSec * num / LPB` — every step a C# `float` operation.
 */
function unityNoteTimeSeconds(bpm: number, num: number, lpb: number): number {
  const kankaku = fround(60 / fround(bpm * lpb));
  const beatSec = fround(kankaku * lpb);
  return fround(fround(beatSec * num) / lpb);
}

/**
 * `LongNote.EndSeconds` in single precision: `kankaku * endNum`.
 * Note the different operation order versus the start time — pinned on
 * purpose, the two can differ by one float32 ulp.
 */
function unityHoldEndSeconds(bpm: number, endNum: number, lpb: number): number {
  const kankaku = fround(60 / fround(bpm * lpb));
  return fround(kankaku * endNum);
}

/** Relative tolerance covering float32 vs float64 rounding (~1e-7). */
function withinFloat32Rounding(actual: number, expected: number): boolean {
  return Math.abs(actual - expected) <= Math.max(1e-9, Math.abs(expected) * 1e-6);
}

function noteOf(c: TimingCase): ChartNote {
  return {
    type: c.endNum === undefined ? NOTE_TYPE_TAP : NOTE_TYPE_HOLD,
    num: c.num,
    block: 0,
    LPB: c.lpb,
    endNum: c.endNum,
  };
}

describe("chart timing parity vectors (contracts/parity/chart-timing.json)", () => {
  it("has cases to check", () => {
    assert.ok(fixture.cases.length > 0);
  });

  for (const c of fixture.cases) {
    it(`${c.name}: start time matches`, () => {
      assert.equal(unityNoteTimeSeconds(c.bpm, c.num, c.lpb), c.seconds);
      assert.ok(
        withinFloat32Rounding(noteTimeSec(noteOf(c), c.bpm), c.seconds),
        `noteTimeSec ${noteTimeSec(noteOf(c), c.bpm)} vs ${c.seconds}`,
      );
    });

    if (c.endNum !== undefined) {
      it(`${c.name}: hold end time matches`, () => {
        assert.equal(unityHoldEndSeconds(c.bpm, c.endNum!, c.lpb), c.endSeconds);
        assert.ok(
          withinFloat32Rounding(holdEndSec(noteOf(c), c.bpm), c.endSeconds!),
          `holdEndSec ${holdEndSec(noteOf(c), c.bpm)} vs ${c.endSeconds}`,
        );
      });
    }
  }
});
