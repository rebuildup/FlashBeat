# Spec: Responsive Layout & Keyboard Navigation (#74)

## Breakpoint Reference (Panda CSS defaults)
| Token | rem | px (16px base) | Note |
|-------|-----|----------------|------|
| `sm`  | 40  | 640            | small tablet / large phone landscape |
| `md`  | 48  | 768            | tablet portrait |
| `lg`  | 64  | 1024           | laptop / small desktop |
| `xl`  | 80  | 1280           | desktop |
| `2xl` | 96  | 1536           | large desktop |

Target viewports per issue: **1440×900**, **1280×720**, **1024×768**, **390×844**, **200% zoom**.

---

## T1: Concrete Stress Points

### 1. Global shell (`web/src/app/app-shell.tsx:10-16`)
```tsx
const shell = css({
  display: "grid",
  gridTemplateColumns: { base: "1fr", md: "13rem minmax(0, 1fr)" },
  gridTemplateRows: { base: "auto 1fr", md: "1fr" },
  height: "100dvh",
  overflow: "hidden",        // ← kills all scrolling at page level
});
```
- `height: 100dvh` + `overflow: hidden` means **no vertical scroll** ever.
- On mobile (`base`) sidebar collapses to top bar but **topbar + sidebar + content** all fight for the same 100dvh.
- At 200% zoom (effective width ≤ 640px = `sm`), `md` breakpoint not reached → sidebar stays as top bar, content gets squeezed.
- `minHeight: 0` / `minWidth: 0` missing on grid children → flex/grid items won't shrink below content size.

### 2. AppShell content area (`web/src/app/app-shell.tsx:18-33`)
```tsx
const content = css({
  display: "flex",
  flex: "1",
  flexDirection: "column",
  gap: "3",
  minHeight: "0",
  overflowY: "auto",         // ← only this scrolls
  p: "4",
});
```
- Only **one scroll owner** (`content`) — correct in principle.
- But `shell` has `overflow: hidden` → if `content` grows taller than remaining space, it scrolls internally. However at narrow widths the **topbar + sidebar (as top bar)** + `content` padding may exceed 100dvh.

### 3. Topbar (`web/src/app/topbar.tsx:40-53`)
```tsx
const tools = css({
  display: "flex",
  flexWrap: "wrap",
  gap: "2",
  alignItems: "center",
});
const toolField = css({ width: "15rem" });     // ← fixed width
const selectRoot = css({ width: "11rem" });    // ← fixed width
```
- Search field **15rem (240px)**, level select **11rem (176px)**, sort select **11rem (176px)**.
- At 390px viewport (370px after padding), these **cannot fit side-by-side** → `flexWrap: wrap` stacks them but total height grows.
- `flexWrap: wrap` on tools row helps but combined with fixed 100dvh shell → content area shrinks.

### 4. Catalog panel columns (`web/src/app/catalog-panel.tsx:22-26, 214-216`)
```tsx
const COLUMNS = { num: "3rem", title: "1fr", artist: "12rem", level: "5rem", version: "5rem", actions: "7rem" };
```
```tsx
const levelCell = css({ display: { base: "none", md: "block" } });
```
- Fixed-width columns (`num`, `artist`, `level`, `version`, `actions`) **don't reflow**.
- At < 768px (`md`), `level` column hidden but `artist` (12rem=192px) + `actions` (7rem=112px) + `version` (5rem=80px) + `num` (3rem=48px) = **432px minimum** before `title` gets any space.
- 390px viewport → horizontal overflow or title truncated to 0.
- Row uses `whiteSpace: "nowrap"` + `textOverflow: "ellipsis"` → long Japanese titles become unreadable.

### 5. Chart editor layout (`web/src/components/chart-editor/chart-editor.tsx:38-87`)
```tsx
const layout = css({
  display: "grid",
  gridTemplateRows: "auto minmax(0, 1fr)",
  height: "100%",
  minHeight: 0,
  overflow: "hidden",
});
const body = css({
  display: "grid",
  gap: "3",
  gridTemplateColumns: "minmax(0, 1fr) 17rem",  // ← fixed 17rem (272px) side panel
  minHeight: "0",
  overflow: "hidden",
  p: "3",
});
const gridPanel = css({ ..., overflow: "hidden" });
const side = css({ ..., overflowY: "auto" });
```
- **Fixed 17rem (272px) inspector panel** on the right.
- At 1024px (`lg`): 1024 - 272 - padding ≈ 700px for grid — OK.
- At 768px (`md`): 768 - 272 - padding ≈ 450px for grid — tight but usable.
- At 640px (`sm`): 640 - 272 - padding ≈ 320px for grid — 8 lanes × 56px = 448px minimum (MIN_LANE_WIDTH) → **horizontal overflow**.
- At 390px: 390 - 272 = 118px for grid → completely broken.
- `overflow: hidden` on both `layout` and `body` → no scroll escape.

### 6. Editor toolbar (`web/src/components/chart-editor/editor-toolbar.tsx:38-41`)
```tsx
const field = css({ width: "8rem" });
const wideField = css({ width: "10rem" });
const selectRoot = css({ width: "6.5rem" });
```
- Fixed widths: name (8rem=128px), BPM (10rem=160px), resolution (6.5rem=104px), zoom (6.5rem=104px).
- Toolbar uses `flexWrap: "wrap"` (`bar` css) but at narrow widths stacks into tall row → pushes grid down inside fixed 100dvh.

### 7. Note grid (`web/src/components/chart-editor/note-grid.tsx:24, 43-50`)
```tsx
export const MIN_LANE_WIDTH = 56;   // 8 lanes = 448px minimum
```
```tsx
const scrollArea = css({
  flex: "1",
  minHeight: "0",
  overflow: "auto",
  display: "flex",
  justifyContent: "flex-start",
  width: "full",
});
```
- Stage width = `8 * MIN_LANE_WIDTH * zoom` → at zoom 1: **448px minimum**.
- `scrollArea` has `overflow: auto` but parent `gridPanel` has `overflow: hidden` → **scroll clipped**.
- `usePanelSize` measures panel but doesn't enforce minimum → at narrow widths stage overflows invisibly.

### 8. Song publisher layout (`web/src/components/song-publisher/song-publisher.tsx:78-110`)
```tsx
const body = css({
  display: "grid",
  gap: "3",
  gridTemplateColumns: "minmax(0, 1fr) 21rem",  // ← fixed 21rem (336px) side
  minHeight: 0,
  overflow: "hidden",
  p: "3",
});
const sideScroll = css({ ..., overflowY: "auto" });
```
- Fixed 21rem (336px) metadata panel.
- At 1024px: 1024 - 336 = 688px for editor — OK.
- At 768px: 768 - 336 = 432px for editor — below 448px minimum.
- At 390px: completely broken.
- `overflow: hidden` on body → no page scroll.

### 9. Publish dialog (`web/src/components/song-publisher/publish-dialog.tsx`)
- Uses Ark UI `Dialog` — generally responsive but summary grid `gridTemplateColumns: "auto minmax(0, 1fr)"` may need attention at very narrow widths.

### 10. New song flow (`web/src/components/new-song/new-song-flow.tsx:33-41`)
```tsx
const root = css({
  ...
  gridTemplateRows: "auto auto minmax(0, 1fr)",
  height: "100dvh",
  overflow: "hidden",
});
const stepFrame = css({ maxWidth: "44rem" });  // 704px centered
```
- `100dvh` + `overflow: hidden` again.
- Step content (`stepScroll`) has `overflowY: auto` but frame is centered with `maxWidth: 44rem` — at narrow widths horizontal padding from `px: "4"` on `stepScroll` + frame margins may cause horizontal overflow inside the scroll container.

### 11. Chart step (`web/src/components/new-song/chart-step.tsx`)
```tsx
const layout = css({
  display: "grid",
  gridTemplateRows: "minmax(0, 1fr) auto",
  height: "100%",
  minHeight: 0,
});
```
- Uses `ChartEditor` (lazy) which has its own fixed 17rem side panel → same problem as #5.

### 12. Page-level shells (`web/pages/chart.tsx:13-20`, `web/pages/publish.tsx:29`)
```tsx
// chart.tsx
const shell = css({ ..., height: "100dvh", overflow: "hidden" });
// publish.tsx
<div style={{ height: "100dvh", overflow: "hidden" }}>
```
- Every page repeats `height: 100dvh; overflow: hidden` → **no global scroll fallback**.

### 13. Global CSS (`web/src/styles/app.css:11-13`)
```css
body {
  overflow: hidden;
}
```
- Reinforces no-scroll at document level.

---

## T2: Required Reflow/Disclosure Behaviors

| Component | ≥1280px (xl) | 1024px (lg) | 768px (md) | 640px (sm) | 390px (mobile) | 200% zoom |
|-----------|--------------|-------------|------------|------------|----------------|-----------|
| Sidebar | Persistent left rail (13rem) | Persistent left rail | Top bar (horizontal) | Top bar (horizontal) | Top bar (horizontal) | Top bar |
| Topbar tools | All inline | All inline | Wrapped (2 rows) | Wrapped (2-3 rows) | Stacked (search full, selects full) | Wrapped |
| Catalog columns | All visible | All visible | Level hidden | Level hidden, artist truncated | Only num, title, actions | Level hidden |
| Chart editor side | Persistent (17rem) | Persistent (17rem) | Collapsible drawer | Collapsible drawer | Collapsible drawer | Collapsible |
| Chart editor toolbar | All inline | All inline | Wrapped | Stacked | Stacked | Wrapped |
| Note grid | Full, no scroll | Full, no scroll | Horizontal scroll if <448px | Horizontal scroll | Horizontal scroll | Horizontal scroll |
| Publisher side | Persistent (21rem) | Persistent (21rem) | Collapsible drawer | Collapsible drawer | Collapsible drawer | Collapsible |
| New song step | Centered 44rem | Centered 44rem | Full width, padded | Full width, padded | Full width, padded | Full width |

**Key principle**: Each screen has **exactly one scroll owner** (the content area). Sidebars/inspectors become **drawers** (off-canvas) or **accordions** at narrow widths. Toolbars **wrap/stack**. Tables **hide secondary columns** or become **card lists**.

---

## T3: Keyboard Requirements

| Surface | Tab order | Enter | Escape | Focus visible | Focus trap |
|---------|-----------|-------|--------|---------------|------------|
| Catalog list | Rows (links) | Open chart/publish | — | Yes (ring) | No |
| Chart editor toolbar | All controls | Activate button/select | Close dropdown | Yes | Dropdown only |
| Note grid (Konva) | Canvas focusable | Add note at beat | Clear selection | Yes (canvas ring) | No |
| Validation panel | List items | — | — | Yes | No |
| Publisher metadata form | All fields | Submit (if valid) | Close dialog | Yes | Dialog only |
| Publish dialog | Token input, buttons | Publish | Cancel/close | Yes | Dialog |
| New song stepper | Step buttons | Activate step | — | Yes | No |
| Dialogs (all) | Modal elements | Default action | Close | Yes | Yes (Ark UI) |

**Focus restore**: When a drawer/dialog closes, focus returns to its trigger.

---

## T4: Long Japanese Title / Loading / Error States

| State | Container | Risk | Fix |
|-------|-----------|------|-----|
| Long song title (JP) | Catalog row `titleText` | `whiteSpace: nowrap` + `textOverflow: ellipsis` → unreadable | At `<md`: allow wrap, clamp lines, tooltip on hover |
| Loading skeleton | Catalog panel `stateBlock` | Fixed height may not match content | Use aspect-ratio placeholders |
| Error message | `StateBlock` / inline | Long error text overflows | `maxWidth: 100%`, `wordBreak: break-word` |
| Validation issues | `ValidationPanel` list | Path `wordBreak: break-all` OK, message may wrap | Ensure `maxWidth: 100%` |
| YouTube preview | `YouTubePreview` | Aspect ratio fixed, may overflow at narrow | `aspectRatio: "16/9"`, `maxWidth: 100%` |

---

## T5: Verification Commands

```bash
# Type check
cd /Users/rebui/work/FlashBeat.74/web && npx tsc --noEmit

# Tests
cd /Users/rebui/work/FlashBeat.74/web && npm test
# → node --test tests/ (read API integration)
# → vitest run (unit tests)

# Lint + format
cd /Users/rebui/work/FlashBeat.74/web && vp check

# Local dev server (for manual viewport testing)
cd /Users/rebui/work/FlashBeat.74/web && FLASHBEAT_ADMIN_TOKEN=devtoken vp dev
# → http://localhost:5173
```

**Manual viewport testing checklist** (record before/after screenshots at each):
1. 1440×900 — desktop full
2. 1280×720 — desktop compact
3. 1024×768 — laptop / tablet landscape
4. 390×844 — mobile portrait (iPhone 12/13/14 logical)
5. 200% browser zoom at 1440×900 (effective 720×450)

**Visual evidence note**: Automated screenshot capture not available in this environment. Manual verification via local dev server required. PR will include **before/after screenshots** for each breakpoint where changes are visible.

---

## Files to Modify (Owned Paths)

| File | Primary Concern |
|------|-----------------|
| `web/src/styles/app.css` | Remove `body { overflow: hidden }`, add focus-visible ring |
| `web/src/app/app-shell.tsx` | Shell layout: remove fixed 100dvh, make sidebar responsive drawer |
| `web/src/app/sidebar.tsx` | Convert to drawer at `<md`, top bar at `<sm` |
| `web/src/app/topbar.tsx` | Make tool fields fluid, stack at narrow widths |
| `web/src/app/catalog-panel.tsx` | Column hiding, card layout at narrow, title wrapping |
| `web/src/app/panel.tsx` | Ensure panel doesn't trap scroll |
| `web/src/components/chart-editor/chart-editor.tsx` | Responsive side panel (drawer), toolbar wrap |
| `web/src/components/chart-editor/editor-toolbar.tsx` | Fluid fields, wrap/stack |
| `web/src/components/chart-editor/note-grid.tsx` | Horizontal scroll when stage > container, respect MIN_LANE_WIDTH |
| `web/src/components/song-publisher/song-publisher.tsx` | Responsive side panel (drawer) |
| `web/src/components/song-publisher/song-metadata-form.tsx` | Fluid form fields |
| `web/src/components/song-publisher/publish-dialog.tsx` | Verify dialog responsive |
| `web/src/components/new-song/new-song-flow.tsx` | Remove 100dvh, allow page scroll |
| `web/src/components/new-song/chart-step.tsx` | Inherit responsive ChartEditor |
| `web/src/components/new-song/stepper.tsx` | Verify stepper wrap |
| `web/pages/chart.tsx` | Remove page-level 100dvh shell |
| `web/pages/publish.tsx` | Remove page-level 100dvh shell |

---

## Acceptance Criteria

1. **No horizontal overflow** at any target viewport (DevTools → Elements → check `html`/`body` scrollWidth === clientWidth).
2. **All primary actions reachable** without horizontal scroll: Catalog → Chart → Publish → New Song.
3. **Single scroll owner** per screen: page (or content area) scrolls, not multiple nested scrollers fighting.
4. **Keyboard navigation** works end-to-end: Tab through every interactive element, Enter activates, Escape closes dialogs/drawers, focus visible always.
5. **Focus restore**: Closing a drawer/dialog returns focus to trigger.
6. **Long Japanese titles** readable (wrap or tooltip) at 390px.
7. **Loading/error/empty states** fit containers without overflow.
8. **Type check, tests, lint all pass**.