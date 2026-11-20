import { css, cx } from "styled-system/css";
import type { ReactNode } from "react";
import { Sidebar } from "./sidebar";
import { PageHeader } from "./page-header";
import { contentArea, shellColumns } from "./shell-metrics";

/**
 * The layout of a studio page (issue #151).
 *
 * The catalog (`app-shell.tsx`) has its own shell because it carries the
 * search toolbar. Every other page uses this one: the sidebar on the left,
 * a heading and the content below it.
 */
export const shell = css({
  display: "grid",
  gridTemplateRows: { base: "auto minmax(0, 1fr)", md: "1fr" },
  height: "100dvh",
  overflow: "hidden",
});

const main = css({
  display: "flex",
  flexDirection: "column",
  minHeight: "0",
  minWidth: "0",
});

export function StudioLayout({
  title,
  subtitle,
  active,
  children,
}: {
  title: string;
  subtitle: string;
  /** The navigation entry of this page (`NAV_ITEMS`). */
  active: string;
  children: ReactNode;
}) {
  return (
    <div className={cx(shell, shellColumns)}>
      <Sidebar active={active} />
      <main className={main}>
        <PageHeader title={title} subtitle={subtitle} />
        <div className={contentArea}>{children}</div>
      </main>
    </div>
  );
}
