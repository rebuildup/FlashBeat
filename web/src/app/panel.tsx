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

export const panelHeader = css({
  alignItems: "center",
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
  fontSize: "sm",
  fontWeight: "semibold",
  letterSpacing: "0.01em",
  margin: "0",
});

export const panelSubtitle = css({
  color: "fg.muted",
  fontSize: "xs",
  margin: "0",
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
  fontSize: "sm",
  fontWeight: "semibold",
  margin: "0",
});

export const stateDescription = css({
  color: "fg.muted",
  fontSize: "xs",
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
