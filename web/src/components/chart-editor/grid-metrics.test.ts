import { describe, expect, it } from "vitest";
import { gridMetrics, LANE_COUNT, MIN_LANE_WIDTH } from "./grid-metrics";

/**
 * Issue #158, finding #1 — the eight lanes have to stay operable on a phone.
 *
 * The rule: a lane never becomes narrower than the playable minimum. When the
 * panel is too narrow for eight lanes, the stage keeps its width and the
 * grid scrolls sideways instead of squeezing the lanes into a strip.
 */

const phonePanel = 366; // 390px viewport minus the padding of the work area
const desktopPanel = 1024;

describe("gridMetrics", () => {
  it("keeps every lane playable on a narrow viewport", () => {
    const metrics = gridMetrics({
      panelWidth: phonePanel,
      panelHeight: 400,
      beatHeight: 44,
      lastBeat: 8,
    });

    expect(metrics.laneWidth).toBe(MIN_LANE_WIDTH);
    expect(metrics.laneWidth).toBeGreaterThanOrEqual(MIN_LANE_WIDTH);
  });

  it("scrolls sideways on a narrow viewport instead of squeezing the lanes", () => {
    const metrics = gridMetrics({
      panelWidth: phonePanel,
      panelHeight: 400,
      beatHeight: 44,
      lastBeat: 8,
    });

    expect(metrics.stageWidth).toBe(MIN_LANE_WIDTH * LANE_COUNT);
    expect(metrics.stageWidth).toBeGreaterThan(phonePanel);
    expect(metrics.scrollsHorizontally).toBe(true);
  });

  it("fills a wide panel with the eight lanes and does not scroll", () => {
    const metrics = gridMetrics({
      panelWidth: desktopPanel,
      panelHeight: 600,
      beatHeight: 44,
      lastBeat: 8,
    });

    expect(metrics.laneWidth).toBe(Math.floor(desktopPanel / LANE_COUNT));
    expect(metrics.stageWidth).toBe(desktopPanel);
    expect(metrics.scrollsHorizontally).toBe(false);
  });

  it("keeps the tap note inside its lane", () => {
    for (const panelWidth of [phonePanel, 480, desktopPanel]) {
      const metrics = gridMetrics({
        panelWidth,
        panelHeight: 400,
        beatHeight: 44,
        lastBeat: 8,
      });

      expect(metrics.noteWidth).toBeGreaterThan(0);
      expect(metrics.noteWidth).toBeLessThanOrEqual(metrics.laneWidth);
    }
  });

  it("fills the panel height with beats", () => {
    const metrics = gridMetrics({
      panelWidth: desktopPanel,
      panelHeight: 440,
      beatHeight: 44,
      lastBeat: 8,
    });

    expect(metrics.stageHeight).toBe(440);
  });
});
