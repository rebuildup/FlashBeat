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
  testMatch: ["studio-flow.spec.ts", "visitor-flow.spec.ts", "editor-note-flow.spec.ts"],
  // The dev server compiles pages on demand; a cold first hit of the editor
  // and publish chunks can exceed 30s within one test (kept from #232: the
  // editor-note-flow spec hits both cold chunks in one run).
  timeout: 120_000,
  retries: 0,
  // The dev server compiles pages on demand; a cold first hit can exceed the
  // 5s default expect timeout when workers run in parallel.
  expect: { timeout: 15_000 },
  reporter: [["list"]],
  use: {
    baseURL: process.env.E2E_BASE_URL ?? "http://localhost:5173",
    ...devices["Desktop Chrome"],
    viewport: { width: 1920, height: 1080 },
  },
});
