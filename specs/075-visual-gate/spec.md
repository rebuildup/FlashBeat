# Spec: 075 Visual Gate (Web Studio)

Parent: #61 (Web Studio UI/UX), Unit: visualQA (P0). Trunk: `origin/release-0-1-3`.

## Problem

Prior reviews were inferred from code and diagrams; no real rendered pixels existed.
The release gate requires real browser captures of the live Studio with measured
layout facts, separated from human/model visual judgment.

## Goal

A repeatable capture tool under `web/tests/visual/` that:

1. Starts the local Void dev server (or accepts `VISUAL_BASE_URL`).
2. Drives a **real headless Chromium** (`puppeteer-core` + system Chrome; no browser
   download) over the Studio routes.
3. Captures the routes **and their states** — `/` (ready / error / empty / loading),
   `/chart` (with and without a selected song), `/publish` (with and without a
   selected song) — at 1440×900, 1280×720, 1024×768, 390×844 and a 200 % browser
   zoom case, plus keyboard-focus states after Tab.
4. Writes `out/<timestamp>/` (gitignored) with PNG screenshots and an `index.json`
   holding per-capture **measured** facts: horizontal scroll, elements clipped
   outside the viewport, computed `font-size < 12px`, tap targets `< 32px`,
   console errors, focus descriptors.
5. Copies the viewport screenshots into the PR-facing evidence set
   `specs/075-visual-gate/evidence/<viewport>/<route>-<state>.png` and writes the
   same report to `specs/075-visual-gate/evidence/index.json`.
6. Exits non-zero if horizontal overflow exists at any viewport or console errors occur.
7. Explicitly separates `measured` from `needsHumanVisualReview` — the script never
   claims a DOM heuristic is a pixel review. The pixel review itself is
   `specs/075-visual-gate/evidence/visual-review.md`, written from the PNGs.

## Non-goals

No app/style/component edits. Defects found are reported, not fixed here. No
production deploy; local D1 seed only.

## Acceptance

- `npm run visual` against the current build produces `index.json` + screenshots.
- Evidence (index.json + the viewport matrix + the redline review) attached to the
  Draft PR under `specs/075-visual-gate/evidence/`.
- Exit code semantics implemented and demonstrated.
- An explicit release-readiness verdict for the Web Studio.

## Release-readiness verdict (recorded with this pull request)

**Web Studio: CONDITIONAL FAIL — not release-ready as it stands.**

The Visual Gate itself is complete and green: 40 real screenshots over five
viewports and eight route states, measured facts separated from the pixel
review, exit code semantics demonstrated (`0` clean, `2` setup failure).

- **Desktop (≥ 1024 px): acceptable.** No console errors, no horizontal
  scroll, the brand expression follows Pen v5, and the four catalog states work.
  The open findings are Medium/Low copy, density and target-size items.
- **Mobile (390 px): not acceptable.** Two **High** findings make the chart
  editor and the publish screen unusable: a 448 px Konva canvas inside a ~85 px
  pane (lanes 3–8 unreachable) and, on `/publish`, two overlapping columns.

Because the High findings are open, the release criterion of issue #75 is not
met for the mobile surface. The findings are handed back to the editor and
responsive owners (#72 editorUX, #74 responsive); after the fix, re-run
`npm run visual` — the harness needs no change for that.

Details: `specs/075-visual-gate/evidence/visual-review.md`.
