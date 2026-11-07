# Spec 133 — Festival scoring API

Issue: #133 (refs #41). Base: `origin/release-0-1-3`.

## Goal

Implement the two missing operations of `contracts/openapi.v1.yaml`:

- `submitScore` — `POST /api/v1/scores`
- `getLeaderboard` — `GET /api/v1/leaderboards/{songId}?version=N`

## Requirements

1. **Submission validation** (`web/routes/api/v1/_score-model.ts`):
   - body valid per the frozen `ScoreSubmission` schema (bundled copy +
     drift test, same pattern as `_publish-model.ts`);
   - `songId` a published song, `chartVersion` an existing `chart_versions`
     row (current or older snapshot); unknown → 422;
   - `perfect + great + bad + miss` equals `chart_versions.noteCount`;
   - `maxCombo` (optional) ≤ noteCount;
   - nickname: 1–12 characters after trim, no control characters;
   - score recomputed **server-side** with the exact Unity formula
     (`Judge.deleteData` / `NotesManager.OnEnable`):
     `score = round(1e6 * floor((5p + 3g + b) / (5 * noteCount) * 1e6) / 1e6)`;
     a client-sent `score` that differs → 422 with expected/received.
2. **Storage + response**: row in `scores`; `201` with `{ score, rank }`
   (rank = 1 + strictly better scores of the same chart version).
3. **Leaderboard**: top 10 of one chart version, score desc, earliest first
   on ties; `Cache-Control: no-store`; unknown song/version → 404; missing
   or invalid `version` → 400; empty → `entries: []`.
4. **Abuse limits without new infra**: duplicate identical submission
   (songId + nickname + score) within 10 s → 429 + `Retry-After`; server
   clock only (`scores.createdAt`), never client time.
5. **No migration**: the `scores` table already carries every needed column.

## Out of scope

- Deployment (Supervisor owns deploys), anti-cheat beyond the listed checks,
  any new infrastructure.
