import { readFileSync, readdirSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

/**
 * Access to the contract chart corpus.
 *
 * Since issue #823 the Unity project ships no local song charts: charts are
 * delivered by the cloud catalog and pinned per session. The tests therefore
 * run against the rights-cleared synthetic charts of `contracts/charts/`,
 * which cover the same shapes the legacy files exercised (lyrics blocks,
 * ragged lyric arrays, empty note lists, fewer lanes, holds).
 */
const REPO_ROOT = fileURLToPath(new URL("../../../../..", import.meta.url));

/** Directory with the synthetic contract charts: `contracts/charts`. */
export const CHART_DIR = path.join(REPO_ROOT, "contracts", "charts");

/** Directory with the frozen contract schemas. */
export const CONTRACT_DIR = path.join(REPO_ROOT, "contracts", "schemas");

/** Read one contract chart as raw text. `name` may be a nested path. */
export function readRepositoryChart(name: string): string {
  return readFileSync(path.join(CHART_DIR, name), "utf8");
}

/**
 * Names of every chart in `contracts/charts`, including the `corpus/`
 * subdirectory. Paths are returned relative to `CHART_DIR` with `/`
 * separators so they work on every platform.
 */
export function listRepositoryCharts(): string[] {
  const out: string[] = [];
  const walk = (dir: string, prefix: string) => {
    for (const entry of readdirSync(dir, { withFileTypes: true }).sort((a, b) =>
      a.name.localeCompare(b.name),
    )) {
      const full = path.join(dir, entry.name);
      if (entry.isDirectory()) {
        walk(full, `${prefix}${entry.name}/`);
      } else if (entry.name.endsWith(".json") && isChartFile(full)) {
        out.push(`${prefix}${entry.name}`);
      }
    }
  };
  walk(CHART_DIR, "");
  return out.sort();
}

/** True when the file is a legacy chart (it has the `notes` array). */
function isChartFile(filePath: string): boolean {
  try {
    const parsed = JSON.parse(readFileSync(filePath, "utf8")) as { notes?: unknown };
    return Array.isArray(parsed.notes);
  } catch {
    return false;
  }
}
