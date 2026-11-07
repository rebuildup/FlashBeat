# Visual review — Issue #75 findings fixed in #158

BEFORE: branch `75` — `specs/075-visual-gate/evidence/<viewport>/*.png` (40 images, real Chromium).
AFTER: this folder — `specs/158-visual-fixes/evidence/<viewport>/*.png` (40 images, real Chromium,
same harness `web/tests/visual/capture.mjs`).

Capture run (after): `web/tests/visual/out/2026-11-07T08-02-12-475Z/index.json`
→ `horizontal overflow: none`, `console errors: none`, `small fonts ( < 12px): 0` on every route.

| # | Severity | Finding (from the #75 review) | Status | AFTER evidence |
|---|---|---|---|---|
| 1 | High | chart-editor @ 390×844: the 448 px Konva canvas sat in an ~85 px pane, only lanes 1–2 reachable | **fixed** | `390x844/chart-editor.png` — the editor stacks, the grid is full width, lanes 1–7 are visible and the hint "Swipe the grid sideways to reach the lanes on the right." states that lane 8 is off screen on purpose; `390x844/chart-editor.lanes-right.png` is the grid scrolled to the right (lane 8 reachable) |
| 2 | High | publish @ 390×844: the metadata column (`21rem`) was drawn on top of the editor column | **fixed** | `390x844/publish-song.png`, `390x844/publish-empty.png` — one column, metadata under the editor, no overlap |
| 3 | Medium | the developer string `GET /api/v1/catalog failed` was shown as the user-facing error text | **fixed** | `390x844/home-error.png` — "The catalog could not be loaded. Please check your connection and try again." |
| 4 | Medium | the panel subtitle said "0 songs published" while the panel was in the error state | **fixed** | `390x844/home-error.png` — subtitle "Unable to load catalog" |
| 5 | Medium | "Catalog" appeared three times (page title, panel title, sort option) | **fixed** | `1440x900/home-ready.png` — page title "FlashBeat Studio", sort option "Published order", panel title "Catalog" |
| 6 | Medium | 8 px text (below the 12 px legibility floor) in the editor, toolbar, publisher | **fixed** | `small fonts: 0` on every route in `index.json`; visible in `1440x900/publish-song.png` (facts, badges) |
| 8 | Medium | the `Catalog` back link on the chart page was 43×18 px (below the 32 px tap target) | **fixed** | `1440x900/chart-editor.png` — the link now has 32 px+ of height (`smallTargets` count on the chart routes dropped to the remaining icon-only control) |
| 9 | High | publish @ 1440×900 / 1280×720: the primary action "Review publish" was cut off below the viewport | **fixed** | `1440x900/publish-song.png`, `1280x720/publish-song.png` — the publish card with the primary button sits at the top of the side column, above the scrolling metadata |

## Still open (not fixed here)

- `smallTargets` is still 1–2 per route: these are the icon-only controls (the YouTube link and the
  grid-density toggle). They were out of scope for this pass and are listed in `index.json`
  (`needsHumanVisualReview`).
- The note grid is wider than a 390 px viewport by design (8 lanes × 56 px minimum). It scrolls
  sideways instead of scaling down, so the lanes stay legible on a phone; the hint above the grid
  says so.
