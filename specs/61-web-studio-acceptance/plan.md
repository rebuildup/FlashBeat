# Plan: Web Studio 受け入れ監査 (実操作ベース)

**Issue**: #61 | **Branch**: `w-61` | **Base**: `origin/fix/main-sync-014` | **Date**: 2026-11-08

## Approach

1. **監査ハーネス**: 既存 `web/tests/e2e/` (issue #166 の studio-flow spec) を土台に、
   「全ボタン・全タブ・全設定を実操作する」監査 spec を追加する。解像度 3 種
   (1920×1080 / 2560×1440 / 2560×1664) をループし、各操作のスクリーンショットを
   `docs/evidence/web-acceptance/` に出力する。
2. **実行環境**: ローカル dev server (`vp dev`) + ローカル D1
   (`void db migrate && void db seed`)。本番 API は読み取りのみ。
3. **監査対象** (Owned Paths 内の実機能):
   - `/` — Catalog: 検索・レベルフィルタ・ソート・行リンク・`?state=loading|empty|error`・Try again
   - `/chart` — シェル導線・Review & Publish (chart-editor 本体は他 Worker のため
     クリックの結果確認は最小限に留め、シェルレベルの死活のみ監査)
   - `/publish` — フォーム操作・ダイアログ開閉 (token は入力せず送信もしない。
     送信導線の UI 死活まで)
   - `/new-song` — ステッパー・各ステップの導線
4. **修正方針**: 見つかった不具合のうち Owned Paths 内のものは実装で直す
   (最低 1 修正)。Owned Paths 外は Known Issues に記録。
5. **証跡**: 操作ログ (JSON/Markdown) + PNG スクリーンショット + README。

## Risks

- chart-editor が他 Worker 排他のため、エディタ内部の操作要素の修正はできない。
  発見したら Known Issues に記録して報告する。
- `/publish` の実送信は本番 D1 に書き込むため禁止。ローカル D1 でも
  監査 spec は送信ボタンを押さずダイアログ開閉までで止める。
- `styled-system/` は gitignored・生成物。`postinstall` (panda codegen) が
  worktree で完結することを確認する。

## Verification

`npm test` / `npm run build` / `npx tsc --noEmit` / 監査 spec + studio-flow spec
(Playwright, ローカル dev server)。
