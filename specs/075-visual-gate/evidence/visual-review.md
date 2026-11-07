# Visual review — FlashBeat Web Studio (Issue #75)

Redline pass over the **real** screenshots of this pull request. Every finding
below was read off the PNGs in `specs/075-visual-gate/evidence/<viewport>/`
(not from the code alone); the measured facts in `index.json` are quoted where
they back a finding.

## What was reviewed

| Viewport | CSS viewport | Device pixels |
| --- | --- | --- |
| `1440x900` | 1440×900 | 1440×900 |
| `1280x720` | 1280×720 | 1280×720 |
| `1024x768` | 1024×768 | 1024×768 |
| `390x844` | 390×844 | 780×1688 (DPR 2, touch) |
| `1440x900@200%` | 720×450 | 1440×900 (browser zoom 200 %) |

Routes and states: `home-ready`, `home-error`, `home-empty`, `home-loading`,
`chart-editor`, `chart-empty`, `publish-song`, `publish-empty` — **40 viewport
screenshots**, plus 8 keyboard-focus screenshots and 40 full-page screenshots in
`web/tests/visual/out/<run>/`.

Every state really shows its state: the capture waits for a state marker
(`a[data-row-link]`, `Catalog unavailable`, `No songs published yet`,
`Loading catalog`, `Review & Publish`, `Publish a song`, the Konva `canvas`,
the YouTube player `.ytp-player`) and for the DOM to stop changing before it
reads the pixels. All 40 captures report `settled: true` in `index.json`.

## Criteria used

- **Pen v5 reference renders** — `design/exports/screens/*`,
  `design/exports/components/components-v5.png`. (`git-lfs` is not installed in
  this environment, so the files in the worktree are LFS pointers; they were
  fetched through the GitHub LFS API into a scratch directory for this review.
  The pointer files in the worktree are untouched.) The reference language:
  graphite well `#131313` under the `#1B1B1B` canvas, one blue accent
  `#3FA9F5` / `#9AD3FA`, a light raised primary button with dark text, keycap
  hints, tabular figures, no glow, no gradient, no emoji.
- **design-skills** — `layout-system` (alignment, scroll ownership, grid),
  `visual-hierarchy` (attention targets, salience competition),
  `information-density` (information per view, geometric density, hierarchy
  under compression), `interaction-state` (state authority, focus, transitions).
- **unslop** — `skills/unslop/SKILL.md` and `references/signs.md`.

## Findings

Severity: **High** = the surface cannot be operated (the release gate fails),
**Medium** = a visible defect an operator hits, **Low** = polish.

| # | Severity | Route · viewport | Defect | Responsible file |
| --- | --- | --- | --- | --- |
| 1 | **High** | `chart-editor` · 390×844 | **The editor cannot be operated on a phone.** The Konva canvas is **448 px wide inside a ~85 px pane** (`index.json` → `clippedElements`: `canvas {x: 12, w: 448}` in a 390 px viewport) — only the lane headers `1` and `2` are visible, lanes 3–8 are cut off. The YouTube preview takes the majority of the width while the primary work surface gets ~22 %, and the operation hint is squeezed into a 10-line column. | `web/src/components/chart-editor/chart-editor.tsx` (the grid / preview split has no narrow-viewport transformation) |
| 2 | **High** | `publish-song`, `publish-empty` · 390×844 | **The publish screen overlaps itself.** The body grid is `minmax(0, 1fr) 21rem` with no breakpoint (`web/src/components/song-publisher/song-publisher.tsx`), so the 336 px metadata column is drawn **on top of** the editor column — the editor's labels (`S`, `E`, `C`, `V`, `W`) show through the “Song metadata” card. The editor canvas is again 448 px wide in a 390 px viewport (`index.json` → `canvas {w: 448}`). | `web/src/components/song-publisher/song-publisher.tsx` (`body` grid) |
| 3 | Medium | `home-error` · all | The error state shows the raw developer string `GET /api/v1/catalog failed` as the user-facing description. | `web/src/app/catalog.ts` (`loadCatalog` throws the API string), rendered by `web/src/app/catalog-panel.tsx` (`description={errorMessage}`) |
| 4 | Medium | `home-error` · all | The panel subtitle says `0 songs published` **while the panel is in the error state** — a contradicting status line next to `Catalog unavailable`. | `web/src/app/catalog-panel.tsx` (the subtitle is only specialised for `loading` / `offline` / filters) |
| 5 | Medium | `home-ready` · 1440×900, 1280×720, 1024×768 | `Catalog` appears **three times** on one screen: the page title, the panel title and the value of the `Sort` control. The sort option does not say what it sorts (it is the published order). | `web/src/app/app-shell.tsx` (page title), `web/src/app/catalog-panel.tsx` (`panelTitle`), `web/src/app/catalog-view.ts` (`SORT_OPTIONS[0].label = "Catalog"`) |
| 6 | Medium | `chart-editor`, `chart-empty` · all widths | Six text elements render at **8 px** (`Del` keycap, `139 notes`, the operation hint line, the playhead readout `0.00s · beat 0.00`, `0.0s`, the contract-check line). 8 px is below the 12 px legibility floor and cannot be read on a HiDPI panel. | `web/src/components/chart-editor/chart-editor.tsx` (`fontSize: "2xs"`), `web/src/components/chart-editor/editor-toolbar.tsx` (note count) |
| 7 | Medium | `publish-song`, `publish-empty` · all widths | Eleven text elements render at **8 px**, including the whole facts list of the publish card (`Chart`, `139 notes at 128 BPM`, `Song id`, `who`) and the transport badge `Live API (this origin)`. | `web/src/components/song-publisher/song-publisher.tsx` (`facts`, `factKey`, `factValue`, `badge`) |
| 8 | Medium | `chart-editor` · all widths | The `Catalog` back link is **43×18 px** — the interactive target is 18 px high, below the 32 px minimum (`index.json` → `smallTapTargets` → `a {w: 43, h: 18}`). | `web/pages/chart.tsx` (`catalogLink`) |
| 9 | Medium | `publish-song` · 1440×900, 1280×720 | The primary action **“Review publish” is cut off at the bottom of the viewport**; the operator has to scroll the side column to reach the button that publishes. | `web/src/components/song-publisher/song-publisher.tsx` (`side` / `sideScroll` order: metadata card first, publish card last) |
| 10 | Medium | `chart-editor` · all widths | The lane guides are only an alternating column background (`#131313` / `#161616`, a 3/255 step), while the beat lines are drawn as an explicit 1 px `#222222` stroke. A note's **lane** is much harder to read than its beat, which is the wrong way round for a note editor. | `web/src/components/chart-editor/note-grid.tsx` (`LANE_BACKGROUND`, `LANE_BACKGROUND_ALT`, `GRID_LINE`) |
| 11 | Medium | `home-ready` · 1024×768 | The YouTube check action of a row is **clipped out of the panel**: the link sits at `x = 1011` with `w = 30` in a 1024 px viewport (`index.json` → `clippedElements`), so the action is unreachable at this width — it only exists at `lg`. | `web/src/app/catalog-panel.tsx` (`COLUMNS` grid) |
| 12 | Low | `home-ready` · all widths | The YouTube action link is a **30×32 px** target (`index.json` → `smallTapTargets`); its smaller dimension is below the 32 px minimum, and it is the only icon-only control in the row next to two text links. | `web/src/app/catalog-panel.tsx` (the row action group) |
| 13 | Low | `home-ready` · 390×844 | The shell is **394 px wide in a 390 px viewport** (`index.json` → `clippedElements` on `aside` / `main` / `header`). `overflow: hidden` hides it, so no scrollbar appears, but the layout is wider than the screen. | `web/src/app/app-shell.tsx` (`shell` grid, `1fr` column with an `auto` minimum) |
| 14 | Low | `home-ready` · 390×844 | The row metadata wraps and leaves a dangling separator: the second line starts with `· v1`. | `web/src/app/catalog-panel.tsx` (mobile row metadata) |
| 15 | Low | `home-ready` · 1440×900 | The catalog well stretches to the full viewport height; with the seeded catalog (one song) the panel is a ~600 px empty field with a single row in it. The panel should not grow past its content. | `web/src/app/catalog-panel.tsx` / `web/src/app/panel.tsx` (`panelFrame`) |
| 16 | Low | `home-empty` · all | The empty state asks for a chart (“Publish a chart to the FlashBeat API …”) but offers **no action** — there is no button in the state itself. | `web/src/app/catalog-panel.tsx` (the `empty` `StateBlock` has no `action`) |
| 17 | Low | `home-loading` / `home-empty` / `home-error` · all | The state block is vertically centred in a ~700 px tall well, so the state reads in the middle of an empty field instead of near the header where the eye lands. | `web/src/app/panel.tsx` (`stateWrap`) |
| 18 | Low | `chart-editor` / `publish-song` · all | The YouTube field shows the opaque id `8JXiXt0D6tw` instead of the video title; the operator has to open the preview to know what it is. | `web/src/components/chart-editor/youtube-preview.tsx` |
| 19 | Low | `chart-editor` / `publish-empty` · all | The empty form is filled with **example values that look real**: the placeholders are `who`, `Azari`, `128` and the YouTube placeholder is `https://www.youtube.com/watch?v=dQw4w9WgXcQ`, truncated to `https://www.youtube.com/wa` in the narrow preview field. An untouched form reads as a filled one. | `web/src/components/song-publisher/song-metadata-form.tsx` (placeholders), `web/src/components/chart-editor/youtube-preview.tsx` (the truncated placeholder) |

### Checked and **not** a defect

- The empty chart on `/chart` reports “The chart satisfies the chart contract.”
  — correct: `contracts/schemas/chart.v1.schema.json` states that an empty note
  list is a valid chart, so the message is right.
- The 8-lane column header and the `Grid 1/4` control are not in conflict:
  `1/4` is the beat subdivision, `1…8` are the lanes.

## What is already right (no action)

- The brand expression matches the Pen v5 reference: graphite surfaces, one blue
  accent, a light raised primary button, keycap hints in the legend
  (`/ Search  ↑↓ Move  Enter Open chart`), tabular figures, no glow, no
  gradient, no emoji, no fake data.
- The keyboard focus ring is visible (2 px blue outline on the focused nav
  item) and the tab order follows the visual order.
- No console error and no horizontal scrollbar at any of the five viewports
  (`index.json.summary`: `horizontalOverflow: []`, `consoleErrors: []`).
- The four catalog states (ready / loading / empty / error) all render their own
  frame without a layout jump, and the loading state really is captured with
  the request held.
- The YouTube preview renders the real video (the seeded “Who? / Azari” clip) on
  `/chart` and `/publish`.

## Release-readiness verdict

**Web Studio: CONDITIONAL FAIL — not release-ready as it stands.**

- The **gate itself is executed**: 40 real screenshots, 5 viewports, 8 route
  states, measured facts separated from the pixel review, exit code semantics
  demonstrated (`0` clean, `2` setup failure).
- The **desktop widths (≥ 1024 px) are acceptable**: no console errors, no
  overflow, the brand expression is right and the four catalog states work. The
  remaining findings there are Medium/Low copy, density and target-size items
  that can go into the normal bug queue.
- The **390 px width is not acceptable**: findings #1 and #2 make the chart
  editor and the publish screen unusable on a phone (a 448 px canvas in a ~85 px
  pane, lanes 3–8 unreachable, two overlapping columns on `/publish`). Until
  both surfaces have a narrow-viewport transformation, the Web Studio must not
  be presented as a mobile-capable tool.
- Because one **High** defect class is still open, the gate criterion of issue
  #75 (“目に見える欠陥と操作不能が記録され、未解決ならReleaseは不合格”) is not
  met for the mobile surface. The findings are handed back to the editor and
  responsive owners (#72 editorUX, #74 responsive); after the fix, re-run
  `npm run visual` — the harness needs no change for that.
