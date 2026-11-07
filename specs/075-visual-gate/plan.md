# Plan: 075 Visual Gate

## Approach

- **Driver**: `puppeteer-core` (devDependency, zero browser download) launching the
  system Chrome in headless mode. Playwright was rejected: it would download a
  browser bundle at install time, violating the offline-free requirement.
- **Server**: the script spawns `vp dev` on a **free** port (starting at 5199)
  after `void db seed` so the catalog/chart fixtures exist, or reuses
  `VISUAL_BASE_URL` when set. The dev server is started in its own process group
  and the group is killed at the end.
- **Readiness**: the dev server compiles the worker on its first request (~50 s).
  The script therefore waits for `/api/v1/catalog` to answer (300 s budget, one
  request in flight at a time) and warms every route up before the screenshots
  start, so the first navigation never races a compilation.
- **Routes / states**: `/` (ready / error / empty / loading), `/chart` (with and
  without a selected song), `/publish` (with and without a selected song). The
  loading state is produced by holding the catalog request in the browser.
- **State truth**: every route has a **state marker** (`a[data-row-link]`,
  `Catalog unavailable`, `No songs published yet`, `Loading catalog`,
  `Review & Publish`, `Publish a song`, the Konva `canvas`, the YouTube player
  `.ytp-player`). The capture waits for the marker and for the DOM to stop
  changing before it reads the pixels, so a screenshot can never show the
  loading state under the name of another state. The result is recorded per
  capture as `settled`.
- **Viewports**: 1440×900, 1280×720, 1024×768, 390×844 (mobile emulation, DPR 2)
  and `1440x900@200%` — a 1440×900 window with the browser zoom at 200 %, i.e. a
  720×450 CSS viewport at DPR 2.
- **Focus**: per route at 1440×900 — screenshot after 1 Tab and after 3 Tabs, with
  the focused element descriptor recorded.
- **Measurement**: single in-page evaluate per capture — document scroll width,
  per-element rects/computed styles filtered to visible elements. Below-fold
  elements are counted informationally, not flagged as defects (vertical scroll is
  expected); horizontal clipping is a defect.
- **Evidence**: the viewport screenshots are copied to
  `specs/075-visual-gate/evidence/<viewport>/<route>-<state>.png`; the raw run
  (full-page shots, focus states, per-run `index.json`) stays in
  `web/tests/visual/out/` (gitignored).
- **Failure**: exit 1 when any capture has horizontal scroll or console errors.

## Files

| Path | Kind |
| --- | --- |
| `web/tests/visual/capture.mjs` | capture driver |
| `web/tests/visual/README.md` | run book (routes, viewports, exit codes) |
| `web/tests/visual/.gitignore` | ignores `out/` |
| `web/package.json` | `visual` script + `puppeteer-core` devDependency |
| `web/package-lock.json` | lock file of the devDependency |
| `specs/075-visual-gate/evidence/index.json` | measured facts of every evidence image |
| `specs/075-visual-gate/evidence/<viewport>/*.png` | the PR-facing screenshot matrix |
| `specs/075-visual-gate/evidence/visual-review.md` | the redline review of the real pixels |
| `specs/075-visual-gate/evidence/.gitattributes` | keeps the PNGs plain blobs (LFS is not available in this environment) |

## Risks

- Chrome path varies per machine → `CHROME_PATH` env override + common-path probe.
- Konva editor loads lazily → wait for `canvas` before measuring `/chart`.
- Cold worker compile (~50 s for the first request) → readiness wait + route warm-up.
- A stale dev server of another worktree can hold the preferred port and answer
  the captures with the wrong application → the capture binds a verified free port.
- `git-lfs` is not installed in this environment, so the Pen reference renders
  (`design/exports/**`) are LFS pointers in the worktree → they were fetched
  through the GitHub LFS API into a scratch directory for the review only; the
  worktree copies stay untouched.
- PNG evidence under LFS rules → per-directory `.gitattributes` with `-filter`.
