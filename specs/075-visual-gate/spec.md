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
   download) over `/`, `/chart?songId=…&version=…`, `/publish`.
3. Captures at 1440×900, 1280×720, 390×844 plus keyboard-focus states after Tab.
4. Writes `out/<timestamp>/` (gitignored) with PNG screenshots and an `index.json`
   holding per-capture **measured** facts: horizontal scroll, elements clipped
   outside the viewport, computed `font-size < 12px`, tap targets `< 32px`,
   console errors, focus descriptors.
5. Exits non-zero if horizontal overflow exists at any viewport or console errors occur.
6. Explicitly separates `measured` from `needsHumanVisualReview` — the script never
   claims a DOM heuristic is a pixel review.

## Non-goals

No app/style/component edits. Defects found are reported, not fixed here. No
production deploy; local D1 seed only.

## Acceptance

- `npm run visual` against the current trunk build produces `index.json` + screenshots.
- Evidence (index.json + 3 representative screenshots + defect list) attached to the
  Draft PR under `specs/075-visual-gate/evidence/`.
- Exit code semantics implemented and demonstrated.
