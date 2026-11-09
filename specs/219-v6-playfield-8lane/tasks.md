---
description: "FlashBeat lean tasks — Issue 219"
---

# Tasks: 8 レーン遠近法プレイフィールドの移植 (Issue #219)

**Issue**: #219 | **Branch**: `w/spec-gameui` | **Spec**: [`spec.md`](spec.md) / [`plan.md`](plan.md)

> **排他合意が前提**: 既存 `NotesManager` / `Notes` / `Judge` / `MediaTimeline` / シーン
> (`Assets/Game/Scenes/GameScene.unity`) の改変は、**統合 owner と排他的な合意を得てから**
> のみ行う。合意のないまま変更しない。**Unity Editor / シーンの単独所有は統合担当。**

## Tasks

- [x] **T1** 既存 GameScene / NotesManager / Judge / 動画背景 / 既存レーン描画の追跡と、
  シーン依存・入力・タイムスタンプ・ノーツ位置の契約の特定。
  **既存判定・HOLD は再実装しない。**
  — **done when**: 契約が file:line 付きで記録され、
  `V6LaneGeometry.cs:48` が `Judge.LaneKeys` と同一定義を参照していることが確認されていること
  — **実測**: `Assets/Game/Scripts/UI/V6/Game/Playfield/V6LaneGeometry.cs:48` (`LaneCount = 8`)、
  `:45` (`JudgeLineY = 792f`)、`:66` (`JudgeLineHeightDesignPx = 4f`)、`:72` (`JudgeGlowY = 796f`)、`:84`
  (`ExitT01`)、`:125` (遠近法 positions)。
  ノーツは `Assets/Game/Scripts/Gameplay/Notes.cs:110-129,158-165,181` が
  `V6PlayfieldView.NoteLayer` へ head/body の Image を生成し、`:25` で
  `V6PlayfieldView.Current == null` のとき 3D へフォールバックする設計。判定は
  `Assets/Game/Scripts/Gameplay/Judge.cs` / `JudgeQueue.cs` / `LongNote.cs` の既存契約 (読み取りのみ)

- [x] **T2** v6 の 8 レーン遠近形状・判定ライン・ノーツを **実のプレイデータに投影する描画** に
  作り替えた状態にあることの確認 (静止の譜面貼り付けでない)。
  — **done when**: 描画が file:line 付きで記録され、NoteLayer への投影が確認できること
  — **実測**: `Assets/Game/Scripts/UI/V6/Game/Playfield/V6PlayfieldView.cs:38,118-124` が
  `NoteLayer` (uGUI "Notes" RectTransform) を提供し、`Notes.cs:129` が HOLD の body Image、
  `V6PlayfieldView.cs:155-165` がレーンフラッシュを `KeyBindings.IsLaneKeyDown(lane)` で駆動する。
  **描画の有効化は T4 / T5 が前提** (現状は `V6UiMode.Playfield = false` で無効)

- [x] **T3** レーン描画 / 時間進行 / リサイズの EditMode ロジックテストが main にあることの確認。
  — **done when**: テストファイルと対象範囲が記録されていること
  — **実測**: `Assets/Game/Tests/V6PlayfieldGeometryTests.cs` が存在。
  **緑でも描画上の問題は検出しない** (フラグで無効化されている箇所は純論理的テストを通る)

- [x] **T4** 統合 Worker が GameScene へ一度で導入できるよう、公開 API / 接続手順 /
  所有オブジェクト / 必要な Unity アセットを PR に明記する状態になっていることの確認。
  — **done when**: API surface と接続手順が file:line 付きで記録されていること
  — **実測**: `Assets/Game/Scripts/UI/V6/Game/Playfield/V6PlayfieldRuntime.cs` (Canvas 生成と
  `Ensure()`)、`V6PlayfieldView.cs` (`NoteLayer` / レーンフラッシュ)、
  `Assets/Game/Scripts/UI/V6/V6UiMode.cs:27` (有効化フラグ) が接続点。
  公開 API の一覧は PR の説明欄 (**#134 / #140 の移植と同じ「PR へ明記」要件**)

- [ ] **T5** **統合 owner と排他的な合意を取得する**。
  合意の対象ファイルを明示し、合意の日時・合意者・変更内容を記録する。
  — **done when**: 合意記録が PR に残り、
  対象ファイル (`Assets/Game/Scenes/GameScene.unity` /
  `Assets/Game/Scripts/Gameplay/NotesManager.cs` /
  `Assets/Game/Scripts/Gameplay/Notes.cs` /
  `Assets/Game/Scripts/Gameplay/Judge.cs` /
  `Assets/Game/Scripts/Gameplay/MediaTimeline.cs`) が特定されていること
  — **現時点で未取得**。Issue 本文の Owned Paths が「**統合 owner と排他的な合意後のみ**」と
  要求している。**合意が得られるまで T6 以降の改変作業には進まない**

- [ ] **T6** `V6UiMode.Playfield` の owner rollback (2026-11-08) の **再承認** を取得する。
  — **done when**: 再承認の記録があり、`Assets/Game/Scripts/UI/V6/V6UiMode.cs:27` が
  `true` になった理由が PR に残っていること
  — **オーナー判断待ち**。**cc は勝手に変更しない**。
  承認が無い限り `V6PlayfieldRuntime.cs:52` の `if (!V6UiMode.Playfield) return;` により
  Canvas 自体が作られない

- [ ] **T7** 3D レーン (`GameScene.unity:2832` `m_Name: Lane` / `:2837` `m_IsActive: 0`) の
  フォールバック先を解く (合意後のみ)。
  — **done when**: 案 A (シーンの `m_IsActive` を `1` に戻す) か
  案 B (コード側の enabler で active にする) の **どちらか一方** が合意の範囲で選ばれ、
  GameScene へ一度で導入できる形で PR にまとめられていること
  — **合意 (T5) が前提**。**Unity Editor / シーンの単独所有は統合担当**のため、
  cc はシーンを編集しない
  — 現状はどちらの描画も出ない: `Notes.cs:25,111` の 3D フォールバック先は非 active、
  `V6PlayfieldRuntime.cs:52` の v6 Canvas も作られない

- [ ] **T8** legacy レーンの排除が `Playfield` 有効時に効くことの確認
  (`V6GameRuntime.cs:370-374` `HideLegacyPlayfield()` が `HideByName("Lane")` / `"JudgeLine"` /
  `"LaneLights"` / `LaneLight1..8` を隠す)。
  — **done when**: v6 有効時に旧 3D レーンが同時に出ないことが確認できること
  — **T6 / T7 が前提**。`Playfield` が false のままだと旧 v5 レーンも隠れない

- [ ] **T9** 動画背景との前後関係 (レイヤー順序 / sortingOrder / 平面配置) を確認し記録する。
  — **done when**: 映像プレーンがプレイフィールドより後ろにあり、
  prepare 前は白板のままにならないことが確認できること
  — **#13 / PR #263 のブロッカー 1 (映像プレーンが prepare 前は白板のまま) の再発防止**。
  **合意 (T5) が前提**

- [ ] **T10** 実機での画像比較 (FHD 1920×1080 / 2K 2560×1440 / Mac 2560×1664):
  フレーム切れ・レーンのズレ・上下反転がないことを確認する。
  — **done when**: 3 解像度の実画像と判定記録が `docs/evidence/219-v6-playfield/` に残ること
  — **Windows 実機は展示 PC のため cc は実行しない。オーナー実施**。
  **窓リサイズによる擬似全画面は証拠にならない**
  — macOS 実機 (実ディスプレイ全画面 2560×1664) で 1 曲プレイは可能だが、
  Win64 の代替にはしない

- [ ] **T11** 既存プレイ完走と音 / 動画同期を退化させていないことの確認 (非退化の回帰)。
  — **done when**: 1 曲完走の回帰 (=#233 / `#226` の成果物) が壊れていないことの記録があること
  — **Windows 実機は展示 PC のため cc は実行しない。オーナー実施**。
  macOS では `script/mac-songend-harness.sh` で確認できる

- [ ] **T12** 完成していなくても **独立 PR で進捗** (合意記録 / 設計 / 公開 API / 接続手順) を残す。
  — **done when**: PR が作成され、完了していない項目が「未充足 / 未検証」で明示されていること
  — **Issue コメントの投稿は cc が行わない** (PR を作成して cc に報告する)
