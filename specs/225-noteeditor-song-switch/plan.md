---
description: "Issue #225 実装計画 — ロードを置換トランザクションにする"
---

# Plan: #225 NoteEditor 曲切替で旧曲ノーツが混入する

**Issue**: #225 | **Branch**: `w/a225` | **Base**: `main` | **Date**: 2026-11-08
**Spec**: [`spec.md`](spec.md)

## Summary

`EditDataSerializer.Deserialize` を「パース → 旧状態破棄 → 適用」の順に作り直す。
`JsonUtility.FromJson` を最初に実行することで、壊れた JSON に対しては旧状態を
一切変更せず例外を投げられる。パース成功後、旧 `NoteObject` を全て `Dispose`
してから `EditData.Notes.Clear()` する (`NoteObject` は C# オブジェクトで
GameObject を持たない。`Init()` が購読した UniRx subscriptions — GL 線描画、
色変更、クリック処理 — は `CompositeDisposable` 経由で `Dispose` される。
既存の `RemoveNote` / `MusicLoader.ResetEditor` と同じ破棄責務のパターン)。
加えて `EditState.LongNoteTailPosition` を `None` に戻す。

`FlashBeatSongLoader.OnFileSelected` は、Deserialize 成功後に再生位置
(`Audio.TimeSamples` / `SmoothedTimeSamples` / `IsPlaying`) を新セッション用に
リセットする。undo/redo 履歴は既存の `EditCommandManager` が
`Audio.OnLoad → Clear` で破棄するため追加変更不要。
曲名 / `IsDirty` の切替は既存のローダーコードが行う。

## Design decisions

- **クリアを Deserialize 側に入れる** (ローダー側ではない):
  `MusicLoader.LoadEditData` 等の他の Deserialize 呼び出し経路も同時に安全になる。
  Deserialize の契約を「EditData を JSON で置換する」と定義するのが素直。
- **完全な一時モデルへの読み替えはしない**: Issue の受入条件にある「別の一時モデルへ
  読込してから原子的置換」のうち、パース失敗 (壊れ JSON) は先行パースで保護される。
  パース成功後の適用中例外は理論上あり得るが、`ToNoteObject` / `AddNote` は
  純粋かつ辞書操作のみで、既知の throw 経路がない。ボーリングな実装を選ぶ。
- **`editData.notes` の null 正規化**: `{"notes": null}` / notes 欠落の JSON は
  空譜面として読める (旧コードは NPE で落ち、メタデータだけ更新される中途半端な
  状態を作れていた)。

## Owned Paths

```text
Assets/NoteEditor/Scripts/Model/EditDataSerializer.cs
Assets/NoteEditor/Scripts/Presenter/FlashBeatSong/FlashBeatSongLoader.cs
Assets/Tests/Editor/NoteEditorSongSwitchTests.cs
Assets/Tests/Editor/SingletonTestHelper.cs
specs/225-noteeditor-song-switch/**
docs/evidence/225-noteeditor-song-switch.md
```

## Dependencies & Gate

- **Depends on**: なし
- **Blocks**: なし (Unity 実機 EditMode 実行は Supervisor が統合時に実施)
- **Gate**: NoteEditor アセンブリと FlashBeat.Tests アセンブリの
  compile-check (dotnet build) が通ること

## Exclusions

- 保存/破棄/キャンセルダイアログ、`EditData.LPB`、ゲーム本体側 (#183)。
