import { css } from "styled-system/css";
import type { ReactNode } from "react";
import { PageHeader } from "./page-header";

/**
 * The scaffold of a staff studio page (issue #151): a heading and the
 * padded, internally scrolling content area.
 *
 * Issue #801 moved the shared frame — the sidebar and the viewport-locked
 * work area — into `pages/layout.tsx`, so this component no longer renders
 * navigation. Every page receives the same persistent frame; only the
 * content below the header is swapped on navigation.
 */
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
  children,
}: {
  title: string;
  subtitle: string;
  children: ReactNode;
}) {
  return (
    <>
      <PageHeader title={title} subtitle={subtitle} />
      <div className={content}>{children}</div>
    </>
  );
}
