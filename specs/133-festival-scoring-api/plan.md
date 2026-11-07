# Plan 133

| Step | Content |
| --- | --- |
| 1 | `contracts/openapi.v1.yaml`: additive v1 changes — `rank` in the 201 response, optional `maxCombo`/`score` in `ScoreSubmission`, documented 429. |
| 2 | `web/routes/api/v1/_score-model.ts`: bundled `SCORE_SUBMISSION_SCHEMA` + validator, nickname rules, judgment-sum/combo semantics, `computeUnityScore` (Unity formula), leaderboard response shape. |
| 3 | `web/routes/api/v1/scores.ts`: POST handler (422 diagnostics, 429 duplicate window, 201 `{score, rank}`). |
| 4 | `web/routes/api/v1/leaderboards/[songId].ts`: GET handler (400/404, top 10, no-store). |
| 5 | Tests: `web/tests/score-model.test.ts` (formula vs hand-computed Unity examples + schema drift), `web/tests/api.test.ts` integration describes, e2e `leaderboardStage` + `run.ts` wiring + `harness.spec.ts` unit tests. |

No D1 migration: `scores` already has `id, songId, version, nickname, p, g, b, m, score, createdAt`.
