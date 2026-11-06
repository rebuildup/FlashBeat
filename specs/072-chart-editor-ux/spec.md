# #72 Chart editor UX — visible work area, tool placement, feedback

The `/chart` editor must feel like a chart-authoring tool: the 8-lane grid is the
protagonist, chrome is compact and quiet. Keep the draft contract of
`lib/publish/draft.ts` (`savePublishDraft` → `/publish?draft=…`) intact.

## Problems (from the issue and code audit @ edbc5d4)

1. Grid stage is a fixed 448 px × N column (56 px lanes) inside a panel that can be
   far wider — most of the work area is empty panel, not lanes.
2. Page shell (`pages/chart.tsx`) is inline-styled; header reads like a dashboard
   row, not an editor top bar.
3. Toolbar carries full-width Park UI fields (song name, BPM, grid, view) that
   compete with the canvas; actions and meta are mixed in one wrap row.
4. Feedback: lane numbers are small bare digits with no keycap affordance; bars are
   unnumbered; playhead has no time readout; selection is a barely different fill.
5. Keyboard: Delete/Backspace guard exists for inputs; no Escape-to-deselect; no
   arrow-key nudging of the playhead-independent selection.

## Direction (unslop, applied to this product)

- Product: 8-lane rhythm chart authoring against a YouTube preview, solo operator.
- Character: precise, arcade-dark, calm chrome.
- World: rhythm-game play field — dark lane stage, teal accent.
- Moments: the lane grid itself carries the color; everything else is quiet.
- Signature: keycap-style lane labels matching the Unity play keymap digits.

## Scope

Owned only: `web/src/components/chart-editor/**`, `web/pages/chart.tsx`,
`web/pages/chart.server.ts`, `web/src/lib/chart/**`. Tests under
`web/src/lib/chart/__tests__` and new editor tests if needed. Do not touch
`styles/**`, `ui/**`, `panda.config.ts`, `publish*`, `lib/publish/**`, `app/**`.

## Acceptance

- Grid lanes stretch to the panel width (ResponsiveStage via ResizeObserver), bars
  numbered, beat/sub-beat lines readable at 1× zoom.
- Selected note is unmistakable (accent stroke + halo), hover cursor feedback.
- Keyboard: Delete/Backspace never fire while typing in inputs; Escape deselects;
  arrow left/right nudge selected note by one grid line; shortcuts documented in UI.
- Header: quiet Catalog link, song identity center, single primary Review & Publish.
- Import failure and video error states recoverable, visible in the editor frame.
- JSON export/import roundtrip unchanged (existing tests pass).
