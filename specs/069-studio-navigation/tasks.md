# Tasks: Studio route navigation

- [x] T1 `web/src/app/sidebar.tsx`, `app-shell.tsx`: replace tab panels with semantic route links, Catalog current state. Acceptance: anchors resolve `/`, `/chart`, `/publish`, no Tabs root.
- [x] T2 `topbar.tsx`, `editor-panel.tsx`, `publish-panel.tsx`, `index.server.ts`: remove duplicate actions, fake panels, roadmap and stale backend comment. Acceptance: one catalog Refresh, no placeholder wording/false success toast. Depends T1.
- [x] T3 Run web tests/typecheck/build at candidate SHA; record commands and outcomes. Depends T2.
- [ ] T4 Capture real browser before/after at desktop/narrow sizes and exercise keyboard/Back/refresh. Depends T3; unresolved visual evidence does not imply visual approval.

## Validation evidence

Candidate `fe6201a` (plus formatter-only whitespace): `npm test` API 19 pass; Vitest 10 files / 89 pass. `npx tsc --noEmit` exit 0. `npm run build` exit 0, worker and client bundles generated. Production API suites intentionally skipped in normal tests; no publish performed. Browser validation in progress.
