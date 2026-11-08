# Spec: Festival dashboard, statistics and song archiving

**Issue**: #151 | **Branch**: `w/wweb` (merge of `feature/web-dashboard-stats` into `integration/v6-followup`) | **Status**: Implemented, verified locally (evidence in `docs/evidence/151-dashboard-archive.md`)

## Objective
Give the Web Studio three things the festival needs: a ranking page, a statistics
dashboard and a song archive that removes a song from the public catalog without
ever deleting it. The archive is a logical delete (`songs.archived`), so the
chart, the scores and the ranking of an archived song survive.

## Owned Paths
- `web/routes/api/v1/rankings.ts`, `web/routes/api/v1/stats.ts`
- `web/routes/api/v1/admin/songs.ts`, `web/routes/api/v1/admin/songs/[songId].ts`
- `web/routes/api/v1/_archive-model.ts`, `web/routes/api/v1/catalog.ts`
- `web/pages/dashboard.tsx`, `web/pages/dashboard.server.ts`
- `web/pages/songs.tsx`, `web/pages/songs.server.ts`
- `web/src/app/dashboard-panel.tsx`, `web/src/app/songs-panel.tsx`
- `web/src/app/studio-layout.tsx`, `web/src/app/page-header.tsx`, `web/src/app/sidebar.tsx`
- `web/src/lib/dashboard/**`, `web/db/schema.ts`, `web/db/migrations/**`
- `contracts/openapi.v1.yaml`, `docs/operations/d1-migrations.md`

## Exclusions
- No `DELETE` route for a song. The festival never deletes a song.
- No change to the Unity code (`Assets/`) and no deploy to production D1 or
  Cloudflare from this branch.
- No change of the Japanese navigation design of issue #215 (the visitor entry
  "曲を選ぶ" and the staff group stay as they are).

## User Scenarios & Testing
### Scenario 1 (P1)
Given the festival has played songs, when the operator opens `/dashboard`, then
the page lists the best score, the play count and the song count of every song,
and the totals of the festival.
Independent test: `GET /api/v1/rankings` and `GET /api/v1/stats` answer 200 and
match the contract (`contracts/openapi.v1.yaml`).

### Scenario 2 (P1)
Given a live song, when the operator archives it from `/songs`, then the song
leaves `GET /api/v1/catalog` and appears in `GET /api/v1/admin/songs` with
`archived: true`, and the archive can be undone.
Independent test: `POST /api/v1/admin/songs/{songId}` with `{"archived": true}`
then `{"archived": false}`.

### Scenario 3 (P1)
Given a visitor, when the visitor opens the studio, then the Japanese navigation
of issue #215 is intact and `/dashboard` and `/songs` are reachable from the
sidebar.
Independent test: open `/` and follow the two new entries of the staff group.

### Edge Cases
Archived song that was never played (no score row); staff token missing (401);
a body that is not `{"archived": <boolean>}` (422 with diagnostics); a song id
that does not exist (404).

## Requirements
- R1: The public catalog lists only the live songs (`archived = 0`).
- R2: The archive is reversible and never deletes data.
- R3: The staff list and the archive route need the same bearer token as the
  publish route.
- R4: The contract (`contracts/openapi.v1.yaml`) describes every new endpoint.
- R5: The navigation design of issue #215 and the `DraftInput` duration input of
  issue #163 are preserved.

## Dependencies & Gate
Depends on the D1 migration `20261107042653_friendly_purple_man.sql` (see
`docs/operations/d1-migrations.md` §8). The migration is **not applied** to the
production database by this branch.

## Rollback
Revert the merge commit. The migration is additive; the columns can stay unused.
