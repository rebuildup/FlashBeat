# Evidence: 224 — ポーズゲート (時間・音声を止める)

- 日付: 2026-11-08
- Branch: `w/wpause`
- Issue: #224 ([P1/v6] Game Pause・楽曲ローディングをPencilデザインに置換) の未達部分
  「ポーズ中に時間・音声が止まらない」
- 範囲: **コード側のポーズゲートのみ**。実機プレイ検証は未実施 (下記「残存リスク」)。

## 実装

### 新規: `Assets/Game/Scripts/Gameplay/PauseGate.cs`

- `PauseGateLogic` (純ロジック, Unity の静的状態に触らない):
  - `CanJudge(bool isPaused)` — ポーズ中は判定しない
  - `FrozenMediaTime(float currentStepTime)` — ポーズ開始時に固定する譜面時刻
  - `ResumeJudgeStartTime(float wallClockTime, float frozenMediaTime)` —
    再開時の `StartTime` (= `wallClock - frozenMediaTime`)
- `PauseGate` (状態):
  - `IsPaused` / `FrozenMediaTime`
  - `Enter()` — `GManager.step_time` を固定 (冪等)
  - `Exit()` — `GManager.StartTime = Time.time - FrozenMediaTime` に補正し、
    `step_time` を固定値に戻す (冪等)。ポーズ前後でノーツ位置が飛ばない。
  - `HardReset()` — シーンをまたぐリセット

### 変更: `MusicManager.cs`

- `Update` 冒頭で `PauseGate.IsPaused` を見て早期リターン。ポーズ中は
  メディア時計の追従 (`GManager.StartTime` / `GManager.step_time` の更新)、
  stall 判定、曲終了状態機械 (`SongEndMachine`) のいずれも走らない。
- `PauseMediaForGate()` (毎フレーム呼ばれて冪等): 実メンバーに対して
  - `songVideo.Pause()` (再生中のみ)
  - `audioMode == PlaybackSource.StreamSeparate` → `streamAudioPlayer.Pause()`
  - `audioMode == PlaybackSource.BundledWave` → `audioS.Pause()`
- `ResumeMediaFromGate()` (解除の最初のフレーム):
  - `mediaEnded == false` のときのみ `Play()` / `UnPause()` —
    メディア終端後のポーズで映像が再再生 (ループ) しないようにする。
  - `lastMediaTime` / `mediaTime` を `FrozenMediaTime` に戻し、`stallSeconds = 0`、
    `lastMediaProgressWallTime = Time.time` — ポーズ時間 (30s 超でも) が
    `StallFailSeconds` のストール誤検出 (`FailSongEnd(MediaStalled)`) に
    ならないようにする。
- `Start` の先頭で `PauseGate.HardReset()` — ポーズ中のリトライ / リタイアで
  次のプレイが止まったままにならない。(`GManager.ResetSession` は所有外のため触れない。)

### 変更: `Judge.cs`

- `Update` に `if (PauseGate.IsPaused) return;` — 判定・スコア加算を停止。
- 判定時刻を `Time.time - GManager.StartTime` から `GManager.step_time` に変更。
  通常再生中は `MusicManager` が毎フレーム `step_time` を同じ式で更新するため
  両者は同一 (既存の判定タイミングの仕様は不変)。ポーズ中は `step_time` が
  固定されるため、判定時刻が進まない。

### 変更: `Notes.cs`

- `Update` の `GManager.Start` ブロック内に `if (PauseGate.IsPaused) return;`
  (step_time 固定により自然に止まるが、明示的にガード)。

### 変更: `V6PauseView.cs` (v6)

- `Open()` → `PauseGate.Enter()`、`Close()` → `PauseGate.Exit()`。
- フッタのキー凡例を実際に動くキーに一致させた (キーボード操作の嘘の是正):
  - 削除: 「↑↓ 選択」(上向きキーはどこにも配線されていない) /
    「←→ 変更」(`AdjustSelected` の呼び出し元はゼロ)
  - 追加 / 修正: 「←→ 選択」(`V6GameRuntime.UpdatePause` が `MoveSelection` に配線済み)、
    「タブ切替 T」(実キーは `KeyActions.MenuSwitch` = `KeyCode.T`、旧表記は「Tab」)、
    「再開 Esc」(変更なし)
  - `AdjustSelected(int)` は行のステッパー / クリック (マウス) と同居する公開 API として
    維持。←→ への配線はしない: 未使用のナビゲーションキーがなく、
    `V6GameRuntime.cs` / `KeyActions.cs` は他担当所有のため。

### 変更: `PauseMenuView.cs` (v5 legacy)

- `Open()` → `PauseGate.Enter()`、`Close()` → `PauseGate.Exit()` (v6 と同じ挙動)。
  `Toggle()` は Open / Close 経由なので追加変更なし。

### 所有境界外の変更

なし。`V6GameRuntime.cs` / `NotesManager.cs` / `KeyActions.cs` / `GManager.cs` は
変更していない (PauseView の API 変更も不要だった)。

## テスト

### 新規: `Assets/Tests/Editor/PauseGateTests.cs` (EditMode)

数値を固定して検証する:

- `ResumeJudgeStartTime(110, 42.5) == 67.5`、再開直後の判定時刻
  `110 - 67.5 == 42.5` (ノーツ位置が飛ばないことの数値根拠)
- ポーズ時間 0 なら `StartTime` 不変 (既存の判定タイミングを壊さない)
- `Enter` が `GManager.step_time` を固定 / 冪等 (2 回目の Enter で再取得しない)
- `Exit` 後に `Time.time - StartTime == FrozenMediaTime` (許容 1ms)
- 非ポーズ時の `Exit` は無操作 / `HardReset` で状態クリア
- `SetUp` / `TearDown` で `GManager.step_time` / `StartTime` を退避 / 復元

### 既存テスト

- `PauseMenuViewTests` / `V6PauseRuntimeTests` / `V6PauseViewLogicTests` は
  変更対象の API (`NextNoteSpeed` / `V6PauseViewLogic` の表示式 / `Enabled`) に
  触れないため不変。期待値の変更は不要だった。

## 検証結果

- `script/compile-check.sh --project /Users/rebui/work/FlashBeat.wpause --assembly flashbeat`
  → **Build succeeded** (141 sources)
- `script/compile-check.sh --project /Users/rebui/work/FlashBeat.wpause --assembly tests`
  → **Build succeeded** (37 sources)
- EditMode テストの実行は **Supervisor が統合時に行う** (本タスクでは Unity Editor を
  起動しない制約のため未実行)。

## 未検証 (実機プレイ)

以下は **実機未検証** です。Unity Editor を起動できない制約のため、
代替検証として上記の compile-check と EditMode テストを用意した。

- 実際にポーズ中に映像 / 音声が止まり、再開で音ずれなしに続くこと
- ポーズ解除直後にノーツ位置が視覚的に飛ばないこと
- ポーズ中のリトライ / リタイアが正常に動作すること
- v5 ポーズ (V6UiMode 無効環境) で同じ挙動になること

## 残存リスク

1. **再生開始前のポーズ**: ポーズメニューが開いている間は Space で再生を開始できない
   (メニューをモーダル扱い)。再生開始前のポーズ解除では何も再生しない
   (`ResumeMediaFromGate` は `playbackStarted` を条件にしている —
   再生を始めるのは `StartPlayback` だけ)。
2. **キーボードでの値変更は不可**: v6 ポーズの設定値はマウス (ステッパー / クリック) のみ。
   キー割当の追加は `KeyActions.cs` / `V6GameRuntime.cs` の所有者と要調整。
3. **`judge` の `step_time` 移行**: `Judge` の判定時刻の基準を `step_time` に寄せた。
   通常再生中は同一値だが、`MusicManager` のスクリプト実行順が変わった場合
   (Project Settings の変更) は 1 フレーム分のずれが生じうる
   (旧実装も同じ構造で `StartTime` を前フレーム値として読んでいたため、
   リスクは既存と同水準)。
