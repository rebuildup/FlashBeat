---
description: "FlashBeat lean per-issue specification — 歌詞エディタ (Issue #165)"
---

# Spec: 歌詞 (本文・読み・ローマ字・開始終了時刻) の Web 編集 → Unity 表示 (Issue #165)

**Issue**: #165 | **Branch**: `165` | **PR base**: `release-0-1-14` | **Date**: 2026-11-08 | **Status**: Draft

**Input**: `[Chart/P0] リリックの本文・読み・開始終了時刻をWebで編集しUnity表示に反映`

> Lean rules (enforced by `docs/agent/spec-kit-workflow.md`):
> one Issue = one branch = one worktree = one Draft PR; ≤5 tasks; no retrospective
> re-specification of the legacy game; no second authority for project governance.

## Objective

譜面エディタで `chart.lyrics` の 1 行ごとに **本文 (`mondai`)・ふりがな
(`furigana`)・ローマ字 (`romaji`)・開始時刻 (`startTime`)・終了時刻
(`endTime`)** を確認・編集・追加・削除・並べ替えできるようにし、その結果を
既存の並列配列 JSON として無損失に保存する。編集した歌詞は Unity
`LyricsTimeline` が読むのと同じ表示区間でプレビューされ、既存曲 (特に
`Who` の 28 行 `endTime < startTime`) を壊さない。

## Owned Paths

- `web/src/lib/chart/lyrics.ts` (新規) — 歌詞の表示モデル・編集・保存
  (並列配列への直列化)・入力パース・検証・要約
- `web/src/lib/chart/__tests__/lyrics.test.ts` (新規) — 実リポジトリ譜面
  (`Who` / `GURU` / `チュートリアル` / `まにまに`) に対する読み書き往復
- `web/src/components/chart-editor/lyrics-editor.tsx` (新規) — 歌詞面板
  (ヘッダ・課題一覧・行一覧・再生位置同期)
- `web/src/components/chart-editor/lyrics-row.tsx` (新規) — 1 行の時刻・本文・
  ふりがな・ローマ字入力、行操作
- `web/src/components/chart-editor/lyrics-timeline.tsx` (新規) — 行リスト
  (スクロール領域と空状態)
- `web/src/components/chart-editor/lyrics-editor.test.tsx` (新規) — DOM
  (jsdom) での編集・追加・削除・シーク・IME 入力のテスト
- `web/src/components/chart-editor/chart-editor.tsx` — **歌詞面板の組み込み
  部分のみ** (左カラムの行を 2 段にし、`LyricsEditor` を配置、シーク転送)
- `specs/165-lyrics-editor/{spec,plan,tasks}.md`

## Exclusions (explicitly out of scope)

- `web/src/components/chart-editor/note-grid.tsx`, `editor-toolbar.tsx`,
  `chart-editor.test.ts`, `validation-panel.tsx` — #164 (D) の所有。
  `chart-editor.tsx` も #164 の上に stack し、歌詞面板の mount 以外は触らない
- `web/src/lib/chart/{notes,long-note,validate,schema,types,import,export}.ts`,
  `contracts/schemas/chart.v1.schema.json` — #164 の所有
- `web/src/app/**`, `web/src/components/ui/**` — 他 Worker の排他所有
- `Assets/**` — Unity 側の歌詞ランタイム (`LyricsTimeline.cs`) は既に存在し、
  本 Issue は変更しない。Unity Editor 起動・実機検証は Supervisor の直列作業
- 歌詞の自動同期 (音声認識/強制アラインメント)、複数行の一括シフト、
  Undo/Redo — 本 Issue の受入外
- 本番 Cloudflare デプロイ

## User Scenarios & Testing *(mandatory)*

### Scenario 1 — 既存曲の歌詞を壊さずに読む・直す (Priority: P1)

**Given** `chart.lyrics` を持つ既存譜面 (例: `Who` は 28 行すべて
`endTime < startTime`、`GURU` は 5 配列の長さが揃っていない) を開いた状態
**When** 1 行の本文・ふりがな・ローマ字だけを編集して保存する
**Then** 編集した行の文字だけが変わり、**全行の `startTime` / `endTime` は
バイト同一**で残る。時刻を編集した行だけが保存時に修復される
(終了が開始以前なら「次の行の開始」または「開始 + 4 秒」)。
5 配列は保存後すべて同じ行数になる。

**Independent Test**:
`cd web && npx vitest run src/lib/chart/__tests__/lyrics.test.ts`
(`Who` の 28 行逆転が text-only 編集で保存されること、`GURU` の不揃い配列が
54 行へ正規化されることを検証)

### Scenario 2 — 新規行を作り、動画位置から開始/終了を決めて保存する (Priority: P2)

**Given** YouTube プレビュー付きの譜面エディタ
**When** 「Add line」で行を追加し、「@ Head」で再生位置を開始時刻に設定し、
終了時刻を入力して保存する
**Then** 行が時系列に追加され、`startTime` / `endTime` / `furigana` /
`mondai` / `romaji` の 5 配列すべてに同じ行数の値が入り、`onChartChange`
経由で譜面が更新される。「Seek」でその行の開始位置へプレビューが移動する。

**Independent Test**:
`cd web && npx vitest run src/components/chart-editor/lyrics-editor.test.tsx`

### Edge Cases

- 時刻欄を空にした場合は「未設定」として扱い、保存時に前の行の終了 (または 0)
  と次の行の開始から補完する。`abc` や `1:75` は確定せずエラー表示を残す。
- 全角数字・全角コロン (`３．５`, `１：０３．２５`) を IME 入力として受理する。
- `endTime` が `startTime` 以前の行は「legacy end」と表示し、Unity と同じ
  導出区間でプレビューする。時刻を編集するまで生値は書き換えない。
- 開始時刻が無い行がある場合は保存をブロックするエラーとして面板に出す。
- 行が重なる場合は警告を出す (Unity は「後から始まった行」を表示する)。

## Requirements

- **R1**: `chart.lyrics` の 5 並列配列を 1 行モデルへ読み出し、本文・ふりがな・
  ローマ字・開始・終了を 1 画面で編集できること。
- **R2**: 行の追加・削除・本文の上下入れ替え・時刻順ソートができること。
- **R3**: 未編集の歌詞ブロックはオブジェクト同一のまま返し、文字だけの編集では
  時刻配列を 1 値も変えないこと。
- **R4**: 時刻を編集した行は保存時に `end > start` へ修復し、5 配列を同じ行数に
  揃えること。未知キーは保持すること。
- **R5**: 時刻入力は秒 (`12.5`)・`m:ss`・`h:mm:ss`・全角数字/コロンを受け付け、
  空欄・不正値・IME 変換中の Enter を安全に扱うこと。
- **R6**: 再生位置に一致する行を強調し、行クリックでプレビューをシークできること。

## Dependencies & Gate

- **Depends on**: #164 (branch `164`, PR #173) — `chart-editor.tsx` の HOLD 実装
  (タップ/HOLD 切替・`kindSwitch`・`holdCount`) を含む。本ブランチは
  `origin/164` の上に rebase して stack し、`chart-editor.tsx` は歌詞面板の
  mount と左カラム 2 段化だけを追加する
- **Blocks**: なし
- **Gate**: `npm test` / `npm run build` が green。Owner が実機 (FHD 1920×1080,
  2K 2560×1440, Mac 2560×1664) で「既存歌詞の確認 → 編集 → 保存 → Unity 表示
  タイミング反映」を操作できること。画面確認は Supervisor / Owner の直列作業

## Verification Evidence

| # | Check | Command / Method | Expected result |
|---|-------|-------------------|-----------------|
| V1 | 歌詞モデルの単体テスト | `cd web && npx vitest run src/lib/chart/__tests__/lyrics.test.ts` | 23 passed |
| V2 | 歌詞面板の DOM テスト | `cd web && npx vitest run src/components/chart-editor/lyrics-editor.test.tsx` | 10 passed |
| V3 | Web 全体のテスト | `cd web && npm test` | 全 suite passed (node --test + vitest) |
| V4 | プロダクションビルド | `cd web && npm run build` | `✓ built` |
| V5 | 実機操作 (Owner) | エディタで `Who` を開き 1 行編集 → 保存 → Unity 表示 | **未実施** (Supervisor 直列) |

## Rollback

`165` ブランチの merge commit を revert するか、ブランチを削除する。歌詞面板は
`lyrics-*` の新規ファイルと `chart-editor.tsx` の mount だけに閉じているため、
revert しても #164 の HOLD 機能 (`note-grid.tsx` / `long-note.ts` など) には
影響しない。

## Success Criteria

- **SC1**: 既存 4 曲の歌詞を読み、`Who` の逆転時刻を保ったまま文字だけ編集して
  往復できる (V1)。
- **SC2**: 面板上で行の追加・削除・時刻編集・シークが動作する (V2)。
- **SC3**: `npm test` と `npm run build` が green で、#164 の既存テストを含む
  全 suite が通る (V3, V4)。
