# Frozen v1 interface verification

Date: 2026-11-06 (JST) · Branch: `interface-freeze` · Base: `release-0-1-0`

The v1 read/write boundary of the festival API is frozen (issue #3, comment of
2026-11-06 22:55). This document records what the **deployed** Worker actually answers, so a
drift between the contract and the running API is visible before the exhibition.

## 1. What is verified

| Rule of the frozen v1 | Where it is checked |
|---|---|
| `GET /api/v1/catalog` → 200, `Cache-Control: no-store`, `{catalogVersion, songs[]}` | `web/tests/production-api.test.ts` |
| `catalogVersion = COUNT(chart_versions)` | same test, cross-checked against the real D1 with `cf d1 query` |
| `songs[]` lists only songs with a `(songId, songs.currentVersion)` snapshot, id ascending | same test |
| `GET /api/v1/charts/{songId}?version=N` → 200 `{songId, chartVersion, chart}` | same test |
| `version` missing / `0` / non-numeric → 400 | same test |
| unknown song or unknown version → 404 (no fallback to the current version) | same test |
| `PUT /api/v1/admin/songs/{songId}` → 200 `{songId, chartVersion, catalogVersion}` | same test (opt-in) |
| wrong staff token → 401, invalid payload → 422 with a diagnostic | same test (opt-in) |
| the same song published again → `chartVersion` + 1, `catalogVersion` + 1 | same test (opt-in) |

The response shapes are validated against the **frozen contract files**
(`contracts/openapi.v1.yaml`, `contracts/schemas/chart.v1.schema.json`), read from disk at
runtime. No copy of the contract exists in the test code.

## 2. Resources

| Fact | Value |
|---|---|
| Deployed Worker | `flashbeat-web` (`https://flashbeat-web.rebuild-up-up.workers.dev`) |
| D1 database | `flashbeat`, id `7de6fd23-dd7b-4e21-bddd-b28b33ee0462` |
| Staff token | Worker secret `FLASHBEAT_ADMIN_TOKEN` (value only in Infisical, never logged) |
| Migration | `20261106124714_heavy_harrier.sql` (`d1_migrations`, applied `2026-11-06 12:54:38`) |

## 3. How to run it

```sh
# local suite (default; never touches a deployed resource)
cd web && npm test

# deployed read checks, with the D1 cross-check
./script/with-secrets bash -c 'cd web && FLASHBEAT_E2E=1 node --test tests/production-api.test.ts'

# deployed publish checks (mutating, opt-in)
./script/with-secrets bash -c 'cd web && FLASHBEAT_E2E=1 FLASHBEAT_E2E_PUBLISH=1 node --test tests/production-api.test.ts'
```

The deployed suite is disabled unless `FLASHBEAT_E2E=1` is set, and the mutating publish
checks additionally need `FLASHBEAT_E2E_PUBLISH=1`. The token is read from the environment
variable `FLASHBEAT_ADMIN_TOKEN`; its value is never written into the repository, a log or a
test.

## 4. Observed behaviour of the deployed API (2026-11-06, 23:0x JST)

```
GET /api/v1/catalog
HTTP/2 200
cache-control: no-store
{"catalogVersion":0,"songs":[]}

GET /api/v1/charts/who?version=1
404 {"error":"chart version not found"}
```

| Frozen rule | Measured on the deployed API |
|---|---|
| catalog → 200 | 200 |
| `Cache-Control: no-store` | `no-store` |
| `catalogVersion = COUNT(chart_versions)` | `0` = `0` (D1 read with `cf d1 query`) |
| `version` missing / `0` / `-1` / `1.5` / `abc` → 400 | 400 for all five |
| unknown song → 404 | 404 |
| unknown version → 404 (no fallback) | 404 |
| response shapes | validated against `contracts/openapi.v1.yaml` |

The deployed read checks: **5 tests, 5 passed, 0 failed** (`FLASHBEAT_E2E=1`).

**Nothing is published yet.** `chart_versions` is empty, so `songs[]` is empty and
`GET /api/v1/charts/who?version=1` is a 404. That is the correct answer for an empty
catalog — but it also means the exhibition content is missing until the publish route is
deployed and used (see §5).

## 5. Deployment state and the publish path

The publish route (`PUT /api/v1/admin/songs/{songId}`, issue #16) is **not deployed**:

```
PUT /api/v1/admin/songs/who  →  404 Not Found
```

The integration build that is live contains the scaffold and the read API (#12, #15), not
#16. Therefore no production publish was performed: the publish path could not be executed
against the deployed API, and the supervisor's rule is to publish only through the API.

To publish the exhibition song once the publish route is live:

```sh
./script/with-secrets bash -c 'cd web && FLASHBEAT_E2E=1 FLASHBEAT_E2E_PUBLISH=1 node --test tests/production-api.test.ts'
```

That run publishes `who` with `db/fixtures/who.chart.json` (the byte identical copy of
`Assets/Resources/Who.json`), checks the 401/422 cases first, and verifies that the second
publish moves `chartVersion` and `catalogVersion` on by one.

### Deployment attempt of this branch (failed, nothing uploaded)

`void deploy --platform cloudflare` of the merged stack stopped before the upload:

```
■ deploy: the existing Worker has dashboard-managed routes or visibility that
  Void cannot safely republish.
  Add its current routes, workers_dev, and preview_urls values to the root
  Cloudflare config, then retry; no version was uploaded.
```

**No version was uploaded** and no production state was changed by this branch. The fix is
now in `web/void.config.ts`: `workers_dev: true`, `preview_urls: true` and `routes: []`,
mirroring the visibility of the existing Worker (`https://flashbeat-web.rebuild-up-up.workers.dev`,
preview url suffix `-flashbeat-web.rebuild-up-up.workers.dev`, no custom route). The next
deployer (the production owner) can deploy without hitting that wall.

## 6. Test results

| Suite | Result |
|---|---|
| `web/tests/api.test.ts` (#15, local Worker + local D1) | green |
| `web/tests/publish-schema.test.ts` (#16, contract drift guard) | green |
| `web/tests/db.test.mjs` (#14, migrations, fixture, constraints) | green |
| `web/tests/production-api.test.ts` (deployed, opt-in) | 5 passed / 0 failed with `FLASHBEAT_E2E=1`; skipped without it |

Local suite (`npm test`): **19 tests, 19 passed, 0 failed** (the two deployed suites are
reported as skipped without the environment flag).
