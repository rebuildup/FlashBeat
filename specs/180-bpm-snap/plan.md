---
description: "FlashBeat lean per-issue implementation plan template (project-local override of the Spec Kit core plan template)"
---

# Plan: Webエディターのノート配置をBPMグリッドにスナップ (1/1〜1/16)

**Issue**: #180 | **Branch**: `180` | **Base**: `release-0-1-14` @ `7eb8e0a` | **Date**: 2026-11-08
**Spec**: [`specs/180-bpm-snap/spec.md`](spec.md)

## Summary

`web/src/lib/chart/snap.ts` に BPM 基準スナップの純関数を新設し、既存の `timing.ts` (`60 / BPM * beat`) と時間軸を共有する。粒度は `off / 1 / 2 / 4 / 8 / 16` の 6 種で、1 拍 = `60 / BPM` 秒を基準に「拍インデックス → 行インデックス → 行インデックス / 拍数」という順序で計算する (拍インデックス基準なので小節/拍境界をまたいでも連続的に正しい)。`note-grid.tsx` は配置・ドラッグ・ロングノーツ端点の拍をこの関数群で snap してから呼び出し側へ渡し、`editor-toolbar.tsx` に Ark UI Select の粒度ピッカーを足す。BPM は `chart.BPM` から導出して snap 計算へ渡すので、BPM を変えると格子が即追従する。

## Owned Paths

```text
web/src/lib/chart/snap.ts                              (new)
web/src/lib/chart/__tests__/snap.test.ts               (new)
web/src/components/chart-editor/note-grid.tsx          (modified)
web/src/components/chart-editor/note-grid.test.tsx     (new)
web/src/components/chart-editor/editor-toolbar.tsx     (modified)
web/src/components/chart-editor/editor-toolbar.test.tsx (modified)
specs/180-bpm-snap/{spec.md,plan.md,tasks.md}
```

**Structure Decision**: `chart-editor.tsx` は Worker E (歌詞パネル) と所有パスが重なるため触らない。スナップの計算は純関数として `lib/chart` に置き、UI 側は `note-grid.tsx` (配置) と `editor-toolbar.tsx` (粒度ピッカー) だけで完結させる。

## Dependencies & Gate

- **Depends on**: なし (単独)。base は `origin/release-0-1-14` @ `7eb8e0a`
- **Blocks**: なし
- **Gate**: `cd web && npx tsc --noEmit && npm test` が緑、`npm run build` が成功、Draft PR 作成済み

## Exclusions

- `chart-editor.tsx` の変更 (Worker E と競合するため)
- 譜面 JSON のフォーマット変更 / 既存公開データ (D1) の更新
- スナップ粒度の永続化 (sessionStorage / localStorage)
- ライブ入力 (入力中に音を出す機能) 类似的scope

## Constitution Check

*GATE: must pass before implementation.*

- Authority: [`constitution/CONSTITUTION.md`](../../../constitution/CONSTITUTION.md) と [`organization/profiles/release-driven-solo.md`](../../../organization/profiles/release-driven-solo.md) は変更しない (最上位 contract の二重化を作らない)
- Mutable Ownership Safety: `chart-editor.tsx` を所有パスから外すことで Worker E (Issue #165) と同一ファイルへの同時変更を回避する。既存の `web/src/lib/chart/**` の他ファイルも変更しない
- Evidence Integrity: テスト結果・ビルド結果は候補 SHA に紐づけて PR body に貼る
- Canonical Consistency: 既存の `timing.ts` (`60 / BPM * beat`) を唯一の時間軸の定義として維持し、スナップはそれ之上の一つの式として実装する (二番目の時間軸を作らない)
- Violations: なし

## Verification Plan

| # | Check | Command / Method | Evidence to attach |
|---|-------|-------------------|--------------------|
| V1 | スナップ純関数 | `cd web && npx vitest run src/lib/chart/__tests__/snap.test.ts` | テスト出力 |
| V2 | グリッド (配置/ドラッグ/端点) | `cd web && npx vitest run src/components/chart-editor/note-grid.test.tsx` | テスト出力 |
| V3 | ツールバー (粒度 Select) | `cd web && npx vitest run src/components/chart-editor/editor-toolbar.test.tsx` | テスト出力 |
| V4 | 型チェック | `cd web && npx tsc --noEmit` | 出力 |
| V5 | 全テスト | `cd web && npm test` | 出力 |
| V6 | ビルド | `cd web && npm run build` | 出力 |

## Rollback

`git revert` ではなく Draft PR のクローズ + branch 削除で取り消す。Owned Paths が他 Issue と分離されているため他ブランチへ影響しない。
