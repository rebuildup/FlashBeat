# Plan: Responsive Layout & Keyboard Navigation (#74)

> The plan follows the implementation that is on the branch. Where the first
> draft of the plan and the implementation differ, the implementation is
> described here and the reason is given.

## 1. Architecture decisions

### 1.1 Scroll ownership

| Screen | Owner | Implementation |
|--------|-------|----------------|
| Catalog (`/`) | document | `app.css`: `body { overflow: auto }`; `app-shell.tsx`: `minHeight: 100dvh` instead of `height: 100dvh` |
| Editor / publisher | the note grid | the page shells (`web/pages/**`) stay `100dvh`, the owned components fill them |
| Inspector drawer | the drawer panel | `overflowY: auto` on the panel |

The original plan also removed the `100dvh` page shells of
`web/pages/chart.tsx` and `web/pages/publish.tsx`. Those pages belong to
other issues, so the shells stay and the components own their scrolling
instead. The result is the same for the user: no screen has two competing
scroll areas.

### 1.2 Sidebar

* `md` and up: the persistent 13rem rail (unchanged).
* below `md`: a horizontal bar that sticks to the top of the document.
  The shell is a **block** below `md` (`display: { base: "block", md: "grid" }`)
  because a sticky element inside a grid area is limited to that area and
  could never travel.

### 1.3 Inspector panels (chart editor, publisher)

One element, two layouts:

* `lg` and up: the persistent right column (17rem / 21rem),
* below `lg`: an off-canvas drawer (fixed, right, 100dvh) with a
  backdrop, closed with `translateX(100%)` + `visibility: hidden`.

The first implementation rendered the inspector **twice** (once as the
column, once inside the drawer). That duplicated the YouTube iframe and
the whole metadata form — two sets of form fields with the same ids in one
document. The single element removes the duplication.

The shared parts live in `web/src/app/drawer.tsx`: the backdrop, the focus
management and the pure focus trap (`nextFocusTarget`), which is unit
tested in `web/src/app/drawer.test.ts`.

### 1.4 Toolbar fields

Fixed widths became fluid: `minWidth` + `maxWidth` + `width: 100%` for
the inputs, `minWidth` + `flexShrink: 0` for the selects. The rows already
had `flexWrap: "wrap"`, so the controls now wrap instead of overflowing.

### 1.5 Catalog columns

A table cannot become a card list with CSS alone, so the panel renders the
songs twice and shows one of the two: the table from `md` up, the card
list below `md`. The invisible copy is `display: none` and therefore
invisible to the screen reader and to the tab order.

### 1.6 Note grid

The horizontal scroll of the stage already existed (`scrollArea` has
`overflow: auto` and the stage keeps the 8 × 56px floor). The parents
that clipped it were relaxed, and the grid became focusable so the
shortcuts have a visible anchor.

### 1.7 Focus management

`useDrawerFocus` in `web/src/app/drawer.tsx`:

* opening → the focus moves to the first control of the panel,
* `Tab` / `Shift+Tab` → the focus cycles inside the panel,
* `Escape` → the drawer closes and the event is stopped, so the editor
  does not clear the note selection at the same time,
* closing → the focus returns to the trigger.

## 2. Implementation phases (as done)

1. **Global scroll foundation** — `app.css` (`body { overflow: auto }`,
   `:focus-visible` ring), `app-shell.tsx` (`minHeight`, block layout below
   `md`).
2. **Sidebar and topbar** — sticky horizontal bar below `md`, fluid tool
   fields.
3. **Catalog panel** — card list below `md`, wrapping titles, breakable
   error text in `panel.tsx`.
4. **Chart editor** — inspector drawer, fluid toolbar, focusable note grid.
5. **Song publisher** — inspector drawer, focus management, `aria-expanded`
   on the toggle.
6. **Keyboard and focus** — shared drawer module, global focus ring.
7. **Verification** — `tsc`, `npm test`, `vp check`; the visual gate stays
   open for #75.

## 3. Risks

| Risk | Mitigation |
|------|------------|
| A drawer without a visible close button traps the user | the panel has its own close button, the backdrop closes it, `Escape` closes it |
| The focus leaves the drawer through the page behind it | the trap is enforced on `Tab` and the panel is `aria-modal` |
| The inspector is mounted twice (forms, iframes) | one element, two layouts (§1.3) |
| A sticky sidebar inside a grid never travels | the shell becomes a block below `md` (§1.2) |
| The catalog card list duplicates content for the screen reader | the invisible copy is `display: none` |
| A closed drawer keeps its controls in the tab order | `visibility: hidden` removes them |

## 4. Dependencies

No new dependency. Panda CSS conditions, Ark UI (dialog), React refs and
`ResizeObserver` (already used by the note grid) are enough.
