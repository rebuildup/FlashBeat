# Tasks: exhibition web line

- [x] Merge `origin/135`, resolve Judge.cs / Update.ps1 conflicts (both behaviours kept)
- [x] Merge `origin/133` (clean; score/leaderboard routes + fround model in)
- [x] Verify single-precision score model (`Math.fround` in `_score-model.ts`)
- [x] Verify openapi.v1.yaml covers `/api/v1/scores` and `/api/v1/leaderboards/{songId}`
- [x] `npm test` (47 node --test + 146 vitest, 0 fail) and `npx vp build` green
