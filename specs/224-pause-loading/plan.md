# Plan: 224 — ポーズゲート (時間・音声を止める)

## 設計

### 構成

```
FlashBeat.Gameplay.PauseGateLogic   純ロジック (testable, Unity 状態に触らない)
FlashBeat.Gameplay.PauseGate        状態 (IsPaused / FrozenMediaTime) + GManager 書き込み
MusicManager                        メディア (映像 / 音声) の Pause / 再開 + 時計停止
Judge / Notes                       IsPaused ガード
V6PauseView / PauseMenuView         Open / Close から Enter / Exit
```

### 時計の不変条件

- 通常再生中: `MusicManager` が毎フレーム
  `GManager.StartTime = Time.time - mediaTime`、`GManager.step_time = mediaTime`
  を維持する (既存)。よって `Time.time - StartTime == step_time`。
- ポーズ中: `MusicManager.Update` は早期リターンし `step_time` を更新しない → 固定。
  `Judge` は判定時刻を `GManager.step_time` から取る (旧: `Time.time - StartTime`)。
  通常中は両者同一なので判定タイミングは不変、ポーズ中は固定される。
- 再開: `PauseGate.Exit()` が `StartTime = Time.time - FrozenMediaTime` を書く。
  再開直後の `Time.time - StartTime == FrozenMediaTime` → ノーツ位置が飛ばない。
  直後の `MusicManager.Update` がメディア時計から通常どおり再同期する。

### メディアの停止 / 再開 (`MusicManager`)

- `PauseGate.IsPaused` の間は `Update` を早期リターンし、毎フレーム
  `PauseMediaForGate()` (冪等) を呼ぶ:
  - `songVideo.Pause()` (再生中のみ)
  - `audioMode == StreamSeparate` → `streamAudioPlayer.Pause()`
  - `audioMode == BundledWave` → `audioS.Pause()`
- 解除検出 (`pausedByGate`) で `ResumeMediaFromGate()`:
  - `mediaEnded == false` のときのみ `Play()` / `UnPause()`
    (終端で止まっているメディアを再再生してループさせない)
  - `lastMediaTime` / `mediaTime` を `FrozenMediaTime` に戻し、`stallSeconds = 0`、
    `lastMediaProgressWallTime = Time.time` — ポーズ時間が
    `StallFailSeconds` (30s) 判定に誤爆しないようにする。
- 再生開始前 (`!playbackStarted`) もポーズ中は早期リターンするため、
  Space で再生を始められない (メニューはモーダル)。

### ライフサイクル

- `MusicManager.Start` で `PauseGate.HardReset()` — GameScene の読み直し
  (リトライ / 再プレイ) でゲートを閉じる。`GManager.cs` は所有外のため
  `ResetSession` には触れない。

### キー凡例 (`V6PauseView.BuildFooter`)

実キー (`V6GameRuntime.UpdatePause` + `KeyActions` 既定):
`←→ = NavPrev/NavNext = 行選択`、`T = MenuSwitch = タブ切替`、`Esc = Back = 再開`。
凡例をこれに一致させ、実在しない ↑↓ と「←→ 変更」を削除。
`AdjustSelected(int)` はマウス操作と同居する公開 API として維持
(呼び出し元がないことを凡例が主張しなくなる)。

## テスト計画

`Assets/Tests/Editor/PauseGateTests.cs` (EditMode, `FlashBeat.Tests`):

- `PauseGateLogic`: `CanJudge` / `FrozenMediaTime` / `ResumeJudgeStartTime`
  (数値固定: 110s 壁時計・42.5s 固定 → StartTime 67.5、再開後の判定時刻 42.5)。
- `PauseGate`: Enter で `step_time` 固定、Enter の冪等性、Exit で
  `Time.time - StartTime == FrozenMediaTime` (許容 1ms)、非ポーズ時 Exit は無操作、
  `HardReset` で状態クリア。`SetUp`/`TearDown` で `GManager` の静的値を退避 / 復元。

## 検証

- `script/compile-check.sh --assembly flashbeat` / `--assembly tests` を通す。
- EditMode テストの実行は Supervisor が統合時に行う (本タスクでは実行しない)。
- 実機 (Win64 / 実機プレイ) 検証は不可 → `docs/evidence/224-pause-gate.md` に未検証として記録。
