---
description: "FlashBeat lean per-issue task list template (project-local override of the Spec Kit core tasks template)"
---

# Tasks: 判定キューの再設計 (レーン別・時刻順) と HOLD 状態機械の一本化 (Issue #164 / #182)

**Issue**: #164 + #182 | **Branch**: `w-182` | **Base**: `origin/fix/main-sync-014` (4f616af)
**Inputs**: [`spec.md`](spec.md), [`plan.md`](plan.md)

**Hard limit: at most 5 tasks per issue.** If the work does not fit in 5 tasks,
the issue is too big — split it and open a follow-up Issue.

## Format: `[ID] [P?] Description (path)`

- **[P]**: can run in parallel with other `[P]` tasks — different files, no ordering dependency
- Every task names the exact file path it creates or changes
- One task = one verifiable state change

## Tasks

- [x] **T1** 判定キュー (純粋クラス): `Assets/Game/Scripts/Gameplay/JudgeQueue.cs`
  (新規) にレーン別・時刻順キュー (`Add` / `TryPress` / `ConsumeTap` /
  `TryPeekMiss` / `Remove`) と HOLD 状態機械 (`BeginHold` / `UpdateHold` /
  `IsHolding`) を実装する。Input / Time 非依存。保持中のレーンは
  `TryPress` を拒否する (押し直しで新しい判定を生まない)
  — **done when**: Unity 同梱 csc (6000.5.6f1) でのコンパイルが通る
  — **実測**: `COMPILE_OK` (UnityEngine.CoreModule 参照で検証)

- [x] **T2** スコア集計 (純粋クラス): `Assets/Game/Scripts/Gameplay/JudgeScoreboard.cs`
  (新規) に「1 ノーツ = 1 判定」の集計 (`ApplyHead` / `DowngradeHoldToBad` /
  `ApplyMiss`) を実装する。HOLD 中断は先頭判定の BAD への差し替えであり、
  追加加点しない
  — **done when**: 中断後に `Perfect + Great + Bad + Miss == 1` かつ
  `RatioScore == SCORE_BAD` となること (T4 のテストで固定)
  — **実測**: `JudgeQueueTests.FullPath_HoldHeadThenBroken_YieldsExactlyOneJudgement`
  PASS

- [x] **T3** Judge の書き直し: `Assets/Game/Scripts/Gameplay/Judge.cs` を
  `JudgeQueue` / `JudgeScoreboard` へ委譲する薄いアダプタに書き直す。
  全レーン走査 (同時押し対応)、`mediaTime = Time.time - GManager.StartTime` の
  既存時刻基準の維持、`NotesManager` の並列リストを書き換えない設計
  (旧 `deleteData` / `ShiftHeldHolds` / `FirstCheckableIndex` /
  `FirstMissableIndex` / `CheckAll` / `Judgement` / `JudgementHold` /
  `UpdateHolds` を廃止)、`LaneKeys` / `EffectPosition` のシグネチャ維持
  — **done when**: Unity 同梱 csc でのコンパイルが通る
  (`LongNote` / `KeyBindings` / `GManager` との参照を含む)、
  `JudgeEffectPlacementTests` が無変更でコンパイルできる
  — **実測**: `JUDGE_COMPILE_OK`、`EFFECT_TESTS_COMPILE_OK`

- [x] **T4** EditMode テスト: `Assets/Tests/Editor/JudgeQueueTests.cs` (新規) に
  spec の Acceptance Criteria A1–A5 を固定する 15 テストを書く
  (同時押し 2 レーン成立 / 他レーン未処理ノーツの非阻害 / 編集順≠時刻順でも
  判定が飛ばない / HOLD 中断の二重加点なし / 保持中・中断後の押し直しで
  スコアが増えない / 終端離し・押し続け・未押下 HOLD / レーン別 Miss)
  — **done when**: テストが Unity EditMode で全 green になる (実行は
  Supervisor)。本 Worker は Unity 同梱 mono + nunit で同一コードを実行して
  検証する
  — **実測**: `15 passed, 0 failed` (Unity 同梱 mono 6.13 + nunit 3.5 で実行)

- [x] **T5** Spec Kit 一式: `specs/182-judge-queue/spec.md` / `plan.md` /
  `tasks.md` を作り、挙動の変更点 (HOLD 中断の配点差し替え、空振り入力の
  ヒット音廃止、Miss のレーン別化、NotesManager リストの不変化) を spec に
  記述する。`.specify/feature.json` は書き換えない (他 Worker と共有のため
  `SPECIFY_FEATURE_DIRECTORY` で指して検証する)
  — **done when**:
  `SPECIFY_FEATURE_DIRECTORY=specs/182-judge-queue SPECIFY_FEATURE_NO_PERSIST=1 \
   .specify/scripts/bash/check-prerequisites.sh --json --require-tasks --include-tasks`
  が通る
  — **実測**: 実行して通ることを確認 (本タスクの検証で実施)
