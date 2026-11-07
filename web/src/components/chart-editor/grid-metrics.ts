/**
 * Geometry of the eight lane grid.
 *
 * The rules live in a pure module, so they can be tested without a browser:
 * the lanes keep their playable width on a narrow viewport and the grid
 * scrolls sideways instead of squeezing the lanes into an unusable strip
 * (issue #158, finding #1).
 */

/** Narrowest lane that stays playable. Below this the stage scrolls. */
export const MIN_LANE_WIDTH = 56;

/** Lane count of the eight lane editor. */
export const LANE_COUNT = 8;

/** Geometry of the stage. */
export interface GridMetrics {
  /** Width of one lane in pixels. */
  laneWidth: number;
  /** Width of a tap note in pixels. */
  noteWidth: number;
  /** Width of the whole stage in pixels. */
  stageWidth: number;
  /** Height of the whole stage in pixels. */
  stageHeight: number;
  /** The stage is wider than the panel: the grid has to scroll sideways. */
  scrollsHorizontally: boolean;
}

/**
 * Geometry of the grid for a panel of the given size.
 *
 * Pure, so the "lanes stay playable, the grid scrolls" rule can be tested
 * without a browser.
 */
export function gridMetrics(options: {
  /** Width of the panel the grid lives in. */
  panelWidth: number;
  /** Height of the panel the grid lives in. */
  panelHeight: number;
  /** Height of one beat in pixels. */
  beatHeight: number;
  /** Last beat of the chart. */
  lastBeat: number;
}): GridMetrics {
  const { panelWidth, panelHeight, beatHeight, lastBeat } = options;

  // Lanes stretch to the panel; the stage scrolls once the floor would make
  // a lane narrower than the playable minimum.
  const laneWidth = Math.max(MIN_LANE_WIDTH, Math.floor(panelWidth / LANE_COUNT));
  // Tap notes stay compact rectangles centered in the lane, never bars that
  // read across the lane boundary.
  const noteWidth = Math.min(laneWidth - 12, 72);
  // Lanes fill the panel down to the last visible beat, so the work area is
  // never empty panel below the chart.
  const stageHeight = Math.max(lastBeat + 2, Math.ceil(panelHeight / beatHeight)) * beatHeight;
  const stageWidth = laneWidth * LANE_COUNT;

  return {
    laneWidth,
    noteWidth,
    stageWidth,
    stageHeight,
    scrollsHorizontally: stageWidth > panelWidth,
  };
}
