import { readFileSync, readdirSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

/**
 * Access to the real charts of the Unity project.
 *
 * The tests must run against the files Unity actually ships, not against
 * hand written fixtures, so the repository charts are read from disk.
 */
const REPO_ROOT = fileURLToPath(new URL("../../../../..", import.meta.url));

/** Directory with the legacy charts: `Assets/Game/Resources`. */
export const CHART_DIR = path.join(REPO_ROOT, "Assets/Game/Resources");

/** Directory with the frozen contract schemas. */
export const CONTRACT_DIR = path.join(REPO_ROOT, "contracts/schemas");

/** Read one repository chart as raw text. */
export function readRepositoryChart(name: string): string {
  return readFileSync(path.join(CHART_DIR, name), "utf8");
}

/** Names of every chart in `Assets/Game/Resources`. */
export function listRepositoryCharts(): string[] {
  return readdirSync(CHART_DIR)
    .filter((entry) => entry.endsWith(".json"))
    .sort();
}
