---
description: "FlashBeat lean tasks — Issue 226"
---

# Tasks: 曲終了の状態機械 (Issue #226)

**Issue**: #226 | **Branch**: `w/b226` | **Spec**: [`spec.md`](spec.md) / [`plan.md`](plan.md)

## Tasks

- [x] **T1** 純粋状態機械: `Assets/Game/Scripts/Gameplay/SongEndStateMachine.cs` (新規) に
  `SongEndState` / `SongEndFailure` / `SongEndTick` / `SongEndMachine`
  (Playing → Finishing → Completed / Failed、期限切れ強制完了、Fail) を実装する。
  — **done when**: Input / Time / MonoBehaviour に依存せず、.NET ハーネスで
  遷移契約を検証できること
  — **実測**: .NET ハーネス 14 チェックすべて PASS (issue の最小確認シナリオ含む)。
  `script/compile-check.sh --assembly flashbeat` → Build succeeded

- [x] **T2** duration 契約: `Assets/Game/Scripts/Gameplay/MediaTimeline.cs` に
  `IsUsableDuration` と `FallbackSongEnd` を追加し、`HasSongEnded` を
  不正 duration (0 / 負 / NaN / Infinity) で常に false にガードする。
  既存の有効 duration + lead 1s の契約は維持。
  — **done when** : 不正 duration で即フェードしない、かつ既存契約の値が不変
  — **実測** .NET ハーネス (s3/s4) と `SongEndStateMachineTests` の
  `MediaTimelineSongEndContractTests` で固定。既存 `MediaTimelineTests` の
  契約 (47s/48s 系) は値を変更していないため不変

- [x] **T3** MusicManager 配線: `Assets/Game/Scripts/Gameplay/MusicManager.cs` を改修、
  (1) `loopPointReached` 購読と `DetectMediaEnd()` でメディア終端を検出、
  (2) 終端後は `StartTime` を固定し judge 時計を継続、
  (3) `SongEndMachine.Update` で Completed を受理してから従来どおり
  `Initiate.Fade("ResultScene", ...)`、
  (4) 再生中エラー / 30 秒ストールで `FailSongEnd`。
  Judge / JudgeQueue / PlaybackRecovery / UI は無変更。
  — **done when**: duration だけの Result 遷移がなくなり、全ノーツ確定 +
  終端条件の両方が揃ったときだけ遷移する
  — **実測**: `script/compile-check.sh --assembly flashbeat` → Build succeeded

- [x] **T4** EditMode テスト: `Assets/Tests/Editor/SongEndStateMachineTests.cs` (新規)。
  末尾ノーツ待ち、全確定 + 終端で完了、期限強制完了、duration 不正
  (0 / 極端に短い / 極端に長い / NaN / Infinity)、FallbackSongEnd、
  Failed 経路 (Completed にならない) を固定。
  — **done when** : テストアセンブリがコンパイルでき、少なくとも
  Task の受け入れケースを網羅すること
  — **実測** `script/compile-check.sh --assembly tests` → Build succeeded
  (EditMode 実行は Supervisor が統合時に実施)

- [x] **T5** 記録: `docs/evidence/226-song-end-state-machine.md` に実装 / 検証 /
  残存リスク (必要 API を含む) を記録。
  — **done when**: 実測コマンド出力と残存リスクが明記されていること
