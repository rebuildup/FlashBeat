# Plan: Merging `feature/web-dashboard-stats` into the v6 integration line

## Situation
`feature/web-dashboard-stats` (4 commits) implements issue #151. It was built on
an older line (`origin/expo-web`) than `integration/v6-followup`, so the merge
touches the components that the v6 line has since redesigned.

## Merge strategy
1. `git merge feature/web-dashboard-stats` on `w/wweb` (HEAD = `integration/v6-followup`).
2. Take the merge side of everything that is a **new** route, page, DB column,
   contract entry or test of issue #151 — those are the acceptance criteria.
3. Keep the HEAD side of the **design** decisions that the v6 line owns:
   - `web/src/app/sidebar.tsx`: the Japanese `NAV_VISITOR` / `NAV_STAFF_ITEMS`
     navigation of issue #215 stays the rendered navigation. The icon set of the
     merge side is added to it, and `/dashboard` and `/songs` become staff
     entries. `NAV_ITEMS` stays exported (flat, icon-first list) for callers.
   - `web/src/app/catalog-panel.tsx`: the per-row publish link of the old line
     stays replaced by the "公開はスタッフ（要トークン）" badge; only the icons
     of the merge side are added.
   - `web/src/components/new-song/new-song-flow.tsx`: the "曲を選ぶ / Back to
     songs" link stays, with the `ArrowLeft` icon added.
   - `web/src/components/song-publisher/song-metadata-form.tsx`: the duration
     field stays a `DraftInput` (issue #163); only the `Timer` icon is added.
   - `web/src/components/song-publisher/song-publisher.tsx`: both the HEAD css
     definitions (`buttonSecondary`, `sidebarToggle`, `sideTitle`) and the merge
     side `buttonIcon` are kept.
   - `web/src/components/chart-editor/*`: the guide, the footer and the
     Undo/Redo semantics of HEAD stay; the icons and the "Back to start" button
     of the merge side are added.
4. Verification: `bunx tsc --noEmit`, `bun run test`, `bun run build`.
5. Evidence in `docs/evidence/151-dashboard-archive.md`.

## Out of scope
No production D1 apply, no Cloudflare deploy, no Unity change, no `DELETE`
route.
