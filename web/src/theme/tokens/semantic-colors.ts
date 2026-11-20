import { defineSemanticTokens } from "@pandacss/dev";

/**
 * Application-level semantic color tokens (issue #802).
 *
 * Park UI palettes (`src/theme/colors/**`) answer "which hue, which step";
 * this module answers "which role". Components must reach for these names
 * first — `fg.*`, `border`, `canvas`, `error`, `cta.*` — and only drop to a
 * palette step (`gray.8`, `blue.11`, …) when no semantic role fits.
 * Raw hex values are forbidden in `src/app`, `pages` and the feature
 * components; `src/theme/design-system.test.ts` enforces that.
 */
export const semanticColors = defineSemanticTokens.colors({
  fg: {
    default: {
      value: {
        _light: "{colors.gray.12}",
        _dark: "{colors.gray.12}",
      },
    },

    muted: {
      value: {
        _light: "{colors.gray.11}",
        _dark: "{colors.gray.11}",
      },
    },

    subtle: {
      value: {
        _light: "{colors.gray.10}",
        _dark: "{colors.gray.10}",
      },
    },
  },

  border: {
    value: {
      _light: "{colors.gray.4}",
      _dark: "{colors.gray.6}",
    },
  },

  error: {
    value: {
      _light: "{colors.red.9}",
      _dark: "{colors.red.9}",
    },
  },

  /**
   * Application canvas. Pen v5 `bg #1B1B1B`; the recessed surfaces
   * (panels, lists) sit on `gray.1` (Pen `well #131313`).
   */
  canvas: {
    value: {
      _light: "{colors.gray.2}",
      _dark: "{colors.gray.2}",
    },
  },

  /**
   * The one solid call-to-action class (Pen `Prefab/Button Primary`):
   * `#EDEDED` fill with `#1B1B1B` label, identical in both color schemes.
   * Accent blue stays reserved for links, focus rings and selected states —
   * never a filled button (docs/design/web-token-mapping.md §2).
   */
  cta: {
    bg: { value: "#EDEDED" },
    fg: { value: "#1B1B1B" },
  },
});

export default semanticColors;
