# Plan

1. **Page shell** (`pages/chart.tsx`): Panda CSS grid shell — 3rem top bar
   (Catalog link left, identity center, Review & Publish primary right), error
   banner, editor fills the rest of `100dvh`. No inline `style` props.
2. **Editor layout** (`chart-editor.tsx`): canvas panel `flex: 1`, side column
   fixed 18rem; toolbar single compact row (actions left, meta right); grid hint
   line carries the keymap shortcuts.
3. **Grid** (`note-grid.tsx`): measure the panel with ResizeObserver, lane width =
   floor(panelWidth/8) clamped ≥56, stage centered; bar numbers every 4 beats;
   selected note gets teal stroke + softer halo; playhead keeps time readout in
   the hint row; Escape/arrow-key handling stays in chart-editor (selection state
   owner) and reaches the grid via props.
4. **Toolbar** (`editor-toolbar.tsx`): compact controls (`size="sm"`), keycap
   hint for Delete, note counter as quiet mono meta.
5. **Verify**: vitest + tsc + build; real browser screenshots 1440×900 and
   1280×720 via Playwright, critique, fix, commit finals to
   `specs/072-chart-editor-ux/evidence/`.
