# Plan: 075 Visual Gate

## Approach

- **Driver**: `puppeteer-core` (devDependency, zero browser download) launching the
  system Chrome in headless mode. Playwright was rejected: it would download a
  browser bundle at install time, violating the offline-free requirement.
- **Server**: the script spawns `vp dev` on port 5199 (after `void db seed` so the
  catalog/chart fixtures exist), or reuses `VISUAL_BASE_URL` when set.
- **Routes**: `/`, `/chart?songId=who&version=1` (seeded fixture), `/publish`.
- **Viewports**: 1440×900, 1280×720, 390×844 (mobile emulation, DPR 2).
- **Focus**: per route at 1440×900 — screenshot after 1 Tab and after 3 Tabs, with
  the focused element descriptor recorded.
- **Measurement**: single in-page evaluate per capture — document scroll width,
  per-element rects/computed styles filtered to visible elements. Below-fold
  elements are counted informationally, not flagged as defects (vertical scroll is
  expected); horizontal clipping is a defect.
- **Failure**: exit 1 when any capture has horizontal scroll or console errors.

## Files

| Path | Kind |
| --- | --- |
| `web/tests/visual/capture.mjs` | new, capture driver |
| `web/tests/visual/README.md` | new |
| `web/tests/visual/.gitignore` | new, ignores `out/` |
| `web/package.json` | `visual` script + `puppeteer-core` devDependency |
| `specs/075-visual-gate/evidence/**` | index.json, 3 screenshots (+ local `-filter` gitattributes so PNGs stay plain blobs, LFS untouched) |

## Risks

- Chrome path varies per machine → `CHROME_PATH` env override + common-path probe.
- Konva editor loads lazily → wait for `canvas` before measuring `/chart`.
- PNG evidence under LFS rules → per-directory `.gitattributes` with `-filter`.
