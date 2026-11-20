import { cx, css } from "styled-system/css";
import type { ReactNode } from "react";
import { Sidebar } from "./sidebar";
import { shellColumns } from "./shell-metrics";

/**
 * The persistent frame of the Studio (issue #801).
 *
 * Void's pages layout (`pages/layout.tsx`) renders this once and keeps it
 * mounted across client-side navigations, so the navigation rail — and the
 * staff disclosure state inside it — survives every route change. Each page
 * only swaps the content of `<main id="studio-content">`.
 *
 * `fixed` selects the scroll owner, mirroring the old `StudioShell`'s
 * `scroll` prop (inverted):
 * - `false`: the shell grows with the document and the page scrolls with it
 *   (the catalog, whose hero and filters read as one document).
 * - `true`: the shell is locked to the viewport and the page scrolls inside
 *   its own content area (editor, publish and the other studio screens).
 */
const shell = css({
  display: { base: "block", md: "grid" },
  gridTemplateRows: { md: "1fr" },
  minHeight: "100dvh",
  "&[data-fixed]": {
    display: "grid",
    gridTemplateRows: { base: "auto minmax(0, 1fr)", md: "1fr" },
    height: "100dvh",
    overflow: "hidden",
  },
});

const main = css({
  display: "flex",
  flexDirection: "column",
  minHeight: "0",
  minWidth: "0",
});

const mainFixed = css({
  height: "full",
});

export function StudioFrame({
  fixed = false,
  children,
}: {
  /** `true` locks the shell to the viewport (canvas and form pages). */
  fixed?: boolean;
  children: ReactNode;
}) {
  return (
    <div className={cx(shell, shellColumns)} data-fixed={fixed ? "true" : undefined}>
      <Sidebar />
      <main id="studio-content" className={cx(main, fixed && mainFixed)}>
        {children}
      </main>
    </div>
  );
}
