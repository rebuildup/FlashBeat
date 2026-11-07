---
description: "FlashBeat lean per-issue specification template (project-local override of the Spec Kit core spec template)"
---

# Spec: Webエディターのノート配置をBPMグリッドにスナップ (1/1〜1/16)

**Issue**: #180 | **Branch**: `180` | **PR base**: `release-0-1-14` | **Date**: 2026-11-08 | **Status**: Draft

**Input**: Web 譜面エディター (Web Studio `/chart`) でノートを新規配置・ドラッグ移動する時に BPM 基準のグリッドへ吸着できるようにする。

> Lean rules (enforced by `docs/agent/spec-kit-workflow.md`):
> one Issue = one branch = one worktree = one Draft PR; ≤5 tasks; no retrospective
> re-specification of the legacy game; no second authority for project governance.

## Objective

Web 譜面エディターのグリッド上で、ノートの新規配置とドラッグ移動 (およびロングノーツの端点) が、選択した粒度 (off / 1 / 2 / 4 / 8 / 16) の **BPM 基準グリッド** へ吸着する。BPM を変更すると格子も即追従し、表示上の時刻 (プレイヘッド等) と実データがずれない。既存の tap 譜面・既存公開データ (D1 の who 曲など) は一切壊れない。

## Owned Paths

- `web/src/lib/chart/snap.ts` — 新規。BPM 基準スナップの純関数 (`beatDuration` / `snapDuration` / `snapTime` / `snapTimeExclusive` / `snapBeat` / 粒度列)
- `web/src/lib/chart/__tests__/snap.test.ts` — 新規。純関数の単体テスト
- `web/src/components/chart-editor/note-grid.tsx` — ノートの新規配置・ドラッグ移動・ロングノーツ端点の位置を snap 済み拍で計算する
- `web/src/components/chart-editor/editor-toolbar.tsx` — 粒度 (Snap) 選択 Select の追加
- `web/src/components/chart-editor/editor-toolbar.test.tsx` — 粒度選択 UI のテスト追加
- `web/src/components/chart-editor/note-grid-snap.test.ts` — 新規。配置/ドラッグが粒度に従うことのテスト
- `web/src/components/chart-editor/snap-store.ts` — 新規。ツールバーとグリッドとエディターが共有する粒度のストア
- `web/src/components/chart-editor/chart-editor.tsx` — 粒度を配置パイプラインへ渡す (行インデックスへの変換)
- `web/src/lib/chart/{notes,long-note}.ts` — 配置・移動・端点変更で粒度に従うための引数追加
- `specs/180-bpm-snap/{spec.md,plan.md,tasks.md}` — 本 Issue の意図記録

## Exclusions (explicitly out of scope)

- `web/src/lib/chart/{types,timing,import,export,schema,validate}.ts` — 既存データモデルと既存挙動は変更しない (スナップは新しい純関数として追加する)
- `web/src/components/chart-editor/lyrics-*.tsx` — Worker E の歌詞パネルとは所有が分かれる (衝突しない領域)
- Unity 側 (`Assets/**`)、D1、Cloudflare デプロイ
- スナップentes の永続化 (選択した粒度は編集セッション内のみ)
- 譜面 JSON のフォーマット変更 (既存公開データのバイト互換性を守る)

## User Scenarios & Testing *(mandatory)*

### Scenario 1 — 粒度を選んで配置する (Priority: P1)

> 既定は **off** (エディター導入前と同じ behaviour: 譜面の lines-per-beat に載る)。
> 運用者が粒度を選ぶと、以後の配置はその格子へ吸着する。

**Given** BPM=120 の譜面を `/chart` で開いて Snap を「1/8」にしている, **When** グリッドの空いたマスをクリックしてノートを置く (またはノートをドラッグする), **Then** ノートは 1/8 拍グリッドの行に落到ち、配置拍は `round(beat * 8) / 8` になる。Snap を「off」にすると行tablelessに自由配置へ戻る。

**Independent Test**: `cd web && npx vitest run src/components/chart-editor/note-grid-snap.test.ts` (jsdom)

### Scenario 2 — BPM を変えると格子が即追従する (Priority: P1)

**Given** Snap=1/4 で BPM=120 の譜面を開いている, **When** BPM を 240 に変更する, **Then** 以後の配置は 1/4 拍 = 0.25 秒刻みになり、同じ.Union 座標でも同じ拍に落ちる (BPM が state として snap 計算へ渡される)。既に置いたノートの拍は変わらない (BPM は時間軸の尺度であって拍ではない)。

**Independent Test**: `cd web && npx vitest run src/lib/chart/__tests__/snap.test.ts` (BPM 変更への追従) + `note-grid-snap.test.ts`

### Scenario 3 — ロングノーツの端点も同じ格子へ (Priority: P2)

**Given** Snap=1/16 でロングノーツ (type 2) を置いている, **When** 端点ハンドルをドラッグする, **Then** 端点も 1/16 拍グリッドに吸附し、端点拍はノート開始拍を下回らない。

**Independent Test**: `cd web && npx vitest run src/components/chart-editor/note-grid.test.tsx` + `src/lib/chart/__tests__/snap.test.ts`

### Edge Cases

- Snap=off: スナップ計算を一切行わない (既存挙動 = 1/resolution の行に最も近い拍)。既存の譜面が壊れないことを保証する
- BPM が 0 / 負 / NaN: 有限でない BPM ではスナップを無効化 (off と同じ扱い) し、譜面を壊さない
- 時刻が負 (開始前): 拍インデックス基準で計算するため、小節/拍境界をまたいでも 0 境界に正しく寄る
- 小節/拍境界をまたぐ連続値 (4/4 の 3 拍目): 拍インデックスを基準に計算するので境界をまたいでも破綻しない
- 粒度 1 (1拍) / 2 (2分) / 4 / 8 / 16: すべて同じ純関数で表現される
- ロングノーツの端点を snap しても開始拍を下回らない (端点 < 開始補正)

## Requirements

- **R1**: `web/src/lib/chart/snap.ts` を純関数のみ (副作用なし・I/O なし・React 依存なし) で実装し、粒度列 `off / 1 / 2 / 4 / 8 / 16`、`beatDuration(bpm)`、`snapDuration(bpm, division)`、`snapTime(time, bpm, division)`、`snapTimeExclusive(time, bpm, division)`、`snapBeat(beat, division)` などを exported する
- **R2**: 丸めは「0.5 超は上へ (half-up)」の最近接snap で決定的に行う (拍インデックス基準)
- **R3**: `note-grid.tsx` の新規配置 (`onAddNote`) とドラッグ移動 (`onMoveNote`)、およびロングノーツの端点 (`onResizeHold`) が、選択中の粒度に従い snap 済み拍を通知する
- **R4**: `editor-toolbar.tsx` に粒度選択 Select (Ark UI Select、Panda token) を追加し、既定は 1/4。既存の Grid / View Select の挙動は壊さない
- **R5**: BPM を state として snap 計算へ渡し、BPM 変更が格子へ即追従する (メモ化)
- **R6**: 表示 (グリッド線・プレイヘッド等) と実データがずれない — snap 済みの拍を単一の計算源から供給する
- **R7**: 既存公開データ (D1 の who 曲など) と既存 tap 譜面を変えない (JSON のバイト互換・検証結果の不変)
- **R8**: `cd web && npm test` が全件 pass、`npm run build` が成功する

## Dependencies & Gate

- **Depends on**: なし (単独)。base は `origin/release-0-1-14` @ `7eb8e0a`
- **Blocks**: なし (Issue #165 の歌詞パネルと `chart-editor.tsx` の競合を避けて分離されている)
- **Gate**: `cd web && npx tsc --noEmit && npm test` が緑、`npm run build` が成功、Draft PR 作成済み

## Verification Evidence

| # | Check | Command / Method | Expected result |
|---|-------|-------------------|-----------------|
| V1 | スナップ純関数の単体テスト | `cd web && npx vitest run src/lib/chart/__tests__/snap.test.ts` | 全件 pass (beatDuration / 各 division / 境界 / BPM 追従 / 拍跨ぎ) |
| V2 | グリッドの配置・ドラッグ・端点 snap | `cd web && npx vitest run src/components/chart-editor/note-grid.test.tsx` | 全件 pass |
| V3 | ツールバーの粒度 UI | `cd web && npx vitest run src/components/chart-editor/editor-toolbar.test.tsx` | 既存 + 新規が pass |
| V4 | 型チェック | `cd web && npx tsc --noEmit` | エラー 0 |
| V5 | 全テスト | `cd web && npm test` | 全件 pass |
| V6 | ビルド | `cd web && npm run build` | 成功 |

## Rollback

branch `180` の Draft PR を閉じる / revert する。Owned Paths が他 Issue と分離されているため単独で取り消せる。データ・DB への影響はない。

## Success Criteria

- **SC1**: 粒度 off/1/2/4/8/16 の選択で、ノート配置・ドラッグ・ロングノーツ端点がその格子へ吸着する (Scenario 1, 3)
- **SC2**: BPM を変更した直後の配置がその BPM の刻みに追従する (Scenario 2)
- **SC3**: 既存 tap 譜面・既存公開データ (who 曲) の検証結果とバイトが一切変わらない (R7)
