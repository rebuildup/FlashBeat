import { describe, expect, it } from "vitest";
import {
  beatDuration,
  snapLine,
  snapStepLines,
  beatToTime,
  DEFAULT_SNAP_DIVISION,
  isSnapableBpm,
  isSnapEnabled,
  snapBeat,
  snapBeatExclusive,
  snapDivisionLabel,
  snapDuration,
  snapStepIndex,
  snapTime,
  snapTimeExclusive,
  SNAP_DIVISIONS,
  SNAP_SETTINGS,
  stepIndexToBeat,
  timeToBeat,
} from "../snap";

/**
 * Snapping onto the BPM grid (issue #180).
 *
 * The grid follows the tempo of the chart: one beat lasts `60 / BPM`
 * seconds, exactly like the Unity player computes it. The rounding happens
 * on the index of the step, so a value that sits across a bar or a beat
 * boundary lands on the same step no matter which side it came from.
 *
 * At 120 BPM one beat lasts 0.5s, so a 1/4 step lasts 0.125s and a 1/16
 * step lasts 0.03125s. Those numbers anchor the expectations below.
 */
describe("the BPM grid", () => {
  it("offers the divisions off, 1, 2, 4, 8 and 16", () => {
    expect(SNAP_SETTINGS).toEqual(["off", 1, 2, 4, 8, 16]);
    expect(SNAP_DIVISIONS).toEqual([1, 2, 4, 8, 16]);
  });

  it("starts with the grid off, so the behaviour of the editor is unchanged", () => {
    expect(DEFAULT_SNAP_DIVISION).toBe("off");
    expect(snapDivisionLabel(DEFAULT_SNAP_DIVISION)).toBe("off");
  });

  it("labels the divisions the way the toolbar shows them", () => {
    expect(snapDivisionLabel("off")).toBe("off");
    expect(snapDivisionLabel(1)).toBe("1/1");
    expect(snapDivisionLabel(2)).toBe("1/2");
    expect(snapDivisionLabel(4)).toBe("1/4");
    expect(snapDivisionLabel(8)).toBe("1/8");
    expect(snapDivisionLabel(16)).toBe("1/16");
  });

  it("knows when the grid is on", () => {
    expect(isSnapEnabled("off")).toBe(false);
    expect(isSnapEnabled(1)).toBe(true);
    expect(isSnapEnabled(16)).toBe(true);
  });

  it("measures a beat the way the Unity player does", () => {
    expect(beatDuration(120)).toBe(0.5);
    expect(beatDuration(60)).toBe(1);
    expect(beatDuration(240)).toBe(0.25);
    expect(beatDuration(95)).toBeCloseTo(60 / 95, 12);
  });

  it("refuses to measure a beat without a tempo", () => {
    expect(beatDuration(0)).toBe(0);
    expect(beatDuration(-120)).toBe(0);
    expect(beatDuration(Number.NaN)).toBe(0);
    expect(isSnapableBpm(0)).toBe(false);
    expect(isSnapableBpm(-1)).toBe(false);
    expect(isSnapableBpm(Number.NaN)).toBe(false);
    expect(isSnapableBpm(120)).toBe(true);
  });

  it("measures a step of the grid", () => {
    expect(snapDuration(120, 1)).toBe(0.5);
    expect(snapDuration(120, 2)).toBe(0.25);
    expect(snapDuration(120, 4)).toBe(0.125);
    expect(snapDuration(120, 8)).toBe(0.0625);
    expect(snapDuration(120, 16)).toBe(0.03125);
  });

  it("measures a step of the grid at another tempo", () => {
    expect(snapDuration(240, 4)).toBe(0.0625);
    expect(snapDuration(60, 16)).toBe(0.0625);
  });
});

describe("snapping a time onto the grid", () => {
  it("keeps a time that already sits on the grid", () => {
    expect(snapTime(0, 120, 1)).toBe(0);
    expect(snapTime(0.5, 120, 1)).toBe(0.5);
    expect(snapTime(0.5, 120, 2)).toBe(0.5);
    expect(snapTime(0.25, 120, 4)).toBe(0.25);
    expect(snapTime(0.125, 120, 8)).toBe(0.125);
    expect(snapTime(0.0625, 120, 16)).toBe(0.0625);
  });

  it("moves a time between two steps to the nearer one", () => {
    // 0.3s is 0.6 beats at 120 BPM: nearer to the half beat (0.25s) than to
    // the whole beat (0.5s).
    expect(snapTime(0.3, 120, 2)).toBeCloseTo(0.25, 12);
    // 0.2s is 0.4 beats: nearer to the half beat (0.25s) than to the whole
    // beat (0s).
    expect(snapTime(0.2, 120, 2)).toBeCloseTo(0.25, 12);
    // 0.6s is 1.2 beats: nearer to the beat (0.5s) than to the half beat
    // after it (0.75s).
    expect(snapTime(0.6, 120, 2)).toBeCloseTo(0.5, 12);
  });

  it("rounds a tie up", () => {
    // Exactly between 0.25s and 0.5s at 120 BPM.
    expect(snapTime(0.375, 120, 2)).toBeCloseTo(0.5, 12);
    expect(snapStepIndex(0.5, 2)).toBe(1);
    expect(snapStepIndex(1.5, 2)).toBe(3);
  });

  it("snaps onto the start of the song", () => {
    // 0.02s is 0.04 beats: the nearest 1/4 step is the start of the song.
    expect(snapTime(0.02, 120, 4)).toBe(0);
    expect(snapTime(0.02, 120, 2)).toBe(0);
    // 0.02s is 0.04 beats: the nearest 1/16 step is the start of the song
    // as well (the next one is 0.03125s away).
    expect(snapTime(0.015, 120, 16)).toBe(0);
  });

  it("snaps a time before the song onto the grid behind it", () => {
    // -0.1s is -0.2 beats: the nearest 1/4 step is -0.125s.
    expect(snapTime(-0.1, 120, 4)).toBeCloseTo(-0.125, 12);
    // -0.05s is -0.1 beats: the nearest 1/4 step is the start of the song.
    expect(snapTime(-0.05, 120, 4)).toBeCloseTo(0, 12);
  });

  it("leaves the time alone when the grid is off", () => {
    expect(snapTime(0.31, 120, "off")).toBe(0.31);
    expect(snapTime(0, 120, "off")).toBe(0);
  });

  it("leaves the time alone when the chart has no tempo", () => {
    expect(snapTime(0.31, 0, 4)).toBe(0.31);
    expect(snapTime(0.31, Number.NaN, 4)).toBe(0.31);
  });
});

describe("snapping across bars and beats", () => {
  it("snaps a value that sits on the third beat of a 4/4 bar", () => {
    // 120 BPM, 4/4: the third beat starts at 1.0s.
    expect(snapTime(1.0, 120, 4)).toBe(1);
    expect(snapTime(1.02, 120, 4)).toBe(1);
    // 1.4s is 2.8 beats: nearer to 2.75 beats (1.375s) than to 3 beats.
    expect(snapTime(1.4, 120, 4)).toBeCloseTo(1.375, 12);
  });

  it("snaps the same way on both sides of a bar line", () => {
    // The bar line of the second bar is at 2.0s at 120 BPM.
    expect(snapTime(1.98, 120, 8)).toBeCloseTo(2, 12);
    expect(snapTime(2.02, 120, 8)).toBeCloseTo(2, 12);
  });

  it("snaps a value that sits on the fourth beat of a 4/4 bar", () => {
    // 1.5s is the last beat of the first bar at 120 BPM.
    expect(snapTime(1.49, 120, 4)).toBeCloseTo(1.5, 12);
    expect(snapTime(1.51, 120, 4)).toBeCloseTo(1.5, 12);
  });

  it("keeps the step index of a whole song", () => {
    // 32 beats at 120 BPM are 16 seconds.
    expect(snapTime(16, 120, 4)).toBe(16);
    expect(snapStepIndex(timeToBeat(16, 120), 4)).toBe(128);
    expect(stepIndexToBeat(128, 4)).toBe(32);
  });

  it("snaps a beat position the same way as a time", () => {
    expect(snapBeat(2.4, 4)).toBe(2.5);
    expect(snapBeat(2.4, 8)).toBe(2.375);
    expect(snapBeat(2.4, "off")).toBe(2.4);
  });

  it("turns a beat into a time and back", () => {
    expect(beatToTime(3, 120)).toBe(1.5);
    expect(timeToBeat(1.5, 120)).toBe(3);
  });
});

describe("snapping exclusively", () => {
  it("keeps a time that already sits on the grid", () => {
    expect(snapTimeExclusive(1, 120, 4)).toBe(1);
    expect(snapTimeExclusive(0.5, 120, 4)).toBe(0.5);
  });

  it("never moves a time back onto the previous step", () => {
    // 0.99s is 1.98 beats: the nearest 1/4 step is 2.0 beats, which is ahead.
    expect(snapTimeExclusive(0.99, 120, 4)).toBeCloseTo(1, 12);
    // 1.01s is 2.02 beats: the nearest 1/4 step is 2.0 beats, behind the
    // value, so the next one (2.25 beats) is used instead.
    expect(snapTimeExclusive(1.01, 120, 4)).toBeCloseTo(1.125, 12);
  });

  it("snaps a beat exclusively as well", () => {
    expect(snapBeatExclusive(2.1, 4)).toBe(2.25);
    expect(snapBeatExclusive(2.0, 4)).toBe(2);
    expect(snapBeatExclusive(2.1, "off")).toBe(2.1);
  });

  it("keeps the order of the end of a hold", () => {
    const start = 1;
    const end = snapTimeExclusive(1.01, 120, 4);
    expect(end).toBeGreaterThan(start);
  });

  it("leaves the time alone when the grid is off", () => {
    expect(snapTimeExclusive(1.01, 120, "off")).toBe(1.01);
  });
});

describe("the grid follows the tempo", () => {
  it("places the same beat on a different time when the tempo changes", () => {
    // The second beat of a 120 BPM song is at 1.0s, of a 240 BPM song at
    // 0.5s. Both sit exactly on the 1/4 grid.
    expect(snapTime(1, 120, 4)).toBe(1);
    expect(snapTime(0.5, 240, 4)).toBe(0.5);
  });

  it("moves a time that is off the grid to the grid of the new tempo", () => {
    // 0.55s is 1.1 beats at 120 BPM, so the nearest 1/4 step is the beat
    // itself (0.5s). At 240 BPM the same time is 2.2 beats, and the nearest
    // 1/4 step is 2.25 beats (0.5625s). The grid follows the tempo.
    expect(snapTime(0.55, 120, 4)).toBeCloseTo(0.5, 12);
    expect(snapTime(0.55, 240, 4)).toBeCloseTo(0.5625, 12);
  });

  it("keeps the beat of a note when the tempo changes", () => {
    // A note on the second beat of a 120 BPM song sits at 0.5s; the same
    // beat of a 240 BPM song sits at 0.25s.
    expect(snapTime(0.25, 240, 4)).toBe(0.25);
    expect(snapTime(0.26, 240, 4)).toBe(0.25);
    expect(snapTime(0.26, 120, 4)).toBe(0.25);
  });

  it("snaps the same beat to the same step at every tempo", () => {
    for (const bpm of [60, 95, 120, 174, 240]) {
      const beat = 3.5;
      const time = beatDuration(bpm) * beat;
      expect(snapTime(time, bpm, 4)).toBeCloseTo(beatDuration(bpm) * 3.5, 12);
      expect(snapBeat(timeToBeat(time, bpm), 4)).toBeCloseTo(3.5, 12);
    }
  });

  it("snaps the same beat of a slower tempo as well", () => {
    // 3.5 beats are 3.5s at 60 BPM and 1.4s at 120 BPM.
    expect(snapTime(3.5, 60, 4)).toBe(3.5);
    expect(snapTime(1.4, 120, 4)).toBe(1.375);
  });
});

/**
 * The grid on the level of the lines a note is written in.
 *
 * A note stores its position as a line index and the number of lines per
 * beat (`LPB`). The grid of the toolbar says how many steps fit into a
 * beat, so the step of the grid is a number of lines.
 */
describe("snapping a line onto the grid", () => {
  it("measures the step of the grid in lines", () => {
    expect(snapStepLines(16, 1)).toBe(16);
    expect(snapStepLines(16, 2)).toBe(8);
    expect(snapStepLines(16, 4)).toBe(4);
    expect(snapStepLines(16, 8)).toBe(2);
    expect(snapStepLines(16, 16)).toBe(1);
  });

  it("keeps the lines of the note when the grid is finer than the note", () => {
    // A note of 8 lines per beat cannot carry a 1/16 grid: the line of the
    // note is the finest step it can stand on.
    expect(snapStepLines(8, 16)).toBe(1);
  });

  it("puts the line on the step of the grid", () => {
    // 5 lines of 16 lines per beat is the beat 0.3125: the quarter beat grid
    // puts it on the line 4 (the beat 0.25).
    expect(snapLine(5, 16, 4)).toBe(4);
    // The eighth beat grid has a step of two lines: the line 5 sits exactly
    // between the lines 4 and 6, the tie goes up.
    expect(snapLine(5, 16, 8)).toBe(6);
    // The whole beat grid puts it on the line 0.
    expect(snapLine(5, 16, 1)).toBe(0);
    // The sixteenth grid keeps the line where it is.
    expect(snapLine(5, 16, 16)).toBe(5);
  });

  it("keeps the line where it is when the grid is off", () => {
    expect(snapLine(5, 16, "off")).toBe(5);
    expect(snapLine(5.4, 16, "off")).toBe(5);
  });
});
