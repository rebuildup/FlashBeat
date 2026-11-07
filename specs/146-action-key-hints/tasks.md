# Tasks: Key hint single-source slice

**Issue**: #146 | **Branch**: `146` | **Base**: `expo-ui`

- [x] **T1** Add `ActionKeyHint.cs` with KeyActions-driven names, duplicate Enter collapse and short cap labels.
- [x] **T2** Replace the static `Judge.LaneKeys` reference in `V5/GameHudView.cs` with `KeyBindings`.
- [x] **T3** Replace fixed Confirm/Retry cap strings in `V5/ResultView.cs` with `ActionKeyHint.Primary`.
- [x] **T4** Add `ActionKeyHintTests.cs` with default, custom, punctuation and empty binding cases.
- [ ] **T5** Validate Unity compile/EditMode and real Win64 interactions at PR SHA; attach evidence to PR. Do not claim completion without this.

## Dependencies & Execution Order
T1 → T3; T2 and T4 independent. PR #143 base `expo-ui` required.

## Verification Evidence
Code was added through GitHub; Unity Editor and Windows screenshot must be executed by the current exclusive verification owner.

## Rollback
Revert ticket PR only.
