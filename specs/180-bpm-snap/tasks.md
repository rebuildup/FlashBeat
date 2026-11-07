---
description: "FlashBeat lean per-issue task list template (project-local override of the Spec Kit core tasks template)"
---

# Tasks: Webエディターのノート配置をBPMグリッドにスナップ (1/1〜1/16)

**Issue**: #180 | **Branch**: `180` | **Base**: `release-0-1-14` @ `7eb8e0a`
**Inputs**: [`spec.md`](spec.md), [`plan.md`](plan.md)

**Hard limit: at most 5 tasks per issue.** If the work does not fit in 5 tasks,
the issue is too big — split it and open a follow-up Issue.

## Format: `[ID] [P?] Description (path)`

- **[P]**: can run in parallel with other `[P]` tasks — different files, no ordering dependency
- Every task names the exact file path it creates or changes
- One task = one verifiable state change

## Tasks

- [ ] **T1** `web/src/lib/chart/snap.ts` を新規作成する。粒度列 `off / 1 / 2 / 4 / 8 / 16` と純関数 `beatDuration(bpm)` / `snapDuration(bpm, division)` / `snapTime(time, bpm, division)` / `snapTimeExclusive(time, bpm, division)` / `snapBeat(beat, division)` / `snapDivisionLabel(division)` を export する。拍インデックス基準の half-up 丸め (0.5 超は上へ)、BPM が非有限や 0 以下なら snap を無効化する。完了条件: 副作用なし・I/O なし・React 非依存 (lint/型チェックで純関数として通る)。
- [ ] **T2** `web/src/lib/chart/__tests__/snap.test.ts` を新規作成する。beatDuration / 各 division の snapTime (0・境界・中間・負・境界) / BPM 変更への追従 / 4/4 の拍跨ぎ / off の無変更 を網羅する。完了条件: `npx vitest run src/lib/chart/__tests__/snap.test.ts` が全件 pass。
- [ ] **T3** `web/src/components/chart-editor/note-grid.tsx` を snap 対応にする。`snapDivision` prop を受け、配置 (`onAddNote`)・ドラッグ移動 (`onMoveNote`)・ロングノーツ端点 (`onResizeHold`) が snap 済み拍を通知する。`chart.BPM` を snap 計算へ渡し (メモ化)、スナップ後の表示 (グリッド線・ノート位置) が実データとずれないようにする。完了条件: 粒度 off で既存挙動が同じ、粒度 1/8 で 1/8 拍へ吸着する。
- [ ] **T4** `web/src/components/chart-editor/editor-toolbar.tsx` に粒度選択 Select (Ark UI Select、Panda token、既定 1/4) を追加し、`snapDivision` / `onSnapDivisionChange` prop を追加する。`web/src/components/chart-editor/editor-toolbar.test.tsx` に粒度変更が反映されるテストを追加する。完了条件: `npx vitest run src/components/chart-editor/editor-toolbar.test.tsx` が既存 + 新規で pass。
- [ ] **T5** `web/src/components/chart-editor/note-grid.test.tsx` を新規作成する (jsdom)。粒度変更が配置結果・ドラッグ移動・ロングノーツ端点へ反映されることを確かめる。`cd web && npx tsc --noEmit && npm test && npm run build` を実行する。完了条件: 3 コマンドがすべて緑、既存公開データ (who 曲) の検証結果が変わらない。

## Dependencies & Execution Order

1. [T1] → [T2] → [T3] → [T4] → [T5]
2. [T4] は [T3] と並列可能だが、同じ props 契約に依存するため [T3] の形を先に決める

**Blocking dependency**: なし (単独 Issue)。Worker E (Issue #165) は `chart-editor.tsx` を所有しているため、本 Issue は同ファイルを触らない。

## Verification Evidence

| Task | Check | Command / Method | Expected result |
|------|-------|-------------------|-----------------|
| T1 | 型チェック + lint | `cd web && npx tsc --noEmit` | エラー 0 |
| T2 | 単体テスト | `cd web && npx vitest run src/lib/chart/__tests__/snap.test.ts` | 全件 pass |
| T3 | グリッドのテスト | `cd web && npx vitest run src/components/chart-editor/note-grid.test.tsx` | 全件 pass |
| T4 | ツールバーのテスト | `cd web && npx vitest run src/components/chart-editor/editor-toolbar.test.tsx` | 既存 + 新規が pass |
| T5 | 全テスト + ビルド | `cd web && npm test && npm run build` | 全緑 + ビルド成功 |

## Rollback

Draft PR をクローズし branch `180` を削除する。Owned Paths が `chart-editor.tsx` を含まないため他 Issue の作業へ影響しない。

## Notes

- Scope discipline: implement only the tasks above; new ideas become follow-up Issues.
- The Unity Editor / scene / prefab mutation is serialized: only the issue that
  owns the scene may run the Editor. 本 Issue は Unity に触れない。
