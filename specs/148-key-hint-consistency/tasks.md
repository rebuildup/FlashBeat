# Tasks: #148 v5 key hint consistency

- [x] T1: Audit displayed vs accepted keys per screen/state; matrix in spec.md
- [x] T2a: TitleView legend 決定 cap → `Primary(Start)` (was fixed "Enter")
- [x] T2b: SelectView back routing → `KeyActions.Back` in Ready/Typing/History/Ranking; back caps → `BackCap()`
- [x] T2c: OptionView back routing → `KeyActions.Back`; legend 戻る cap → `BackCap()`
- [x] T2d: PauseMenuView open/close → `KeyActions.Back`; Retry/Retire/Resume caps → `Primary(...)`
- [x] T2e: ResultView legend R/Space caps → `Primary(Retry)`/`Primary(Confirm)` (modal Esc/Enter stay fixed = match hard-coded input)
- [x] T3: `KeyActions.Changed` (and `KeyBindings.Changed` in OptionView) subscriptions re-render caps; SelectView rebuilds
- [x] T4: `Assets/Tests/Editor/KeyHintConsistencyTests.cs` (+meta): default / custom symbol / empty binding, `All()` losslessness
- [x] T5: spec.md / plan.md / tasks.md; checkboxes kept current
- [x] Verify: EditMode batch run, report total/passed/failed
- [x] Deliver: commit, push, draft PR to `expo-ui`, issue comment
