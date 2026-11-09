---
description: "FlashBeat lean per-issue specification template (project-local override of the Spec Kit core spec template)"
---

# Spec: 074 Responsive (Web Studio)

**Issue**: #74 | **Branch**: `w/w74fix2` | **PR base**: `main` | **Date**: 2026-11-09 | **Status**: Draft

**Input**: `Web Studio 390x844 で横スクロールが発生し、chart / publish では操作不能な要素が並ぶ。CSS / レイアウトで reflow & disclosure し、scroll ownership を確定する。`

> Lean rules (enforced by `docs/agent/spec-kit-workflow.md`):
> one Issue = one branch = one worktree = one Draft PR; ≤5 tasks; no retrospective
> re-specification of the legacy game; no second authority for project governance.

## Objective

Web Studio (home / chart / publish) must not produce a horizontal document scroll
at 390×844, and every screen must have exactly one visible scroll owner, so that
the primary actions of a screen stay reachable on a narrow viewport.

## Owned Paths

- `web/src/app/sidebar.tsx` — narrow rail: reflow + disclosure of the staff entries
- `web/src/app/app-shell.tsx` — shell scroll ownership (fixed pages own their inner scroll)
- `web/src/app/studio-layout.tsx` — grid column must not be widened by its content
- `web/src/app/panel.tsx` — page header / action cluster must wrap instead of overflow
- `web/src/app/catalog-panel.tsx` — the card title link must not overlay the page
- `web/src/components/chart-editor/editor-toolbar.tsx` — transport + `Chart setup` panel
- `pages/chart.tsx` — the publish token hint of the chart header
- `docs/evidence/web-studio/responsive-before/`, `docs/evidence/web-studio/responsive-after/` — measured evidence
- `specs/074-responsive/` — this specification

## Exclusions (explicitly out of scope)

- `web/tests/visual/**` — the harness belongs to #75; it is executed, never edited.
- No font size is reduced anywhere (Issue #74 forbids "shrink the font" as a fix).
- No feature is removed: every navigation entry, editor control and page action stays
  in the DOM and reachable (the staff rail becomes a disclosure, it is not dropped).
- No Unity file (`Assets/**`, `ProjectSettings/**`) is touched.
- The note grid keeps its own horizontal scroll (see Edge Cases); the stage is not shrunk.

## User Scenarios & Testing *(mandatory)*

### Scenario 1 — Narrow home without horizontal scroll (Priority: P1)

**Given** the Studio at 390×844, **When** the catalog is opened, **Then** the document
scroll width equals the viewport width (390) and no element sticks out to the right.

**Independent Test**: `cd web && CHROME_PATH=/Applications/Google\ Chrome.app/Contents/MacOS/Google\ Chrome npm run visual`
→ `home @ 390x844: hScroll=false clipped=0` and the run ends with `PASS` (exit code 0).

### Scenario 2 — Chart / publish actions reachable (Priority: P1)

**Given** the Studio at 390×844, **When** the chart page or the publish page is opened,
**Then** the primary actions (`公開に進む / Review & Publish`, the transport, `Chart setup`)
are inside the viewport and the document does not scroll sideways.

**Independent Test**: the same visual run →
`chart @ 390x844: hScroll=false`, `publish @ 390x844: hScroll=false`.

### Scenario 3 — Staff navigation still reachable on a narrow viewport (Priority: P2)

**Given** the Studio at 390×844, **When** the staff disclosure is opened, **Then** all
five staff entries are visible, laid out inside the viewport and operable.

**Independent Test**: manual / DOM check that `[data-testid="nav-staff-toggle"]` opens
`#studio-staff-nav` and that `nav-dashboard` … `nav-publish` are visible afterwards.

## Edge Cases

- The note grid stage is wider than a 390px viewport (8 lanes × 56px minimum = 448px).
  The grid panel owns that scroll (`overflow:auto`), the document does not. The stage is
  not shrunk below the playable lane width.
- `Chart setup` is a native `<details>`; on narrow viewports it becomes a fixed sheet with
  its own vertical scroll, so its controls cannot be pushed out of the viewport.
- The catalog card title link is absolutely positioned in the table layout (stretched link).
  In the narrow card layout it stays in the flow, otherwise it would cover the viewport.

## Requirements

- **R1**: At 390×844 the document scroll width must equal the viewport width on
  `/`, `/chart` and `/publish` (`horizontalScroll=false` in the visual report).
- **R2**: The narrow navigation must reflow (grid) and use a disclosure; no entry may
  stick out of the viewport and no entry may be removed.
- **R3**: Every screen must have one scroll owner: scrolling pages scroll the document,
  the chart editor scrolls inside its work area.
- **R4**: Font sizes are unchanged; the fix is layout only.
- **R5**: Keyboard operation is unchanged: `Tab` still reaches every entry, `Escape`
  still closes `Chart setup`, and no focus is dropped by the reflow.
- **R6**: `npm test` (node --test + vitest) passes completely, and `npm run visual` exits 0.

## Dependencies & Gate

- **Depends on**: #75 (the visual gate) — the harness is executed as the measurement
  authority; it is not modified by this issue.
- **Blocks**: —
- **Gate**: `npm run visual` exit code 0 with `hScroll=false` on every viewport, plus
  `npm test` fully green, plus before/after evidence under `docs/evidence/web-studio/`.
