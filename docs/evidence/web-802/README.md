# Issue #802 — Web Studio grid / typography / tokens / image review

Branch `802` (base `release-0-2-1`). Rebuilds the Web Studio visual
foundation: shared grid contract, semantic design tokens, typography
scale, shared list primitives, and a mechanized image-review step.

## What changed

| Area | Change |
|---|---|
| Grid / layout | `web/src/app/shell-metrics.ts` — `shellColumns` (`13rem` rail + `minmax(0,1fr)`) and `contentArea` (padding `4`/`5`, gap `4`, scroll owner) are defined once; `app-shell.tsx` and `studio-layout.tsx` compose them. The literal lives in exactly one place because Panda extracts `css()` values statically. |
| Semantic colors | `web/src/theme/tokens/semantic-colors.ts` — role tokens (`fg.*`, `border`, `canvas`, `error`, `cta.bg/fg`). `panel.tsx` `primaryCta` now uses `cta.*` instead of raw `#EDEDED`/`#1B1B1B`. |
| Typography | `web/src/theme/text-styles.ts` — semantic styles `pageTitle`, `sectionTitle`, `panelTitle`, `rowTitle` (16px medium floor for list rows), `rowMeta`, `caption`, `numeric`. Adopted across shell, panels, topbar, page header, welcome hero. |
| List atoms | `web/src/app/list.tsx` — `Badge` (neutral/muted/outline), `num`/`numWide`, `metaLine`/`metaText`, `rowActions`/`actionLink(Subtle)`, `keyCap`, `visuallyHidden`. Shared by `catalog-panel.tsx` and `songs-panel.tsx`. |
| Song lists | Catalog: row titles → `rowTitle`, artist → `metaText`, level/publish → `Badge`, thumbnail 2.25rem → 3rem. Songs (archive): labeled header row (曲/プレイ/最高スコア/Ver), hover state, `Badge` for archived, plays folded into the meta line on narrow widths. |
| Feature screens | Raw hex colors in `new-song/*` and `song-publisher` migrated to semantic tokens. `chart-editor` stays exempt (Konva paints raw colors). |
| Governance | `web/src/theme/design-system.test.ts` rejects raw `#rrggbb(aa)` literals and off-scale `fontSize` values in governed dirs, and asserts the shell grid contract. Contract documented in `docs/design/web-design-system.md`. |
| Image review | `web/tests/visual/capture.mjs` now covers 1920/1440/1280/1024/768/390(+200% zoom) × `/`, `/songs`, `/dashboard`, `/chart`, `/new-song`, `/publish`. `web/tests/visual/REVIEW.md` is the reviewer checklist; `index.json.needsHumanVisualReview` points at it. |

## Verification

- `npx tsc --noEmit` — pass
- `npx vp check` (oxfmt + oxlint) — pass
- `npm run build` (`vp build`) — pass
- `node --test tests/` — 178 pass
- `vitest run` — pass. Note: under heavy parallel load (concurrent
  visual captures + dev servers on this machine) a few React tests
  exceeded their 5 s timeout during the first full run; every file
  passes on rerun and the touched areas have no logic changes.
- `npm run test:e2e:harness` — 22 pass
- `npm run visual` — exit 0; `index.json` reports no horizontal overflow
  and no console errors across all 42 captures.

## Image-review evidence

Capture run: `web/tests/visual/out/2026-11-20T18-50-16-908Z/` (gitignored;
regenerate with `cd web && npm run visual`). 42 captures = 6 routes ×
7 viewports, all DOM-clean.

Curated before/after pairs are committed under `before/` and `after/`
(`home` = catalog `/`, `songs` = archive; widths 1920/1440/768/390 plus a
1440 focus shot). The full 42-capture set stays in the gitignored
`out/<timestamp>/` directory.

**Review checklist**: apply `web/tests/visual/REVIEW.md` to every PNG —
layout, typography, spacing/density, color/contrast, states, operations.
For each pair record pass / fix-needed with the concrete defect.

### Review status

- [x] Capture tooling extended to the issue's required widths and routes
- [x] DOM-measurement gate PASS (no overflow, no console errors)
- [x] Checklist mechanized (`tests/visual/REVIEW.md`)
- [x] **Pixel review of the PNGs** — vision-model review (nemotron-3.5-super-vl
  via opencode-zen) on the 2026-11-21 capture set
  (`web/tests/visual/out/2026-11-21T05-25-16-538Z/`): catalog loaded state,
  single publish-requirement note, disabled-button contrast, sidebar
  current-page indicator and mobile filters all PASS; no new defects.

### 2026-11-21 fix pass (commit e842679d)

The first review round flagged six defects; `after/` now holds the
post-fix captures. Capture synchronization was fixed first —
`capture.mjs` waits until loading text is gone, so `home`/`songs` shots
show the loaded list (and `/songs` its pre-token state) instead of
"カタログを読み込み中". The disabled-button contrast fix required a
Panda cascade-layer correction: recipe `base._disabled` loses to the
later `variants` layer, so the muted colors are declared per variant.

### 2026-11-22 measure + review pass (commit 8a7225a)

Second round on the same branch: bounded the content measure and fixed
the catalog/sidebar findings from the rendered review.

- `shell-metrics.ts` adds `CONTENT_MAX_WIDTH` (`90rem`) + `contentMeasure`;
  `StudioFrame` caps ordinary studio content and centers it on ultrawide
  viewports. `/chart` opts out (`fluid`) because the Konva workspace needs
  the full width. DOM-verified: at 2560/1920 the main column is 1440px
  centered; at 1440 it fills the viewport; `/chart` stays fluid.
- Catalog publication note moved above the column header with aligned
  padding (was an orphaned row inside the table body).
- Sort/Level selects widened 8rem→9rem so `すべてのレベル` no longer
  ellipsizes; sidebar staff nav `要トークン` badge no longer wraps under
  the label at `md+`.
- `design-system.test.ts` pins the measure literal and the
  `contentMeasure`/`fluid` wiring so the contract cannot drift.

Capture run: `web/tests/visual/out/2026-11-22T03-03-10-948Z/` — 42
captures, `hScroll=false`, no console errors. The 4 `clipped` samples on
`/chart` and `/publish` at 390px are the Konva note grid: `MIN_LANE_WIDTH`
(56px × 8 lanes = 448px) is a playable minimum and the grid scrolls
horizontally inside its own `overflow:auto` region — by design, not a
document overflow.

Vision review (nemotron-3.5-super-vl-free via opencode) on the new set:

- `home` 1440 / 768 / 390 — **PASS**, no defects.
- `songs` 1440 — **PASS**.
- `chart` 1440 — **PASS**.
- `publish` 1440 — one minor note: the lyrics-section hint text is cut at
  the viewport bottom edge (below-fold scroll, expected) and the YouTube
  URL input shows a truncated value without an ellipsis affordance.
  Input values scroll natively; recorded as accepted, not a blocker.
