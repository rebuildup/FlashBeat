# FlashBeat Festival v0.1 — executable architecture & worker contract

Target: 2026-11-07 高専祭. Parent: #3, contract PR #8.
Status: architectural decision, **not evidence of successful deployment or Win64 gameplay**.
This file is subordinate to `constitution/CONSTITUTION.md` and the project-init operating profile.

## Non-negotiable requirements
- **One agent / one GitHub issue / one worktrunk worktree / one Draft PR**. One owner for mutable Unity scenes and build/editor runtime.
- Every integrated **stage** must preserve a usable Windows festival artifact; no stage is declared done with source-only evidence.
- No attempt at offline YouTube audio library. Streaming is yt-dlp + Deno + Unity VideoPlayer (audio and video from one compatible stream).
- Unity remains a **player**: fetch catalog/chart, stream, judge, results, rankings. Song management, edits and scores belong to Cloudflare.
- Prioritize library integration instead of home-grown routing, ORM, UI primitives, canvas hit-testing, auth system, and build tooling.

## Locked libraries and design decisions

**Cloud app (web/):**
- `void` **native Void app** + **Vite+**, **React + TypeScript**. Not TanStack Start/React Router; avoid a second meta-framework and duplicate server routes.
- Initialize with `npm create void@latest web -- --toolchain vite-plus --framework react --starter sqlite --deploy none`; review generated files, then `vp install`.
- API handlers: Void's **file routes + defineHandler** (Hono already internal), NOT a new Hono app or custom express router.
- DB: Void `void/db` + **Drizzle built in** and D1 migration generation (`void db generate`, `void db migrate`). Do not add Prisma or a second ORM.
- Deploy: `void connect --platform cloudflare`, `void cloudflare status`, `void deploy --platform cloudflare`. This is the primary CF CLI bridge; Wrangler diagnostics are optional, do not build custom deployment scripts.
- Tooling: `vp check`, `vp test`, `vp build`; do not add a duplicate ESLint/Prettier/Vitest toolchain.
- UI: **shadcn/ui** (official CLI components, a single consistent **Nova preset**, **Base UI primitives**), **Tailwind CSS 4**, **lucide-react**, **Sonner** for toasts. Use official dashboard/sidebar example for shell; no homemade buttons, dialogs, form widgets or novel design system.
- Editor grid: **Konva + react-konva** for eight lanes, beat grid, click/drag to add/move/remove standard tap notes. No new canvas engine, no custom pointer hit-testing, no timeline editing framework.
- Web video preview: YouTube IFrame Player API. Never embed yt-dlp in the browser, and do not store or proxy media.
- Static verification: original JSON Schema `contracts/schemas/chart.v1.schema.json`, simple semantic checks in shared TS service. No chart format migration tonight.
- UX acceptance: dark, compact, editorial, consistent tokens; 1280×720 and 1440×900 screenshots; loading/empty/failure/publish-success states; keyboard controls discernible; UI feedback allowed without changing API.

**Unity:**
- Preserve current Unity 6, **uGUI + TextMeshPro**, `UnityWebRequest`, `JsonUtility`, `VideoPlayer`, Unity Test Framework. No new bespoke UI architecture or full UI Toolkit migration.
- Reuse current scene/prefab UI wherever feasible. Do not design an entirely new game UI before stream loop is proven.
- Score/judgment is calculated locally during a run; send final result once to server. Server owns scores/rankings. Avoid per-frame network writes.

**Tokens and authorization:**
- **Infisical is the only secrets source-of-truth.** Mac and Win use their existing Infisical CLI identity and environment/path. Machine identities are preferred for noninteractive workers.
- Expected keys: `CLOUDFLARE_API_TOKEN`, `CLOUDFLARE_ACCOUNT_ID` (non-secret), `FLASHBEAT_ADMIN_TOKEN`. Missing keys cause a **single immediate operator bootstrap gate**, not repeated "owner permission" deferrals.
- Cloudflare Worker runtime receives `FLASHBEAT_ADMIN_TOKEN` via `void secret put` as a deployment copy. Do not commit `.env` or use `VITE_*` for secrets. Do not log tokens.
- Authorized: create Cloudflare Worker + D1, generate/apply required migrations, set runtime secret, deploy to workers.dev, inspect logs, rollback failed deployments. **Do not buy paid plans, set production custom domains, alter other projects or leak sensitive values.**
- On a Mac worker with CLI credentials, run `void cloudflare status` noninteractively and check scoped account. In CI Void accepts `CLOUDFLARE_API_TOKEN` and account id. A missing/expired credential is real operator involvement; escalate once early.
- Avoid custom auth UI: one Bearer admin token on song publish, public read/score endpoints. Apply built-in input validation, prepared SQL, small rate bounds; do not design full identity/anti-cheat.

## Contracts and minimal data
Authoritative files: `contracts/openapi.v1.yaml` and `contracts/schemas/chart.v1.schema.json`.
Five endpoints, not more:
- GET catalog; GET immutable songId + chartVersion
- PUT admin song metadata + chart in one publish action
- POST score (nickname + judge counts); GET leaderboard top ten
One active chart per song, versioned snapshots; exact version pinned during gameplay. Existing `name,maxBlock,BPM,offset,notes[{type,num,block,LPB}],lyrics?` unchanged.
D1 stores `songs`, `chart_versions`, `scores`; no separate session, auth, draft, score-owner, or push infrastructure.
Server recalculates score = floor(1_000_000*(5P+3G+B)/(5*N)), requires P+G+B+M == N, labels ranking casual/client-unverified.
Unity only refreshes catalog on selection screen; no WebSocket.

## Release stages and stop conditions

1. **S0 working baseline**: Unity builds Win64 and existing playable song reaches ResultScene. Archive executable and SHA.
2. **S1 streamed**: yt-dlp + Deno stream works (A/V); notes/judgment aligned to video timeline, buffering handled. Archive executable.
3. **S2 remotely seeded**: Void API + D1 publish via curl; Unity pulls NEW song without rebuild; song plays to ResultScene. Archive executable.
4. **S3 Web Studio**: editor imports/modifies/validates/publishes JSON via official UI components and Konva grid; Mac browser publish -> Win Unity play. Archive executable. This is the primary feature target.
5. **S4 casual ranking**: POST score, leaderboard GET, Unity result submit + display. Optional if S3 is ready and time remains.

Do not proceed to destructive integration if the prior stage's proven build does not exist. The macOS backend can be developed in a separate worktree while Windows completes S0/S1. **Source changes are not evidence of a working build.**

## Work units, dependency and UI review

Each issue is a single slice with owned directories, acceptance criteria and tests, <= ~5 task checklist items. Use the Spec Kit `specify → plan → tasks → implement → converge` flow on substantive changes, preserving existing project-init constitution. Initialize Spec Kit once on a dedicated issue branch; do not overwrite governance files.

Use remote refs for macOS/Windows worker handoff, no shared mutable Unity Library. All work targets `release-0-1-0` through issue-number branches, Draft PR on first durable commit. Integrator lands by dependency order with merge commits. No automatic launch of unreviewed production changes. New UI feedback can be handled by an unallocated UI iteration issue without blocking playable S2/S3.

## Scope explicitly excluded
Cloud draft history, guest login/session IDs, anticheat/replays, websocket/CRDT, auto chart generation, waveform DSP, R2/KV, rebuilding Unity game interface, custom domain and marketing site, extra libraries without specific need.

## Verification and failure response
- Mac: `vp check`, `vp test`, API fixture and `void deploy` health + direct GET/PUT smoke.
- Win: Unity tests, actual Win64 build, one full song, retry, remote publish test, no exception spam.
- Keep each last-known-good build under external artifact storage; PR links commit SHA/build log/which chartVersion passed.
- When blocked by external credentials, record exact missing name/scope/owner once immediately, continue only independent work; don't fabricate a successful deployment.
