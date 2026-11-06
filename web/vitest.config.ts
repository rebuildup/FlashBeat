import { fileURLToPath } from "node:url";
import { defineConfig } from "vitest/config";

/**
 * Vitest configuration for the Web Studio.
 *
 * The app config (`vite.config.ts`) loads the Void plugin, which starts a
 * workerd inspector and cannot run inside the Vitest worker. The chart
 * editor tests only exercise the pure `src/lib/**` modules, so they use this
 * standalone config with the same `@` / `styled-system` aliases.
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
    include: ["src/**/*.test.ts", "tests/**/*.test.ts"],
  },
});
