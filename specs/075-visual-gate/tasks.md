# Tasks: 075 Visual Gate

- [x] **T1** `web/tests/visual/capture.mjs`: puppeteer-core + system Chrome, managed dev server (or `VISUAL_BASE_URL`), 3 routes × 3 viewports, Tab-focus shots, `index.json`, non-zero exit on horizontal overflow / console errors.
- [x] **T2** `web/tests/visual/README.md` + `.gitignore` for `out/`; `npm run visual` script in `web/package.json`.
- [x] **T3** Run against current trunk (`edbc5d4`), attach `index.json` + 3 representative screenshots to `specs/075-visual-gate/evidence/` with honest defect list.
- [x] **T4** Push branch `75`, open Draft PR → `release-0-1-3`, referencing #75 and #41.
- [x] **T5** Verify exit-code semantics (overflow/error paths) and report measured vs. needs-human-review split.
