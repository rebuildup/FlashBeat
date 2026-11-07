# Production E2E harness (issue #97, refs #41)

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
npm run test:e2e
```
