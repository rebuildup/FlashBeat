# Visual Gate (`web/tests/visual/`)

Real-pixel capture tooling for the Web Studio release gate (Issue #75, parent #61).

It drives a **real headless Chromium** — the system Chrome via `puppeteer-core`
(no browser download, works offline once deps are installed) — over the Studio
routes and records both screenshots and **measured** DOM layout facts.

## Run

```sh
cd web
npm run visual                                # seeds local D1, starts dev server on :5199
VISUAL_BASE_URL=http://127.0.0.1:5173 npm run visual   # reuse an already-running server
CHROME_PATH=/path/to/chrome npm run visual    # non-standard Chrome location
```

Output: `web/tests/visual/out/<timestamp>/` (gitignored):

- `<route>_<viewport>.png` — viewport screenshot (what the user actually sees)
- `<route>_<viewport>.full.png` — full-page screenshot (for below-fold review)
- `<route>_<viewport>_focus{1,3}.png` — keyboard focus after 1 / 3 Tab presses (desktop viewport)
- `index.json` — the report

## Routes / viewports

| Route | Note |
| --- | --- |
| `/` | catalog home |
| `/chart?songId=who&version=1` | seeded fixture chart; waits for the Konva `canvas` |
| `/publish` | publish screen |

Viewports: 1440×900, 1280×720, 390×844 (mobile emulation, DPR 2, touch).

## What index.json records

**Measured** (per capture, from real layout):

- `horizontalScroll` — document `scrollWidth > clientWidth`
- `clippedElements` — visible elements extending beyond the viewport **horizontally**
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
that pass.

## Exit codes

| Code | Meaning |
| --- | --- |
| 0 | no horizontal overflow, no console errors |
| 1 | **gate failure**: horizontal overflow at ≥1 viewport and/or console errors |
| 2 | setup failure (no Chrome found, server never became ready) |

Horizontal overflow or console errors at any viewport fail the run.
