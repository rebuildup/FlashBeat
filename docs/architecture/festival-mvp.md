# FlashBeat Festival MVP — smallest staged delivery

Status: PROPOSED, not yet built or deployed. Target: 2026-11-07. Parent: #3.
Respect constitution/CONSTITUTION.md and organization/profiles/release-driven-solo.md.

## Frozen product boundaries

- Unity: only read catalog/chart, resolve yt-dlp stream, render audiovisual gameplay and input/judgment, display result and leaderboard. Do not keep online high scores locally.
- Cloudflare: ONE Worker app (Hono API + Vite/React SPA assets) and ONE D1 database. No media proxy, KV, R2 or additional service.
- Web Studio: edit the EXISTING JSON format with a simple 8-lane grid and YouTube iframe preview. One Publish action.
- Library first: Hono, React, Vite, Ajv, D1 prepared statements, existing Unity VideoPlayer, yt-dlp + Deno. No bespoke framework, ORM or note format redesign.
- Minimal access boundary: public reads and casual score writes; a single server-side secret ADMIN_TOKEN for song publishing, entered by operator at runtime, never bundled in frontend/Unity. Validate JSON and parameterize SQL.
- No accounts, guest sessions, anti-cheat, multiplayer, CRDT, WebSocket, cloud drafts, separate song revisions UI, per-user identity or offline music.

## Interface contract

Machine-readable:
- contracts/openapi.v1.yaml — exactly five routes
- contracts/schemas/chart.v1.schema.json — existing chart shape: name, maxBlock, BPM, offset, notes[{type,num,block,LPB}], lyrics?

Public routes:
1. GET /api/v1/catalog — lightweight song summaries + chartVersion.
2. GET /api/v1/charts/{songId}?version=N — immutable snapshot, Unity pins version during play.
3. PUT /api/v1/admin/songs/{songId} — validated atomic upsert, single Publish action.
4. POST /api/v1/scores — counts and nickname; server recalculates score.
5. GET /api/v1/leaderboards/{songId}?version=N — top ten for exact chart version.

One chart per song for festival; no chartId/difficulty hierarchy. Stable songId slug, chartVersion increments on each publish. Keep legacy note representation; translating old chart to timeMs would add unnecessary work and risk tonight.
Suggested D1 tables: songs(id, metadata, current_version), chart_versions(song_id, version, chart_json, note_count), scores(id, song_id, chart_version, nickname, p,g,b,m,score,created_at). Minimal catalogVersion can be a singleton counter if useful.
Unity polls catalog on select-scene entry and every 5–10 seconds while idle, NEVER applies chart changes during play.

Score formula (server side): floor(1,000,000*(5*perfect + 3*great + bad)/(5*totalNotes)).
Require perfect+great+bad+miss == chart note count; verify numeric ranges. Allow repeated nickname entries; no identity/login. Ranking is casual/client-unverified.

## Strict delivery gates — each leaves a runnable exhibition build

S0 BASELINE
- Windows restores repository + needed LFS assets, Unity 6000.5.6f1, compiles, builds Win64.
- Gate: an existing song selectable and playable to result (fix any blocking issue before advancing).
- Freeze build artifact with commit SHA; if S0 fails, do NOT start speculative feature refactors.

S1 YOUTUBE STREAM
- yt-dlp/Deno resolves a single compatible audio+video HTTP stream. Unity VideoPlayer outputs sound and drives judgment on playback time, including stalls.
- Gate: one streamed song completes; retry works on actual Win64 build. Keep S0 artifact.

S2 REMOTE SONG CATALOG
- Mac: implement only GET catalog, GET chart version, PUT song + D1 schema, seed existing chart; publish using curl/script.
- Win: replace fixed catalog access and per-song player binding only as required for dynamic addition.
- Gate: API publish a NEW playable chart; Unity displays and plays it WITHOUT recompiling. Keep S1 artifact.

S3 WEB CHART EDITOR
- Mac: Vite React SPA import existing JSON, minimal note grid, iframe preview, schema validation and Publish.
- Gate: edit/publish on Mac; appears in Windows Unity; play to result. Direct API publish remains fallback.

S4 CASUAL LEADERBOARD (optional if time)
- Mac: implement POST scores + GET leaderboard.
- Win: send final counts and render top ten; do not persist score/highscore locally.
- Gate: score appears from a second client after a completed run. If fails, exhibit S3 artifact.

A stage is not green from unit tests or source inspection alone. Real Windows build + play is required. Each next stage begins only after a runnable version is archived.

## Mac/Win Herdr allocation

- First: contract owner freezes OpenAPI, JSON Schema and an example based on an existing valid chart.
- Mac worker owns web/ and api/ + Worker/D1 migrations, validates endpoints with fixtures.
- Win worker solely owns Unity Assets/Game/Scripts, *.unity, *.prefab and Unity Editor. Never run concurrent editors on a shared project.
- Separate reviewer handles contract and build-evidence checks without simultaneously modifying Unity scene assets.
- Use agent-router / Herdr / worktrunk with separate remote branches; never assume Mac and Windows share a filesystem or Unity Library cache.
- GitHub Issue -> issue-number branch -> Draft PR to release-0-1-0 -> review -> merge commit -> authorized release PR to main. Record SHA and artifact/scene test evidence.
- Features after a gate must not break the last runnable build.

## Explicitly deferred

Staff login UI, anti-cheat and playback proofs, durable cloud drafts, multiple difficulty charts, score identities, telemetry, realtime socket sync, advanced waveform editing, media storage, app migration, full design-system overhaul.

First action: Windows prove an actual yt-dlp streamed song. Mac can scaffold API in parallel from frozen contract. Do NOT claim launch readiness until tested on exhibition hardware.

Technical playback does not establish permission to exhibit copyrighted YouTube content; ensure event use is authorized.
