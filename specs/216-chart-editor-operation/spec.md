---
description: "FlashBeat lean per-issue specification template (project-local override of the Spec Kit core spec template)"
---

# Spec: #216 譜面エディター操作面の再構成 (P0/Exhibit UX)

**Issue**: #216 | **Branch**: `216` | **PR base**: `main` | **Date**: 2026-11-08 | **Status**: Draft

**Input**: `BRIEF-216.md`, `gh issue view 216`, `gh issue view 61`

> Lean rules (enforced by `docs/agent/spec-kit-workflow.md`):
> one Issue = one branch = one worktree = one Draft PR; ≤5 tasks per issue.

## Objective

初見の展示来場者が、説明なしに「動画を見ながら8レーンへノーツを置き、選び直し、
長さを変え、消し、元に戻し、再生位置を確認する」までをマウスだけで完走できる
編集画面にする。プレビュー・再生・ノーツ作成を最初から見える主操作へ昇格し、
Grid/Snap/Zoom/Import/Export/BPM は意味でまとめた詳細へ整理する。既存機能と
JSON の無損失入出力、Validation 表示は維持する。

## Owned Paths

- `web/src/components/chart-editor/chart-editor.tsx` — 画面構成、履歴、再生位置、選択状態
- `web/src/components/chart-editor/editor-toolbar.tsx` — トランスポート、Undo/Redo、詳細設定のグループ化
- `web/src/components/chart-editor/note-grid.tsx` — 選択レーンの強調、タッチ配置
- `web/src/components/chart-editor/selection-panel.tsx` — 新規。選択ノーツの読み取りとマウス操作
- `web/src/components/chart-editor/use-chart-history.ts` — 新規。Undo/Redo の reducer
- `web/src/components/chart-editor/youtube-preview.tsx` — 外部トランスポートに合わせたコントロール表示切替
- `web/tests/e2e/editor-note-flow.spec.ts`、`web/tests/e2e/playwright.config.ts` — 実画面 E2E
- `specs/216-chart-editor-operation/**` — 仕様と検証証拠

## Exclusions (explicitly out of scope)

- `web/src/app/**`、`web/pages/**`（`chart.tsx` を含む）。入口・Shell・ページヘッダは #215/#74 の所有。
- `web/src/components/new-song/**`、`web/src/components/song-publisher/**`、API/schema/publish 認証。
- `ChartEditor` の props 契約（`initialChart` / `initialYouTubeId` / `onChartChange` /
  `onValidationChange` / `initialIssues`）の変更。
- Unity 側の判定・譜面契約の変更。Web の編集画面だけを扱う。

## User Scenarios & Testing *(mandatory)*

### Scenario 1 — 初見でノーツを置いて消して戻す (Priority: P1)

**Given** 公開譜面 `Who` を `/chart?songId=who&version=1` で開いた状態で、
**When** 8レーンの空きセルをクリックし、右の「Selected note」でレーン/拍/時刻を確認し、
Delete を押して Undo を押すと、
**Then** ノーツ数が `139 → 140 → 139 → 140` と戻り、選択状態も復元される。

**Independent Test**: `web/` で `npm run test:studio`（`editor-note-flow.spec.ts`）を
1920×1080 / 2560×1440 / 2560×1664 で実行。

### Scenario 2 — マウスだけで長さと位置を変える (Priority: P2)

**Given** Hold を置いた直後、**When** 右パネルの `Make Hold` / `+` / `−` / 矢印ボタンを
操作すると、**Then** 選択ノーツの種類・長さ・レーン・拍が表示に反映される。

**Independent Test**: `selection-panel.test.tsx` と `use-chart-history.test.ts`。

### Scenario 3 — 動画と再生位置を同時に見る (Priority: P1)

**Given** エディタを開いた状態で、**When** 上部の Play と再生位置スライダーを操作すると、
**Then** 動画・再生位置の秒数/拍・グリッドの青い再生線が同じ位置を示す。

**Independent Test**: `editor-note-flow.spec.ts` でスライダーを 5.00s へ動かし、
`transport-time` と `playhead-readout` が一致することを確認。

### Edge Cases

- ノーツが 0 件の新規譜面: グリッド上に「Tap a lane to place the first note」を重ね、
  クリックを妨げない。
- 再生できない/YouTube に接続できない: エラーはプレビュー内に表示し、編集と
  再生位置の移動は継続できる。
- 最後のノーツを消した場合も Undo で復元できる。
- Import 失敗時は譜面を null にし、既存の Validation 表示を維持する。
- 狭い viewport ではプレビュー・グリッド・インスペクタを縦に積み、ドロワーに隠さない。

## Requirements

- **R1**: プレビュー、Play/Pause、Restart、8レーン、Tap/Hold 作成を初期表示に置く。
- **R2**: 選択ノーツのレーン・拍・時刻・種類・Hold 長を常時表示し、削除・種類変更・
  長さ変更・ナッジ・Undo/Redo をマウスで完結できる。
- **R3**: Grid（Resolution）/Snap/Zoom/Import/Export/BPM を「Chart setup」へ意味で
  グループ化し、全コントロールを既存機能へ接続する。機能を削除しない。
- **R4**: 再生位置を秒/拍で常時表示し、スライダーと動画の seek を同期する。
- **R5**: `importChartJson` / `exportChartJson` の無損失往復と Validation 表示を維持する。
- **R6**: 既存キーボード操作（Delete/Escape/矢印/Shift+矢印）を維持し、Ctrl/⌘+Z の
  Undo を追加する。ヘルプは補助に留める。
- **R7**: `design/flashbeat-ui.pen` v5 の k0〜k4 と #3FA9F5 アクセント、既存 Panda/Park UI
  のみを使う。新フレームワークを追加しない。

## Dependencies & Gate

- **Depends on**: #164（Hold）、#165（Lyrics）、#180（Snap）、#208（Lenient load）。
  いずれも `origin/main` に取込済み。
- **Blocks**: #215 の入口導線は本画面の props 契約に依存する。契約は変更しない。
- **Gate**: `npm test` 363+ が pass、`npm run test:studio` が 3 viewport で pass、
  console error 0、published chart 読み込みの E2E が pass。

## Verification Evidence

| # | Check | Command / Method | Expected result |
|---|-------|-------------------|-----------------|
| V1 | Unit / integration | `cd web && npm test` | 既存 363 tests + 追加分が pass |
| V2 | Type check | `cd web && npx tsc --noEmit` | 0 errors |
| V3 | Studio E2E | `cd web && npm run test:studio` | 3 viewports pass |
| V4 | Screenshots | Playwright `editor-note-flow.spec.ts` | 配置/削除/復元/再生位置の PNG |
| V5 | Console | Playwright page listeners | error 0 |
| V6 | Published chart | `studio-flow.spec.ts` + `chart-identity` | `Who · v1 · 139 notes` |

## Rollback

`216` ブランチの merge commit を revert するか、ブランチを削除する。変更は
`web/src/components/chart-editor/**`、E2E spec、`specs/216-*` に閉じており、
`web/pages/**` と API/schema に触れていないため他 Issue へ波及しない。

## Success Criteria

- **SC1**: 初見操作（配置→選択→削除→Undo→再生位置確認）がマウスだけで完了する。
- **SC2**: 既存 363 tests と追加テストがすべて pass し、console error が 0。
- **SC3**: 公開譜面の読み込みと JSON 無損失往復が E2E/unit で維持される。
