import { css } from "styled-system/css";
import type { ReactNode } from "react";

/**
 * Shared surfaces for the Web Studio content area.
 *
 * Every state the Web Studio has to render (loading, empty, error, ready)
 * uses the same frame so the layout does not jump between states.
 */
export const panelFrame = css({
  /**
   * Recessed surface: Pen v5 lists sit in wells darker than the canvas
   * (`well #131313` under `bg #1B1B1B`), so the panel steps down, not up.
   */
  bg: "gray.1",
  borderColor: "border",
  borderRadius: "l2",
  borderWidth: "1px",
  display: "flex",
  flex: "1",
  flexDirection: "column",
  minHeight: "0",
  overflow: "auto",
});

/**
 * Shared page header band (issue #166): the row every screen puts at the top
 * of the work area — back link, title block, primary action. The catalog
 * topbar (search + filters) keeps its own component; this band is for the
 * screens that carry one job and one primary action (chart, publish, drafts).
 */
export const pageHeader = css({
  alignItems: "center",
  borderBottomColor: "gray.surface.border",
  borderBottomWidth: "1px",
  display: "flex",
  flexShrink: "0",
  flexWrap: "wrap",
  gap: "3",
  justifyContent: "space-between",
  minHeight: "3rem",
  px: "4",
  py: "2",
});

export const pageHeaderIdentity = css({
  display: "flex",
  flexDirection: "column",
  /** Narrow: the heading takes its own row so the actions keep full width. */
  flex: { base: "1 0 100%", md: "1 1 0%" },
  gap: "0",
  minWidth: "0",
});

export const pageTitle = css({
  color: "fg.default",
  /** Compact header title — same step as panel titles (issue #802). */
  textStyle: "panelTitle",
  margin: "0",
  overflow: "hidden",
  textOverflow: "ellipsis",
  whiteSpace: "nowrap",
});

export const pageSubtitle = css({
  color: "fg.muted",
  /** Shared caption step (issue #802). */
  textStyle: "caption",
  margin: "0",
  overflow: "hidden",
  textOverflow: "ellipsis",
  whiteSpace: "nowrap",
});

export const pageBackLink = css({
  alignItems: "center",
  color: "fg.muted",
  display: "inline-flex",
  flexShrink: "0",
  fontSize: "xs",
  minHeight: "8",
  textDecoration: "none",
  _hover: { color: "fg.default" },
  _focusVisible: { outline: "2px solid", outlineOffset: "2px" },
});

/**
 * The action cluster of a page header. On a narrow viewport the cluster takes
 * its own row and wraps: a rigid cluster used to push the primary action
 * (issue #74, `Review & Publish`) out of the viewport.
 */
export const pageActions = css({
  alignItems: "center",
  display: "flex",
  flex: { base: "1 0 100%", md: "0 0 auto" },
  flexWrap: "wrap",
  gap: "3",
  justifyContent: { base: "flex-start", md: "flex-end" },
  maxWidth: "100%",
  minWidth: "0",
});

export const panelHeader = css({
  alignItems: "center",
  bg: "gray.2",
  borderBottomColor: "gray.surface.border",
  borderBottomWidth: "1px",
  display: "flex",
  gap: "3",
  justifyContent: "space-between",
  paddingBottom: "3",
  paddingTop: "3",
  px: "4",
});

export const panelTitle = css({
  color: "fg.default",
  /** Shared panel-title step (issue #802). */
  textStyle: "panelTitle",
  margin: "0",
});

export const panelSubtitle = css({
  color: "fg.muted",
  /** Shared caption step (issue #802). */
  textStyle: "caption",
  margin: "0",
});

/**
 * v5 primary CTA (Prefab/Button Primary): the one solid palette action of a
 * screen at the 56px class. Shared so every primary CTA is the same class
 * (issue #215 review) — accent as a filled button only here, per the Pen.
 */
export const primaryCta = css({
  /** Pen `Prefab/Button Primary` — the `cta` semantic tokens (issue #802). */
  bg: "cta.bg",
  color: "cta.fg",
  display: "inline-flex",
  alignItems: "center",
  justifyContent: "center",
  minHeight: "14",
  paddingInline: "6",
  borderRadius: "l2",
  fontSize: "sm",
  fontWeight: "medium",
  textDecoration: "none",
  whiteSpace: "nowrap",
  border: "none",
  cursor: "pointer",
  _hover: { opacity: "0.9" },
  _focusVisible: {
    outline: "2px solid",
    outlineColor: "blue.9",
    outlineOffset: "2px",
  },
  _disabled: { opacity: "0.4" },
});

export const panelBody = css({
  flex: "1",
  minHeight: "0",
  overflowY: "auto",
  p: "4",
});

/** Body variant for lists that run edge to edge inside the well (catalog rows). */
export const panelBodyFlush = css({
  flex: "1",
  minHeight: "0",
  overflowY: "auto",
});

export const stateBlock = css({
  alignItems: "center",
  display: "flex",
  flexDirection: "column",
  gap: "3",
  justifyContent: "center",
  minHeight: "16rem",
  textAlign: "center",
});

export const stateTitle = css({
  color: "fg.default",
  /** Shared panel-title step (issue #802). */
  textStyle: "panelTitle",
  margin: "0",
});

export const stateDescription = css({
  color: "fg.muted",
  /** Shared caption step (issue #802). */
  textStyle: "caption",
  margin: "0",
  maxWidth: "28rem",
  wordBreak: "break-word",
  overflowWrap: "break-word",
});

export function StateBlock({
  title,
  description,
  action,
}: {
  title: string;
  description: string;
  action?: ReactNode;
}) {
  return (
    <div className={stateBlock}>
      <p className={stateTitle}>{title}</p>
      <p className={stateDescription}>{description}</p>
      {action}
    </div>
  );
}
