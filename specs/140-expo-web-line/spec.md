# Spec: exhibition web line (ranking API + Web Studio song registration)

## Goal
One branch (`expo-web`, based on `origin/109-patch`) carrying the exhibition-ready web studio so the
Supervisor can deploy https://flashbeat.rebuildup.dev in one step.

## Scope
- Merge `origin/135` (PR #139): Web Studio new-song registration UI (`web/pages/new-song.tsx`,
  `web/src/components/new-song/*`, `web/src/lib/new-song/*`, `PUT /api/v1/admin/songs/{songId}`).
- Merge `origin/133` (PR #137/#138): score submission + leaderboard API
  (`web/routes/api/v1/scores.ts`, `web/routes/api/v1/leaderboards/[songId].ts`,
  `web/routes/api/v1/_score-model.ts`).
- Score model must stay single-precision (`Math.fround`) to match Unity's float32 score math.
- `contracts/openapi.v1.yaml` must describe `/api/v1/scores` and `/api/v1/leaderboards/{songId}`.

## Non-goals
- No deploy, no production data writes, no D1 writes. Unity side (`Assets/`) untouched.

## Acceptance
- `cd web && npm test` green; `cd web && npx vp build` succeeds.
- Both API surfaces present and conflict-free on the release line.
