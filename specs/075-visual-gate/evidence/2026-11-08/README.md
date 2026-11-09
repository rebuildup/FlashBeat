# Visual gate — failing baseline (2026-11-08)

This is the **"before"** record of Issue #74 / #75. The visual gate
(`cd web && npm run visual`, driven by `web/tests/visual/capture.mjs` with real headless
Chrome) was run against `integration/v6-followup` and **exited 1 (FAIL)**.

Reproduce:

```bash
cd web
npm install          # puppeteer-core must be installed
CHROME_PATH="/Applications/Google Chrome.app/Contents/MacOS/Google Chrome" npm run visual
```

## Measured result

| screen | viewport | hScroll | clipped | smallFonts | smallTargets | consoleErrors |
|---|---|---|---|---|---|---|
| home | 1440×900 | false | 0 | 11 | 3 | 0 |
| home | 1280×720 | false | 0 | 11 | 3 | 0 |
| home | 390×844 | **TRUE** | 31 | 10 | 3 | 0 |
| chart | 1440×900 | false | 0 | 37 | 10 | 0 |
| chart | 1280×720 | false | 19 | 37 | 10 | 0 |
| chart | 390×844 | false | 129 | 36 | 10 | 0 |
| publish | 1440×900 | false | 0 | 41 | 11 | 0 |
| publish | 1280×720 | false | 0 | 41 | 10 | 0 |
| publish | 390×844 | false | 128 | 37 | 10 | 0 |

```
[visual] horizontal overflow: home@390x844
[visual] console errors: none
[visual] FAIL (exit 1)
```

## What the failures mean

- **home @ 390×844 — horizontal overflow (hard gate).** The mobile-width studio shell does not
  fit: the top navigation is wider than the viewport, so the page scrolls sideways. This is the
  single reason the gate fails today.
- **chart @ 1280×720 — 19 clipped elements.** Elements are cut off at the 720p-height breakpoint.
- **chart / publish @ 390×844 — 129 / 128 clipped elements.** At mobile width the chart list and
  the publish form overflow their containers.
- **smallFonts / smallTargets** counts are *reported* by the harness but are **not** part of the
  current exit-code rule (only horizontal overflow and console errors are). They are the backlog
  for the readability audit, not the reason for the FAIL.
- **consoleErrors = 0 everywhere.** The failure is layout, not runtime.

## Files

- `index.json` — the machine-readable report the harness emitted (per-screen, per-viewport metrics).
- `<screen>_<w>x<h>.png` — the viewport screenshot at that size.
- `<screen>_<w>x<h>.full.png` — the full-page screenshot (taller than the viewport).
- `<screen>_1440x900_focus1.png` / `_focus3.png` — focus-state captures used to check focus rings.

A **passing** run for the same command is recorded separately once Issue #74 is fixed; this
directory is kept as the failing baseline so the regression is visible.
