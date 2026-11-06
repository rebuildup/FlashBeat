# Tasks: Catalog information design / density / row actions

**Issue**: #70 | **Branch**: `70` | **Base**: `67` (`9ad25d7`)
**Inputs**: [`spec.md`](spec.md), [`plan.md`](plan.md)

## Tasks

- [x] **T1** Define columns/rows/click area/keyboard/selection feedback and rebuild
      `web/src/app/catalog-panel.tsx` (full-width row links, tabular numerals,
      sentence-case headers, JP-safe ellipsis) — done when rows are anchors with
      hover/focus feedback and no 52rem cap.
- [x] **T2** Recessed panel surface + state blocks in `web/src/app/panel.tsx`
      (`panelFrame` bg = Pen `well`), keep `StateBlock` API — done when the list sits
      recessed on the canvas.
- [x] **T3** Five states: loading / empty / error+retry / filter-no-match (with reset
      action) / 12+ songs density (local publish API seed) — done when each state is
      captured and distinct. Plus offline state (live `online`/`offline` events) and
      sort (`catalog-view.ts`, topbar) per supervisor task.
- [x] **T4** Verify `npx tsc --noEmit` + `npm test` + `vp build`; screenshots
      1440×900/1280×720/390 before+after → `specs/070-catalog-ux/evidence/`.
- [x] **T5** Draft PR (base `67`) with evidence and residuals.

## Verification Evidence

| Task | Check | Command | Expected |
|------|-------|---------|----------|
| T1 | rows are links | DOM query `section a[href*="/chart?"]` | one per song |
| T3 | density seed | `curl -X PUT api/v1/admin/songs/...` ×11 | 201, catalog 12 songs |
| T4 | types/tests/build | `npx tsc --noEmit` / `npm test` / `vp build` | pass |
| T4 | pixels | Chrome headless 3 sizes | evidence committed |

## Rollback

Revert the PR merge commit.
