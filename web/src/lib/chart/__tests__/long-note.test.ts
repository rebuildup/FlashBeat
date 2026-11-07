import { describe, expect, it } from "vitest";
import { exportChartJson } from "../export";
import { importChartJson } from "../import";
import {
  createHold,
  holdEndBeat,
  holdEndLine,
  holdEndSec,
  holdLengthBeats,
  holdLengthLines,
  holdLengthSec,
  holdStartSec,
  holdsOverlap,
  MIN_HOLD_LINES,
} from "../long-note";
import { addNote, createNote, holdCount, setHoldEnd, setNoteKind } from "../notes";
import { validateChart } from "../schema";
import { NOTE_TYPE_HOLD, NOTE_TYPE_TAP } from "../types";
import { readRepositoryChart } from "./repository-charts";

/**
 * Long notes (holds) of issue #164.
 *
 * A hold is a tap note that is held down: `type` becomes 2 and `endNum` says
 * where the hold ends. These tests pin the three things the whole feature
 * rests on: the data model survives a round trip through the JSON, the
 * timing matches the formula the Unity player uses, and a broken hold is
 * refused before it is published.
 */

const VALID_CHART = {
  name: "HoldTest",
  maxBlock: 8,
  BPM: 120,
  offset: 0,
  notes: [{ type: 1, num: 4, block: 3, LPB: 4 }],
};

describe("the hold data model", () => {
  it("creates a hold with an end", () => {
    const hold = createHold(2, 1, 3, 4);

    expect(hold.type).toBe(NOTE_TYPE_HOLD);
    expect(hold.num).toBe(4);
    expect(hold.endNum).toBe(12);
    expect(hold.block).toBe(2);
    expect(hold.LPB).toBe(4);
  });

  it("never creates a hold shorter than one line", () => {
    const hold = createHold(0, 2, 2, 4);

    expect(hold.endNum).toBeGreaterThan(hold.num);
    expect(holdLengthLines(hold)).toBe(MIN_HOLD_LINES);
  });

  it("measures the length in lines, in beats and in seconds", () => {
    const hold = createHold(0, 1, 4, 4);

    expect(holdLengthLines(hold)).toBe(12);
    expect(holdLengthBeats(hold)).toBe(3);
    // 3 beats at 120 BPM are 1.5 seconds.
    expect(holdLengthSec(hold, 120)).toBeCloseTo(1.5, 6);
  });

  it("places the start and the end on the beat grid", () => {
    const hold = createHold(0, 1, 3, 4);

    expect(holdStartSec(hold, 120)).toBeCloseTo(0.5, 6);
    expect(holdEndBeat(hold)).toBeCloseTo(3, 6);
  });

  it("uses the timing formula of the Unity player", () => {
    // `NotesManager.NoteTimeSeconds` computes `60 / BPM * num / LPB`.
    const hold = createHold(0, 2, 4, 8);

    const start = holdStartSec(hold, 174);
    const end = holdEndBeat(hold);

    expect(start).toBeCloseTo((60 / 174) * (16 / 8), 6);
    expect((60 / 174) * end).toBeCloseTo(holdEndSec(hold, 174), 6);
  });

  it("changes the end of a hold without moving its start", () => {
    const hold = createHold(1, 2, 4, 4);

    const longer = setHoldEndEnd(hold, 8);

    expect(longer.num).toBe(hold.num);
    expect(longer.endNum).toBe(32);
  });

  it("does not let a hold end before it starts", () => {
    const hold = createHold(1, 4, 8, 4);

    const shorter = setHoldEndEnd(hold, 0);

    expect(shorter.endNum).toBeGreaterThan(shorter.num);
  });
});

describe("holds in a chart", () => {
  it("counts the holds of a chart", () => {
    const chart = {
      ...VALID_CHART,
      notes: [
        { type: 1, num: 0, block: 0, LPB: 4 },
        { type: 2, num: 4, block: 1, LPB: 4, endNum: 12 },
      ],
    };

    expect(holdCount(chart)).toBe(1);
  });

  it("turns a tap into a hold with the minimum length", () => {
    const chart = { ...VALID_CHART };

    const edited = setNoteKind(chart, 0, NOTE_TYPE_HOLD);

    expect(edited.notes[0].type).toBe(NOTE_TYPE_HOLD);
    expect(edited.notes[0].endNum).toBe(edited.notes[0].num + 1);
  });

  it("turns a hold back into a tap and drops its end", () => {
    const chart = {
      ...VALID_CHART,
      notes: [{ type: 2, num: 4, block: 1, LPB: 4, endNum: 12 }],
    };

    const edited = setNoteKind(chart, 0, NOTE_TYPE_TAP);

    expect(edited.notes[0].type).toBe(NOTE_TYPE_TAP);
    expect(edited.notes[0].endNum).toBeUndefined();
  });

  it("finds two holds that fight over the same lane", () => {
    const first = createHold(3, 0, 4, 4);
    const second = createHold(3, 2, 6, 4);

    expect(holdsOverlap(first, second)).toBe(true);
  });

  it("lets two holds of the same lane touch each other", () => {
    const first = createHold(3, 0, 4, 4);
    const second = createHold(3, 4, 8, 4);

    expect(holdsOverlap(first, second)).toBe(false);
  });

  it("never confuses two lanes", () => {
    const first = createHold(3, 0, 4, 4);
    const second = createHold(4, 2, 6, 4);

    expect(holdsOverlap(first, second)).toBe(false);
  });
});

describe("validating holds", () => {
  it("accepts a valid hold", () => {
    const issues = validateChart({
      ...VALID_CHART,
      notes: [{ type: 2, num: 4, block: 1, LPB: 4, endNum: 12 }],
    });

    expect(issues).toEqual([]);
  });

  it("accepts a chart that mixes taps and holds", () => {
    const issues = validateChart({
      ...VALID_CHART,
      notes: [
        { type: 1, num: 0, block: 0, LPB: 4 },
        { type: 2, num: 4, block: 1, LPB: 4, endNum: 12 },
        { type: 1, num: 16, block: 2, LPB: 4 },
      ],
    });

    expect(issues).toEqual([]);
  });

  it("refuses a hold without an end", () => {
    const issues = validateChart({
      ...VALID_CHART,
      notes: [{ type: 2, num: 4, block: 1, LPB: 4 }],
    });

    expect(issues.length).toBeGreaterThanOrEqual(1);
    expect(issues[0].path).toBe("/notes/0/endNum");
  });

  it("refuses a hold that ends before it starts", () => {
    const issues = validateChart({
      ...VALID_CHART,
      notes: [{ type: 2, num: 12, block: 1, LPB: 4, endNum: 4 }],
    });

    expect(issues.length).toBeGreaterThanOrEqual(1);
    expect(issues[0].path).toBe("/notes/0/endNum");
    expect(issues[0].message).toContain("after the start");
  });

  it("refuses two holds that fight over the same lane", () => {
    const issues = validateChart({
      ...VALID_CHART,
      notes: [
        { type: 2, num: 0, block: 3, LPB: 4, endNum: 16 },
        { type: 2, num: 8, block: 3, LPB: 4, endNum: 24 },
      ],
    });

    expect(issues.length).toBeGreaterThanOrEqual(1);
    expect(issues[0].path).toBe("/notes/1/num");
    expect(issues[0].message).toContain("overlaps");
  });

  it("accepts two holds of different lanes that overlap in time", () => {
    const issues = validateChart({
      ...VALID_CHART,
      notes: [
        { type: 2, num: 0, block: 3, LPB: 4, endNum: 16 },
        { type: 2, num: 8, block: 4, LPB: 4, endNum: 24 },
      ],
    });

    expect(issues).toEqual([]);
  });

  it("refuses a hold that reaches past the end of the song", () => {
    const issues = validateChart(
      {
        ...VALID_CHART,
        notes: [{ type: 2, num: 0, block: 1, LPB: 4, endNum: 400 }],
      },
      { durationSec: 10 },
    );

    expect(issues.length).toBeGreaterThanOrEqual(1);
    expect(issues[0].path).toBe("/notes/0/endNum");
    expect(issues[0].message).toContain("beyond");
  });

  it("still accepts the tap charts of the Unity project", () => {
    const imported = importChartJson(readRepositoryChart("Who.json"));

    expect(imported.ok).toBe(true);
    if (!imported.ok) {
      return;
    }

    expect(validateChart(imported.chart)).toEqual([]);
  });
});

describe("the JSON round trip of a hold", () => {
  it("keeps the hold through import, export and import", () => {
    const chart = {
      ...VALID_CHART,
      notes: [
        { type: 1, num: 0, block: 0, LPB: 4 },
        { type: 2, num: 4, block: 1, LPB: 4, endNum: 12 },
      ],
    };

    const written = exportChartJson(chart);
    const reimported = importChartJson(written);

    expect(reimported.ok).toBe(true);
    if (!reimported.ok) {
      return;
    }

    expect(reimported.chart.notes).toEqual(chart.notes);
    expect(exportChartJson(reimported.chart)).toBe(written);
  });

  it("writes the end of a hold into the JSON", () => {
    const chart = addNote(
      VALID_CHART,
      createHold(1, 1, 3, 4),
    );

    const written = exportChartJson(chart);

    expect(written).toContain('"type":2');
    expect(written).toContain('"endNum":12');
  });

  it("keeps the hold when a real chart is edited and written back", () => {
    const imported = importChartJson(readRepositoryChart("Who.json"));
    expect(imported.ok).toBe(true);
    if (!imported.ok) {
      return;
    }

    const edited = addNote(imported.chart, createHold(5, 0, 4, 4));

    const written = exportChartJson(edited);
    const reimported = importChartJson(written);

    expect(reimported.ok).toBe(true);
    if (!reimported.ok) {
      return;
    }

    expect(reimported.chart.notes).toHaveLength(imported.chart.notes.length + 1);
    expect(reimported.chart.notes[reimported.chart.notes.length - 1]).toEqual({
      type: 2,
      num: 0,
      block: 5,
      LPB: 4,
      endNum: 16,
    });
    // Every note that was not edited keeps its exact shape.
    expect(reimported.chart.notes.slice(0, imported.chart.notes.length)).toEqual(
      imported.chart.notes,
    );
  });

  it("keeps the lyrics and the unknown fields next to a hold", () => {
    const chart = {
      ...VALID_CHART,
      videoId: "dQw4w9WgXcQ",
      lyrics: { mondai: ["はい"], startTime: [0] },
      notes: [{ type: 2, num: 0, block: 0, LPB: 4, endNum: 8 }],
    };

    const reimported = importChartJson(exportChartJson(chart));

    expect(reimported.ok).toBe(true);
    if (!reimported.ok) {
      return;
    }

    expect(reimported.chart.videoId).toBe("dQw4w9WgXcQ");
    expect(reimported.chart.lyrics).toEqual(chart.lyrics);
    expect(reimported.chart.notes).toEqual(chart.notes);
  });

  it("leaves the tap charts of the Unity project unchanged", () => {
    const original = readRepositoryChart("Who.json");

    const imported = importChartJson(original);
    expect(imported.ok).toBe(true);
    if (!imported.ok) {
      return;
    }

    // The notes are written back exactly as they came in: no field is added,
    // no field is dropped and the order is kept. (The whole file is not
    // compared byte for byte, because the float precision of the lyrics
    // block is lost by `JSON.parse` - that is a property of the platform and
    // not of the hold feature.)
    const written = JSON.parse(exportChartJson(imported.chart)) as { notes: unknown };
    const expected = JSON.parse(original) as { notes: unknown };

    expect(written.notes).toEqual(expected.notes);
    expect(JSON.stringify(written.notes)).toBe(JSON.stringify(expected.notes));

    // Every note of the repository is a tap: the hold feature adds nothing
    // to the charts that ship with the game.
    for (const note of imported.chart.notes) {
      expect(note.type).toBe(NOTE_TYPE_TAP);
      expect(note.endNum).toBeUndefined();
    }
  });
});

/**
 * Change the end of a hold to a beat position. The chart level helper lives
 * in `notes.ts`; this one works on a single note.
 */
function setHoldEndEnd(note: ReturnType<typeof createHold>, endBeat: number): ReturnType<typeof createHold> {
  return { ...note, endNum: Math.max(note.num + MIN_HOLD_LINES, Math.round(endBeat * note.LPB)) };
}
