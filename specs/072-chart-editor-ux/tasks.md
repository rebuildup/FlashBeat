# Tasks
- [x] T1 Panda CSS page shell in web/pages/chart.tsx (top bar, error/loading, editor fill).
- [x] T2 Responsive full-width lane grid with bar numbers, selection/hover states (note-grid.tsx).
- [x] T3 Compact toolbar + side column layout, shortcut hints (chart-editor.tsx, editor-toolbar.tsx).
- [x] T4 Keyboard ops: Escape deselect, arrow nudge, input-safe Delete (chart-editor.tsx).
- [x] T5 vitest/tsc/build + real screenshots 1440×900 & 1280×720 into specs/072-chart-editor-ux/evidence/.

## Verification record

- tsc --noEmit: pass (0 errors)
- vitest: 13 files / 99 tests passed
- vp build: success (note-grid chunk 334.63 kB)
- vp check: owned files clean; 12 pre-existing format issues on files this slice does not own (verified present on base edbc5d4 via stash)
- Screenshots: real Chrome (playwright-core) at 1440×900 and 1280×720 against `vp dev` @ final SHA; interactions exercised (add 3 notes, select dead-center click, ArrowRight+ArrowDown nudge, note count readout "3 notes", playhead readout). Duplicate-stack defect found in review and fixed (occupied cell now selects instead of stacking).
- Draft contract untouched: `lib/publish/draft.ts` unmodified; Review & Publish still routes through `savePublishDraft`.
