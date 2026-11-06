# Tasks: Studio route navigation

- [x] T1 `web/src/app/sidebar.tsx`, `app-shell.tsx`: replace tab panels with semantic route links, Catalog current state. Acceptance: anchors resolve `/`, `/chart`, `/publish`, no Tabs root.
- [x] T2 `topbar.tsx`, `editor-panel.tsx`, `publish-panel.tsx`, `index.server.ts`: remove duplicate actions, fake panels, roadmap and stale backend comment. Acceptance: one catalog Refresh, no placeholder wording/false success toast. Depends T1.
- [x] T3 Run web tests/typecheck/build at candidate SHA; record commands and outcomes. Depends T2.
- [x] T4 Capture real browser before/after at desktop/narrow sizes and exercise keyboard/Back/refresh. Depends T3; unresolved visual evidence does not imply visual approval.

## Validation evidence

Candidate `fe6201a` (plus formatter-only whitespace): `npm test` API 19 pass; Vitest 10 files / 89 pass. `npx tsc --noEmit` exit 0. `npm run build` exit 0, worker and client bundles generated. Production API suites intentionally skipped in normal tests; no publish performed. Browser validation in progress.

Browser evidence: actual Chromium1243 with cached Playwright at integration `ae13470` (#69 `21b1d8c` + #71 `058b53e`). Read-only API GET requests relayed to live production endpoint, catalog26 songs; no mocked songs and no writes. Keyboard Enter on 4th-smile selected `/chart?songId=4th-smile&version=1`, API200 and105notes; reload preserved identity; Catalog return, New chart explicit unpublished draft, browser Back and Publish real heading passed. `evidence/journey.log` records results. This proves navigation/identity, not working canvas or YouTube preview; screenshot initially still had grid loading. Browser captured1440x900/1280x720/390x844. Narrow width failed layout badly; #74 owns correction. Brand/icon/type/table-density still pending #67/#70. No original pre-change browser capture; image is integrated after-state, not complete visual approval.
