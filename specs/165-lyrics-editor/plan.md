---
description: "FlashBeat lean per-issue implementation plan — 歌詞エディタ (Issue #165)"
---

# Plan: 歌詞エディタ — Web で本文・読み・時刻を編集し Unity 表示に反映 (Issue #165)

**Issue**: #165 | **Branch**: `165` | **Base**: `release-0-1-14` (stacked on #173) | **Date**: 2026-11-08
**Spec**: [`specs/165-lyrics-editor/spec.md`](spec.md)

## Summary

`chart.lyrics` の 5 並列配列を「1 行 = 1 オブジェクト」の編集ドキュメントへ読み、
編集後に元の並列配列へ直列化する純ロジック (`web/src/lib/chart/lyrics.ts`) を作る。
UI は `lyrics-editor` / `lyrics-row` / `lyrics-timeline` の 3 コンポーネントに閉じ込め、
`chart-editor.tsx` は左カラムを「譜面グリッド + 歌詞面板」の 2 段にして
`LyricsEditor` を mount するだけにする。既存の `Assets/Game/Scripts/UI/LyricsTimeline.cs`
の表示規則 (次の行の開始または +4 秒で終了を導出、重なりは後から始まった行) を
そのまま写し、Web のプレビューと Unity の実表示を一致させる。

## Owned Paths

```text
web/src/lib/chart/lyrics.ts
web/src/lib/chart/__tests__/lyrics.test.ts
web/src/components/chart-editor/lyrics-editor.tsx
web/src/components/chart-editor/lyrics-row.tsx
web/src/components/chart-editor/lyrics-timeline.tsx
web/src/components/chart-editor/lyrics-editor.test.tsx
web/src/components/chart-editor/chart-editor.tsx   (lyrics panel mount only)
specs/165-lyrics-editor/{spec,plan,tasks}.md
```

**Structure Decision**: 歌詞専用の状態・変換・UI を `lyrics-*` に閉じ込め、
`chart-editor.tsx` の変更を「面板の配置」だけに限定することで、#164 の
`note-grid` / HOLD 実装と同一ファイルを編集しない。`chart.lyrics` の
構造 (`types.ts`) や契約スキーマは既に #164 / 既存契約が持つため触らない。

## Dependencies & Gate

- **Depends on**: #164 (branch `164`, PR #173) — `chart-editor.tsx` の HOLD UI。
  `origin/164` へ rebase して stack し、`chart-editor.tsx` の歌詞面板 mount のみを
  追加する。未 merge でも本ブランチ単体でビルド・テスト可能
- **Blocks**: なし
- **Gate**: `npm test` / `npm run build` green。Owner の実機操作 (保存 → Unity
  表示反映、FHD / 2K / Mac の画面確認) は Supervisor が直列で行う

## Exclusions

- `note-grid.tsx`, `editor-toolbar.tsx`, `validation-panel.tsx`,
  `chart-editor.test.ts` — #164 の所有
- `web/src/lib/chart/{notes,long-note,validate,schema,types,import,export}.ts`,
  `contracts/schemas/**` — 既存 / #164 の所有
- `Assets/**` の変更 (Unity 歌詞ランタイムは既存のまま利用)
- 自動歌詞同期、複数行一括シフト、Undo/Redo、本番デプロイ

## Constitution Check

*GATE: must pass before implementation.*

- Authority: [`constitution/CONSTITUTION.md`](../../../constitution/CONSTITUTION.md) と
  [`organization/profiles/release-driven-solo.md`](../../../organization/profiles/release-driven-solo.md)
  は本 Issue で変更しない。
- Mutable Ownership Safety: 歌詞処理は `lyrics-*` の新規ファイルに閉じ、
  `chart-editor.tsx` は mount のみ。#164 のファイル (`note-grid.tsx` ほか) は
  編集しない。
- Evidence Integrity: 単体/DOM テストの結果を候補 SHA に紐づけ、PR 本文へ記録。
  実機確認は Owner 実施後に追記する。
- Violations: なし

## Verification Plan

| # | Check | Command / Method | Evidence to attach |
|---|-------|-------------------|--------------------|
| V1 | 歌詞モデル | `cd web && npx vitest run src/lib/chart/__tests__/lyrics.test.ts` | 23 passed |
| V2 | 歌詞面板 (jsdom) | `cd web && npx vitest run src/components/chart-editor/lyrics-editor.test.tsx` | 10 passed |
| V3 | 全体テスト | `cd web && npm test` | node --test + vitest 全 pass |
| V4 | ビルド | `cd web && npm run build` | `✓ built` |
| V5 | 実機 (Owner) | `Who` を編集 → 保存 → Unity 表示 | 未実施 (Supervisor) |

## Rollback

`165` の merge commit を revert、またはブランチ削除。`lyrics-*` と
`chart-editor.tsx` の mount だけの差分なので、#164 の HOLD 実装には触れない。
