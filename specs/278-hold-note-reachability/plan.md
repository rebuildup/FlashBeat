---
description: "FlashBeat lean implementation plan — Issue 278"
---

# Plan: HOLD ノーツの実機到達 — Issue #278

**Issue**: #278 | **Branch**: `w/spec-gameui` | **Spec**: [`spec.md`](spec.md) | **Date**: 2026-11-10

## Architecture

```
[ 現状 (main) ]

Assets/Game/Resources/*.json   ← 28 ファイル (27 曲 + E2EProbeSong)
  notes[].type = 1 (Tap) のみ   ← type=2 (HOLD) は 0 個  ★ 到達不能
  → 既存 27 曲 (ニセモノ / E2EProbeSong は 0 notes)

HOLD の実装は main にある (変更しない)
  LongNote.cs:26 TYPE_TAP = 1 / :29 TYPE_HOLD = 2 / :71 IsHold
  JudgeQueue.cs:165 BeginHold / :183 UpdateHold
  Judge.cs:178 DowngradeHoldToBad / :230 queue.BeginHold(hit)
  仕様: specs/164-long-note/spec.md (凍結済 / PR #265)
  テスト: LongNoteTests.cs (23 件) + JudgeQueueTests.cs (HOLD 2 件)

[ 対策 (A) — 検証用譜面 + macOS 実機ハーネス ]  ★ 別 Worker が実装中
  譜面はカタログ外。差し替えは PlaySessionSnapshot のシーム
  Assets/Game/Scripts/Songs/PlaySessionSnapshot.cs:25 (sealed class)
      :64 Begin(SongData, chartVersion, chartJson) … :90+ (Current / PinnedSongFor)
      → プレイセッション開始時に譜面 JSON を固定する既存の継ぎ目を使う
  ハーネス: Assets/Game/Scripts/Common/AutoTestV6SongEnd.cs (既存) を起点に
            HOLD 用の注入ハーネスを足す / script/mac-songend-harness.sh を起点にする
  観測: 開始 (タップ) → 保持 (押しっぱなし) → 終端 (離す) の 3 段階
        判定数・コンボが Tap のみの場合と違うこと
        途中で離すと BAD になること

[ 対策 (B) — 実曲へ HOLD 追加 ]                ★ オーナー判断
  例: チュートリアル曲に HOLD を数本入れる
  注意: 曲数 (GManager の 7 並列配列) は増やさない。譜面 JSON のみの変更に留める
        既存 Tap 譜面の最終 note 時刻が変わらないことを LongNoteTests が固定している
  注意: Resources 名は重複不可 (ResourceChartUniquenessTests が固定)

[ 残作業 ]
  ① 対策 (A) の完了を待ち、結果を受入記録に反映
  ② 既存曲の実測回帰 (Tap のみ → 追加後でも同じか)
  ③ Windows 実機での HOLD 演奏 (別途残す受入条件 3)
  ④ 判定表 (OK / NG / 未検証)
```

## 実装順序

1. **現状の実測 (変更なし)**
   - `Assets/Game/Resources/*.json` の `notes[].type` を全件走査し、`type=2` が 0 個であることを再確認する。
   - 結果を `docs/evidence/278-hold-note-reachability/` に残す。
2. **対策 (A) の Worker の進捗を確認する (重複実装しない)**
   - `docs/audit/open-issues-audit.md` §4 U12 の記載に従い、**別 Worker が実装中**。
   - 本 Worker は **A を重複実装しない**。完了するまで待つ。
   - 完了したら、A が使った **差し替えシーム** (`PlaySessionSnapshot`) と **観測点** を
     本 Issue の受入記録に反映する。
3. **対策 (B) の採否をオーナーに確認する**
   - 実曲 (例: チュートリアル) へ HOLD を入れるなら、**譜面内容の方針決定が要る** = オーナー判断。
   - **cc は実曲へ HOLD を追加しない。** 指示があった場合のみ、
     `GManager` の 7 並列配列を増やさずに **譜面 JSON のみ** を編集する。
   - 編集時は `LongNoteTests` の「既存 tap 譜面の最終 note 時刻が変わらない」を必ず確認する。
4. **macOS 実機での 3 段階観測**
   - 実ディスプレイ 2560×1664 全画面で HOLD の開始・保持・終端を観測する。
   - 判定数・コンボが Tap のみの場合と違うことを確認する (HOLD はタップと同点なので、
     **スコア差ではなく判定数とコンボ** で区別する)。
   - 途中で離すと BAD になることも確認する。
5. **既存曲の実測回帰**
   - `script/mac-songend-harness.sh` で既存曲 (「ツイッターランド」130 notes) の完走を再確認する。
   - 判定 130/130 / `Completed` / `ResultScene` 到達が保たれていること
     (`docs/evidence/233-song-end-harness.md` の基準値と比較)。
6. **Windows 実機での HOLD 演奏 (オーナー実施)**
   - Issue 本文の受入条件 3 として **別途残す**。#59 の残りとも重なる。
7. **判定表**
   - 3 つの受入条件 × OK / NG / 未検証。対策 (A)/(B) の採否判断も記録する。

## ブロッカー

| ブロッカー | 内容 | 対処 |
|---|---|---|
| **対策 (A) の Worker が実装中** | テスト用譜面 + macOS 実機ハーネスは別 Worker の担当 | **重複実装しない**。完了を待つ。完了後は結果の反映だけ |
| **対策 (B) はオーナー判断** | 実曲へ HOLD を入れるかは譜面内容の方針決定が要る | **cc は追加しない**。判断が出たら譜面 JSON のみを編集 (7 並列配列は増やさない) |
| **Windows 実機 (展示 PC)** | HOLD の実機演奏は #278 の受入条件 3 / #59 の残り | **Windows 実機は展示 PC のため cc は実行しない。オーナー実施** |
| **Resources 名の重複** | テスト用譜面を `Assets/Game/Resources/` に置く場合、Resources 名の重複で `Resources.Load` が不定になる | `Assets/Tests/Editor/ResourceChartUniquenessTests.cs` が固定。**対策 (A) の Worker と重複しない名前を使う** |
| **HOLD の判定仕様** | `specs/164-long-note/spec.md` は凍結済み | 再設計しない。仕様変更が必要な場合は オーナーの判断 |
| **`docs/audit/open-issues-audit.md` の編集** | 他 Worker 所有 | 触らない |

## テスト戦略

| 種別 | 内容 | 備考 |
|---|---|---|
| EditMode (既存のみ) | `LongNoteTests.cs` (23 件)、`JudgeQueueTests.cs` (HOLD 2 件) | **判定仕様は変更しない**。新規テストは A の Worker が追加するならそちら |
| 譜面データ検査 | `Assets/Game/Resources/*.json` の `type=2` の全件走査 (スクリプトで再現可能にする) | **判定基準**: 27 曲 (HOLD 追加後) に `type=2` が 1 個以上あること |
| 実機 (macOS) | HOLD の開始・保持・終端の 3 段階 + 中断 BAD。実ディスプレイ 2560×1664 全画面 | **macOS 実機 (実ディスプレイ全画面) で実行可能** |
| 実機 (Windows) | HOLD の実機演奏 | **オーナー実施**。判定は「未検証」 |
| 回帰 | 既存曲 (「ツイッターランド」) の 130/130 確定・`Completed`・Result 到達 | `script/mac-songend-harness.sh`。**macOS 実機 (実ディスプレイ全画面) で実行可能** |
