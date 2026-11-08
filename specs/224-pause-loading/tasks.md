# Tasks: 224 — ポーズゲート (時間・音声を止める)

## 1. ポーズゲートの新設

- [x] `Assets/Game/Scripts/Gameplay/PauseGate.cs` を新設
  - `PauseGateLogic` (純ロジック): `CanJudge` / `FrozenMediaTime` / `ResumeJudgeStartTime`
  - `PauseGate`: `IsPaused` / `FrozenMediaTime` / `Enter()` / `Exit()` / `HardReset()`
- [x] `.meta` を生成 (guid: 038ac965e9eb0e0fb62bce0ba3d17daa)

## 2. ゲーム側の停止

- [x] `MusicManager.cs`: `Update` 冒頭で `PauseGate.IsPaused` を見て早期リターン
  (時計更新 / stall 判定 / 曲終了状態機械を停止)。`PauseMediaForGate` /
  `ResumeMediaFromGate` を追加 (`songVideo` / `streamAudioPlayer` / `audioS`)。
  `Start` で `PauseGate.HardReset()`。
- [x] `Judge.cs`: `Update` にポーズガード。判定時刻を `GManager.step_time` 基準に変更。
- [x] `Notes.cs`: `Update` にポーズガード。

## 3. ポーズ画面からの呼び出し

- [x] `V6PauseView.cs`: `Open()` → `PauseGate.Enter()`、`Close()` → `PauseGate.Exit()`。
- [x] `PauseMenuView.cs` (v5): 同じく `Enter()` / `Exit()` (v6 と同じ挙動)。

## 4. キー凡例の真実化

- [x] `V6PauseView.BuildFooter`: 「↑↓ 選択」(実キーなし) と「←→ 変更」(未配線) を削除。
  「←→ 選択」「タブ切替 T」(旧表記 Tab)、「再開 Esc」に修正。
  `AdjustSelected` はマウス操作の公開 API として維持 (←→ へは配線しない)。

## 5. テスト

- [x] `Assets/Tests/Editor/PauseGateTests.cs` を追加 (純ロジック + 状態機械、数値固定)。
- [x] 既存テスト (`PauseMenuViewTests` / `V6PauseRuntimeTests` / `V6PauseViewLogicTests`) の
  期待値は不変 (変更対象に含まれないことを確認)。

## 6. 検証と記録

- [x] `script/compile-check.sh --assembly flashbeat` → Build succeeded
- [x] `script/compile-check.sh --assembly tests` → Build succeeded
- [x] `docs/evidence/224-pause-gate.md` に実装 / 検証 / 残存リスクを記録
- [ ] (Supervisor) 統合時に EditMode テストを実行
