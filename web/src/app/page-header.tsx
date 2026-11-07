import { css } from "styled-system/css";
import type { ReactNode } from "react";

/**
 * Header of a studio page (issue #151).
 *
 * The catalog uses the search toolbar of `topbar.tsx`; the dashboard and the
 * song management have no filters, so they use this plain heading with an
 * optional action area.
 */
const bar = css({
  alignItems: "flex-end",
  borderBottomColor: "gray.surface.border",
  borderBottomWidth: "1px",
  display: "flex",
  flexWrap: "wrap",
  gap: "3",
  justifyContent: "space-between",
  paddingBottom: "3",
  paddingTop: "3",
  px: "4",
});

const heading = css({
  display: "flex",
  flexDirection: "column",
  gap: "1",
  minWidth: "0",
});

const titleText = css({
  color: "fg.default",
  fontSize: "sm",
  fontWeight: "semibold",
  margin: "0",
});

const subtitleText = css({
  color: "fg.muted",
  fontSize: "xs",
  margin: "0",
});

export function PageHeader({
  title,
  subtitle,
  actions,
}: {
  title: string;
  subtitle: string;
  actions?: ReactNode;
}) {
  return (
    <header className={bar}>
      <div className={heading}>
        <h1 className={titleText}>{title}</h1>
        <p className={subtitleText}>{subtitle}</p>
      </div>
      {actions === undefined ? null : <div>{actions}</div>}
    </header>
  );
}
