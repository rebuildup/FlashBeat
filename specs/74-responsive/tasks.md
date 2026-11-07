# Tasks: Responsive Layout & Keyboard Navigation (#74)

## Task List (max 5 per spec)

| ID | Description | Files | Test |
|----|-------------|-------|------|
| **T1** | **Identify stress points** — Complete. Documented in `spec.md` with file:line references. | `specs/74-responsive/spec.md` | — |
| **T2** | **Implement reflow/disclosure** — Global scroll, sidebar drawer, responsive side panels, fluid toolbars, catalog card layout, note-grid horizontal scroll. | `web/src/styles/app.css`<br>`web/src/app/app-shell.tsx`<br>`web/src/app/sidebar.tsx`<br>`web/src/app/topbar.tsx`<br>`web/src/app/catalog-panel.tsx`<br>`web/src/app/panel.tsx`<br>`web/src/components/chart-editor/chart-editor.tsx`<br>`web/src/components/chart-editor/editor-toolbar.tsx`<br>`web/src/components/chart-editor/note-grid.tsx`<br>`web/src/components/song-publisher/song-publisher.tsx`<br>`web/src/components/song-publisher/song-metadata-form.tsx`<br>`web/src/components/new-song/new-song-flow.tsx`<br>`web/pages/chart.tsx`<br>`web/pages/publish.tsx` | Manual viewport test at 5 sizes |
| **T3** | **Keyboard: Tab/Enter/Escape/focus restore** — Focus-visible ring, drawer focus trap & restore, Konva canvas focusable, dialog focus management. | `web/src/styles/app.css`<br>`web/src/components/chart-editor/chart-editor.tsx` (drawer)<br>`web/src/components/song-publisher/song-publisher.tsx` (drawer)<br>`web/src/components/chart-editor/note-grid.tsx` (canvas focus)<br>`web/src/components/song-publisher/publish-dialog.tsx` | Tab through all screens; verify focus ring, Escape closes, focus restore |
| **T4** | **Long JP titles / loading / error states** — Title wrap/clamp, error word-break, skeleton aspect-ratio, validation list max-width. | `web/src/app/catalog-panel.tsx`<br>`web/src/components/chart-editor/validation-panel.tsx`<br>`web/src/app/panel.tsx` | View at 390px; verify no overflow |
| **T5** | **Verification evidence** — Run type check, tests, lint; manual dev server test; capture before/after screenshots. | — | `npx tsc --noEmit`<br>`npm test`<br>`vp check`<br>Dev server screenshots |

---

## Detailed Task Breakdown

### T2: Reflow/Disclosure Implementation

#### T2.1 Global Scroll Foundation
- [ ] `web/src/styles/app.css`: Remove `body { overflow: hidden }`; add global `:focus-visible` ring using Panda token `--global-color-focus-ring`
- [ ] `web/src/app/app-shell.tsx`: Remove `height: 100dvh; overflow: hidden` from `shell`; change to `minHeight: "100dvh"`; content remains `overflowY: auto`
- [ ] `web/pages/chart.tsx`: Remove inline `shell` with `100dvh/overflow:hidden`; wrap ChartEditor in AppShell or minimal shell
- [ ] `web/pages/publish.tsx`: Remove inline `div` with `100dvh/overflow:hidden`; wrap SongPublisher in AppShell or minimal shell
- [ ] `web/src/components/new-song/new-song-flow.tsx`: Remove `height: 100dvh; overflow: hidden` from `root`; use `minHeight: "100dvh"`

#### T2.2 Sidebar Responsive
- [ ] `web/src/app/sidebar.tsx`: At `<md` (base), make nav bar `position: sticky; top: 0; zIndex: "sticky"` (Panda token); rail only at `md`
- [ ] Ensure sidebar doesn't consume content height when horizontal

#### T2.3 Topbar Fluid Fields
- [ ] `web/src/app/topbar.tsx`: Replace fixed `width: "15rem"` / `"11rem"` with `minWidth: "8rem", maxWidth: "20rem", flex: "1 1 auto"` for search; `flexShrink: 0` for selects
- [ ] `tools` already has `flexWrap: "wrap"` — verify stacking at narrow

#### T2.4 Catalog Panel Responsive
- [ ] `web/src/app/catalog-panel.tsx`:
  - Add `sm` breakpoint: card layout (vertical stack per song) replacing table
  - At `md`: hide `level`, `version` columns (already done); truncate `artist` with tooltip
  - Title: at `<md` allow wrap with `lineClamp: 2`, tooltip on hover for full text
  - Ensure row actions (Edit/Publish/YouTube) remain accessible

#### T2.5 Chart Editor Responsive
- [ ] `web/src/components/chart-editor/chart-editor.tsx`:
  - Side panel (`side`): at `<lg` become drawer (state: `isSideOpen`, trigger button in toolbar)
  - Drawer: `position: fixed; right: 0; top: 0; bottom: 0; width: 17rem; zIndex: "drawer"`; backdrop `fixed inset-0 bg: black/50`
  - Focus trap in drawer (reuse Ark UI pattern); focus restore to trigger on close
  - Toolbar: ensure `flexWrap: "wrap"` and gap
  - `gridPanel`: remove `overflow: hidden` → let `scrollArea` in NoteGrid handle horizontal scroll
- [ ] `web/src/components/chart-editor/editor-toolbar.tsx`:
  - Replace fixed widths with fluid: `minWidth: "8rem", width: "100%", maxWidth: "20rem"` for name/BPM; `minWidth: "6.5rem", flexShrink: 0` for selects
  - Add drawer toggle button (icon: panel-right) at `<lg`

#### T2.6 Note Grid Horizontal Scroll
- [ ] `web/src/components/chart-editor/note-grid.tsx`:
  - `scrollArea` already has `overflow: auto` — verify parent `gridPanel` no longer clips
  - Stage width calculation: `Math.max(containerWidth, 8 * MIN_LANE_WIDTH * zoom)`
  - Ensure horizontal scrollbar appears when needed (Konva stage inside scroll container)

#### T2.7 Publisher Responsive
- [ ] `web/src/components/song-publisher/song-publisher.tsx`:
  - Side panel (`side`): at `<lg` become drawer (trigger in header)
  - Same drawer pattern as ChartEditor
- [ ] `web/src/components/song-publisher/song-metadata-form.tsx`:
  - Fields already `width: "full"` — verify at narrow widths

#### T2.8 New Song Flow
- [ ] `web/src/components/new-song/chart-step.tsx`: Inherits ChartEditor responsiveness
- [ ] `web/src/components/new-song/stepper.tsx`: Verify `flexWrap: "wrap"` on rail
- [ ] `web/src/components/new-song/song-info-step.tsx`: Grid already responsive (`base: "1fr", md: "1fr 1fr"`)
- [ ] `web/src/components/new-song/publish-step.tsx`: Grid already responsive (`base: "1fr", md: "minmax(0, 1fr) 24rem"`)

---

### T3: Keyboard & Focus

#### T3.1 Global Focus Ring
- [ ] `web/src/styles/app.css`: Add:
  ```css
  @layer base {
    *:focus-visible {
      outline: 2px solid var(--global-color-focus-ring);
      outline-offset: 2px;
    }
  }
  ```

#### T3.2 Drawer Focus Management (ChartEditor & Publisher)
- [ ] Create `useDrawerFocus` hook or inline logic:
  - On open: trap focus inside drawer, focus first focusable element
  - On close: restore focus to trigger button
  - Escape key closes drawer
  - Tab cycles within drawer only

#### T3.3 Konva Canvas Focus
- [ ] `web/src/components/chart-editor/note-grid.tsx`:
  - Add `tabIndex={0}` to `Stage` wrapper (`scrollArea` div)
  - Ensure `:focus-visible` ring visible on canvas wrapper
  - Keyboard: Arrow keys navigate notes (existing), Enter adds note, Escape clears selection

#### T3.4 Dialog Focus (Publish Dialog)
- [ ] `web/src/components/song-publisher/publish-dialog.tsx`: Ark UI Dialog handles focus trap; verify token input focused on open

#### T3.5 Catalog Keyboard
- [ ] `web/src/app/app-shell.tsx`: `onListKeyDown` already handles ArrowUp/Down; verify Enter opens link, Escape clears search focus

---

### T4: Content Overflow Fixes

#### T4.1 Long Japanese Titles
- [ ] `web/src/app/catalog-panel.tsx`: `titleText` — at `<md`: remove `whiteSpace: "nowrap"`, add `display: "-webkit-box", webkitLineClamp: 2, webkitBoxOrient: "vertical", overflow: "hidden"`; tooltip with full title

#### T4.2 Error/Loading States
- [ ] `web/src/app/panel.tsx`: `stateBlock` — add `maxWidth: "100%", wordBreak: "break-word"` to description
- [ ] `web/src/components/chart-editor/validation-panel.tsx`: `list` — ensure `maxWidth: "100%"` on items

#### T4.3 YouTube Preview Aspect Ratio
- [ ] `web/src/components/chart-editor/youtube-preview.tsx`: Ensure `aspectRatio: "16/9", maxWidth: "100%"`

---

### T5: Verification

- [ ] Run `cd /Users/rebui/work/FlashBeat.74/web && npx tsc --noEmit` — must pass
- [ ] Run `cd /Users/rebui/work/FlashBeat.74/web && npm test` — report pass/fail counts
- [ ] Run `cd /Users/rebui/work/FlashBeat.74/web && vp check` — fix any lint/format issues
- [ ] Start dev server: `FLASHBEAT_ADMIN_TOKEN=devtoken vp dev` — test at 5 viewports
- [ ] Capture screenshots (before/after) for each viewport where changes visible
- [ ] Record test outputs, dev server URL/port in PR body

---

## File Ownership Map (No Cross-Contamination)

| Owner | Files |
|-------|-------|
| This PR | `web/src/styles/app.css`, `web/src/app/**`, `web/src/components/chart-editor/**`, `web/src/components/song-publisher/**`, `web/src/components/new-song/**`, `web/pages/chart.tsx`, `web/pages/publish.tsx`, `specs/74-responsive/**` |
| Other PRs | `web/pages/index.tsx`, `web/routes/**`, `web/src/lib/**`, `web/db/**`, `web/tests/**` (except new layout tests) |

---

## Acceptance Checklist (per issue)

- [ ] 1440×900: All UI usable, no overflow
- [ ] 1280×720: All UI usable, no overflow
- [ ] 1024×768: Sidebar rail, side panels persistent, no overflow
- [ ] 390×844: Sidebar top bar, side panels as drawers, catalog as cards, horizontal scroll on grid
- [ ] 200% zoom: Equivalent to ≤720px width — same as 390px behavior
- [ ] Tab/Enter/Escape work on all screens
- [ ] Focus visible everywhere
- [ ] Focus restored after drawer/dialog close
- [ ] No overlapping panes
- [ ] No unreachable buttons
- [ ] Long JP titles readable (wrap/tooltip)
- [ ] Loading/error states fit containers
- [ ] Type check passes
- [ ] Tests pass
- [ ] Lint passes