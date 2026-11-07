---
description: "FlashBeat lean per-issue specification template (project-local override of the Spec Kit core spec template)"
---

# Spec: 公開 API と Web の譜面検証を単一の共有 validator に寄せる (Issue #191)

**Issue**: #191 | **Branch**: `w-189-chart-score` | **PR base**: `main` | **Date**: 2026-11-08 | **Status**: Draft

**Input**: `[Chart/API/P1] 公開APIとWebの譜面検証が不一致：公開後に編集不能となり、LPB混在の重複HOLDも見逃す`

> Lean rules (enforced by `docs/agent/spec-kit-workflow.md`):
> one Issue = one branch = one worktree = one Draft PR; ≤5 tasks; no retrospective
> re-specification of the legacy game; no second authority for project governance.

## Objective

Web (Editor / Import) と公開 API が **同じ意味検証関数** を使い、
同じ譜面に対して **同じ結果** を返すようにする。重複 HOLD / 長さ判定は
**各ノーツの LPB で正規化した拍 (または秒)** で比較し、LPB が混在していても
重なった HOLD を検出する。サーバーで受理した譜面は **Web で再取得・編集できる**
(round-trip 不変条件) をテストで固定する。

## Owned Paths

- `web/src/lib/chart/validate.ts` — 共有意味検証モジュール
- `web/src/lib/chart/schema.ts` — 共有 validator の入口
- `web/src/lib/chart/types.ts` — 検証結果型の共有
- `web/src/lib/chart/chart.v1.schema.json` — 契約のバイト同一コピー
- `contracts/schemas/chart.v1.schema.json` — 凍結契約
- `web/tests/api.test.ts`, `web/tests/publish-semantics.test.ts` — 検証
- `web/routes/api/v1/_publish-model.ts` — 共有 validator を使う側
  (Issue #191 の受け皿。**既に #189 の Worker が触る** 範囲の拡張)
- `specs/191-chart-validation/{spec,plan,tasks}.md`

## Exclusions (explicitly out of scope)

- `web/src/components/**` — 別 Worker の排他所有
  (検証パネルが issue の表示を変える必要があれば別 Issue)
- `web/src/lib/song-session/**` — 別 Worker の排他所有。
  `loadSongSession` は `importChartJson` を使うので、共有 validator Hern change
  により **自動的に** 同じ判定になる (コード変更はしない)
- `contracts/openapi.v1.yaml` — 契約は凍結済み。変更しない
- 本番 Cloudflare デプロイ / 本番 DB への書き込み — Supervisor の直列作業
  (D1 は **読み取りのみ**。書き込み禁止)
- Unity 側の譜面検証 (`Assets/Game/Scripts/**`) — 別 Worker の排他所有。
  共有 validator は **Web/Worker 側** のFact である

## User Scenarios & Testing *(mandatory)*

### Scenario 1 — 同じ譜面が Web と API で同じ結果になる (Priority: P1)

**Given** API が受理する (意味検証を通過する) 譜面
**When** 同じ譜面を Web の `validateChart` / `importChartJson` に渡す
**Then** **両者是同じ結果** を返す (受理なら受理、拒否なら同じ拒否理由と
同じ JSON pointer)

**Independent Test**:
`cd web && npx vitest run src/lib/chart/__tests__/` と
`cd web && node --test tests/publish-semantics.test.ts`
(共有 fixture に対して **Web と API の判定集合を突き合わせる** テストが通る)

### Scenario 2 — LPB が混在した重複 HOLD を両者が検出する (Priority: P1)

**Given** 同じレーンで時刻が重なる 2 本の HOLD (`LPB` が 4 と 8)
**When** Web と API の検証にこの譜面を渡す
**Then** **両者とも** 重複を検出する (行番号の生比較では検出できなかった重叠も)

**Independent Test**:
`cd web && npx vitest run src/lib/chart/__tests__/chart-validation-parity.test.ts`
(同じ fixture が両者で同じ issue を返す)

### Scenario 3 — サーバーで公開した譜面が Web で再取得・編集できる (Priority: P1)

**Given** 公開 API が受理した譜面
**When** `GET /api/v1/charts/{songId}` で取得し、Web の `importChartJson` に渡す
**Then** **読み込みが成功** する (「Published chart failed validation」で落ちない)

**Independent Test**:
`cd web && node --test tests/api.test.ts`
(publish → GET → Web validator 往復の統合テスト)

### Edge Cases

- **境界接触** (片方の HOLD の終了拍 == 他方の開始拍): **重なりではない**
  (両者が同時に同じトラクを握れない)。区切り接触は許容する
- **異なる LPB だが重ならない HOLD**: **重なりではない** (誤検出しない)
- **最小長 / 最大長** (0.25 拍 / 64 拍): 両者が同じ境界で同じ判定を返す
  (境界値は **拒否**、`MIN` ちょうどは受理)
- **曲長超過**: `durationSec` を持つ側 (API) はそれを使い、Web 側は
  `durationSec` を持たないため **長さは拍数で** 判定する (共通の規則)
- **LPB = 0 / 不正値**: スキーマ層で弾かれる。意味検証層は
  **ゼロ除算しない** (Infinity / NaN を出さない)
- **Tap は重複判定の対象外**: 同じレーンに Tap が何個あっても問題なし
- **HOLD 以外の type**: スキーマ層で弾かれる (意味検証層はTap/HOLD 以外を無視)
- **既に公開済みの不整合譜面**: 取り出し / 修正の回復経路として、
  **Web 側で「strict / lenient」の 2 モード** を用意し、
  lenient モードでは **既存の公開譜面を読み出せる** (黙って欠落・書換えしない)

## Requirements

- **R1**: 意味検証 (構文スキーマでは表現できない関係) は
  **単一の共有モジュール** (`web/src/lib/chart/validate.ts`) に置き、
  Web の Editor / Import と公開 API の **両方** が **同じ関数** を使う
- **R2**: 重複判定と長さ判定は **各ノーツの LPB で正規化した拍** で行う
  (`num / LPB` と `endNum / LPB` を比較する)。生 行番号の比較はしない
- **R3**: 共有 validator は **実行環境に依存しない** (ブラウザ / Workers /
  Node のいずれでも動く純関数。DOM / fs / ネットワークを使わない)
- **R4**: 共有 validator は **issue の集合** を返す (順序は安定)。
  既存の `ValidationIssue` / `PublishDiagnostic` 形式を壊さない
- **R5**: Web と API は **同じ fixture** に対して **同じ判定** を返す
  (テストで固定する)
- **R6**: サーバーで受理した譜面は **Web で再取得・編集できる**
  (round-trip 不変条件)
- **R7**: 既に公開済みの不整合譜面は **黙って欠落・書換えしない**。
  Web 側は **strict (既定) / lenient (公開済み読み出し用)** の 2 モードを
  持ち、lenient では **意味検証の違反を警告として残しつつ読み出せる**
- **R8**: API の 422 応答は **共有 validator の結果** をそのまま反映する
  (別ルールの二重管理をしない)
- **R9**: 既存の `MIN_HOLD_BEATS` / `MAX_HOLD_BEATS` の値は **変えない**
  (既存の Web 挙動が変わらない)
- **R10**: 構造層 (JSON スキーマ) は **据え置き**。意味検証層の差分だけを
  共有化する (契約のバイト同一コピーは維持)

## Known Issues (この Issue が解かないもの)

### K1 — 構造層は Web (Ajv) と API (@cfworker/json-schema) で実装が違う

Workers ランタイムは `new Function` を禁じるため、API 側 Ajv を使えない
(スキーマの **エンジン** だけが違う。**規則** は同じ)。
これは #16 からの既知の制約で、本 Issue の範囲外。
共有化するのは **意味検証層** (R1)。

### K2 — `web/src/lib/song-session/load.ts` はコードを変更していない

`loadSongSession` は `importChartJson` を使うので、共有 validator への
切り替えにより **自動的に** 同じ判定になる。ただし **lenient モード**を
ここに接线する検討は `web/src/lib/song-session/**` の排他所有なので触っていない。
→ follow-up: 「公開済み譜面の lenient 読み込みを song-session に接続する」。

### K3 — 既に公開済みの不整合譜面の **実データ** は触らない

本番 D1 の読み取りは可能だが、**書き込みは禁止**。既に公開済みの不整合譜面は
**この Issue のデプロイ後の lenient モード** で読み出せるようになるが、
**公開済みのデータを書き換えること** はしない (recovery は人手)。
→ Supervisor が公開済み譜面の一覧を確認し、必要なら個別に再 publish する運用。

### K4 — Unity 側の譜面検証は共通化していない

`Assets/Game/Scripts/**` は他 Worker の排他所有で、共有 validator は
**TypeScript 側** の単一権威。Unity は自分 Schema で読む (既存の agreed
shape を変えない)。C# への移植は follow-up |Issue。

## Dependencies & Gate

- **Depends on**: なし (Issue #191 は自立している)
- **Blocks**: 「公開済み譜面の lenient 読み込みを song-session に接続する」
  follow-up Issue
- **Gate**:
  1. `cd web && npm test` が green (`node --test tests/ && vitest run`)
  2. `cd web && npx tsc --noEmit` がエラー 0
  3. `cd web && npx vp check` (oxfmt / oxlint) が green
  4. 契約コピー (`web/src/lib/chart/chart.v1.schema.json`) が
     `contracts/schemas/chart.v1.schema.json` とバイト同一
  5. 本番 DB への **書き込みがない** こと (読み取りのみ)

## Verification Evidence

| # | Check | Command / Method | Expected result |
|---|-------|-------------------|-----------------|
| V1 | Web ユニットテスト | `cd web && npx vitest run` | 全 green |
| V2 | Web テスト一式 | `cd web && npm test` | green |
| V3 | 型チェック | `cd web && npx tsc --noEmit` | エラー 0 |
| V4 | lint / format | `cd web && npx vp check` | green |
| V5 | 契約コピー整合 | `cd web && npx vitest run src/lib/chart/__tests__/contract-schema.test.ts` | green (バイト同一) |
| V6 | 公開 API 統合 | `cd web && node --test tests/api.test.ts` | green |
| V7 | 本番 DB 非変更 | 手動 (read-only) | 書き込み 0 件 |

## Rollback

共有 validator は **既存ファイルの変更** (`web/src/lib/chart/validate.ts` /
`schema.ts` / `web/routes/api/v1/_publish-model.ts`) なので、
revert すると元の 2 系統の検証に戻る。**新しい永続化形式** はない
(外部フォーマットを増やさない) ので、revert 後のデータ不整合リスクはない。
契約を触らないため、他 Issue の Web 作業との干渉は最小。

## Success Criteria

- **SC1**: 同じ譜面が **Web と API で同じ結果** を返す (テストで証明)
- **SC2**: **LPB 混在の重複 HOLD** を両者が検出する (テストで証明)
- **SC3**: **サーバーで公開した譜面が Web で再取得・編集できる**
  (統合テストで証明)
- **SC4**: 既に公開済みの不整合譜面は **黙って欠落・書換えされない**
  (lenient モードで読み出せる)
