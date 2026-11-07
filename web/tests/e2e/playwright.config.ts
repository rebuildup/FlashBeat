import { defineConfig, devices } from "@playwright/test";

/**
 * Playwright configuration for the Studio flow E2E (issue #166).
 *
 * The spec drives the real dev server (`vp dev`) with a seeded local D1
 * (`void db migrate && void db seed`), so the Catalog → Editor → Publish
 * → Catalog 戻り導線 runs against the same worker the studio ships.
 *
 * Run (dev server must be up; the port moves when 5173 is taken):
 *
 *   cd web && npx vp dev            # terminal 1
 *   E2E_BASE_URL=http://localhost:5173 npx playwright test -c tests/e2e/playwright.config.ts
 *
 * `E2E_BASE_URL` defaults to http://localhost:5173.
 */
export default defineConfig({
  testDir: ".",
  testMatch: "studio-flow.spec.ts",
  // The spec runs against `vp dev`, where each route's first transform can
  // take seconds; on a machine with several dev servers running, loads hang
  // past the defaults.
  timeout: 60_000,
  // Under load, void may serve a route without SSR (`data-void-ssr="false"`,
  // empty #app): the page stays empty until the full client bundle (konva,
  // note-grid) is transformed and hydrated, which can take tens of seconds.
  expect: { timeout: 30_000 },
  retries: 0,
  reporter: [["list"]],
  use: {
    baseURL: process.env.E2E_BASE_URL ?? "http://localhost:5173",
    ...devices["Desktop Chrome"],
    viewport: { width: 1920, height: 1080 },
  },
});
