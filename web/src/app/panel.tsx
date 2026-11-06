import { css } from "styled-system/css";
import type { ReactNode } from "react";

/**
 * Shared surfaces for the Web Studio content area.
 *
 * Every state the Web Studio has to render (loading, empty, error, ready)
 * uses the same frame so the layout does not jump between states.
 */
export const panelFrame = css({
  bg: "gray.surface.bg",
  borderColor: "gray.surface.border",
  borderRadius: "l2",
  borderWidth: "1px",
  display: "flex",
  flex: "1",
  flexDirection: "column",
  minHeight: "0",
  overflow: "hidden",
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
