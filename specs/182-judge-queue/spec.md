---
description: "FlashBeat lean per-issue specification template (project-local override of the Spec Kit core spec template)"
---

# Spec: 判定キューの再設計 (レーン別・時刻順) と HOLD 状態機械の一本化 (Issue #164 / #182)

**Issue**: #164 + #182 | **Branch**: `w-182` | **PR base**: `main` | **Date**: 2026-11-08 | **Status**: Draft

**Input**: `判定の二重加点 / レーン間干渉 / ノーツ順序依存`

> Lean rules (enforced by `docs/agent/spec-kit-workflow.md`):
> one Issue = one branch = one worktree = one Draft PR; ≤5 tasks; no retrospective
> re-specification of the legacy game; no second authority for project governance.

## Objective

展示 8 レーン音ゲーの判定を壊さずに、次の 2 つの報告を解消する。

1. **#164**: HOLD (ロングノーツ) の途中でキーを離すと二重加点される。HOLD の
   開始 / 保持 / 終了を 1 本の状態機械にまとめ、離鍵・押し直しでスコアが
   二重計上も消失もしないようにする。
2. **#182**: 判定キューがレーン別・時刻順でないため、他レーンの未処理ノーツが
   正常な入力を阻害する。Web 編集順 (block 順) と時刻順が食い違う譜面で
   判定が飛ぶ / 取りこぼす。1 入力は最も近い未判定ノーツ 1 つにだけ効き、
   同時押しは別入力として両方成立すること。

## Background (現状の障害)

旧 `Judge.cs` は `NotesManager` の単一リスト (譜面ファイル編集順) の
**先頭 1 件 (+ 同一時刻グループ) だけ**を毎フレーム判定していた:

- 先頭に未処理ノーツが残る (時刻切れ待ち) と、その後ろにある別レーンの
  正常な入力がブロックされる (#182 のレーン間干渉)。
- 編集順と時刻順が食い違うと、時刻が早いノーツが判定対象から外れる (#182 の飛び)。
- HOLD 中断時に先頭判定 (Perfect/Great) に **加えて** BAD を追加加点していた。
  1 ノーツ最大 6 点となり `maxScore = note数 × 5` と矛盾する (#164 の二重加点)。

## Decisions (凍結する設計)

| 項目 | 決定 |
| --- | --- |
| 判定キュー | 新規純粋クラス `JudgeQueue` (`Assets/Game/Scripts/Gameplay/JudgeQueue.cs`)。ノーツをレーン別に保持し、レーン内は **時刻順** (同一時刻は譜面編集順) に並べる。Input / Time 非依存で EditMode テストから直接検証できる |
| 入力の割り当て | 押下 1 回は「そのレーンの、押した時刻に最も近い未判定ノーツ 1 つ」にのみ効く (`JudgeQueue.TryPress`, 窓 = `LongNote.BAD_WINDOW` 0.20s)。窓外の空振り入力は何もしない (旧仕様どおり) |
| 同時押し | 別レーンの入力は独立に判定される (`Judge.Update` が全レーンを毎フレーム走査)。同一レーンの同一時刻 2 ノーツは、時刻が早い方 (編集順が先) を優先 |
| 時刻基準 | 既存方式を維持: `mediaTime = Time.time - GManager.StartTime` (`MediaTimeline` の不変式)。譜面時刻はメディア時刻 |
| スコア集計 | 新規純粋クラス `JudgeScoreboard` (`Assets/Game/Scripts/Gameplay/JudgeScoreboard.cs`)。**1 ノーツ = 1 判定**。Judge が各変更後に GManager の static フィールドへ写すので、ResultScene など他シーンの参照方式は不変 |
| HOLD 状態機械 | `開始 (先頭押下で 1 回加点) → 保持 (評価なし) → 終了 (離しで確定)`。状態は `JudgeQueue` がレーンごとに最大 1 件保持する |
| HOLD 中断 | 途中で離す → 先頭判定を **BAD に差し替える** (`JudgeScoreboard.DowngradeHoldToBad`)。BAD を追加加点しないので二重計上にならない。コンボは先頭で 1 回だけ数え、中断で追加も消失もしない (frozen spec 164 R4 の「コンボ維持」を維持) |
| HOLD 押し直し | 保持中のレーンは新しい判定を生まない (`TryPress` が保持中を拒否する)。中断後の押し直しも救済しない (ノーツはキューから消えている) |
| HOLD 成功 | 終端 ±`GREAT_WINDOW` (0.15s) で離す → 成功、追加加点なし。押したまま終端 + 0.15s をすぎる → 成功扱いで確定 (旧実装と同じ) |
| HOLD 未押下 | 開始 + `BAD_WINDOW` をすぎて押されなければ Miss (frozen spec 164 と同じ) |
| Miss | レーンごとに判定 (`TryPeekMiss`)。他レーンの未処理ノーツに阻害されない。保持中の HOLD は Miss にならない |
| NotesManager のリスト | Judge は `NotesManager` の並列リストを **書き換えない** (旧 `deleteData` の RemoveAt と `ShiftHeldHolds` を廃止)。リストは譜面編集順の正本として残り、判定エフェクトの表示位置 (`Judge.EffectPosition`) に使う。`SourceIndex` で譜面行を参照する |
| 判定エフェクト | 判定されたノーツ自身のレーンに表示 (既存 `EffectPosition` を維持, #168) |

## Owned Paths

- `Assets/Game/Scripts/Gameplay/Judge.cs` — 判定の入力層。`JudgeQueue` /
  `JudgeScoreboard` へ委譲する薄いアダプタに書き直す
- `Assets/Game/Scripts/Gameplay/JudgeQueue.cs` (新規) — レーン別・時刻順キュー +
  HOLD 状態機械 (純粋クラス)
- `Assets/Game/Scripts/Gameplay/JudgeScoreboard.cs` (新規) — 判定回数とスコアの
  集計 (純粋クラス)
- `Assets/Tests/Editor/JudgeQueueTests.cs` (新規) — 判定キュー / スコア集計の
  EditMode テスト
- `specs/182-judge-queue/` — 本 Spec Kit 一式

触らない (`Assets/Game/Scripts/Gameplay/NotesManager.cs` / `Notes.cs` /
`MusicManager.cs` / `YouTubeStreamResolver.cs` / `Persistence/**` / `UI/**` /
`Assets/**/*.unity`): 今回の設計では NotesManager と Notes の変更は不要だった
(Judge 側の参照読み替えで完結する)。MusicManager / YouTube 関係は別 Worker の
排他所有。

## Behavior Changes (挙動の変更点)

1. **HOLD 中断の配点**: 旧 = 先頭判定 + BAD を追加 (1 ノーツ最大 6 点)。
   新 = 先頭判定を BAD に差し替え (1 ノーツ最大 5 点)。`maxScore = note数 × 5`
   の定義と一致するようになる。
2. **別レーンの未処理ノーツが入力を阻害しない**: 旧 = 全体の先頭ノーツだけを
   判定。新 = レーン別キューで各レーン独立。
3. **同時押し**: 旧 = 同一時刻グループのみ (編集順依存)。新 = 全レーンを毎フレーム
   走査するので、時刻が近ければ同一フレームの 2 押しが両方成立する。
4. **空振り入力のヒット音**: 旧 = 窓外の押下でもヒット音が鳴っていた
   (`Judgement` が判定前に PlayOneShot)。新 = 実際にノーツを判定したときだけ鳴る。
5. **Miss の処理数**: 旧 = 1 フレームに全球 1 件。新 = レーンごとに 1 件
   (最大 8 件/フレーム)。取りこぼしがなくなる。
6. **NotesManager のリストがプレー中に縮まなくなる**: 旧 = 判定ごとに RemoveAt。
   新 = 不変。`GameSceneManager.SaveNotesTime` やリプレイ系のコードへの影響なし
   (リストを参照するのは Judge のエフェクト配置だけ)。

## Acceptance Criteria

- **A1** (EditMode テスト): 同時押し (2 レーン同時) で両レーンが判定される。
- **A2** (EditMode テスト): 他レーンの未処理ノーツが正常入力を阻害しない。
- **A3** (EditMode テスト): 編集順と時刻順が食い違っても判定が飛ばない /
  失われない。
- **A4** (EditMode テスト): HOLD 開始 1 回 + 保持中に離す → 判定回数 1、
  二重加点しない (中断で先頭判定が BAD に差し替わるのみ)。
- **A5** (EditMode テスト): HOLD 中の押し直しでスコアが増えない。中断後の
  押し直しでも復活しない。
- **A6**: 既存テスト (`LongNoteTests`, `JudgeEffectPlacementTests`,
  `JudgeLogicTests`) が壊れない (シグネチャ維持)。
- **A7**: Unity EditMode 実行・実機検証は Supervisor が実施 (本 Worker は
  コードとテストコードまで)。

## Known Issues / Out of Scope

- 同一レーンに同一時刻で 2 ノーツがある譜面では、編集順が先の 1 つだけが判定
  される (1 入力 1 ノーツの定義どおり。実譜面で発生しない想定)。
- HOLD 中断時に PERFECT → BAD と判定メッセージが 2 回表示される (旧実装と同じ
  表示回数。メッセージの抑制は UI 側の所有領域なので触らない)。
- `GManager.nextNotes` は従来どおり判定ごとに増加する (歌詞表示との整合は現状維持)。
