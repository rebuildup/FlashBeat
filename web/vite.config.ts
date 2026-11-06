import { fileURLToPath } from "node:url";
import { defineConfig } from "vite";
import { voidPlugin } from "void";
import { voidReact } from "@void/react/plugin";
import pandacss from "@pandacss/vite";

export default defineConfig({
  plugins: [
    voidPlugin(),
    // Panda must run before the React plugin so its CSS layer declaration is
    // the generated stylesheet root.
    pandacss(),
    voidReact({ react: { compiler: true } }),
  ],
  resolve: {
    alias: {
      // Application source alias, mirrors the tsconfig `paths` entry.
      "@": fileURLToPath(new URL("./src", import.meta.url)),
      // Park UI components import the generated styled-system package by name.
      "styled-system": fileURLToPath(new URL("./styled-system", import.meta.url)),
    },
  },
});
