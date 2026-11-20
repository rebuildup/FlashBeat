import { css } from "styled-system/css";

/**
 * Grid metrics of the studio shell (issue #802).
 *
 * Every studio screen shares one grid contract:
 *
 * - **Rail** — a fixed `13rem` track on `md` and up; below `md` the
 *   navigation collapses into a sticky top bar / disclosure and the work
 *   area takes the full width.
 * - **Columns** — `rail | minmax(0, 1fr)`: the work area absorbs every
 *   extra pixel, the rail never grows. The literal lives only here —
 *   Panda extracts `css()` values statically, so shells compose
 *   `shellColumns` instead of restating (or interpolating) the tracks.
 * - **Content area** — `contentArea` owns the padding and the gap between
 *   blocks; pages put their panels inside it and never restate `p`/`gap`.
 * - **Scroll owner** — scrolling pages let the document scroll
 *   (`StudioShell` default); canvas pages lock the shell to the viewport
 *   (`scroll={false}`) and own their inner scroll area. `StudioLayout`
 *   (dashboard, songs) is always locked: its content area is the scroll
 *   owner.
 *
 * `app-shell.tsx` and `studio-layout.tsx` compose these classes, so a
 * density change lands everywhere at once.
 *
 * Enforced by `src/theme/design-system.test.ts` ("studio grid contract").
 */
export const shellColumns = css({
  gridTemplateColumns: { base: "minmax(0, 1fr)", md: "13rem minmax(0, 1fr)" },
});

/**
 * The padded, gapped work area below the page header / topbar.
 * `overflow-y: auto` makes it the scroll owner of locked shells; on
 * document-scrolling pages the property is inert.
 */
export const contentArea = css({
  display: "flex",
  flex: "1",
  flexDirection: "column",
  gap: "4",
  minHeight: "0",
  overflowY: "auto",
  p: { base: "4", md: "5" },
});
