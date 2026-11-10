# Browser E2E & production harness (issues #97, #41; browser gate since 2026-11)

This directory holds two different things:

1. **The Playwright browser E2E** (`*.spec.ts`) — the quality gate that drives
   the real dev server in a real Chromium (`npm run test:e2e`).
2. **The production harness** (`run.ts`) — stage-by-stage real-value
   verification of the production chain at `https://flashbeat.rebuildup.dev`
   (`npm run e2e`), plus its unit tests (`npm run test:e2e:harness`).

## Which command runs which suite

| Command                     | Suite                                                             | Where                        | Tests |
| --------------------------- | ----------------------------------------------------------------- | ---------------------------- | ----- |
| `npm test`                  | Unit: read-API integration (`node --test`) + app unit (Vitest)     | `tests/*.test.*`, `src/**`   | 73 + 436 |
| `npm run test:e2e:harness`  | Vitest unit tests of the production harness comparison logic      | `tests/e2e/harness.test.ts`  | 22    |
| `npm run test:e2e`          | **Playwright browser E2E** (Chromium, seeded local D1, dev server) | `tests/e2e/*.spec.ts`        | 12    |
| `npm run e2e`               | Production harness (real HTTP against the deployed worker)        | `tests/e2e/run.ts`           | stages, not tests |

`test:e2e` used to be the name of the Vitest harness suite (misleading: no
browser was involved). It is now the Playwright gate; the harness suite lives
under `test:e2e:harness`.

## Browser E2E (`npm run test:e2e`)

Playwright config: `playwright.config.ts` (testMatch: `studio-flow`,
`visitor-flow`, `editor-note-flow`, `studio-smoke`, `publish-flow`).

Prerequisites are not optional and **nothing is skipped**:

- The `webServer` entry seeds the local D1 (`void db seed`) and boots the dev
  server when `E2E_BASE_URL` (default `http://localhost:5173`) answers
  nothing; a server that is already up is reused as-is. A server that never
  comes up fails the run loudly.
- A missing browser fails naturally at the first launch.
- `reporters/gate-reporter.ts` counts the outcomes: **0 collected tests or a
  run where every test was skipped exits non-zero.** Individual skips are
  reported as a warning line.

```sh
cd web
npm run test:e2e                                   # seeds + boots the server itself
E2E_BASE_URL=http://localhost:5199 npm run test:e2e  # against an already-running server
```

`publish-flow.spec.ts` is a real write flow: Catalog → Editor → Publish with
the local admin token (`local-dev-only`) against the seeded local D1, asserting
the success panel and the catalog reflection (`catalogVersion`, title, chart
version). It writes to the isolated song id `e2e-publish-flow` and archives it
again afterwards, so the suite stays repeatable. It never touches the remote
database.

## Gate self-check

Proof that the gate can actually fail (2026-11-10, Playwright 1.65.0,
Chromium): with `studio-flow.spec.ts` asserting the row title `Who?` instead
of `Who`, the run exits 1 with:

```
  1) tests/e2e/studio-flow.spec.ts:33:1 › publish shows the admin-token boundary before pressing it

    Error: expect(locator).toBeVisible() failed

    Locator: getByTestId('catalog-list').locator('a[data-row-link]').filter({ hasText: 'Who?' }).first()
    Expected: visible
    Timeout: 15000ms
    Error: element(s) not found

  1 failed
    tests/e2e/studio-flow.spec.ts:33:1 › publish shows the admin-token boundary before pressing it ─
  2 did not run
  9 passed (39.3s)
```

Exit code: 1 (serial mode stops the remaining tests of the file). The reverted
suite is green again (12 passed).

## Production harness (issue #97, refs #41)

Stage-by-stage real-value verification of the production chain at
`https://flashbeat.rebuildup.dev`. Default mode is **read-only**; a write needs
the explicit flags and a staff token in the environment. The token is never
printed or written anywhere.

## Run

```sh
cd web

# Read-only (default): catalog -> one chart -> frozen chart schema
npm run e2e

# Pick a specific song instead of the first catalog entry
npm run e2e -- --song-id who

# Against a non-production origin
npm run e2e -- --base-url http://localhost:5173

# OPTIONAL write stage (publishes a modified copy of the chosen song; use a
# dedicated test song id only). Requires --song-id and a non-empty token env.
FLASHBEAT_ADMIN_TOKEN=… npm run e2e -- --write --song-id <test-song-id>
```

The token env var name defaults to `FLASHBEAT_ADMIN_TOKEN` and can be renamed
with `--token-env <NAME>`. `--write` without `--song-id` or without a non-empty
token env exits with code 2 before any request is made.

## Stages

| Stage                | Checks                                                                                                                                                                                                                                                                                             |
| -------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `catalog.*`          | `GET /api/v1/catalog` 200; `catalogVersion` integer ≥ 0; song shape; ids sorted + unique. Records `catalogVersion`, songs, ids.                                                                                                                                                                    |
| `chart.*`            | `GET /api/v1/charts/{id}?version=N` 200; `songId` and `chartVersion` equal to the catalog entry; `noteCount === chart.notes.length`.                                                                                                                                                               |
| `catalog.youtubeId`  | The catalog entry carries a well-formed 11 character video id. The legacy chart format has no `youtubeId`, so the video id is checked on the catalog entry.                                                                                                                                        |
| `chart.schema`       | The chart snapshot is valid against `contracts/schemas/chart.v1.schema.json` (Ajv draft 2020).                                                                                                                                                                                                     |
| `leaderboard.*`      | `GET /api/v1/leaderboards/{id}?version=N` 200; `songId`/`chartVersion` equal to the catalog entry; entries of `nickname`/`score`, at most 10; scores descending (ties earliest first). Read-only; an empty `entries` list passes.                                                                  |
| `write.*` (optional) | `PUT /api/v1/admin/songs/{id}` via `publishAndVerify` (`src/lib/publish/client.ts`) with a modified copy (an `[e2e #97]` marker in the legacy chart `name`); re-GET catalog and chart; assert `chartVersion + 1`, `catalogVersion + 1`, catalog `noteCount`, identity and exact snapshot equality. |

## Report

Every check becomes one entry `{ stage, expected, actual, pass }`. The report
goes to stdout and to `last-report.json` in this directory (gitignored). Exit
code 0 only when every entry passes.

## Unit tests

The comparison logic is unit tested with mocked `fetch`:

```sh
npm run test:e2e:harness
```
