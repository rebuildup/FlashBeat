import path from "node:path";
import { defineConfig, devices } from "@playwright/test";

/**
 * Playwright configuration for the browser E2E of the Web Studio
 * (`npm run test:e2e`).
 *
 * The specs drive the real dev server (`vp dev`) with a seeded local D1
 * (`void db seed`), so the Catalog → Editor → Publish → Catalog 戻り導線 runs
 * against the same worker the studio ships.
 *
 * Server contract:
 * - `E2E_BASE_URL` (default http://localhost:5173) must point at the dev
 *   server. When nothing answers there, the `webServer` entry seeds the local
 *   D1 and boots one; when a server already answers, it is reused as-is.
 * - A server that never comes up fails the run loudly (webServer timeout with
 *   the captured stderr); nothing is skipped (see reporters/gate-reporter.ts:
 *   a run whose tests are all skipped — or where none were collected — exits
 *   non-zero).
 *
 * Run:
 *
 *   cd web && npm run test:e2e
 *   E2E_BASE_URL=http://localhost:5173 npm run test:e2e
 */
const baseURL = process.env.E2E_BASE_URL ?? "http://localhost:5173";
const port = new URL(baseURL).port || "80";

export default defineConfig({
  testDir: ".",
  testMatch: [
    "studio-flow.spec.ts",
    "visitor-flow.spec.ts",
    "editor-note-flow.spec.ts",
    "studio-smoke.spec.ts",
    "publish-flow.spec.ts",
  ],
  // The dev server compiles pages on demand; a cold first hit of the editor
  // and publish chunks can exceed 30s within one test (kept from #232: the
  // editor-note-flow spec hits both cold chunks in one run).
  timeout: 120_000,
  retries: 0,
  // The dev server compiles pages on demand; a cold first hit can exceed the
  // 5s default expect timeout when workers run in parallel.
  expect: { timeout: 15_000 },
  // One worker: the specs share the seeded catalog's state (publish-flow's
  // test song is only cleaned up in its afterAll), and the on-demand compile
  // of the dev server does not benefit from parallel cold hits.
  workers: 1,
  // Absolute path: relative custom-reporter paths resolve against an
  // undocumented base and silently fail to load.
  reporter: [
    ["list"],
    [path.resolve(import.meta.dirname, "reporters/gate-reporter.ts")],
  ],
  use: {
    baseURL,
    ...devices["Desktop Chrome"],
    viewport: { width: 1920, height: 1080 },
    // The publish-flow spec writes to the local D1; the trace is the failure
    // evidence the CI job uploads.
    trace: "retain-on-failure",
  },
  outputDir: "./test-results",
  webServer: {
    // The command runs from the config directory by default; the seed and
    // the dev server both need the `web/` root.
    cwd: path.resolve(import.meta.dirname, "../.."),
    // `FLASHBEAT_ADMIN_TOKEN` is required by env validation; without it the
    // Worker boots but every route answers 500. The literal `local-dev-only`
    // is the local/CI dev token (never a production secret).
    command:
      `FLASHBEAT_ADMIN_TOKEN=\${FLASHBEAT_ADMIN_TOKEN:-local-dev-only} ` +
      `npx void db seed && ` +
      `FLASHBEAT_ADMIN_TOKEN=\${FLASHBEAT_ADMIN_TOKEN:-local-dev-only} ` +
      `npx vp dev --port ${port} --host 127.0.0.1 --strictPort`,
    url: `${baseURL}/api/v1/catalog`,
    reuseExistingServer: true,
    timeout: 180_000,
    stdout: "pipe",
    stderr: "pipe",
  },
});
