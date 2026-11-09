---
description: "FlashBeat lean per-issue implementation plan template (project-local override of the Spec Kit core plan template)"
---

# Plan: 074 Responsive (Web Studio)

**Issue**: #74 | **Branch**: `w/w74fix2` | **Base**: `main` | **Date**: 2026-11-09
**Spec**: [`specs/074-responsive/spec.md`](spec.md)

## Summary

The horizontal scroll at 390×844 was caused by the Studio rail: the navigation entries
were laid out as one `white-space: nowrap` flex row (887px wide) inside a viewport of
390px, and the rail had no width constraint, so the row widened the document to 899px.
The fix is layout only: the rail reflows into a grid at narrow widths and the staff
entries move into a disclosure, the page header / action cluster wraps, the editor
transport wraps, the `Chart setup` panel becomes a sheet with its own scroll, and the
catalog card title link stops being an absolutely positioned overlay. Each screen keeps
one scroll owner: the document for scrolling pages, the work area for the chart editor.

## Owned Paths

```text
web/src/app/sidebar.tsx
web/src/app/app-shell.tsx
web/src/app/studio-layout.tsx
web/src/app/panel.tsx
web/src/app/catalog-panel.tsx
web/src/components/chart-editor/editor-toolbar.tsx
web/pages/chart.tsx
docs/evidence/web-studio/responsive-before/
docs/evidence/web-studio/responsive-after/
specs/074-responsive/
```

**Structure Decision**: the defects are all CSS layout defects of existing components;
the components stay where they are, only their styles and (for the rail) one disclosure
state are touched. No new component, no new dependency, no new feature.

## Dependencies & Gate

- **Depends on**: #75 (the visual gate) — the harness measures the result; it is not edited.
- **Blocks**: —
- **Gate**: `npm run visual` exits 0 (no horizontal scroll, no console error) and
  `npm test` is fully green; before/after screenshots are attached under `docs/evidence/`.

## Exclusions

- Font sizes are not touched (Issue #74 forbids shrinking fonts).
- The note grid stage keeps its own horizontal scroll; the minimum lane width (56px)
  stays as it is.
- `web/tests/visual/**` is not modified.
- No Unity asset, scene or project setting is modified.

## Constitution Check

*GATE: must pass before implementation.*

- Authority: [`constitution/CONSTITUTION.md`](../../../constitution/CONSTITUTION.md) and
  [`organization/profiles/release-driven-solo.md`](../../../organization/profiles/release-driven-solo.md)
  are unchanged by this issue and remain the top-level contract.
- Mutable Ownership Safety: the work happens in the dedicated worktree
  `/Users/rebui/work/FlashBeat.web74` (branch `w/w74fix2`), so the shared checkout and
  the Unity project are untouched; only `web/**` and `docs/evidence/**` are mutable here.
- Evidence Integrity: the before screenshots come from the untouched capture of
  2026-11-09T04-20-33-490Z (main), the after screenshots from the run of this branch;
  both `index.json` files carry the measured numbers quoted in the PR.
- Violations: none.

## Verification Plan

| # | Check | Command / Method | Evidence to attach |
|---|-------|-------------------|--------------------|
| V1 | Visual gate (no horizontal scroll, no console error) | `cd web && CHROME_PATH=/Applications/Google\ Chrome.app/Contents/MacOS/Google\ Chrome npm run visual` | `docs/evidence/web-studio/responsive-after/index.json` + PNGs, exit code 0 |
| V2 | Unit / integration tests | `cd web && npm test` | test summary (73 + 399 passed, 0 failed) |
| V3 | Before evidence (unchanged main build) | capture of `origin/main` at 2026-11-09T04-20-33-490Z | `docs/evidence/web-studio/responsive-before/` |
| V4 | Keyboard / focus | visual report focus descriptors at 1440×900 (`focus1`, `focus3`) | `docs/evidence/web-studio/responsive-after/*_focus*.png` |

## Rollback

`git revert` the single merge commit of this branch: the change touches only `web/**`
styles plus new evidence and spec files, so no other branch depends on it and the Unity
project is unaffected.
