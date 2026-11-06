# Cloudflare / Infisical bootstrap preflight (Issue #9)

Date: 2026-11-06 (JST) · Branch: `9` · Base: `release-0-1-0` · Scope: credentials, CLI and permissions only. No game/web code touched.

This file is the single preflight record for the Cloudflare side of the festival stack. It states what is **proven**, what is **still missing**, and the **one** manual operator action that closes the gap. It never contains secret values.

## 1. Authorized scope

Already authorized by Issue #9 (owner decision): use the existing Cloudflare account, create **one** new FlashBeat Worker and **one** D1 database on the free tier, apply migrations, deploy to `workers.dev`, configure one runtime publishing secret, inspect/rollback deployments. Forbidden: paid plans, new custom domains, mutation of other projects' resources.

## 2. Cloudflare account (non-secret facts)

| Fact | Value |
|---|---|
| Account ID | `c6ab6651a5d4d6d0d07686bbd3c3d56f` |
| Plan | Free tier — no active subscription (`/accounts/<id>/subscriptions` returns none) |
| `workers.dev` subdomain | `rebuild-up-up` (already provisioned) |
| Worker host pattern | `https://<script>.rebuild-up-up.workers.dev` |
| Pre-existing resources in the account | Other services (D1 + several Worker scripts). **Untouched.** Not listed here; enumerate with `wrangler d1 list` / `wrangler deployments list` when needed. |

## 3. Toolchain

| Tool | Version | Source |
|---|---|---|
| `wrangler` | 4.158.0 | pinned in `mise.toml` (`"npm:wrangler"`) |
| `void` | 1.6.2 | pinned in `mise.toml` (`"npm:void"`) |
| `infisical` | 0.44.15 | mise global install |

`mise install` in the repo root provisions the same versions on the Mac and the Windows agent, so both sides drive identical CLIs.

## 4. Infisical binding (FlashBeat-only)

Infisical is the only secrets source of truth. FlashBeat has its **own** project; no other project's secrets or tokens are read, referenced or copied.

| Fact | Value |
|---|---|
| Instance | self-hosted `https://secrets.rebuildup.dev` (API `https://secrets.rebuildup.dev/api`) |
| Project | `flashbeat` — id `0645e78f-ae7a-48b3-8597-4986be983496` |
| Environments | `dev` (id `fb5dfc09-e64f-4940-a369-de5087bb3767`), `staging`, `prod` |
| Tracked binding | `.infisical.json` (workspaceId + defaultEnvironment `dev`) |
| Instance metadata | `config/infisical.sh` (site URL, project id, environment) |
| Injection wrapper | `script/with-secrets` (macOS/Linux) and `script/with-secrets.ps1` (Windows) — same contract as the dotfiles wrapper, bound to the FlashBeat project |

Key inventory (names only) in project `flashbeat` / environment `dev` / path `/`:

| Key | Kind | State |
|---|---|---|
| `CLOUDFLARE_API_TOKEN` | secret | **MISSING — manual step, see §6** |
| `CLOUDFLARE_ACCOUNT_ID` | non-secret config | present (`c6ab6651a5d4d6d0d07686bbd3c3d56f`) |
| `FLASHBEAT_ADMIN_TOKEN` | secret (runtime publish token) | present, 32-byte random hex, rotated after the probe in §5 |

## 5. What was proven, and how

Probes were run noninteractively against the Cloudflare API and the CLIs. **Provenance:** the credential available during the probe window is not admissible for FlashBeat use under the current policy, so the observations below are treated as *account-level capability evidence*, not as proof that the FlashBeat credential exists. The FlashBeat-scoped token must re-run the same checks (§7) before any deployment work starts.

| Check | Command | Result |
|---|---|---|
| Token liveness | `GET /user/tokens/verify` | active, no expiry recorded on the probe credential |
| Account resolution | `wrangler whoami` | resolves account `c6ab6651a5d4d6d0d07686bbd3c3d56f`, API-token auth from `CLOUDFLARE_API_TOKEN` |
| Void deployment account | `void cloudflare status` | `Authentication: API Token`, `Environment credentials: CLOUDFLARE_API_TOKEN`, deployment account pinned from `CLOUDFLARE_ACCOUNT_ID` |
| Worker write | `wrangler deploy` (probe script) | succeeded — Workers Scripts **Edit** available at account level; URL served HTTP 200 on `workers.dev` |
| Worker runtime secret | `wrangler secret put FLASHBEAT_ADMIN_TOKEN` | succeeded — Workers secret write available; `wrangler secret list` shows the name only |
| D1 read | `wrangler d1 list`, `wrangler d1 execute --remote` | list + query OK (read scope) |
| D1 create | `wrangler d1 create` | **failed with Cloudflare auth error 10000** — the probe credential had D1 *Read* but not D1 *Edit* |
| D1 write / migrations | `wrangler d1 migrations apply <db> --remote` | succeeded on a probe database (create table + migration record) → D1 *Edit* obtainable at account level |
| Token administration | `GET /user/tokens`, `POST /user/tokens` | 9109 / 403 — **no credential in this account can mint another token**; only the dashboard can |

Conclusion: the account itself allows Workers deployment, worker secrets, D1 creation and remote migrations. The missing piece is a **FlashBeat-scoped token** that carries both Workers Scripts Edit and D1 Edit; no other setup, no plan purchase and no other account are involved.

### Resources owned by FlashBeat in the account

| Resource | Id | State | Action |
|---|---|---|---|
| D1 database `flashbeat` | `7de6fd23-dd7b-4e21-bddd-b28b33ee0462` | created, **empty (0 tables)**, no schema applied | keep for Issue #14 (migrations not yet applied) |
| Worker script `flashbeat-preflight` | — | placeholder deployment used for the write proof; carries a stale copy of the pre-rotation admin secret | **delete once the FlashBeat token exists** (deletion needs a compliant credential; the placeholder does not expose any secret) |
| D1 database `flashbeat-preflight-d1` | — | probe database used for the migration proof | already deleted |

No other Cloudflare resource was created, modified or deleted.

## 6. The single manual operation (blocker)

Everything else is automated and ready. One owner action is required, and it is a **credential creation in the Cloudflare dashboard plus one Infisical write**:

1. Open the Cloudflare dashboard → **My Profile → API Tokens → Create Token** (custom token) for account `c6ab6651a5d4d6d0d07686bbd3c3d56f`, with exactly these permissions:
   - `Account → Workers Scripts → Edit`
   - `Account → D1 → Edit`
   - `Account → Account Settings → Read`  (lets `wrangler` resolve membership/plan without warnings)
   - optionally `Account → Workers Tail → Read` (runtime log inspection for Issue #16)
   - Resources: this account only. No zone permissions, no custom domains.
2. Store the token value in Infisical (self-hosted `https://secrets.rebuildup.dev`): project **`flashbeat`** (`0645e78f-ae7a-48b3-8597-4986be983496`), environment **`dev`**, path **`/`**, key **`CLOUDFLARE_API_TOKEN`**.

   ```sh
   # from the repository root, on the operator's machine
   infisical secrets set CLOUDFLARE_API_TOKEN=<value> \
     --projectId 0645e78f-ae7a-48b3-8597-4986be983496 --env dev --path /
   ```

`CLOUDFLARE_ACCOUNT_ID` and `FLASHBEAT_ADMIN_TOKEN` are already stored under the same project/environment/path, so no other secret has to be created or copied.

## 7. Re-verification to run once the token lands

Run from the repository root; the wrapper injects the FlashBeat project only.

```sh
mise install
./script/with-secrets bash -c 'wrangler whoami'
./script/with-secrets bash -c 'void cloudflare status'
./script/with-secrets bash -c 'wrangler d1 list'
./script/with-secrets bash -c 'wrangler deployments list'
```

Expected: the account id above, `Deployment account` pinned from `CLOUDFLARE_ACCOUNT_ID`, and — after the owner step — `CLOUDFLARE_API_TOKEN` visible in the injected environment:

```sh
./script/with-secrets bash -c 'test -n "$CLOUDFLARE_API_TOKEN" && echo present'
```

Immediately after that succeeds, delete the leftover probe Worker (`wrangler delete --name flashbeat-preflight`) and hand the account over to Issue #12 (app scaffold) / #14 (D1 schema + migrations).

## 8. Retry / rollback and safe boundaries

- **Deploy retry:** `wrangler deploy` is idempotent per script name; a failed upload leaves the previous version live.
- **Rollback:** `wrangler deployments list` → `wrangler rollback [<version-id>]`; Void equivalent `void project rollback <deployId>`.
- **D1 rollback:** `wrangler d1 time-travel` restores a database to a point in time; migrations are applied with `wrangler d1 migrations apply <db> --remote` and are recorded in the `_cf_KV`/`d1_migrations` table, so a failing migration must be reverted in a follow-up migration rather than by editing the table.
- **Boundaries (do not cross without new authorization):** no paid plan, no custom domain, no change to other projects' Workers/D1/R2/KV, at most one FlashBeat D1 database, no `.env` committed as source of truth, no secret value printed or written into any log, ticket or commit.
- **Noninteractive rule:** every Cloudflare CLI invocation must run through `script/with-secrets` (or an explicit `--projectId`/`--env`) so the self-hosted project and environment are always explicit.

## 9. Open items handed to the next issues

| Item | Owner issue |
|---|---|
| Create the FlashBeat-scoped Cloudflare token and store it as `CLOUDFLARE_API_TOKEN` (§6) | owner, one manual step |
| Delete the leftover `flashbeat-preflight` Worker after the token lands | #9 follow-up (automatic, no owner input) |
| Void app scaffold, `void connect --platform cloudflare`, `void secret put FLASHBEAT_ADMIN_TOKEN` | #12 / #16 |
| D1 schema + `wrangler d1 migrations apply flashbeat --remote` against `7de6fd23-dd7b-4e21-bddd-b28b33ee0462` | #14 |
