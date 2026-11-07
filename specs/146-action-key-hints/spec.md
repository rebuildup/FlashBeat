# Spec: Key hints reflect active bindings

**Issue**: #146 | **Branch**: `146` | **PR base**: `expo-ui` | **Status**: Draft

## Objective
Make the v5 in-game lane guide and Result action caps reflect actual accepted key bindings. Do not modify Unity scenes or other v5 views owned by the exhibition UI integrator.

## Owned Paths
- `Assets/Game/Scripts/UI/V5/ActionKeyHint.cs` + meta
- `Assets/Game/Scripts/UI/V5/GameHudView.cs`
- `Assets/Game/Scripts/UI/V5/ResultView.cs`
- `Assets/Tests/Editor/ActionKeyHintTests.cs` + meta
- `specs/146-action-key-hints/**`

## Exclusions
Title/Select/Option/Pause legends, new keybinding storage, APIs, Web Studio, Unity scene edits, release publication.

## User Scenarios & Testing
### Scenario 1 (P1)
Given a customized lane mapping, when GameScene builds its eight-lane guide, then every representative key belongs to the currently accepted lane binding.
Independent test: EditMode KeyBindings tests plus actual Win64 GameScene screenshots with customized layout.

### Scenario 2 (P1)
Given customized Confirm/Retry keys, when Result displays its buttons, then OK/Retry caps show a currently accepted key rather than fixed Space/R.
Independent test: `ActionKeyHintTests` and manual Result screenshot/action check.

### Edge Cases
Duplicate Enter/KeypadEnter names collapse only in the full string. Empty tentative assignments show an explicit unassigned marker; persistence validation remains owned by KeyActions.

## Requirements
- R1: Use KeyActions / KeyBindings as sources; do not cache defaults in a second table.
- R2: Existing unbound action inputs and Register/Enter semantics stay unchanged.
- R3: Never claim UI proof without actual Unity screenshot at current SHA.

## Dependencies & Gate
Depends on PR #143 `expo-ui` (v5 UI and KeyActions) and #142 model.
Gate: Unity compile + EditMode + Win64 screenshot + input smoke at the candidate SHA. No release without release approval.

## Verification Evidence
- `Unity -batchmode -projectPath . -runTests -testPlatform editmode -testResults TestResults.xml` → all green.
- Win64 screen, custom mapping and button click/press → visible labels match accepted actions.

## Rollback
Revert the isolated #147 merge commit. No game data migration.
