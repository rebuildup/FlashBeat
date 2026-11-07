---
description: "FlashBeat lean per-issue specification template (project-local override of the Spec Kit core spec template)"
---

# Spec: Web 入力・操作系の実操作対応 (Ctrl+A 全消去 / IME / 数値途中入力 / Ark Select)

**Issue**: #163 | **Branch**: `163` | **PR base**: `release-0-1-11` | **Date**: 2026-11-07 | **Status**: Draft

**Input**: Worker C — Web 入力・操作系 ([Web/P0] Ctrl+A全消去・IME・入力途中の数値・Ark Selectを実際に操作可能にする, 親 #61)

> Lean rules (enforced by `docs/agent/spec-kit-workflow.md`):
> one Issue = one branch = one worktree = one Draft PR; ≤5 tasks; no retrospective
> re-specification of the legacy game; no second authority for project governance.

## Objective

Web Studio の全入力フィールド (Catalog 検索、Chart 名前/BPM、Publish の Song ID/title/artist/YouTube/duration) と Ark Select (Catalog Sort/Level、Chart Grid/View、Publish Level) が実操作で成立する: Ctrl/⌘+A → Delete で消して再入力でき、IME 変換中の値が確定前に飛ばされず、入力途中の空文字/先頭0/負号が確定値を壊さず、Select がクリック・Enter・Escape・矢印キーで開閉/選択できる。

## Owned Paths

- `web/src/components/ui/select.tsx` — Control をインタラクティブにする (Trigger を内包)
- `web/src/components/ui/input.tsx` — draft 保持の入力プリミティブ (DraftInput)
- `web/src/components/chart-editor/editor-toolbar.tsx` — BPM 入力を DraftInput 化
- `web/src/components/song-publisher/song-metadata-form.tsx` — Duration を DraftInput 化
- `web/src/app/topbar.tsx` — 検索・フィルタ (Select 呼出しは primitive 修正で成立)
- `web/tests/**`, `web/src/**/*.test.ts(x)` — 新規/更新テスト

## Exclusions (explicitly out of scope)

- `web/src/app/{app-shell,sidebar}.tsx`, `web/pages/**` (Worker F)
- `web/src/components/chart-editor/{chart-editor,note-grid,youtube-preview,validation-panel}.tsx` (Worker D / #164)
- `web/src/lib/chart/**` (Worker D / Worker E)
- `web/src/components/chart-editor/lyrics-*` 新規 (Worker E / #165)
- `web/src/components/new-song/**` — 所有外。共通 primitive の恩恵は受けるが直接改编しない
- Unity 側・D1・Cloudflare デプロイ・本番DB には触れない

## User Scenarios & Testing *(mandatory)*

### Scenario 1 — 数値入力の draft 保持 (Priority: P1)

**Given** Chart editor で BPM=128 の譜面を開いている, **When** BPM 欄で Ctrl/⌘+A → Delete → 「96」と入力して blur, **Then** フィールドは途中で 128 に戻らず「96」を表示し、譜面の BPM が 96 に更新される。Delete 直後 (空文字) や「-」入力中は確定値が壊れず、blur で直前の確定値に戻る。

**Independent Test**: `cd web && npx vitest run src/components/chart-editor/editor-toolbar.test.tsx` (jsdom 実操作: input event → blur)

### Scenario 2 — Ark Select の実操作 (Priority: P1)

**Given** Publish 画面の Level セレクト (および Catalog Sort/Level、Chart Grid/View), **When** クリック (または Enter/↓) で開いて矢印キーで移動し Enter で選択/Escape で閉じる, **Then** リストが開き、選択した実値がフォーム/フィルタに反映される。Trigger が存在しないことで何も起きない状態は再発しない。

**Independent Test**: `cd web && npx vitest run src/components/ui/select.test.tsx src/app/topbar.test.tsx src/components/song-publisher/song-metadata-form.test.tsx` + Playwright ブラウザE2E (`node tests/e2e/browser/run.ts`)

### Scenario 3 — IME 日本語入力 (Priority: P1)

**Given** Publish の Title で日本語 IME が有効, **When** 変換確定前のローマ字/かなの途中値を入力してから確定する, **Then** 途中値が確定前に確定値として飛ばされず、確定後の文字列がそのまま保存対象になる。数値欄に全角数字を入れても blur で半角数値に正規化される。

**Independent Test**: `npx vitest run src/components/ui/input.test.tsx` (compositionstart/end) + Playwright CDP `Input.imeSetComposition` (chromium)

### Edge Cases

- 空文字で blur → 確定値を表示に戻す (BPM 0 など無効値で譜面を壊さない)
- 先頭0 / 負号途中 / 小数途中 → draft として表示され、blur で正規化または revert
- ペースト → 通常 typing と同じ経路で draft/commit
- ポーリング (catalog 再取得) が入力を妨害する場合は実測で確認し、推測で直さない

## Requirements

- **R1**: 数値入力は一時 string draft を持ち、blur/Enter で parse→commit、無効なら revert する共通プリミティブ (`ui/input.tsx` の DraftInput) を経由する
- **R2**: `ui/select.tsx` の Control が Trigger を内包し、既存・将来の `<Select.Control>` 呼出しがクリック/キーボードで開閉・選択できる (呼出し側 API を壊さない)
- **R3**: Catalog 検索・Chart BPM/名前・Publish 全フィールド・全 Select の操作を jsdom テストで網羅する
- **R4**: Playwright ブラウザE2E スクリプト (Ctrl/⌘+A→Delete→再入力、paste、IME、キーボード、再選択) を `web/tests/e2e/browser/` に用意する。Playwright 未環境では skip (exit 0) して CI を壊さない
- **R5**: FHD 1920x1080 / 2K 2560x1440 / Mac 2560x1664 でのレイアウト検証手順 (スクリーンショット + 横オーバーフロー/ドロップダウン視認性の自動チェック) を用意し、見つかった重なり・クリック阻害は所有ファイル内で修正する

## Dependencies & Gate

- **Depends on**: なし (単独)。親 #61、兄弟 #164 (Worker D) / #165 (Worker E) と所有ファイルのみ分離
- **Blocks**: なし
- **Gate**: `cd web && npx tsc --noEmit && npm test` が緑、Draft PR 作成済み。ブラウザ実機確認 (Playwright 実行 or 手順書) を報告に添付

## Verification Evidence

| # | Check | Command / Method | Expected result |
|---|-------|-------------------|-----------------|
| V1 | 型チェック | `cd web && npx tsc --noEmit` | エラー 0 |
| V2 | 単体テスト | `cd web && npm test` | 全緑 (既存 + 新規) |
| V3 | ブラウザE2E | `cd web && node tests/e2e/browser/run.ts` | Playwright あり: 全 scenario pass / なし: SKIPPED (exit 0) |
| V4 | レイアウト | `cd web && node tests/e2e/browser/run.ts --layout` | 3解像度で横オーバーフローなし・スクリーンショット保存 |

## Rollback

branch `163` の Draft PR を閉じる/revert する。所有パスが他 Issue と分離されているため単独で取り消せる。データ・DB への影響はない。

## Success Criteria

- **SC1**: BPM 欄で Ctrl/⌘+A → Delete → 再入力 → blur が実操作として完結し、確定値が入力途中で壊れない (Scenario 1)
- **SC2**: 全 5 種の Select がクリックとキーボードで開閉/選択でき、実値が保存/フィルタに反映される (Scenario 2)
- **SC3**: IME 途中入力が確定前に確定値として扱われない (Scenario 3)
