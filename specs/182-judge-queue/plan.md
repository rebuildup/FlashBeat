---
description: "FlashBeat lean per-issue implementation plan template (project-local override of the Spec Kit core plan template)"
---

# Plan: 判定キューの再設計 (レーン別・時刻順) と HOLD 状態機械の一本化 (Issue #164 / #182)

**Issue**: #164 + #182 | **Branch**: `w-182` | **Spec**: [`spec.md`](spec.md) | **Date**: 2026-11-08

## Architecture

```
Judge.cs (MonoBehaviour, 入力層)
  ├─ EnsureQueue()      : NotesManager の譜面データから JudgeQueue を 1 度だけ組む
  ├─ Update()
  │    1. 全レーン走査: IsLaneKeyPressed → PressLane
  │    2. 全レーン走査: queue.UpdateHold (保持状態機械)
  │    3. 全レーン走査: queue.TryPeekMiss → Miss
  │    各変更後に FlushScore() で GManager へ写す
  ├─ JudgeQueue (純粋, 新規)   : レーン別・時刻順キュー + HOLD 状態機械
  └─ JudgeScoreboard (純粋, 新規): 1 ノーツ = 1 判定の集計
```

- **時刻**: `mediaTime = Time.time - GManager.StartTime`。
  譜面時刻 (`NotesManager.NoteTimeSeconds`) はメディア時刻なので、この 1 本の
  基準だけでノーツ / 映像 / 音が揃う (`MediaTimeline` の不変式)。既存の
  `GManager.StartTime` と `lag` の扱いは変更しない。
- **判定窓と配点は `LongNote` が正本** (frozen spec 164)。`JudgeHead` /
  `JudgeRelease` / `SCORE_*` / `*_WINDOW` をそのまま使う。
- **テスト容易性**: `JudgeQueue` と `JudgeScoreboard` は Input / Time 非依存。
  EditMode テストは時刻を明示的に与えて判定の全経路を検証する。

## Data Flow

1. `NotesManager.Load` (無変更) が譜面 JSON を編集順に並列リストへ読む
   (`NotesTime` / `LaneNum` / `NoteType` / `NotesEndTime`, 末尾に番兵 10 件)。
2. `Judge.EnsureQueue` が `noteNum` 件だけ読み、`JudgeQueue.Add` で
   レーン別・時刻順に組み替える (番兵と不正レーンは除外)。
3. 以後 `NotesManager` のリストは不変。判定済みノーツは `JudgeQueue` の側から
   消える。エフェクト位置は `SourceIndex` → `LaneNum[SourceIndex]` で引く。

## Scoring Model (1 ノーツ = 1 判定)

| イベント | スコア | コンボ |
| --- | --- | --- |
| タップ命中 (P/G/B) | `SCORE_PERFECT/GREAT/BAD` を 1 回加算 | +1 |
| HOLD 開始命中 | タップと同一 (1 回加算) | +1 |
| HOLD 終端で離す / 押し続けたまま終端超過 | 追加加点なし | 変更なし |
| HOLD 途中で離す (中断) | 先頭判定を BAD に **差し替え** (`DowngradeHoldToBad`) | 変更なし (維持) |
| Miss | 加点なし | 0 にリセット |

これで `Σ 配点 ≤ maxScore = note数 × 5` が常に成立する (旧実装の中断 +BAD は
最大 6 点になり矛盾していた)。

## Risks / Tradeoffs

- **中断時の表示が PERFECT → BAD と 2 回出る**: メッセージ UI は別 Worker の
  所有領域なので触らない。旧実装と表示回数は同じ。
- **`JudgeScoreboard` と GManager の二重管理**: 集計ロジックを EditMode テストで
  固定するために純粋クラスに切り出した。Judge が各変更後に全項目を写すので、
  他シーンからの参照方式 (GManager static) は不変。
- **同一レーン同一時刻の 2 ノーツ**: 編集順が先の 1 つだけ判定 (1 入力 1 ノーツ)。
  実譜面では発生しない想定。

## Verification Plan

| レベル | 方法 | 担当 |
| --- | --- | --- |
| 純粋ロジック | `Assets/Tests/Editor/JudgeQueueTests.cs` (15 テスト) を Unity EditMode で実行 | Supervisor |
| コンパイル | Unity 6000.5.6f1 で再ロード (本 Worker は Unity 同梱 csc + 実アセンブリ参照でコンパイル検証済み) | 本 Worker 済 |
| 既存テスト | `LongNoteTests` / `JudgeEffectPlacementTests` / `JudgeLogicTests` が green | Supervisor |
| 実機 | GameScene で HOLD 曲 (譜面に type 2 を含む) をプレーし、離鍵・押し直し・同時押しを確認 | Supervisor |
