# Tasks: Issue #151

- [x] T1 Merge `feature/web-dashboard-stats` into `w/wweb` and resolve the eight
      conflicts (`catalog-panel`, `sidebar`, `chart-editor`, `editor-toolbar`,
      `youtube-preview`, `new-song-flow`, `song-metadata-form`,
      `song-publisher`).
- [x] T2 Keep the issue #215 Japanese navigation and add `/dashboard` + `/songs`
      as staff entries with the lucide icons.
- [x] T3 Take the new API routes (`/api/v1/rankings`, `/api/v1/stats`,
      `/api/v1/admin/songs`, `POST /api/v1/admin/songs/{songId}`) and the
      `archived = 0` filter of `/api/v1/catalog`.
- [x] T4 Take the new pages `/dashboard` and `/songs` with their panels and the
      `src/lib/dashboard` client.
- [x] T5 Take the DB migration `20261107042653_friendly_purple_man.sql`, the
      schema and the seed update. **Not applied to production D1.**
- [x] T6 Update `contracts/openapi.v1.yaml` and `docs/operations/d1-migrations.md`.
- [x] T7 Verify: `bunx tsc --noEmit` (exit 0), `bun run test` (73 node tests +
      399 vitest tests, all pass), `bun run build` (exit 0).
- [x] T8 Write the evidence file `docs/evidence/151-dashboard-archive.md`.
