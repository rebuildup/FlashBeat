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
- [x] T6: `ActionKeyHint.KeyName` に表示名の規則を追加 (テンキー = `Num` 接頭辞、記号 = 印字面)
- [x] T7: テストの誤りを修正 (テンキー期待値 `Num0`、記号キーは割り当て可能な `Quote` に変更) +
      レーン割当キーの拒否 (Set が false / cap 不変) を検証するテストを追加
- [x] T8: 残るハードコード Escape を grep で証明し、3 件すべてに理由を記録 (spec.md)
- [x] Verify: EditMode batch run, report total/passed/failed
- [x] Deliver: commit, push, draft PR to `expo-ui`, issue comment

## Verification Evidence
- EditMode batch run (no `-quit`): 実行結果は PR 本文と本節に記録する
  (既知の上流失敗 `JapaneseFontCoverageTests.BuildScenes_UseTheJapaneseFont` のみが
  失敗し、本 PR のテストは green であることを確認する)。
- GUI (Unity Editor) / Windows 実機 / スクリーンショットは未検証 (本環境に Unity GUI が無い)。
