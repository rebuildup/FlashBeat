---
description: "Issue #225 タスク — 曲切替の置換トランザクション実装と検証"
---

# Tasks: #225 NoteEditor 曲切替で旧曲ノーツが混入する

**Issue**: #225 | **Branch**: `w/a225` | **Base**: `main`
**Inputs**: [`spec.md`](spec.md), [`plan.md`](plan.md)

**Hard limit: at most 5 tasks per issue.**

## Format: `[ID] [P?] Description (path)`

- **[P]**: can run in parallel with other `[P]` tasks
- Every task names the exact file path it creates or changes
- One task = one verifiable state change

## Tasks

- [x] **T1** `EditDataSerializer.Deserialize` を置換トランザクション化する
  (先行パース → 旧 `NoteObject` 全 `Dispose` + `Notes.Clear()` →
  `EditState.LongNoteTailPosition` リセット → 新データ適用、
  `editData.notes` の null 正規化)
  (`Assets/NoteEditor/Scripts/Model/EditDataSerializer.cs`)
  — [done when: A→B ロードで B のノーツのみが残り、壊れ JSON は旧状態を維持して例外]
- [x] **T2** `FlashBeatSongLoader.OnFileSelected` の成功パスで再生セッション状態を
  リセットする (`Audio.TimeSamples` / `SmoothedTimeSamples` / `IsPlaying`)
  (`Assets/NoteEditor/Scripts/Presenter/FlashBeatSong/FlashBeatSongLoader.cs`)
  — [done when: 曲切替後に旧曲の再生位置・再生状態が持ち込まれない]
- [x] **T3** 回帰テスト `NoteEditorSongSwitchTests` を追加する
  (A→B で A のノーツ残存なし / B だけ保存される / 同曲再読込で重複しない /
  ロングノーツ破棄 / 壊れ JSON で旧状態維持) と、
  `SingletonTestHelper` への `EditState` 注入を追加する
  (`Assets/Tests/Editor/NoteEditorSongSwitchTests.cs`,
  `Assets/Tests/Editor/SingletonTestHelper.cs`)
  — [done when: EditMode テストが Unity で通る (実行は Supervisor)]
- [x] **T4** compile-check を実行し結果を evidence に記録する
  (`docs/evidence/225-noteeditor-song-switch.md`)
  — [done when: NoteEditor / FlashBeat.Tests の dotnet build が通ることが
  evidence に実出力付きで記される]
- [x] **T5** spec / plan / tasks / evidence を整える
  (`specs/225-noteeditor-song-switch/**`)
  — [done when: 本ディレクトリの 3 ファイルが実装と一致する]
