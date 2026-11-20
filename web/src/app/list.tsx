import { css, cx } from "styled-system/css";
import type { ReactNode } from "react";

/**
 * Shared building blocks of the studio's dense lists (issue #802).
 *
 * `panel.tsx` owns surfaces (frames, headers, state blocks); this module
 * owns the *rows inside them*: badges, numeric cells, meta lines, row
 * action links and the invisible row link. The catalog (`catalog-panel.tsx`)
 * and the song management (`songs-panel.tsx`) both build on these so a
 * "level chip" or a "plays column" looks and behaves identically wherever
 * it appears. Screen-specific layout (grid tracks, which columns exist)
 * stays in the panel — only the repeated atoms live here.
 */

/* ------------------------------------------------------------------ */
/* Badges                                                              */
/* ------------------------------------------------------------------ */

const badgeBase = css({
  alignItems: "center",
  display: "inline-flex",
  fontSize: "xs",
  justifyContent: "center",
  lineHeight: "1.2",
  paddingX: "2",
  paddingY: "0.5",
  whiteSpace: "nowrap",
});

/** Filled neutral chip — level, version, counts. */
export const badgeNeutral = css({
  bg: "gray.8",
  borderRadius: "l1",
  color: "fg.default",
  fontWeight: "medium",
});

/** Quieter filled chip — archived / inactive state. */
export const badgeMuted = css({
  bg: "gray.8",
  borderRadius: "l1",
  color: "fg.muted",
  fontWeight: "medium",
});

/** Outlined informational chip — requirements, hints (non-interactive). */
export const badgeOutline = css({
  borderColor: "border",
  borderRadius: "full",
  borderStyle: "solid",
  borderWidth: "1px",
  color: "fg.subtle",
  fontWeight: "normal",
  gap: "1",
});

export type BadgeTone = "neutral" | "muted" | "outline";

const BADGE_TONES: Record<BadgeTone, string> = {
  neutral: badgeNeutral,
  muted: badgeMuted,
  outline: badgeOutline,
};

/**
 * Small status chip. `tone` picks the emphasis; the base geometry
 * (radius, padding, type size) is shared so chips never drift apart.
 */
export function Badge({
  tone = "neutral",
  className,
  children,
  title,
  testId,
}: {
  tone?: BadgeTone;
  className?: string;
  children: ReactNode;
  title?: string;
  /** Rendered as `data-testid` for the e2e/unit selectors. */
  testId?: string;
}) {
  return (
    <span
      className={cx(badgeBase, BADGE_TONES[tone], className)}
      title={title}
      data-testid={testId}
    >
      {children}
    </span>
  );
}

/* ------------------------------------------------------------------ */
/* Numeric cells                                                       */
/* ------------------------------------------------------------------ */

/**
 * Right-aligned tabular numeric cell. Hidden below `md` — narrow layouts
 * fold the values into the row's meta line instead of shrinking the
 * desktop table (issue #802: progressive disclosure, not a squeezed
 * table).
 */
export const num = css({
  color: "fg.muted",
  display: { base: "none", md: "block" },
  fontVariantNumeric: "tabular-nums",
  fontSize: "sm",
  textAlign: "right",
  whiteSpace: "nowrap",
});

/** Same cell, but only from `lg` up (columns the md grid has no track for). */
export const numWide = css({
  color: "fg.muted",
  display: { base: "none", lg: "block" },
  fontVariantNumeric: "tabular-nums",
  fontSize: "sm",
  textAlign: "right",
  whiteSpace: "nowrap",
});

/* ------------------------------------------------------------------ */
/* Text atoms                                                          */
/* ------------------------------------------------------------------ */

/** Secondary meta line under a row title (artist · BPM · duration · …). */
export const metaLine = css({
  color: "fg.subtle",
  display: { base: "flex", md: "none" },
  flexWrap: "wrap",
  fontSize: "xs",
  fontVariantNumeric: "tabular-nums",
  gap: "1",
  lineHeight: "1.3",
});

/** Artist / secondary text of a row. */
export const metaText = css({
  color: "fg.muted",
  fontSize: "sm",
  lineHeight: "1.4",
  overflow: "hidden",
  textOverflow: "ellipsis",
  whiteSpace: "nowrap",
  width: "100%",
});

/** Invisible only to sighted users; screen readers read the full row name. */
export const visuallyHidden = css({
  clip: "rect(0 0 0 0)",
  clipPath: "inset(50%)",
  height: "1px",
  overflow: "hidden",
  position: "absolute",
  whiteSpace: "nowrap",
  width: "1px",
});

/* ------------------------------------------------------------------ */
/* Row actions                                                         */
/* ------------------------------------------------------------------ */

/**
 * Container of a row's secondary actions. It sits above the stretched
 * row link (`zIndex: 1`) but must not swallow clicks that land between
 * its children — only the real links take the pointer (`issue #166`).
 */
export const rowActions = css({
  alignItems: "center",
  display: "flex",
  gap: "2",
  justifyContent: "flex-end",
  minWidth: "0",
  pointerEvents: "none",
  zIndex: "1",
});

/** Ghost text link used for per-row secondary actions (YouTube, publish). */
export const actionLink = css({
  alignItems: "center",
  borderRadius: "l1",
  color: "fg.muted",
  display: "inline-flex",
  fontSize: "xs",
  fontWeight: "medium",
  gap: "1",
  minHeight: "8",
  paddingInline: "2",
  /** Re-enable the pointer the non-interactive `rowActions` turned off. */
  pointerEvents: "auto",
  textDecoration: "none",
  whiteSpace: "nowrap",
  _hover: {
    bg: "gray.a3",
    color: "fg.default",
  },
  _focusVisible: {
    outline: "2px solid",
    outlineColor: "blue.9",
    outlineOffset: "1px",
  },
});

/**
 * De-emphasized variant of `actionLink` (external icon-only links).
 * A complete class rather than a `cx` overlay: two atomic classes setting
 * `color` would resolve by stylesheet order, not by class order.
 */
export const actionLinkSubtle = css({
  alignItems: "center",
  borderRadius: "l1",
  color: "fg.subtle",
  display: "inline-flex",
  fontSize: "xs",
  fontWeight: "medium",
  gap: "1",
  minHeight: "8",
  paddingInline: "2",
  pointerEvents: "auto",
  textDecoration: "none",
  whiteSpace: "nowrap",
  _hover: {
    bg: "gray.a3",
    color: "fg.default",
  },
  _focusVisible: {
    outline: "2px solid",
    outlineColor: "blue.9",
    outlineOffset: "1px",
  },
});

/* ------------------------------------------------------------------ */
/* Keyboard legend                                                     */
/* ------------------------------------------------------------------ */

/** `<kbd>`-looking cap in footers and step legends. */
export const keyCap = css({
  bg: "gray.8",
  borderRadius: "l1",
  color: "fg.default",
  display: "inline-block",
  fontFamily: "inherit",
  fontSize: "xs",
  lineHeight: "1.2",
  minWidth: "4",
  padding: "0.5 1.5",
  textAlign: "center",
});
