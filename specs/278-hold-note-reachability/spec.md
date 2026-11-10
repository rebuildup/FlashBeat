---
description: "FlashBeat lean spec — Issue 278"
---

# Spec: 譜面に HOLD ノーツが存在しない — ロングノーツが到達不能

**Issue**: #278 | **Branch**: `w/spec-gameui` | **Date**: 2026-11-10 | **Status**: Draft

## Input

`[Gameplay] 譜面に HOLD ノーツが 1 つも無い — ロングノーツが到達不能 (#59 の受入を阻害)`

## Objective

`Assets/Game/Resources/*.json` の **全 27 曲** で `notes[].type` は **1 (Tap) のみ** であり、
**type=2 (HOLD) のノーツが 1 つも存在しない** (`LongNote.TYPE_HOLD = 2`,
`Assets/Game/Scripts/Gameplay/LongNote.cs:29`)。

HOLD の **実装と EditMode テストは main に存在する** が、
**プレイヤーが到達できる内容が無く、long note が一度も実機で演奏されない**。
結果として **Issue #59 の受入項目「HOLD の実機演奏」が実行不能のまま残る**。

本 spec は **HOLD が実機で到達できる形にする 2 つの対策 (A / B) を定義**し、
**どちらを採るかをオーナー判断事項として明示する**。

## Decisions (凍結する設計)

| 項目 | 決定 |
| --- | --- |
| 対策 (A) — 検証用譜面 (推奨) | HOLD (Tap と HOLD が混ざるもの) を含む **小さな譜面**を、カタログには **登録せずに** テスト用譜面ファイルとして用意し、macOS 実機ハーネスで HOLD の判定 (タップ開始 → 保持 → 終端) を実機検証する。**リリース用カタログに合成の項目を増やさない**。譜面の差し替えは `Assets/Game/Scripts/Songs/PlaySessionSnapshot.cs` の差し替えシームを使う |
| 対策 (A) の実装主体 | **別 Worker が実装中** (`docs/audit/open-issues-audit.md` §4 U12 「対策 (A) テスト用 HOLD 譜面 + macOS 実機ハーネスを Worker が実装中 (譜面は `PlaySessionSnapshot` の差し替えシームを使い、カタログは増やさない)」。**本 spec の Worker は A を重複実装しない**。A の完了を待ち、結果を本 Issue の受入記録に反映する |
| 対策 (B) — 実曲へ HOLD 追加 | チュートリアル曲などに HOLD を入れる。**体験の設計上も意味があるが、譜面内容の方針決定が要る = オーナー判断**。**cc は実曲へ HOLD を追加しない**。追加する場合は `GManager` の 7 並列配列ではなく **譜面 JSON のみ** の変更に留める (曲数・index は増やさない) |
| HOLD の判定仕様 | `specs/164-long-note/spec.md` に **凍結済み** (PR #265 merged)。先頭 1 判定 + 終端で離す / 離す許容幅 ±0.15s / 中断で即 BAD・押し直し不可 / タップと同一配点 / `type=2` + `endNum`。**判定仕様は再設計しない** |
| 譜面の置き場所 | 対策 (A) のテスト用譜面は **カタログ外**。`Assets/Game/Resources/` に置く場合は **Resources 名の重複禁止** (`Assets/Tests/Editor/ResourceChartUniquenessTests.cs` が固定) に注意 |
| 変更しないもの | `LongNote` / `JudgeQueue` / `Judge` / `JudgeScoreboard` の判定ロジック。**HOLD の実装は main にあり修正不要**。**既存の Tap 譜面の最終 note 時刻が変わらない** ことを `LongNoteTests` が固定している |
| 観測点 | HOLD の **開始・保持・終端の 3 段階**。判定数・スコア・コンボが **Tap のみの場合と違う** こと (`LongNote` はタップと同一配点なので、**判定数とコンボ** で区別する。スコア差ではなく **HOLD 判定が 1 件として数えられる** ことを見る) |
| 環境 | macOS 実機 (実ディスプレイ全画面 2560×1664) で対策 (A) のハーネスを実行できる。
  **Windows 実機での HOLD 演奏は #278 の受入条件 3 として別途残す** (=#59 の残り)。
  **Windows 実機は展示 PC のため cc は実行しない。オーナー実施** |
| 証跡 | `docs/evidence/278-hold-note-reachability/` に集める (新規)。`docs/evidence/index.md` /
  `docs/audit/open-issues-audit.md` は **他 Worker 所有** なので触らない |

## 非目標 (Out of scope)

- HOLD の判定ロジックの再実装・仕様変更 (`specs/164-long-note/spec.md` は凍結済)。
- 対策 (A) の重複実装 (**別 Worker が実装中**)。
- 対策 (B) の実施 (譜面内容の方針決定は **オーナー判断**。cc は実曲へ HOLD を追加しない)。
- カタログへの新規曲登録 / `GManager` の 7 並列配列の追加。
- Windows 実機での HOLD 演奏 (別途残す)。
- `docs/evidence/index.md` / `docs/audit/open-issues-audit.md` の編集 (他 Worker 所有)。

## Acceptance criteria

1. **HOLD を含む譜面が 1 つ以上、実機で到達できる形で存在する**。
   (対策 A のテスト用譜面、または対策 B の実曲への追加のいずれか)
2. **macOS 実機ハーネスで HOLD ノーツの「開始・保持・終端」の 3 段階が観測できる**。
   判定数・スコア・コンボが **Tap のみの場合と違う** ことが記録に残っている。
3. **HOLD を途中で離すと BAD** になる (中断は救済されない) ことの観測がある。
4. **既存 Tap 譜面の判定が退行していない** (LongNoteTests 緑 + 既存曲の実測が同じ)。
5. **Windows 実機での HOLD 演奏** は #278 の受入条件 3 として **未実施のまま明示** されている。
6. 対策 (A) と対策 (B) の **採否が判断として記録** されている (未決のまま放置しない)。
7. 判定が OK / NG / 未検証 の 3 値で記録され、**未検証が「合格」に書き換えられていない**。

## 現在の実装状況 (2026-11-10 時点)

**充足済み (HOLD の実装・仕様)**

| 受入条件 | 判定 | 根拠 (file:line) |
|---|---|---|
| HOLD の実装 | 充足 | `Assets/Game/Scripts/Gameplay/LongNote.cs:26` (`TYPE_TAP = 1`)、`:29` (`TYPE_HOLD = 2`)、`:71` (`IsHold`) |
| HOLD の判定経路 | 充足 | `Assets/Game/Scripts/Gameplay/JudgeQueue.cs:165` (`BeginHold`)、`:183` (`UpdateHold`)、`Assets/Game/Scripts/Gameplay/Judge.cs:178` (`DowngradeHoldToBad`)、`:230` (`queue.BeginHold(hit)`) |
| HOLD の仕様凍結 | 充足 | `specs/164-long-note/spec.md` (PR #265 merged / Issue #164 CLOSE) |
| HOLD の EditMode テスト | 充足 | `Assets/Tests/Editor/LongNoteTests.cs` (23 件)、`Assets/Tests/Editor/JudgeQueueTests.cs` の HOLD 2 件 (`Hold_ExpiryBoundary_MatchesLongNoteRule` / `Hold_RuntimeResult_MatchesLongNoteRules`) |
| 既存 Tap 譜面の非退化 | 充足 (テスト) | `LongNoteTests.cs` が「既存 tap 譜面の最終 note 時刻が変わらない」ことを固定 |

**未充足**

| 受入条件 | 判定 | 未充足の理由 |
|---|---|---|
| 1. HOLD を含む譜面が実機で到達できる形で存在する | **未充足** | 実測: `Assets/Game/Resources/*.json` のうち `notes[]` を持つ **28 ファイル** (うち `E2EProbeSong` は 0 notes、`ニセモノ` は 0 notes のプレースホルダ) の **全件で `type=2` の個数は 0**。Issue 本文の 27 曲の実測 (4th smile 105 / … / 阿吽のビーツ 262 / ニセモノ 0) と一致 |
| 2. macOS 実機ハーネスでの 3 段階観測 | **未充足** | 対策 (A) のテスト用譜面 + ハーネスは **別 Worker が実装中** (`docs/audit/open-issues-audit.md` §4 U12)。`docs/evidence/index.md` §7.3-4 も「HOLD を含む検証用譜面 (または実曲への HOLD 追加) が前提」と明記 |
| 3. HOLD の中断 (BAD) の実機観測 | **未充足** | 同上。既存は `LongNoteTests` の純ロジック検証のみ |
| 4. 実機での既存曲回帰 | **未充足 (macOS で可能)** | `script/mac-songend-harness.sh` で既存曲 (「ツイッターランド」130 notes) の完走は実証済み (`docs/evidence/233-song-end-harness.md`) が、**HOLD 追加後に同じ結果か** は未確認 |
| 5. Windows 実機での HOLD 演奏 | **未充足 (Windows 実機必須)** | Issue 本文の受入条件 3 として **別途残す** 項目。**Windows 実機は展示 PC のため cc は実行しない。オーナー実施** |
| 6. 対策 (A) / (B) の採否判断 | **未決 (オーナー判断)** | 対策 (A) は推奨案だが Worker が実装中、対策 (B) は譜面内容の方針決定が必要 = **オーナー判断** |
| Issue #59 への波及 | **未充足** | `docs/evidence/59-feature-acceptance.md` §2.3 の「残存」が「実機での HOLD 演奏 2 ケース (成功 / 中断) の確認」のまま。#278 完了後に更新される |
