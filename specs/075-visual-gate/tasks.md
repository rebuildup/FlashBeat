# Tasks: 075 Visual Gate

- [x] **T1** `web/tests/visual/capture.mjs`: puppeteer-core + system Chrome, managed dev server on a verified free port (or `VISUAL_BASE_URL`), route **states** (ready / error / empty / loading, chart and publish with and without a selected song), keyboard-focus shots, `index.json`, non-zero exit on horizontal overflow / console errors.
- [x] **T2** `web/tests/visual/README.md` + `.gitignore` for `out/`; `npm run visual` script in `web/package.json` (+ `package-lock.json`).
- [x] **T3** Capture at the viewports the issue names — 1440×900, 1280×720, 1024×768, 390×844 and a 200 % browser zoom case — and publish the PR-facing set to `specs/075-visual-gate/evidence/<viewport>/<route>-<state>.png`.
- [x] **T4** Redline review of the **real** PNGs against the Pen v5 reference renders and the design-skills / unslop criteria: `specs/075-visual-gate/evidence/visual-review.md` (severity, route + viewport, defect, responsible file).
- [x] **T5** Finish `spec.md` / `plan.md` / `tasks.md` and state an explicit release-readiness verdict for the Web Studio.
- [x] **T6** Verify the exit-code semantics and keep the measured facts separate from
  the human review. Demonstrated: the clean path (**exit 0** — the captured run,
  `horizontalOverflow: []`, `consoleErrors: []`) and the setup-failure path
  (**exit 2** — `VISUAL_BASE_URL=http://127.0.0.1:5299 npm run visual` →
  `EXIT=2`). The gate-failure path (**exit 1**) is implemented
  (`horizontalScroll` or a console error at any capture) but did not trigger in
  this run, because no capture of the current build overflows horizontally and
  none logs a console error.

## Notes on what the run needs to be reproducible

- The dev server compiles the worker on the first request (~50 s); the capture
  waits for the API and warms every route up before the first screenshot.
- The capture binds a **free** port. A stale dev server of another worktree on
  the preferred port answers the captures with the wrong application — that is
  what made the first run of this tool fail (a server of `/Users/rebui/work/
  FlashBeat.75` held port 5199 and answered every route with 404).
- The capture waits for a state marker (`a[data-row-link]`, "Catalog
  unavailable", "No songs published yet", "Loading catalog", "Publish a song")
  and for the DOM to stop changing, so a screenshot can never show the loading
  state under the name of another state.
