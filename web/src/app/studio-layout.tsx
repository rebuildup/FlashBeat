import { css } from "styled-system/css";
import type { ReactNode } from "react";
import { Sidebar } from "./sidebar";
import { PageHeader } from "./page-header";

/**
 * The layout of a studio page (issue #151).
 *
 * The catalog (`app-shell.tsx`) has its own shell because it carries the
 * search toolbar. Every other page uses this one: the sidebar on the left,
 * a heading and the content below it.
 */
export const shell = css({
  display: "grid",
  gridTemplateColumns: { base: "1fr", md: "13rem minmax(0, 1fr)" },
  gridTemplateRows: { base: "auto 1fr", md: "1fr" },
  height: "100dvh",
  overflow: "hidden",
});

const main = css({
  display: "flex",
  flexDirection: "column",
  minHeight: "0",
  minWidth: "0",
});

const content = css({
  display: "flex",
  flex: "1",
  flexDirection: "column",
  gap: "5",
  minHeight: "0",
  overflowY: "auto",
  p: "5",
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
    <div className={shell}>
      <Sidebar active={active} />
      <main className={main}>
        <PageHeader title={title} subtitle={subtitle} />
        <div className={content}>{children}</div>
      </main>
    </div>
  );
}
