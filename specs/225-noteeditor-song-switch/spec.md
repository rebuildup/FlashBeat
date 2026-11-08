---
description: "FlashBeat lean per-issue specification template (project-local override of the Spec Kit core spec template)"
---

# Spec: #225 NoteEditor 曲切替で前の曲のノーツが混入し保存される (P1/データ破壊)

**Issue**: #225 | **Branch**: `w/a225` | **PR base**: `main` | **Date**: 2026-11-08 | **Status**: Draft

**Input**: `gh issue view 225`, 監査済みコード (`FlashBeatSongLoader.cs:44-58`, `EditDataSerializer.cs:60-111`)

> Lean rules: one Issue = one branch = one worktree = one Draft PR; ≤5 tasks per issue.

## Objective

NoteEditor 内で曲 A を開いた後 曲 B を開くと、A のノーツが `EditData.Notes` に残ったまま
B の編集が行え、保存すると A のノーツが `Assets/Game/Resources/B.json` に書き込まれる。
曲切替のロードを**置換トランザクション**として扱い、旧曲のノーツと派生状態を確実に破棄する。
読込失敗時は旧曲の状態を完全に維持する。

## Root cause (confirmed)

- `FlashBeatSongLoader.OnFileSelected` は `EditDataSerializer.Deserialize` を直接呼び、
  旧ノーツをクリアしない。`MusicLoader.ResetEditor` (旧曲クリアを持つ) は呼ばれない。
- `EditDataSerializer.Deserialize` は BPM / 歌詞 / VideoId を置換するが
  `EditData.Notes` を Dispose/Clear せず `AddNote` で追加する。
- 結果: A→B のロードで A の NoteObject が残存し、`Serialize` は両方を B.json に書く。

## Owned Paths

- `Assets/NoteEditor/Scripts/Model/EditDataSerializer.cs` — Deserialize の置換トランザクション化
- `Assets/NoteEditor/Scripts/Presenter/FlashBeatSong/FlashBeatSongLoader.cs` — ロード成功時のセッション状態切替
- `Assets/Tests/Editor/NoteEditorSongSwitchTests.cs` — 回帰テスト (新規)
- `Assets/Tests/Editor/SingletonTestHelper.cs` — `EditState` シングルトンのテスト用注入 (追記)
- `specs/225-noteeditor-song-switch/**`, `docs/evidence/225-noteeditor-song-switch.md`

## Acceptance criteria

1. 曲 A ロード → 曲 B ロード後、`EditData.Notes` には B のノーツのみが存在する
   (単音・ロングとも)。同じ曲の再読込で重複しない。
2. A→B→保存で、B の JSON に A のノーツが含まれない (回帰テストで担保)。
3. 曲名 / BPM / offset / 歌詞 / VideoId / `IsDirty` は新曲のものに切り替わる。
   `EditState.LongNoteTailPosition` など旧曲由来の派生状態は破棄される。
4. 旧 NoteObject は Dispose される (`NoteObject` は MonoBehaviour ではなく、
   `Init()` が購読した UniRx subscriptions (GL 線描画含む) を `Dispose` で解放。
   GameObject は保持しないため Dispose + Clear で破棄は完了する)。
5. 壊れた JSON の読込は例外で失敗し、旧曲のノーツ / BPM / 歌詞を一切変更しない。

## Exclusions (explicitly out of scope)

- ゲーム本体の選曲競合 #183 (Unity 実機 GameScene 側) は別 Issue。
- 未保存編集時の保存/破棄/キャンセルダイアログ (曲切替前確認) は本 Issue では
  作らない。現状のロードは即時置換で、`IsDirty` は新曲セッションで false に戻る。
- `EditData.LPB` (グリッド既定値。曲データに含まれないため旧値維持は仕様通り)。
- Unity 実機での操作確認 (Supervisor が統合時に実施)。
