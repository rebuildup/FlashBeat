---
description: "FlashBeat lean per-issue task list template (project-local override of the Spec Kit core tasks template)"
---

# Tasks: 074 Responsive (Web Studio)

**Issue**: #74 | **Branch**: `w/w74fix2` | **Base**: `main`
**Inputs**: [`spec.md`](spec.md), [`plan.md`](plan.md)

**Hard limit: at most 5 tasks per issue.** If the work does not fit in 5 tasks,
the issue is too big — split it and open a follow-up Issue.

## Format: `[ID] [P?] Description (path)`

- **[P]**: can run in parallel with other `[P]` tasks — different files, no ordering dependency
- Every task names the exact file path it creates or changes
- One task = one verifiable state change

## Tasks

- [ ] **T1** [P] Narrow rail: reflow the navigation into a grid and move the staff
      entries into a disclosure (`web/src/app/sidebar.tsx`) — done when: at 390×844 the
      rail is ~110px high, the staff entries are reachable through
      `[data-testid="nav-staff-toggle"]`, and no entry sticks out of the viewport.
- [ ] **T2** [P] Scroll ownership and header reflow: the fixed shell becomes a grid on
      narrow viewports, the grid column can no longer be widened by its content, the page
      header identity and the action cluster take their own row and wrap
      (`web/src/app/app-shell.tsx`, `web/src/app/studio-layout.tsx`,
      `web/src/app/panel.tsx`, `web/pages/chart.tsx`) — done when: `公開に進む /
      Review & Publish` and the token hint are inside the viewport at 390×844.
- [ ] **T3** [P] Catalog card title link: stop the absolutely positioned link from
      covering the thumbnail and the viewport (`web/src/app/catalog-panel.tsx`) — done
      when: the song title is readable next to the thumbnail and no invisible overlay
      link covers the page.
- [ ] **T4** [P] Editor toolbar: let the transport wrap and turn `Chart setup` into a
      sheet that owns its own scroll on narrow viewports
      (`web/src/components/chart-editor/editor-toolbar.tsx`) — done when: every transport
      control and every `Chart setup` field is reachable at 390×844.
- [ ] **T5** Evidence, gate and specification (`docs/evidence/web-studio/responsive-before/`,
      `docs/evidence/web-studio/responsive-after/`, `specs/074-responsive/`) — done when:
      `npm run visual` exits 0, `npm test` is green and the before/after screenshots and
      `index.json` are committed.

## Dependencies & Execution Order

1. [T1] → [T2] → [T3] → [T4] → [T5] (the gate runs last and proves all four)
2. [T1]–[T4] touch different files and could be implemented in parallel; [T5] needs the
   others to be finished.

**Blocking dependency**: the visual gate of #75 — it is only executed, never modified.
If the gate is unavailable, the DOM measurement in `web/tests/visual/out` and a manual
Chrome check at 390×844 are used as fallback.

## Verification Evidence

| Task | Check | Command / Method | Expected result |
|------|-------|-------------------|-----------------|
| T1 | Narrow rail | `npm run visual` → `home @ 390x844` | `hScroll=false clipped=0` |
| T2 | Header / actions | `npm run visual` → `chart @ 390x844` | `hScroll=false`, `Review & Publish` visible |
| T3 | Catalog card | `npm run visual` → `home @ 390x844` | `clipped=0`, title next to the thumbnail |
| T4 | Editor toolbar | `npm run visual` → `chart @ 390x844`, `publish @ 390x844` | `hScroll=false`, only the 4 stage elements of the note grid stay clipped |
| T5 | Gate + tests | `npm run visual` and `npm test` | exit code 0; 73 + 399 tests passed, 0 failed |

## Rollback

`git revert` the merge commit of `w/w74fix2` (or delete the branch before it is merged):
only `web/**` styles, the evidence folders and this spec folder are added, so no other
branch and no Unity asset depends on it.

## Notes

- Scope discipline: implement only the tasks above; new ideas become follow-up Issues.
- The Unity Editor / scene / prefab mutation is serialized: only the issue that
  owns the scene may run the Editor. This issue touches no Unity file at all.
