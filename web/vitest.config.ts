import { fileURLToPath } from "node:url";
import { defineConfig } from "vitest/config";

/**
 * Vitest configuration for the Web Studio.
 *
 * The app config (`vite.config.ts`) loads the Void plugin, which starts a
 * workerd inspector and cannot run inside the Vitest worker. The unit tests
 * only exercise the pure `src/lib/**` modules, so they use this standalone
 * config with the same `@` / `styled-system` aliases.
 *
 * `tests/**` is deliberately not part of the include: the read API test
 * (`tests/api.test.ts`) is written for `node --test` and starts the real dev
 * server, so it runs with `node --test tests/` (see `npm test`).
 */
export default defineConfig({
  resolve: {
    alias: {
      "@": fileURLToPath(new URL("./src", import.meta.url)),
      "styled-system": fileURLToPath(new URL("./styled-system", import.meta.url)),
    },
  },
  test: {
    environment: "node",
    include: ["src/**/*.test.ts", "src/**/*.test.tsx", "pages/**/*.test.ts", "pages/**/*.test.tsx"],
  },
});
