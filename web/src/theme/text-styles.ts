import { defineTextStyles } from "@pandacss/dev";

/**
 * Typography scale of the Web Studio (issue #802).
 *
 * Two layers, used together:
 *
 * 1. **Size steps** (`2xs`…`7xl`) — the raw scale. Components pick a step,
 *    never an arbitrary `fontSize` value; `design-system.test.ts` rejects
 *    raw px/rem sizes outside `src/theme`.
 * 2. **Semantic styles** (below `label`) — the named combinations screens
 *    actually use: page title, hero heading, panel/section title, dense
 *    list row title, muted meta line, tabular numerals. Prefer a semantic
 *    style over re-assembling `fontSize` + `fontWeight` per screen so the
 *    visual hierarchy stays identical everywhere.
 */
export const textStyles = defineTextStyles({
  xs: { value: { fontSize: "xs", lineHeight: "1.125rem" } },
  sm: { value: { fontSize: "sm", lineHeight: "1.25rem" } },
  md: { value: { fontSize: "md", lineHeight: "1.5rem" } },
  lg: { value: { fontSize: "lg", lineHeight: "1.75rem" } },
  xl: { value: { fontSize: "xl", lineHeight: "1.875rem" } },
  "2xl": { value: { fontSize: "2xl", lineHeight: "2rem" } },
  "3xl": { value: { fontSize: "3xl", lineHeight: "2.375rem" } },
  "4xl": { value: { fontSize: "4xl", lineHeight: "2.75rem", letterSpacing: "-0.02em" } },
  "5xl": { value: { fontSize: "5xl", lineHeight: "3.75rem", letterSpacing: "-0.02em" } },
  "6xl": { value: { fontSize: "6xl", lineHeight: "4.5rem", letterSpacing: "-0.02em" } },
  "7xl": { value: { fontSize: "7xl", lineHeight: "5.75rem", letterSpacing: "-0.02em" } },

  label: { value: { fontSize: "sm", lineHeight: "1.25rem", fontWeight: "medium" } },

  /* ---- semantic styles (issue #802) ---- */

  /** Page heading (`PageHeader`, studio screens). */
  pageTitle: {
    value: {
      fontSize: "xl",
      fontWeight: "semibold",
      letterSpacing: "-0.01em",
      lineHeight: "1.2",
    },
  },

  /** Section/hero heading one step below the page title (welcome hero). */
  sectionTitle: {
    value: { fontSize: "lg", fontWeight: "semibold", lineHeight: "1.3" },
  },

  /** Panel and card titles (catalog header, state blocks, form sections). */
  panelTitle: {
    value: {
      fontSize: "sm",
      fontWeight: "semibold",
      letterSpacing: "0.01em",
      lineHeight: "1.3",
    },
  },

  /**
   * Primary line of a dense list row (song title). `md` (16px) is the
   * floor for row titles — the v0.2.3 review flagged `sm` titles as too
   * small to scan on wide screens (issue #802).
   */
  rowTitle: {
    value: { fontSize: "md", fontWeight: "medium", lineHeight: "1.3" },
  },

  /** Secondary line of a list row or supporting copy. */
  rowMeta: { value: { fontSize: "sm", lineHeight: "1.4" } },

  /** Smallest readable meta text (timestamps, hints, badges). */
  caption: { value: { fontSize: "xs", lineHeight: "1.4" } },

  /** Tabular numerals for aligned numeric columns (BPM, notes, scores). */
  numeric: { value: { fontVariantNumeric: "tabular-nums" } },
});

export default textStyles;
