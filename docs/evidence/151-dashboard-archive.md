# Evidence: Issue #151 — dashboard, statistics and song archiving

Date: 2026-11-08 (JST) · Branch: `w/wweb` · Merge source: `feature/web-dashboard-stats`
Base commit of the merge: `5b55248` (`integration/v6-followup`) · Status: implemented and verified locally.

## 1. What was done

`git merge feature/web-dashboard-stats` was run on `w/wweb` (whose HEAD is
`integration/v6-followup`). Eight files conflicted; all were resolved by hand.

### 1.1 Taken from the merge source (the acceptance criteria of #151)

| Area | Files |
|---|---|
| New API | `web/routes/api/v1/rankings.ts`, `web/routes/api/v1/stats.ts`, `web/routes/api/v1/admin/songs.ts`, `web/routes/api/v1/_archive-model.ts` |
| Archive route | `web/routes/api/v1/admin/songs/[songId].ts` (now `PUT` publish + `POST` archive/restore) |
| Public catalog filter | `web/routes/api/v1/catalog.ts` → `.where(eq(songs.archived, 0))` |
| New pages | `web/pages/dashboard.tsx` + `dashboard.server.ts`, `web/pages/songs.tsx` + `songs.server.ts` |
| New UI | `web/src/app/dashboard-panel.tsx`, `songs-panel.tsx`, `page-header.tsx`, `studio-layout.tsx` |
| New lib + test | `web/src/lib/dashboard/{client.ts,types.ts,__tests__/client.test.ts}` |
| DB | `web/db/migrations/20261107042653_friendly_purple_man.sql` (+ meta), `web/db/schema.ts` (`archived`, `archivedAt`, `songs_archived_idx`), `web/db/seed.ts` |
| Contract / ops | `contracts/openapi.v1.yaml` (`/api/v1/rankings`, `/api/v1/stats`, `/api/v1/admin/songs`, `POST /api/v1/admin/songs/{songId}`), `docs/operations/d1-migrations.md` §8 |
| Tests | `web/tests/api.test.ts` (rankings/stats/archive), `web/tests/db.test.mjs`, `web/tests/helpers/contract.ts` |

### 1.2 Conflict resolutions (HEAD design kept, merge-side icons added)

| File | Resolution |
|---|---|
| `web/src/app/sidebar.tsx` | **HEAD wins on the design.** The Japanese navigation of issue #215 (`NAV_VISITOR` = 「曲を選ぶ」 + `NAV_STAFF_ITEMS`) is the rendered rail. The lucide icons of the merge side were added, and `/dashboard` (ダッシュボード) and `/songs` (楽曲アーカイブ) became staff entries. `NAV_ITEMS` is still exported as the flat, icon-first list (visitor + staff) for other callers. `Sidebar` now accepts the optional `active` prop that `StudioLayout` passes. |
| `web/src/app/catalog-panel.tsx` | **HEAD wins on the wording and on the publish boundary.** The per-row "Publish" link of the old line stays replaced by the badge 「公開はスタッフ（要トークン）」 (issue #215 review). The icons of the merge side (`RotateCcw`, `X`) were added to the retry / clear-filters buttons. The unused `CloudUpload` import was dropped. |
| `web/src/components/chart-editor/chart-editor.tsx` | The preview guide and the YouTube footer of HEAD stay; the merge-side "Back to start" button (`Rewind`, `data-testid="rewind-button"`) was appended. `buttonIcon` css and the `buttonSecondary` / `Button` imports were added. |
| `web/src/components/chart-editor/editor-toolbar.tsx` | HEAD's Undo/Redo semantics stay (the merge side carried the older Import/Export buttons). Icons were added with the matching lucide glyphs `Undo2` / `Redo2` / `Trash2`. |
| `web/src/components/chart-editor/youtube-preview.tsx` | HEAD's `showControls` conditional and `startPlay` / `startPause` stay; the `Play` / `Pause` icons of the merge side were added. |
| `web/src/components/new-song/new-song-flow.tsx` | HEAD's 「曲を選ぶ / Back to songs」 link stays, with the merge-side `ArrowLeft` icon added in front of the text. |
| `web/src/components/song-publisher/song-metadata-form.tsx` | **HEAD wins on the input.** The duration field stays a `DraftInput` (issue #163: empty text becomes NaN, not 0); the merge-side `Timer` icon was added to the label. The merge-side plain `Input` (and its `durationValue`) was **not** taken. |
| `web/src/components/song-publisher/song-publisher.tsx` | Both sides are kept: HEAD's css definitions (`buttonSecondary`, `sidebarToggle`, `sideTitle`) **and** the merge-side `buttonIcon`. |

### 1.3 Deliberately not taken from the merge source

- `.claude/skills/unslop/**` (53 files): the merge source picked these up from
  `origin/expo-web`. They are agent tooling, unrelated to issue #151, and they
  are not part of the web product. They are left untracked in the working tree
  and are **not** part of the merge commit.
- `web/bun.lock`: the repository tracks `web/package-lock.json`; the bun
  lockfile is not part of this change and stays untracked.

## 2. Acceptance criteria of issue #151

| Criterion | Result |
|---|---|
| `GET /api/v1/rankings` works | Yes — `web/routes/api/v1/rankings.ts`; covered by `web/tests/api.test.ts` ("GET /api/v1/rankings and GET /api/v1/stats") |
| `GET /api/v1/stats` works | Yes — `web/routes/api/v1/stats.ts`; same test block |
| `GET /api/v1/admin/songs` works | Yes — `web/routes/api/v1/admin/songs.ts` (staff bearer token, 401 without it); test block "GET /api/v1/admin/songs" |
| `POST /api/v1/admin/songs/{songId}` archives and restores | Yes — `{"archived": true}` / `{"archived": false}`; test block "POST /api/v1/admin/songs/{songId} (archive)" |
| `GET /api/v1/catalog` filters `archived = 0` | Yes — `web/routes/api/v1/catalog.ts:46` |
| No `DELETE` route | Yes — `grep -rn "\.delete(" web/routes/api/v1/` returns nothing; the archive route only flips the flag |
| `/dashboard` and `/songs` exist and are reachable | Yes — `web/pages/dashboard.tsx`, `web/pages/songs.tsx`; both are entries of the staff group of the sidebar (`data-testid="nav-dashboard"`, `data-testid="nav-songs"`) |
| The contract has the new endpoints | Yes — `contracts/openapi.v1.yaml`: `/api/v1/rankings`, `/api/v1/stats`, `/api/v1/admin/songs`, `/api/v1/admin/songs/{songId}` |

## 3. Verification (all local, no production touch)

```
$ cd web && bunx tsc --noEmit
(no output — exit 0)

$ cd web && bun run test
ℹ tests 73   ℹ pass 73   ℹ fail 0        # node --test
 Test Files  42 passed (42)
      Tests  399 passed (399)             # vitest

$ cd web && bun run build
✓ built in 368ms
```

The merged API tests exercise the new endpoints against the local test
database (the migration is applied to the **local** database only):
`rankings`, `stats`, the staff list, the archive round-trip and the fact that an
archived song leaves the public catalog and comes back.

## 4. Residual risks / open points

1. **The migration is not applied to the production D1.** The new columns
   (`songs.archived`, `songs.archivedAt`) and the index exist only in the
   repository (`web/db/migrations/20261107042653_friendly_purple_man.sql`) and
   in the local test database. Until it is applied remotely, `GET /api/v1/catalog`
   would fail against production (unknown column). The procedure is written in
   `docs/operations/d1-migrations.md` §8. **This branch did not run it** — no
   `script/with-secrets`, no `d1 migrations apply`, no Cloudflare deploy.
2. **No deploy and no browser check.** `/dashboard` and `/songs` are client-side
   pages (`ssr = false`) that read the live API. They were verified by the type
   check, the build and the API tests, but not by a manual browser session
   against the production API.
3. **`songs.archived` has no `CHECK` constraint** (SQLite cannot add one to an
   existing table). The 0/1 invariant is enforced by the archive route only.
4. **The dashboard and the songs list read the API with the staff token.** The
   staff token handling of the panels was written on the merge branch and was
   not re-reviewed here beyond the type check and the tests.
5. **No `DELETE` anywhere** — a song can only leave the catalog through the
   archive flag, as #151 requires.

## 5. Files of this change

- `specs/151-dashboard-archive/{spec.md,plan.md,tasks.md}`
- `docs/evidence/151-dashboard-archive.md` (this file)
- the merge commit on `w/wweb` (not pushed)
