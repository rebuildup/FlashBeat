# Visual Gate (`web/tests/visual/`)

Real-pixel capture tooling for the Web Studio release gate (Issue #75, parent #61).

It drives a **real headless Chromium** — the system Chrome via `puppeteer-core`
(no browser download, works offline once deps are installed) — over the Studio
routes and records both screenshots and **measured** DOM layout facts.

## Run

```sh
cd web
npm run visual                                # seeds local D1, starts a dev server on a free port
VISUAL_BASE_URL=http://127.0.0.1:5173 npm run visual   # reuse an already-running server
CHROME_PATH=/path/to/chrome npm run visual    # non-standard Chrome location
```

Output:

- `web/tests/visual/out/<timestamp>/` (gitignored) — the raw run:
  - `<route>-<state>_<viewport>.png` — viewport screenshot (what the user actually sees)
  - `<route>-<state>_<viewport>.full.png` — full-page screenshot (for below-fold review)
  - `<route>-<state>_<viewport>.lanes-right.png` — the chart editor with the
    note grid scrolled to the right (proof that the lanes on the right are
    reachable on a narrow viewport)
  - `<route>-<state>_1440x900_focus{1,3}.png` — keyboard focus after 1 / 3 Tab presses
  - `index.json` — the report
- `specs/158-visual-fixes/evidence/` — the PR-facing set
  (`VISUAL_EVIDENCE_DIR` overrides the directory):
  - `<viewport>/<route>-<state>.png` — the same viewport screenshots
  - `<viewport>/<route>-<state>.lanes-right.png` — the scrolled note grid
  - `index.json` — the report plus the measured facts of every evidence image

## Routes / states

| Route   | State   | URL                             | Note                                                           |
| ------- | ------- | ------------------------------- | -------------------------------------------------------------- |
| home    | ready   | `/`                             | seeded catalog (one published song)                            |
| home    | error   | `/?state=error`                 | the catalog request fails                                      |
| home    | empty   | `/?state=empty`                 | the catalog answers no song                                    |
| home    | loading | `/`                             | the catalog request is held, so the loading state is on screen |
| chart   | editor  | `/chart?songId=who&version=1`   | seeded fixture chart; waits for the Konva `canvas`             |
| chart   | empty   | `/chart`                        | no song selected                                               |
| publish | song    | `/publish?songId=who&version=1` | publish form for the seeded song                               |
| publish | empty   | `/publish`                      | no song selected                                               |

Every route has a **state marker** (`a[data-row-link]`, `Catalog unavailable`,
`No songs published yet`, `Loading catalog`, `Review & Publish`,
`Publish a song`). The capture waits for the marker and for the DOM to stop
changing before it reads the pixels, so a screenshot can never show the loading
state under the name of another state. `index.json` records the `settled` flag of
every capture.

## Viewports

| Label           | CSS viewport | Device pixels | Note                                                                                                                |
| --------------- | ------------ | ------------- | ------------------------------------------------------------------------------------------------------------------- |
| `1440x900`      | 1440×900     | 1440×900      | desktop                                                                                                             |
| `1280x720`      | 1280×720     | 1280×720      | laptop                                                                                                              |
| `1024x768`      | 1024×768     | 1024×768      | small desktop / tablet landscape                                                                                    |
| `390x844`       | 390×844      | 780×1688      | mobile emulation, DPR 2, touch                                                                                      |
| `1440x900@200%` | 720×450      | 1440×900      | a 1440×900 window with the browser zoom at 200%: the layout is the zoomed one, the screenshot keeps the window size |

## What index.json records

**Measured** (per capture, from real layout):

- `horizontalScroll` — document `scrollWidth > clientWidth`
- `clippedElements` — visible elements extending beyond the viewport **horizontally**
  and **not** inside a scrollable ancestor: a real defect
- `scrollableOverflowElements` — elements that extend beyond the viewport but
  live inside a horizontally scrollable ancestor, so the content stays
  reachable (the note grid is wider than a phone on purpose)
- `scrollContainers` — every scrollable container with its `clientWidth`,
  `scrollWidth` and overflow directions
- `belowFoldElements` — informational count only; vertical scroll is expected
- `smallFontElements` — visible text with computed `font-size < 12px`
- `smallTapTargets` — interactive elements with `min(width, height) < 32px`
- `consoleErrors` — console `error` messages and uncaught page errors
- `focus` — descriptor of the focused element after Tab presses

**Needs human/model visual review** — the script never claims a DOM check is a
pixel review. Spacing, visual hierarchy, information density, interaction states,
copy and brand match (design-skills layout-system / visual-hierarchy /
information-density / interaction-state, unslop signs) must be judged by looking
at the actual PNGs. `index.json.needsHumanVisualReview` lists every screenshot for
that pass; the result of that pass is
`specs/158-visual-fixes/evidence/visual-review.md`.

## Exit codes

| Code | Meaning                                                                    |
| ---- | -------------------------------------------------------------------------- |
| 0    | no horizontal overflow, no console errors                                  |
| 1    | **gate failure**: horizontal overflow at ≥1 viewport and/or console errors |
| 2    | setup failure (no Chrome found, server never became ready)                 |

Horizontal overflow or console errors at any viewport fail the run.

## Notes on the local server

- The dev server compiles the worker on the first request; that first request
  takes ~50s. The capture therefore waits for the API to answer (300s budget,
  one request in flight at a time) and warms every route up before the
  screenshots start.
- The capture binds a **free** port (starting at 5199). A stale dev server of
  another worktree on the preferred port would answer the captures with the
  wrong application.
- The dev server is started in its own process group and the group is killed
  at the end, so the port does not stay occupied.
