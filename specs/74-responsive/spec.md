# Spec: Responsive Layout & Keyboard Navigation (#74)

> Status: implementation complete, **pixel level visual verification still pending**
> (Issue #75 owns the real browser screenshot gate).
> Baseline analysed: `94a4103` (branch `expo-web` merge). Line numbers in the
> stress point table point at that baseline.

## 0. Scope and ownership

| Path | Owner | In this PR |
|------|-------|------------|
| `web/src/styles/app.css` | #74 | yes |
| `web/src/app/**` | #74 | yes |
| `web/src/components/chart-editor/**` | #74 | yes |
| `web/src/components/song-publisher/**` | #74 | yes |
| `specs/74-responsive/**` | #74 | yes |
| `web/pages/chart.tsx`, `web/pages/publish.tsx` | other issues | **no** (page shells stay as they are) |
| `web/src/components/new-song/**` | #135 / #149 | **no** |
| `web/src/lib/**`, `web/tests/**`, `web/db/**`, `web/routes/**` | other issues | **no** |

The two page shells that wrap the editor and the publisher
(`web/pages/chart.tsx:18`, `web/pages/publish.tsx:29`) keep their
`height: 100dvh; overflow: hidden` because those pages belong to other
issues. The responsive work therefore happens **inside** the owned
components: the editor and the publisher own their scrolling and their
disclosure, the application shell owns the document scrolling.

## 1. Breakpoint reference (Panda CSS defaults)

| Token | rem | px (16px base) | Note |
|-------|-----|----------------|------|
| `sm`  | 40  | 640            | small tablet / large phone landscape |
| `md`  | 48  | 768            | tablet portrait |
| `lg`  | 64  | 1024           | laptop / small desktop |
| `xl`  | 80  | 1280           | desktop |
| `2xl` | 96  | 1536           | large desktop |

Target viewports of the issue: **1440×900**, **1280×720**, **1024×768**,
**390×844**, **200% zoom** (1440×900 at 200% = an effective 720×450, i.e.
below the `md` breakpoint).

## 2. T1: Stress points (baseline `94a4103`)

### 2.1 Global shell — `web/src/app/app-shell.tsx:10-16`

```tsx
const shell = css({
  display: "grid",
  gridTemplateColumns: { base: "1fr", md: "13rem minmax(0, 1fr)" },
  gridTemplateRows: { base: "auto 1fr", md: "1fr" },
  height: "100dvh",      // line 14
  overflow: "hidden",    // line 15
});
```

* `height: 100dvh` + `overflow: hidden` → the document can never scroll.
* The grid rows `auto 1fr` make the sidebar a **grid area of its own height**;
  a `position: sticky` sidebar inside it can never travel (the grid area is
  the containing block), so a sticky navigation needs a block layout below
  `md`.
* `minHeight: 0` / `minWidth: 0` are missing on the grid children, so grid
  items refuse to shrink below their content width.

### 2.2 Shell content area — `web/src/app/app-shell.tsx:25-33`

```tsx
const content = css({
  display: "flex", flex: "1", flexDirection: "column",
  gap: "3", minHeight: "0",
  overflowY: "auto",   // line 31
  p: "4",
});
```

* The content area is the only inner scroller, but with the shell pinned to
  `100dvh` the page cannot grow: a long catalog is clipped instead of
  scrolled.

### 2.3 Topbar — `web/src/app/topbar.tsx:41-53`

```tsx
const tools = css({ display: "flex", flexWrap: "wrap", gap: "2", alignItems: "flex-end" });
const toolField = css({ width: "15rem" });    // line 48
const selectRoot = css({ width: "11rem" });   // line 52
```

* Search 240px + level 176px + sort 176px = 592px of fixed width; the row
  only survives because `flexWrap` stacks it — every wrap costs vertical
  space inside a pinned `100dvh` shell.

### 2.4 Catalog panel — `web/src/app/catalog-panel.tsx:22-26, 122-132, 214-216`

```tsx
const COLUMNS = {
  base: "minmax(0, 1fr) auto",
  md:   "minmax(14rem, 1fr) 3rem 4.5rem 4.5rem 3.5rem 12.5rem",
  lg:   "minmax(14rem, 1fr) 3rem 4.5rem 4rem 4.5rem 3.5rem 12.5rem",
};
```
* `titleText` (line 122) uses `whiteSpace: "nowrap"` (line 129) plus
  `textOverflow: ellipsis`: a long Japanese title is unreadable, it is only
  shortened.
* `levelCell` (line 214) is already hidden below `md`, but `artist`
  (12rem), `version` and `actions` (12.5rem) stay: the row needs ≈ 27rem
  before the title gets a single pixel.
* There is no list/table switch: a table cannot become a card list with
  CSS alone, so the row markup has to exist twice.

### 2.5 Chart editor — `web/src/components/chart-editor/chart-editor.tsx:38-64`

```tsx
const layout = css({ display: "grid", gridTemplateRows: "auto minmax(0, 1fr)", height: "100%", overflow: "hidden" }); // 38-45
const body = css({ display: "grid", gridTemplateColumns: "minmax(0, 1fr) 17rem", overflow: "hidden", p: "3" });          // 47-55
const gridPanel = css({ overflow: "hidden" });                                                                              // 57-64
```
* A fixed 272px inspector column, `overflow: hidden` on three levels.
* Below 640px the grid gets 320px while eight lanes need 448px
  (`MIN_LANE_WIDTH = 56`), so the stage overflows invisibly.

### 2.6 Editor toolbar — `web/src/components/chart-editor/editor-toolbar.tsx:38-40`

```tsx
const field = css({ width: "8rem" });
const wideField = css({ width: "10rem" });
const selectRoot = css({ width: "6.5rem" });
```
* Fixed widths only; the bar wraps into three rows on a phone and pushes the
  grid out of the pinned viewport.

### 2.7 Note grid — `web/src/components/chart-editor/note-grid.tsx:24, 43-50`

```tsx
export const MIN_LANE_WIDTH = 56;                       // line 24
const scrollArea = css({ flex: "1", minHeight: "0", overflow: "auto", ... }); // 43-50
```
* The horizontal scroll of the stage exists, but the parents clip it
  (`gridPanel` / `body` / `layout` all `overflow: hidden`).

### 2.8 Song publisher — `web/src/components/song-publisher/song-publisher.tsx:32-38, 78-102`

```tsx
const root = css({ height: "100%", overflow: "hidden" });                                  // 32-38
const body = css({ gridTemplateColumns: "minmax(0, 1fr) 21rem", overflow: "hidden" });    // 78-85
const sideScroll = css({ overflowY: "auto" });                                            // 96-102
```
* A fixed 336px metadata column; below 768px the editor area drops under the
  448px lane minimum.

### 2.9 Publish dialog / metadata form

* The dialog uses Ark UI (focus trap included); the summary grid
  `auto minmax(0, 1fr)` is already fluid, but the form fields were not
  checked against a 390px viewport.

### 2.10 New song flow — `web/src/components/new-song/new-song-flow.tsx:33-41, 105`

* `height: 100dvh` + `overflow: hidden` and a centred `maxWidth: 44rem`
  frame. **Out of scope** (owned by #135/#149), listed here as a known
  stress point for the follow-up issue.

### 2.11 Page shells — `web/pages/chart.tsx:18`, `web/pages/publish.tsx:29`

* Both repeat `height: 100dvh; overflow: hidden`. **Out of scope**, see §0.

### 2.12 Global CSS — `web/src/styles/app.css:11-13`

```css
body { overflow: hidden; }
```
* The document level confirms: no page ever scrolls.

## 3. T2: Reflow and disclosure design

### 3.1 Scroll ownership (one owner per screen)

| Screen | Scroll owner | Notes |
|--------|--------------|-------|
| Catalog (`/`) | the **document** | `body { overflow: auto }`, the shell is `minHeight: 100dvh` and grows with the content |
| Chart editor (`/chart`) | the **note grid** | the page shell (`web/pages/chart.tsx`) stays `100dvh`, the editor fills it |
| Publish (`/publish`) | the **note grid** / the **drawer** | same page shell; the metadata drawer scrolls on its own |
| Drawer (both screens) | the **drawer panel** | `overflowY: auto` on the panel |

`web/src/styles/app.css` no longer pins `body`; `app-shell.tsx` uses
`minHeight: 100dvh` instead of `height: 100dvh` so the document grows
instead of clipping.

### 3.2 Breakpoint table — what collapses at which width

| Element | ≥1024 (`lg`) | 768–1023 (`md`) | 640–767 (`sm`) | <640 (390px) |
|---------|---------------|-----------------|----------------|--------------|
| App shell | grid: 13rem rail + work area | grid: rail + work area | **block**: the rail becomes a sticky horizontal bar | block, sticky bar |
| Sidebar | vertical rail, static | vertical rail, static | horizontal bar, `position: sticky; top: 0` | horizontal bar, sticky |
| Topbar tools | search `flex: 1 1 auto` (max 24rem) + two selects | same, wraps to two rows | wrapped | wrapped, each control shrinks to `minWidth` |
| Catalog | table, all columns | table, all columns | **card list** (title, meta, actions) | card list |
| Catalog title | nowrap + ellipsis | nowrap + ellipsis | 2 line clamp, wrapping | 2 line clamp, wrapping |
| Chart editor inspector | persistent 17rem column | **drawer** (right, 17rem) | drawer | drawer |
| Editor toolbar | one row | wrapped | wrapped | wrapped, fluid fields |
| Note grid | full width | full width | horizontal scroll (8 × 56px = 448px floor) | horizontal scroll |
| Publisher inspector | persistent 21rem column | **drawer** (right, 21rem) | drawer | drawer |
| Publish dialog | centred, fluid | centred, fluid | fluid, full width | fluid, full width |

### 3.3 The drawer is one element, not two copies

Both inspectors are a **single DOM element** that is

* a static grid column from `lg` up (`position: static`, `width: 17rem` /
  `21rem`),
* an off-canvas drawer below `lg` (`position: fixed`, `right: 0`,
  `height: 100dvh`, `transform: translateX(100%)` and `visibility: hidden`
  while closed).

The content (the YouTube preview, the validation list, the metadata form)
is therefore mounted **once**: no duplicated form fields, no duplicated
iframes, no duplicated `id`s. A closed drawer is `visibility: hidden`,
which also removes it from the tab order.

The shared implementation lives in `web/src/app/drawer.tsx`
(`DrawerBackdrop`, `useDrawerFocus`, `nextFocusTarget`).

### 3.4 The catalog becomes a card list

`web/src/app/catalog-panel.tsx` renders the songs twice: once as a table
(`md` and up) and once as a card list (below `md`). Only one of them is
visible at any width (`display: none` on the other), so the screen reader
and the tab order only ever see the visible one. The card carries the
title (2 line clamp), the artist, BPM, duration, note count and version as
wrapping meta, and the three actions (open chart, publish, YouTube) in a
right aligned action row.

### 3.5 Long Japanese titles, loading and error states

| State | Container | Change |
|-------|-----------|--------|
| Long JP title (table) | `titleText` | `whiteSpace: normal` + `-webkit-line-clamp: 2` |
| Long JP title (card) | `cardTitle` | `whiteSpace: normal` + `-webkit-line-clamp: 2` |
| Error text | `stateDescription` (`panel.tsx`) | `wordBreak/overflowWrap: break-word` |
| Validation issues | `validation-panel.tsx` | `maxWidth: 100%`, `overflowWrap: break-word` |
| YouTube preview | `youtube-preview.tsx` | `maxWidth: 100%` |

## 4. T3: Keyboard and focus

### 4.1 Model

| Surface | Tab order | Enter | Escape | Focus visible | Focus trap |
|---------|-----------|-------|--------|---------------|------------|
| Catalog list | rows are links | opens the chart | clears the search focus | yes (global ring) | no |
| Editor toolbar | every control | activates | closes the Ark UI popup | yes | popup only |
| Note grid | the grid itself is focusable (`tabIndex=0`, `role="group"`) | — | clears the selection | yes (blue outline) | no |
| Inspector drawer | the controls inside the panel | activates | **closes the drawer** | yes | **yes** |
| Publish dialog | Ark UI | publishes | closes | yes | Ark UI |
| Metadata form | every field | submits | closes the dialog | yes | dialog |

### 4.2 What was implemented

* **Global focus ring**: `web/src/styles/app.css` adds
  `*:focus-visible { outline: 2px solid var(--global-color-focus-ring) }`.
* **Drawer focus management** (`web/src/app/drawer.tsx`):
  * opening moves the focus to the first focusable control of the panel,
  * `Tab` / `Shift+Tab` cycle inside the panel and never leave it
    (`nextFocusTarget`, unit tested in `web/src/app/drawer.test.ts`),
  * `Escape` closes the drawer and stops the event, so the editor does not
    also clear the note selection,
  * closing gives the focus back to the element that opened the drawer,
  * clicking the backdrop closes the drawer.
* **Note grid**: the grid is focusable (`tabIndex={0}`, `role="group"`,
  `aria-label="Note grid"`) and shows a focus outline; the existing
  shortcuts (`arrows` nudge, `Delete` removes, `Escape` deselects) work
  while it holds the focus.
* **Publisher toggle**: the header button carries `aria-expanded` and a
  state dependent `aria-label`; the previous build referenced a
  non-existent `sidebar-toggle` class, so the button was visible on wide
  viewports as well — fixed by using the `sidebarToggle` Panda class.

### 4.3 Not implemented / open

* The catalog table keeps its arrow key navigation (`onListKeyDown`); the
  card list below `md` relies on plain `Tab` over the links.
* The note grid has no "add note from the keyboard" command: the grid needs
  a cursor position model that does not exist yet (issue #72 owns the
  editor UX).

## 5. T4: Verification

### 5.1 Automated (run on the branch head, see the PR body for the exact
output)

| Command | Result |
|---------|--------|
| `npx tsc --noEmit` | clean, no diagnostics |
| `npm test` | `node --test tests/`: 47 tests, 47 pass, 0 fail (13 suites); `vitest run`: 22 files, 156 tests passed |
| `npx vp check` | 0 errors, 11 warnings (all pre-existing, none in a line this PR added) |
| `web/src/app/drawer.test.ts` | unit test of the focus trap |
| `web/src/components/chart-editor/chart-editor.test.ts`, `web/src/components/song-publisher/song-publisher.test.ts` | render tests: the inspector is mounted once |
| `FLASHBEAT_ADMIN_TOKEN=devtoken vp dev --port 5199` | boots, `/`, `/chart` and `/publish` answer `200`; the pages are client rendered (`data-void-ssr="false"`), so the HTML carries no application markup — **no rendering could be inspected** |

### 5.2 Manual / visual — **still pending**

This environment has **no browser and no screenshot tool**, so the
following are **not verified** and must not be read as verified:

* the rendered result at 1440×900, 1280×720, 1024×768, 390×844 and at
  200% zoom,
* that `document.scrollWidth === document.clientWidth` holds at each width,
* the real focus order and focus ring rendering in a browser,
* the YouTube preview inside the drawer.

Issue **#75** owns the real browser screenshot gate. The code changes in
this PR are justified by the breakpoint table in §3.2 and by the static
analysis of the box model (fixed widths → drawer, fixed columns → card
list, `100dvh` + `overflow: hidden` → `minHeight` + document scroll), not
by a captured rendering.

## 6. Acceptance checklist

| # | Criterion | State |
|---|-----------|-------|
| 1 | No unintended horizontal overflow | **code addressed, visually pending (#75)** |
| 2 | Primary actions reachable without horizontal scroll | code addressed (drawer + card list), visually pending |
| 3 | One scroll owner per screen | done (§3.1) |
| 4 | Tab / Enter / Escape work, focus always visible | done (§4.2) |
| 5 | Focus restored after closing a drawer | done (§4.2) |
| 6 | Long Japanese titles readable | done (2 line clamp, §3.5) |
| 7 | Loading / error states fit their container | done (§3.5) |
| 8 | Type check, tests, lint pass | done (§5.1) |
| 9 | Screenshots of the five viewports | **open — #75** |
