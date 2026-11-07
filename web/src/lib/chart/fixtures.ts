import sampleChart from "./fixtures/sample-chart.json";
import type { Chart } from "./types";

/**
 * Chart fixtures for the editor.
 *
 * The publish API (#16) does not exist yet, so the editor ships with a real
 * chart from the repository (`Assets/Game/Resources/チュートリアル.json`) as its
 * default document. `src/lib/chart/__tests__/contract-schema.test.ts` keeps
 * the copy in sync with the repository file.
 */
export const SAMPLE_CHART = sampleChart as unknown as Chart;

/** YouTube video used by the preview when no song is selected. */
export const SAMPLE_YOUTUBE_ID = "dQw4w9WgXcQ";

/**
 * A published chart whose holds overlap although their line numbers do not
 * (issue #208).
 *
 * Both holds are on lane 0:
 *
 * - the first covers the lines (0, 4] at `LPB` 2 — the beats 0 to 2,
 * - the second covers the lines (4, 16] at `LPB` 16 — the beats 0.25 to 1.
 *
 * The raw line numbers only touch (4 is the end of the first hold and the
 * start of the second), so the line comparison of the editor before issue
 * #191 saw no overlap. The beat rule sees one: the second hold lies inside
 * the first.
 *
 * Such a chart can be in the database: it was published before the rules
 * were unified. The editor has to be able to open it and to show the problem
 * as a warning (issue #208).
 */
export const MIXED_LPB_OVERLAP_CHART: Chart = {
  name: "Mixed LPB overlap",
  maxBlock: 8,
  BPM: 120,
  offset: 0,
  notes: [
    { type: 2, num: 0, endNum: 4, block: 0, LPB: 2 },
    { type: 2, num: 4, endNum: 16, block: 0, LPB: 16 },
  ],
};
