import type { Chart } from "./types";

/**
 * Serialize a chart back to the legacy JSON format.
 *
 * The output is compact, exactly like the files in
 * `Assets/Game/Resources/`, and keeps the key order of the imported object.
 * `JSON.stringify` on the untouched parsed object is byte identical to the
 * input for every chart in the repository.
 */
export function exportChartJson(chart: Chart): string {
  return JSON.stringify(chart);
}

/**
 * File name the Unity `Resources` loader expects: `<song name>.json`.
 */
export function chartFileName(chart: Chart): string {
  return `${chart.name}.json`;
}
