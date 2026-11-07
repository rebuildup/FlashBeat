# Tasks: Web Studio 受け入れ監査 (実操作ベース)

**Issue**: #61 | **Branch**: `w-61` | **Base**: `origin/fix/main-sync-014`

- [ ] **T1** ローカル実行環境の準備: `web/` で依存インストール、`void db migrate && void db seed`、
  `vp dev` 起動 (ローカル D1、本番書き込みなし)。完了条件: `GET /api/v1/catalog` が
  ローカル dev server から 200 を返す。
- [ ] **T2** 実操作監査 spec `web/tests/e2e/acceptance-audit.spec.ts` を追加する:
  4 ルート × 全ボタン/タブ/設定を実操作し、3 解像度 (1920×1080 / 2560×1440 /
  2560×1664) でスクリーンショットを `docs/evidence/web-acceptance/` に出力する。
  操作ログも同ディレクトリに残す。完了条件: 監査 spec が dev server に対して完走し、
  証跡 (PNG + README) が残る。
- [ ] **T3** 監査で見つかった「動かない UI」のうち Owned Paths 内のものを実装で直す
  (最低 1 修正)。完了条件: 修正差分が本 PR に入り、対応する unit test または
  監査再実行で解消が確認できる。
- [ ] **T4** 検証: `web/` で `npm test` / `npm run build` / `npx tsc --noEmit`、
  studio-flow spec (Playwright) を実行する。完了条件: 全緑、結果を最終報告に記録。
- [ ] **T5** Draft PR 作成: 最初の意味のある commit で `git push -u origin w-61` →
  `gh pr create --draft --base main --head w-61`。完了条件: PR URL が最終報告に
  記録される。

## Dependencies
親 #61。chart-editor / lib/chart は他 Worker 排他 — 触らない。

## Rollback
PR の revert。
