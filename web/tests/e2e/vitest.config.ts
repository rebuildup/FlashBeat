import { fileURLToPath } from "node:url";
import { defineConfig } from "vitest/config";

/**
 * Vitest config for the E2E harness unit tests (issue #97).
 *
 * The root `web/vitest.config.ts` only includes files under `src`, and this
 * directory owns its tests, so they get their own config. Only the unit tests
 * are collected here; the Playwright specs (`*.spec.ts`) are run by Playwright
 * (`npm run test:studio`), not by Vitest. Everything is imported relatively,
 * no aliases needed.
 */
export default defineConfig({
  root: fileURLToPath(new URL("../../", import.meta.url)),
  test: {
    environment: "node",
    include: ["tests/e2e/**/*.test.ts"],
  },
});
