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
