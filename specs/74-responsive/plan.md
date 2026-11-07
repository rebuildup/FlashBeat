# Plan: Responsive Layout & Keyboard Navigation (#74)

## Architecture Decisions

### 1. Scroll Ownership
- **Root**: `html, body` — `overflow: auto` (page scrolls)
- **AppShell content**: `overflow: auto` (content scrolls if taller than viewport)
- **ChartEditor gridPanel**: `overflow: hidden` → `NoteGrid` owns horizontal scroll via `scrollArea`
- **Sidebars/Inspectors**: `overflow: auto` internally when open as drawers
- **No more** `height: 100dvh; overflow: hidden` on page shells

### 2. Sidebar Strategy
- **≥md (768px)**: Persistent left rail (13rem) — current behavior
- **<md**: Top bar (horizontal nav) — current `base` behavior, but **fixed position** so it doesn't consume content height
- **Drawer** for mobile navigation if needed (future enhancement)

### 3. ChartEditor / Publisher Side Panels
- **≥lg (1024px)**: Persistent right panel (17rem / 21rem)
- **<lg**: Collapsible **drawer** (slide-in from right)
  - Trigger: toolbar button (ChartEditor) / header button (Publisher)
  - Backdrop click / Escape closes
  - Focus trap inside drawer
  - Focus restore to trigger on close

### 4. Toolbar Fields
- Replace fixed `width: "Xrem"` with **fluid widths**: `minWidth: "8rem", maxWidth: "20rem", width: "100%"` in flex container
- Use `flex: 1` for search, `flex-shrink: 0` for selects

### 5. Catalog Columns
- **≥md**: Table with all columns
- **<md**: Hide `level`, `version`; truncate `artist` with ellipsis + tooltip
- **<sm**: Card layout (vertical stack per song) — replaces table entirely

### 6. Note Grid Horizontal Scroll
- `scrollArea` already has `overflow: auto` — **remove `overflow: hidden` from `gridPanel`**
- Stage width = `max(containerWidth, 8 * MIN_LANE_WIDTH * zoom)`
- Horizontal scroll appears automatically when stage > container

### 7. Focus Management
- Use `useFocusEffect` / `useRef` for focus restore on drawer/dialog close
- Add global `:focus-visible` ring in `app.css` (Panda tokens: `colors.colorPalette.solid.bg`)
- Ensure Konva canvas is focusable (`tabIndex=0`) and shows focus ring

---

## Implementation Phases

### Phase 1: Global Shell & Scroll Foundation
1. `web/src/styles/app.css` — remove `body { overflow: hidden }`, add `:focus-visible` ring
2. `web/src/app/app-shell.tsx` — remove `height: 100dvh; overflow: hidden` from shell; make content the scroll owner
3. `web/pages/chart.tsx` — remove page-level shell, use AppShell or new ChartShell
4. `web/pages/publish.tsx` — remove page-level shell, use AppShell or new PublisherShell
5. `web/src/components/new-song/new-song-flow.tsx` — remove `height: 100dvh; overflow: hidden`

### Phase 2: Sidebar & Topbar Responsive
6. `web/src/app/sidebar.tsx` — at `<md` make top bar `position: sticky; top: 0; z-index: 100`; hide rail
7. `web/src/app/topbar.tsx` — fluid tool fields, wrap/stack at narrow widths

### Phase 3: Catalog Panel
8. `web/src/app/catalog-panel.tsx` — responsive columns: hide at `<md`, card layout at `<sm`
9. `web/src/app/panel.tsx` — ensure `panelFrame` allows scroll

### Phase 4: Chart Editor
10. `web/src/components/chart-editor/chart-editor.tsx` — side panel → drawer at `<lg`; toolbar wrap; remove `overflow: hidden` from gridPanel
11. `web/src/components/chart-editor/editor-toolbar.tsx` — fluid fields
12. `web/src/components/chart-editor/note-grid.tsx` — ensure horizontal scroll works (remove parent overflow clipping)
13. `web/src/components/chart-editor/validation-panel.tsx` — verify responsive

### Phase 5: Song Publisher
14. `web/src/components/song-publisher/song-publisher.tsx` — side panel → drawer at `<lg`
15. `web/src/components/song-publisher/song-metadata-form.tsx` — fluid fields
16. `web/src/components/song-publisher/publish-dialog.tsx` — verify responsive

### Phase 6: New Song Flow
17. `web/src/components/new-song/chart-step.tsx` — inherits ChartEditor responsiveness
18. `web/src/components/new-song/stepper.tsx` — verify wrap
19. `web/src/components/new-song/song-info-step.tsx` — verify grid wrap
20. `web/src/components/new-song/publish-step.tsx` — verify grid wrap

### Phase 7: Keyboard & Focus
21. Add focus management to drawer components (ChartEditor side, Publisher side)
22. Ensure Konva canvas focusable with visible ring
23. Verify Tab order on all screens
24. Add focus restore on drawer/dialog close

### Phase 8: Verification
25. Run type check, tests, lint
26. Manual viewport testing at all 5 target sizes
27. Capture before/after screenshots

---

## Risk Mitigation

| Risk | Mitigation |
|------|------------|
| Breaking existing desktop layout | Test at 1440px first; changes are additive (drawer only at `<lg`) |
| Konva canvas focus ring | Use CSS `:focus-visible` on canvas wrapper, not Konva internal |
| Drawer focus trap conflicts with Ark UI Dialog | Use same pattern as Ark UI (`Dialog` already handles this) |
| Scroll jumping when drawer opens | `body` overflow stays `auto`; drawer is `fixed` overlay |
| 200% zoom text wrapping | Use `rem` units throughout; test at 200% |

---

## Dependencies
- No new dependencies. Uses existing: Panda CSS responsive syntax, Ark UI Dialog patterns, React refs for focus.
- Design tokens only — no hardcoded colors/spacing.