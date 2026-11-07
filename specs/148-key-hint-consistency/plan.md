# Plan: #148 v5 key hint consistency

## Approach
Display-only pass over five owned v5 views. `ActionKeyHint` (from #147) stays the only
formatter; every configurable cap becomes `ActionKeyHint.Primary(KeyActions...)`.
Fixed inputs (arrows, W/S, Tab, F2/F3, S/D/L/K, Return/Space/R in Ready, inputString)
keep literal captions that match the hard-coded handler (recorded in spec.md matrix).

Two input routings change (behavior-preserving at defaults):
- Select Ready/Typing/History/Ranking "back" and Option/Pause back/resume:
  hard-coded `KeyCode.Escape` → `KeyActions.IsDown(Back)` (default Esc).
- Pause action caps: `KeyConfigUI.GetKeysText(all keys)` → `ActionKeyHint.Primary(...)`
  (48px cap cannot fit multi-key lists).

## Re-render strategy (T3)
- All five views subscribe `KeyActions.Changed` in OnEnable / unsubscribe OnDisable.
- TitleView / OptionView / PauseMenuView / ResultView: keep references to cap TMP labels,
  update text in place (fixed cap widths; Primary names are ≤2–5 chars, "BS" special case).
- SelectView: `KeyBindings`/`KeyActions` changes trigger full `Rebuild()` (legends are
  rebuilt anyway on every state change; jacket caches make this cheap).
- OptionView additionally subscribes `KeyBindings.Changed` (KeyConfigUI overlay can run
  in OptionScene while the view is alive; also refreshes the "Key Layout" row value).
- Scene re-entry rebuilds Title/Select/Result views from scratch (scene-manager Start).

## Layout safety
Cap widths stay as-is (`V5Decor.BuildLegend` widths, `V5Ui.KeyCap` fixed sizes,
Result 80px cap panels). `Primary` returns compact names ("BS", arrows, single glyphs);
empty bindings render "—" which fits. No button geometry changes.

## Test design (T4)
`Assets/Tests/Editor/KeyHintConsistencyTests.cs` (EditMode, NUnit), pattern follows
`ActionKeyHintTests` (in-memory `KeyActions.IStorage`):
1. Default bindings: every owned view's cap accessor equals the literal expected cap
   (Space/Esc/R/BS) — pins slot→action mapping, not the formatter.
2. Custom symbol binding (F8 → Back, Backslash → MenuRetire): accessors re-derive.
3. Empty binding: `—`.
4. Losslessness: for every `KeyActions.All` action, `All()` segment count equals the
   distinct non-None key count of `Get(action)`.

View accessors used by both build code and tests (internal static, block bodies):
- `TitleView.DecideCap()`
- `SelectView.BackCap()`
- `OptionView.BackCap()`
- `PauseMenuView.ResumeCap()/RetryCap()/RetireCap()`
- `ResultView.RetryLegendCap()/DecideLegendCap()`

## Verification
EditMode batch run (no `-quit`), XML counts reported; known upstream failure
`JapaneseFontCoverageTests.BuildScenes_UseTheJapaneseFont` (fixed on branch 153) excluded.
No GUI/Windows verification possible on this machine — stated explicitly in PR body.
