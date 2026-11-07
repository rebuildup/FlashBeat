import { defineConfig, devices } from "@playwright/test";

/**
 * Playwright configuration for the acceptance audit (issue #61).
 *
 * Same prerequisites as studio-flow (local `vp dev` with seeded D1), but the
 * test match is the audit spec only, so the existing studio-flow config stays
 * untouched:
 *
 *   E2E_BASE_URL=http://localhost:5173 npx playwright test -c tests/e2e/acceptance-audit.config.ts
 *
 * `E2E_BASE_URL` defaults to http://localhost:5173.
 */
export default defineConfig({
  testDir: ".",
  testMatch: "acceptance-audit.spec.ts",
  timeout: 60_000,
  retries: 0,
  reporter: [["list"]],
  use: {
    baseURL: process.env.E2E_BASE_URL ?? "http://localhost:5173",
    ...devices["Desktop Chrome"],
    viewport: { width: 1920, height: 1080 },
  },
});
