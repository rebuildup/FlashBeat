---
description: "FlashBeat lean per-issue task list template (project-local override of the Spec Kit core tasks template)"
---

# Tasks: #216 譜面エディター操作面の再構成

**Issue**: #216 | **Branch**: `216` | **Base**: `main`
**Inputs**: [`spec.md`](spec.md), [`plan.md`](plan.md)

**Hard limit: at most 5 tasks per issue.**

## Format: `[ID] [P?] Description (path)`

- **[P]**: can run in parallel with other `[P]` tasks
- Every task names the exact file path it creates or changes

## Tasks

- [x] **T1** 常時表示のトランスポート/プレビュー/設定グループ化を実装 (`editor-toolbar.tsx`, `chart-editor.tsx`, `youtube-preview.tsx`) — 初期表示で Play/Pause・動画・8レーン・Tap/Hold が見え、Grid/Snap/Zoom/Import/Export が `Chart setup` にまとまる。
- [x] **T2** [P] 履歴 reducer と選択インスペクタを追加 (`use-chart-history.ts`, `selection-panel.tsx`) — 配置/移動/長さ変更/削除が Undo/Redo でき、選択のレーン/拍/時刻/種類/長さが表示される。
- [x] **T3** グリッドの選択強調とタッチ配置を追加 (`note-grid.tsx`) — 選択レーンと拍が強調され、タッチでも空きセルへ配置できる。
- [x] **T4** 単体テストを新 UI に合わせて更新・追加 (`*.test.*`) — 363 tests + 履歴/選択/ツールバーの新テストが pass する。
- [x] **T5** 3 viewport の実画面 E2E と証拠を追加 (`tests/e2e/editor-note-flow.spec.ts`, `tests/e2e/playwright.config.ts`, `specs/216-*/evidence/`) — 配置→削除→復元→再生位置が 1920×1080 / 2560×1440 / 2560×1664 で pass し console error 0。

## Dependencies & Execution Order

1. T1 → T2 → T3 → T4 → T5（T2/T3 は別ファイルなので並行可能）
2. T5 は dev server と seed 済み D1 が必要（`void db migrate && void db seed`）。

**Blocking dependency**: なし（#164/#165/#180/#208 は main に取込済み）。

## Verification Evidence

| Task | Check | Command / Method | Expected result |
|------|-------|-------------------|-----------------|
| T1–T4 | Unit/type | `cd web && npm test && npx tsc --noEmit` | pass / 0 errors |
| T5 | E2E | `cd web && npm run test:studio` | 3 viewports pass |
| T5 | Evidence | `specs/216-chart-editor-operation/evidence/*.png` | 配置/削除/復元/再生位置 |

## Rollback

`216` ブランチを revert / 削除する。chart-editor と検証ファイルだけに閉じる。

## Notes

- Scope discipline: 入口（`web/pages/chart.tsx`）は編集しない。必要なら PR 本文で調整依頼。
- `ChartEditor` の props 契約は変更しない。

## Verification record

- `cd web && npx tsc --noEmit`: 0 errors.
- `cd web && npm test`: 38 files / 381 tests passed (existing 363 + 18 editor tests).
- `cd web && E2E_BASE_URL=http://localhost:5184 npm run test:studio`: 6 tests passed
  (`studio-flow` 3 + `editor-note-flow` 3 viewports), console error 0
  (third-party YouTube iframe messages filtered by origin).
- Screenshots: `evidence/fhd-1920x1080-*`, `evidence/2k-2560x1440-*`,
  `evidence/mac-2560x1664-*` (open / placed / deleted / restored / playhead),
  plus `fhd-1920x1080-05-hold.png` and `fhd-1920x1080-06-chart-setup.png`.
- Before images: `evidence/before-{1920x1080,2560x1440,2560x1664}.png`
  captured from the base commit `bbfd447`.
- `vp check` on the 12 changed files: formatted, 0 errors / 0 warnings.
  `lyrics-editor.tsx` / `lyrics-editor.test.tsx` keep their pre-existing
  formatting diffs from `main` and were not reformatted.
