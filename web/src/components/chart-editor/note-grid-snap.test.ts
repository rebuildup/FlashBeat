// @vitest-environment jsdom
import { describe, expect, it } from "vitest";
import { snappedBeat, snappedHoldEndBeat } from "./note-grid";
import { parseSnapDivision } from "./editor-toolbar";
import { getSnapDivision, setSnapDivision } from "./snap-store";

/**
 * Issue #180 — the grid the notes are placed on.
 *
 * The grid is measured in beats (one beat is `beatHeight` pixels tall), the
 * division says how many steps fit into a beat. These tests check the pure
 * placement helpers of the grid and the value the toolbar hands back, so the
 * behaviour can be reasoned about without a canvas (Konva needs one).
 */
describe("where a note lands on the grid", () => {
  it("places a note on the nearest step of the division", () => {
    // 40px on a 40px beat is beat 1: the whole beat, the half beat and the
    // quarter beat all land on a step of their own grid.
    expect(snappedBeat(40, 40, 1)).toBe(1);
    expect(snappedBeat(40, 40, 2)).toBe(1);
    expect(snappedBeat(40, 40, 4)).toBe(1);
  });

  it("rounds to the nearer step and ties go up", () => {
    // 55px is beat 1.375: nearer to 1.5 than to 1.
    expect(snappedBeat(55, 40, 2)).toBe(1.5);
    // 45px is beat 1.125: nearer to 1 than to 1.5.
    expect(snappedBeat(45, 40, 2)).toBe(1);
    // Exactly in the middle (beat 1.25) goes up to 1.5.
    expect(snappedBeat(50, 40, 2)).toBe(1.5);
  });

  it("snaps a note onto the finer steps of the grid", () => {
    // 5px on a 40px beat is beat 0.125: the eighth and the sixteenth of a
    // beat have a step there, the quarter beat ties between 0 and 0.25 and
    // goes up.
    expect(snappedBeat(5, 40, 4)).toBe(0.25);
    expect(snappedBeat(5, 40, 8)).toBe(0.125);
    expect(snappedBeat(5, 40, 16)).toBe(0.125);
  });

  it("leaves the note where it is when the grid is off", () => {
    expect(snappedBeat(55, 40, "off")).toBeCloseTo(1.375, 12);
  });

  it("never places a note before the start of the song", () => {
    expect(snappedBeat(-10, 40, 4)).toBe(0);
  });

  it("ends a hold on the grid, but never before the hold starts", () => {
    // The end is dragged a hair past the start: it snaps up to the next
    // step instead of back behind the start.
    expect(snappedHoldEndBeat(41, 40, 4, 1)).toBe(1.25);
    // An end that is dragged far below the start lands on a later step.
    expect(snappedHoldEndBeat(100, 40, 4, 1)).toBe(2.5);
    // An end that stays above the start keeps its place.
    expect(snappedHoldEndBeat(60, 40, 4, 1)).toBe(1.5);
  });

  it("never lets the end of a hold fall behind its start", () => {
    // The handle is dragged onto the start of the hold: the end stays.
    expect(snappedHoldEndBeat(40, 40, 4, 1)).toBe(1);
    expect(snappedHoldEndBeat(0, 40, 4, 1)).toBe(1);
  });
});

describe("the division the toolbar hands back", () => {
  it("reads the value of the select back into a setting", () => {
    expect(parseSnapDivision("off")).toBe("off");
    expect(parseSnapDivision("1")).toBe(1);
    expect(parseSnapDivision("2")).toBe(2);
    expect(parseSnapDivision("4")).toBe(4);
    expect(parseSnapDivision("8")).toBe(8);
    expect(parseSnapDivision("16")).toBe(16);
  });

  it("falls back to the quarter beat for a value it does not know", () => {
    expect(parseSnapDivision("32")).toBe(4);
    expect(parseSnapDivision("")).toBe(4);
  });
});

describe("the grid the toolbar and the grid share", () => {
  it("starts with the grid off, so the behaviour of the editor is unchanged", () => {
    expect(getSnapDivision()).toBe("off");
  });

  it("carries the division the operator picked", () => {
    setSnapDivision(8);
    expect(getSnapDivision()).toBe(8);
    // The grid places the notes with the very same division.
    expect(snappedBeat(5, 40, getSnapDivision())).toBe(0.125);
    setSnapDivision("off");
  });
});
