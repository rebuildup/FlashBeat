# Tasks: Studio route navigation

- [x] T1 `web/src/app/sidebar.tsx`, `app-shell.tsx`: replace tab panels with semantic route links, Catalog current state. Acceptance: anchors resolve `/`, `/chart`, `/publish`, no Tabs root.
- [x] T2 `topbar.tsx`, `editor-panel.tsx`, `publish-panel.tsx`, `index.server.ts`: remove duplicate actions, fake panels, roadmap and stale backend comment. Acceptance: one catalog Refresh, no placeholder wording/false success toast. Depends T1.
- [ ] T3 Run web tests/typecheck/build at candidate SHA; record commands and outcomes. Depends T2.
- [ ] T4 Capture real browser before/after at desktop/narrow sizes and exercise keyboard/Back/refresh. Depends T3; unresolved visual evidence does not imply visual approval.
