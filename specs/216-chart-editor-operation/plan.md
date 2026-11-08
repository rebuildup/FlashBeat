---
description: "FlashBeat lean per-issue implementation plan template (project-local override of the Spec Kit core plan template)"
---

# Plan: #216 譜面エディター操作面の再構成

**Issue**: #216 | **Branch**: `216` | **Base**: `main` | **Date**: 2026-11-08
**Spec**: [`specs/216-chart-editor-operation/spec.md`](spec.md)

## Summary

`chart-editor.tsx` を「トランスポート + プレビュー + 8レーン + 選択インスペクタ」の
3ゾーン構成へ組み替える。プレビューと検証は常時表示のまま、Grid/Snap/Zoom/
Import/Export/BPM は `EditorToolbar` の `Chart setup` にまとめる。編集操作は
reducer 化した履歴 (`use-chart-history`) を通し、選択ノーツの読み取りと
マウス操作を新規 `SelectionPanel` に集約する。既存の Konva グリッド、
Panda/Park UI、`lib/chart` の純関数はそのまま再利用する。

## Owned Paths

```text
web/src/components/chart-editor/chart-editor.tsx
web/src/components/chart-editor/editor-toolbar.tsx
web/src/components/chart-editor/note-grid.tsx
web/src/components/chart-editor/selection-panel.tsx        (new)
web/src/components/chart-editor/use-chart-history.ts       (new)
web/src/components/chart-editor/youtube-preview.tsx
web/src/components/chart-editor/*.test.*                  (updated/new)
web/tests/e2e/editor-note-flow.spec.ts                     (new)
web/tests/e2e/playwright.config.ts
specs/216-chart-editor-operation/**
```

**Structure Decision**: 既存の chart-editor コンポーネント群の中で閉じる。入口・
ページ・Shell（#215/#74）へは props 契約だけで接続し、ファイルを跨がない。

## Dependencies & Gate

- **Depends on**: #164 / #165 / #180 / #208（すべて `origin/main` に取込済み）
- **Blocks**: #215（入口）。`ChartEditor` の props 契約は変更しない。
- **Gate**: `npm test` + `npx tsc --noEmit` + `npm run test:studio` の証拠を PR に添付。

## Exclusions

- `web/pages/chart.tsx`、`web/src/app/**` の変更（必要なら PR 本文で調整依頼）。
- API/schema/publish 認証、Unity 側の判定・契約。
- 新しい UI フレームワーク、Tailwind/shadcn。

## Constitution Check

*GATE: must pass before implementation.*

- Authority: `constitution/CONSTITUTION.md` と
  `organization/profiles/release-driven-solo.md` を変更しない。
- Mutable Ownership Safety: `web/pages/**` と `web/src/app/**` は #215 の所有として
  触らない。`ChartEditor` の props 契約を固定して入口側と調整不要にする。
- Evidence Integrity: 各検証は候補 SHA で実行し、コマンドと結果、実画面 PNG を
  `specs/216-chart-editor-operation/evidence/` に残す。
- Violations: なし。

## Verification Plan

| # | Check | Command / Method | Evidence to attach |
|---|-------|-------------------|--------------------|
| V1 | Unit tests | `cd web && npm test` | 363+ pass のログ |
| V2 | Type check | `cd web && npx tsc --noEmit` | 0 errors |
| V3 | Studio E2E | `cd web && npm run test:studio` | Playwright ログ |
| V4 | Pixel evidence | `editor-note-flow.spec.ts` | 3 viewport × 配置/削除/復元/再生位置 |
| V5 | Console | Playwright listeners | error 0 |
| V6 | Published chart | `studio-flow.spec.ts` | `Who · v1 · 139 notes` |

## Rollback

`216` ブランチを revert / 削除する。変更は chart-editor と E2E/spec に閉じるため、
#215 や API の状態に影響しない。
