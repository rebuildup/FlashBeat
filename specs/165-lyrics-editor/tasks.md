---
description: "FlashBeat lean per-issue task list — 歌詞エディタ (Issue #165)"
---

# Tasks: 歌詞エディタ — Web 編集 → 並列配列 JSON → Unity 表示 (Issue #165)

**Issue**: #165 | **Branch**: `165` | **Base**: `release-0-1-14` (stacked on #173)
**Inputs**: [`spec.md`](spec.md), [`plan.md`](plan.md)

**Hard limit: at most 5 tasks per issue.**

## Format: `[ID] [P?] Description (path)`

- **[P]**: can run in parallel with other `[P]` tasks — different files, no ordering dependency
- Every task names the exact file path it creates or changes
- One task = one verifiable state change

## Tasks

- [x] **T1** 表示モデル: Unity `Assets/Game/Scripts/UI/LyricsTimeline.cs` の
  規則 (次の行の開始または +4 秒で終了を導出、重なりは後から始まった行) を
  `web/src/lib/chart/lyrics.ts` に写し、1 行モデル (`LyricRow`)、5 並列配列の
  読み出し (`readLyrics`)、保存 (`buildLyricsArrays`)、時刻入力
  (`parseTimeInput`)、検証/要約 (`rowIssues` / `lyricsIssues` /
  `summarizeLyrics`) を実装する
  — **done when**: `cd web && npx vitest run src/lib/chart/__tests__/lyrics.test.ts`
  が green (実譜面 `Who` / `GURU` / `チュートリアル` / `まにまに` の往復)
  — **実測**: 23 passed

- [x] **T2** パネル UI: `web/src/components/chart-editor/lyrics-editor.tsx` /
  `lyrics-timeline.tsx` / `lyrics-row.tsx` を作り、行一覧・追加・削除・
  上下入れ替え・時刻順ソート・開始/終了/本文/ふりがな/ローマ字の入力・
  「@ Head」で再生位置を開始に設定・再生中の行の強調を実装する
  — **done when**: `renderToStaticMarkup` と jsdom で面板が描画され、
  編集操作が `onChartChange` に届く
  — **実測**: `lyrics-editor.test.tsx` 10 passed

- [x] **T3** 入力の堅牢化: 時刻欄をテキスト入力にして、空欄/全選択消去/
  paste/日本語 IME (全角数字・全角コロン) を受理し、Enter 確定・Escape 取消・
  変換中 Enter の無視を実装する。`bad` 値は確定せずインラインエラーを残す
  — **done when**: `parseTimeInput` の単体テストと jsdom の IME 入力テストが green
  — **実測**: `lyrics.test.ts` の time input 5 件、`lyrics-editor.test.tsx` の
  full-width 入力 1 件 pass

- [x] **T4** 往復の保全: 文字だけの編集では `startTime` / `endTime` を
  1 値も変えず、時刻を編集した行だけを保存時に `end > start` へ修復し、
  5 配列を同じ行数へ揃え、未知キーを保持する。`Who` の 28 行逆転と
  `GURU` の不揃い配列を回帰として固定する
  — **done when**: 実リポジトリ譜面の往復テストが green
  — **実測**: `Who` text-only 編集で時刻配列が完全一致、`GURU` 54 行へ正規化

- [x] **T5** 譜面エディタへ組み込み: `web/src/components/chart-editor/chart-editor.tsx`
  の左カラムを「譜面グリッド (1 段目) + 歌詞面板 (2 段目)」にし、
  `LyricsEditor` に `chart` / `playheadSec` / `onChartChange` / `onSeek` を渡す。
  併せて `npm test` と `npm run build` を通す
  — **done when**: 既存の #164 テストを含む `npm test` が全 pass、
  `npm run build` が成功
  — **実測**: `npm test` 25 files / 217 tests + node --test 47 tests pass、
  `npm run build` `✓ built` (詳細は PR 本文)
