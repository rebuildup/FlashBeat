# Evidence — issue #70 (branch `70`, PR #108)

Environment: macOS arm64, Playwright Chromium (headless shell 1243), local dev server
(`web` dev, port 5180). Catalog data: the 26 songs of the production catalog
(`GET https://flashbeat.rebuildup.dev/api/v1/catalog`, read-only) replicated into the
**local** dev D1 through the local publish API (`PUT /api/v1/admin/songs/...` with
`dev-admin-placeholder`); chart bodies are synthetic Tap-note fills sized to each
song's real noteCount/duration — metadata (title/artist/level/bpm/duration/youtubeId)
is the real production data. No production writes.

## Checks (Playwright, head SHA at time of run: see git log)

| Check | Result |
|---|---|
| rows rendered (ready, 26 songs) | PASS — 26 `a[data-row-link]` at 1440/1280/390 |
| no horizontal scroll | PASS — `scrollWidth - clientWidth = 0px` at 1440×900, 1280×720, 390×844 |
| `/` focuses search | PASS — `document.activeElement.id === "catalog-search"` |
| search filters | PASS — 1 row for "poison" |
| row link target | PASS — `/chart?songId=poison&version=1` |
| ArrowDown moves row focus | PASS — focus moves to `/chart?songId=agon-beats&version=1` |
| Enter opens chart | PASS — navigation to `/chart?songId=agon-beats&version=1` |
| column alignment header↔rows | PASS — x positions equal (Lv 831/831, BPM 895/895, Time 983/983, Notes 1063/1063, Ver 1151/1151) |
| states | PASS — empty / error / no-match / offline all render their distinct block |
| offline → online recovery | PASS — "You are offline" block appears, auto-recovers to "26 songs published" |
| sort via `?sort=level\|bpm\|title\|notes\|length` | see commands below |

## Commands

```
npx tsc --noEmit                     # clean
npm test                             # node --test: 19 pass / 0 fail; vitest: 109 pass (14 files)
npm run build                        # OK (vite-plus)
```

## Screenshots (this directory)

- `ready-1440x900.png` / `ready-1280x720.png` / `ready-390x844.png` — 26 real songs,
  full column set / Time hidden / meta-line layout, no horizontal scroll.
- `focus-row-1440x900.png` — row focus (3rd row) with focus ring + row highlight.
- `state-empty/error/no-match/offline-1440x900.png` — the four non-ready states.
- `sort-notes-1440x900.png` — sort dropdown interaction.

## Known residues

- Chart bodies in the local replica are synthetic (real counts, synthetic positions);
  the production chart text itself is not replicated locally.
- Sort dropdown interaction was exercised through the Ark `data-part` triggers;
  native `<select>` automation does not apply.
