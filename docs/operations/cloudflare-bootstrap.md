# Cloudflare / Infisical bootstrap preflight (Issue #9)

Date: 2026-11-06 (JST) · Branch: `9` · Base: `release-0-1-0` · Scope: credentials, CLI and permissions only. No game/web code touched.

This file is the preflight record for the Cloudflare side of the festival stack: what is proven, what the account now contains, and the boundaries for the next issues. It never contains secret values.

## 1. Authorized scope

Authorized by Issue #9 (owner decision): use the existing Cloudflare account, create **one** FlashBeat Worker and **one** D1 database on the free tier, apply migrations, deploy to `workers.dev`, configure one runtime publishing secret, inspect/rollback deployments. Forbidden: paid plans, mutation of other projects' resources, reuse of another project's Infisical secrets or Cloudflare tokens. (Custom domains were originally forbidden; the 2026-11-06 domain decision of issue #9 authorizes exactly one — `flashbeat.rebuildup.dev`, see §8.)

## 2. Cloudflare account (non-secret facts)

| Fact | Value |
|---|---|
| Account ID | `c6ab6651a5d4d6d0d07686bbd3c3d56f` |
| Plan | Free tier — no active subscription |
| `workers.dev` subdomain | `rebuild-up-up` (already provisioned) |
| Worker host pattern | `https://<script>.rebuild-up-up.workers.dev` |
| Pre-existing resources | Other services (D1 + several Worker scripts) — **untouched**. Enumerate with `cf d1 list` / `cf workers list`; never modify them. |

## 3. Toolchain (project-local only)

| Tool | Version | Source |
|---|---|---|
| `cf` (official Cloudflare CLI) | 1.0.0-beta.28 | pinned in `mise.toml` as `"npm:cf" = "1.0.0-beta.28"` |
| `void` | 1.6.2 | pinned in `mise.toml` as `"npm:void" = "1.6.2"` |
| `infisical` | 0.44.15 | mise global install (CLI only, no secrets) |

Rules that hold on both the Mac and the Windows agent:

- **Never install these globally.** `mise install` in the repository root provisions the pinned versions; the binaries then run as `mise exec -- cf ...` / `mise exec -- void ...` (or `mise run <task>`).
- The `cf` package on the mise registry is a different tool (Cloud Foundry CLI) — only the `npm:cf` package above is the Cloudflare CLI.
- `wrangler` is **not** used. `void` bundles its own `cf` dependency (`void/node_modules/cf`) and does **not** depend on `wrangler`, so no indirect `wrangler` usage exists either. Verified in `void`'s installed dependency tree.
- Every Cloudflare command runs under `script/with-secrets` so `CLOUDFLARE_API_TOKEN` / `CLOUDFLARE_ACCOUNT_ID` are injected noninteractively.

## 4. Infisical binding (FlashBeat-only)

Infisical is the only secrets source of truth. FlashBeat has its own project; no other project's secrets or tokens are read, referenced or copied.

| Fact | Value |
|---|---|
| Instance | self-hosted `https://secrets.rebuildup.dev` (API `https://secrets.rebuildup.dev/api`) |
| Project | `flashbeat` — id `0645e78f-ae7a-48b3-8597-4986be983496` |
| Environments | `dev` (id `fb5dfc09-e64f-4940-a369-de5087bb3767`), `staging`, `prod` |
| Tracked binding | `.infisical.json` (workspaceId + defaultEnvironment `dev`) |
| Instance metadata | `config/infisical.sh` (site URL, project id, environment) |
| Injection wrapper | `script/with-secrets` (macOS/Linux), `script/with-secrets.ps1` (Windows) — same contract as the dotfiles wrapper, bound to the FlashBeat project |

Key inventory (names only) in project `flashbeat` / environment `dev` / path `/`:

| Key | Kind | State |
|---|---|---|
| `CLOUDFLARE_API_TOKEN` | secret | present (FlashBeat-scoped token, verified in §5) |
| `CLOUDFLARE_ACCOUNT_ID` | non-secret config | present (`c6ab6651a5d4d6d0d07686bbd3c3d56f`) |
| `FLASHBEAT_ADMIN_TOKEN` | secret (runtime publish token) | present, 32-byte random hex |

## 5. Verified capabilities (FlashBeat token only)

All commands below ran noninteractively through `script/with-secrets` with the FlashBeat-scoped token. No value was printed.

| Capability | Command | Result |
|---|---|---|
| Identity / auth | `cf auth whoami` | `tokenValid: true`, `authSource: CLOUDFLARE_API_TOKEN environment variable`, account `c6ab6651a5d4d6d0d07686bbd3c3d56f` |
| Void bridge | `void cloudflare status` | `Authentication: API Token`; deployment account pinned from `CLOUDFLARE_ACCOUNT_ID` |
| D1 read | `cf d1 list`, `cf d1 query <db> --sql "SELECT 1"` | OK (both the FlashBeat database and other databases are listable; queries return results) |
| D1 create | `cf d1 create --name <probe>` | OK — probe database created, then deleted |
| D1 delete | `cf d1 delete <database-id> --force` | OK — probe database removed |
| D1 schema write | `cf d1 query <flashbeat-db> --sql "CREATE TABLE …"` | OK on `flashbeat` (`rows_written: 2`); probe table dropped afterwards, database back to 0 tables |
| D1 migrations | `cf d1 migrations apply <db> --dir <dir>` | rejected with `SQLITE_AUTH` (7500) **on a database outside the token's resource scope**; the same command path is available for the `flashbeat` database, which is inside the scope (§6) |
| Workers Scripts write | `cf deploy --prebuilt --worker <probe>` | OK — `Uploaded …`, `Deployed … triggers`, `https://<probe>.rebuild-up-up.workers.dev` served HTTP 200 |
| Worker runtime secret | `cf workers secrets update <name> --worker <probe> --text "$FLASHBEAT_ADMIN_TOKEN" --type secret_text` | OK — `cf workers secrets list` shows the name and type only; the deployed worker observed the binding at runtime (`secretPresent: true`) |
| Deployment history | `cf workers deployments list --worker <probe>` | deployment id + version id + 100 % traffic → rollback target available |
| Worker delete | `cf workers delete <worker> --force` | OK — the probe Worker is gone and its `workers.dev` URL returns 404 |

### Scope boundary found while probing

The token's D1 **write** permission is resource-scoped to the FlashBeat database (`7de6fd23-dd7b-4e21-bddd-b28b33ee0462`): `CREATE TABLE` succeeds there, while the same statement against a different database returns `not authorized: SQLITE_AUTH`. Database *create* and *delete* work at account level. Consequences:

- Issue #14 must apply its migrations to the `flashbeat` database — no scope change needed.
- Creating an additional database later is allowed by the token, but writing to it would need a scope extension. Treat `flashbeat` as the single database for this project.

## 6. Resource state in the account

| Resource | Id | State |
|---|---|---|
| D1 database `flashbeat` | `7de6fd23-dd7b-4e21-bddd-b28b33ee0462` | exists, **0 tables**, no schema applied — the database for Issue #14 |
| Worker scripts owned by FlashBeat | — | **none** — every probe Worker was deleted (`flashbeat-preflight` from the earlier probe window, `flashbeat-cf-worker-probe` from this verification) |

No other Cloudflare resource was created, modified or deleted, and no other project's secrets or tokens were used.

## 7. Re-verification (run from the repository root)

```sh
mise install
./script/with-secrets bash -c 'mise exec -- cf auth whoami'
./script/with-secrets bash -c 'mise exec -- cf d1 list'
./script/with-secrets bash -c 'mise exec -- cf workers list'
./script/with-secrets bash -c 'mise exec -- void cloudflare status'
```

Expected: `tokenValid: true`, account `c6ab6651a5d4d6d0d07686bbd3c3d56f`, deployment account pinned from `CLOUDFLARE_ACCOUNT_ID`, and the D1 list containing `flashbeat`.

### Deploy path notes for Issues #12 / #16

- `cf deploy` (normal path) builds the project first and therefore needs a dev-server package such as `@cloudflare/vite-plugin` in the project — Void + Vite+ (Issue #12) already provides it, so `cf deploy` works without extra setup there.
- `cf deploy --prebuilt` deploys an existing Build Output Specification tree and needs no build. Layout that was proven to work:
  ```
  .cloudflare/output/v0/config.json                                   # { "buildContext": { "isPreview": false } }
  .cloudflare/output/v0/workers/default/worker.config.json            # { "name", "compatibilityDate",
                                                                        #   "manifest": { "type": "complete",
                                                                        #                  "mainModule": "worker.js",
                                                                        #                  "modules": { "worker.js": { "type": "esm" } } } }
  .cloudflare/output/v0/workers/default/bundle/worker.js               # the module worker source
  ```
- Secrets can be shipped with the version (`cf deploy --secrets-file <file>`) or bound afterwards (`cf workers secrets update <name> --worker <worker> --text "$VALUE" --type secret_text`). Both were exercised; the value never appeared in output.

## 8. Retry / rollback and safe boundaries

- **Deploy retry:** `cf deploy` is idempotent per Worker name; a failed upload leaves the previous version serving traffic.
- **Rollback:** `cf workers deployments list --worker <worker>` → `cf workers deployments create --worker <worker> --versions '[{"version_id":"…","percentage":100}]'` to re-point traffic at an earlier version; Void equivalent: `void project rollback <deployId>`.
- **D1 rollback:** `cf d1 time-travel` restores a database to a point in time; migrations are applied with `cf d1 migrations apply <database> --dir <dir>` and are recorded in `d1_migrations`, so a failing migration must be reverted by a follow-up migration rather than by editing that table.
- **Boundaries (do not cross without new authorization):** no paid plan, no change to other projects' Workers/DNS/R2/KV, exactly one FlashBeat D1 database, no `.env` committed as source of truth, no secret value printed or written into any log, ticket or commit.
- **Custom domain (authorized 2026-11-06, issue #9 domain decision):** the
  canonical production hostname is `flashbeat.rebuildup.dev`, attached to the
  `flashbeat-web` Worker as a **Custom Domain** (`custom_domain: true` route in
  `web/void.config.ts`), not as a manual-DNS Worker Route. Cloudflare creates
  the DNS record and the Universal SSL certificate automatically; no paid
  change. The `rebuildup.dev` zone is active on the pinned account and the
  hostname had no existing DNS record when attached. `secrets.rebuildup.dev`
  and every other record/service of the zone are untouched.
- **Noninteractive rule:** every Cloudflare command runs through `script/with-secrets` (or an explicit `--projectId` / `--env`) so the self-hosted project and environment are always explicit.

## 9. Handoff

| Item | Owner issue |
|---|---|
| Void app scaffold + `void connect --platform cloudflare` | #12 |
| D1 schema + `cf d1 migrations apply 7de6fd23-dd7b-4e21-bddd-b28b33ee0462 --dir <migrations>` | #14 |
| Catalog/chart/publish API on `flashbeat.rebuildup.dev` (Custom Domain) and `workers.dev` fallback, `void secret put FLASHBEAT_ADMIN_TOKEN` (or `cf workers secrets update`) | #15 / #16 |
