# Plan: Key hint single-source slice

**Issue**: #146 | **Branch**: `146` | **Base**: `expo-ui`
**Spec**: [spec.md](spec.md)

## Summary
Introduce a pure key display formatter, replace old lane-map reads in the v5 HUD, and use the formatter for Result's Confirm/Retry key caps.

## Owned Paths
```text
Assets/Game/Scripts/UI/V5/ActionKeyHint.cs
Assets/Game/Scripts/UI/V5/GameHudView.cs
Assets/Game/Scripts/UI/V5/ResultView.cs
Assets/Tests/Editor/ActionKeyHintTests.cs
specs/146-action-key-hints/
```

## Dependencies & Gate
Stack on Expo UI PR #143; no overlapping writes with other scene workers. Only integrate with Unity/Win64 verification at exact SHA. #147 is code-only until QA.

## Exclusions
Do not change inputs, button click handlers, Canvas layout or scenes. Title/Select/Option/Pause remain follow-up.

## Constitution Check
Uses project-init governance unchanged. One bounded ticket branch and PR; no production mutable state touched. Evidence binds to candidate SHA.

## Verification Plan
1. Compile Unity runtime + tests.
2. EditMode test ActionKeyHint and existing KeyBindings/KeyActions suite.
3. Screenshot Game HUD and Result with custom mapping at two target resolutions.
4. Verify actual actions and key caps match; preserve live build until accepted.

## Rollback
Revert ticket PR only; all modifications are localized to display code.
