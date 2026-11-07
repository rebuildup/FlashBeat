# Tasks: Studio全画面のグリッド・共通要素・操作フロー再構成

**Issue**: #166 | **Branch**: `166` | **Base**: `release-0-1-11` (2f2b6ba)

- [ ] **T1** 汎用 `StudioShell` を `web/src/app/app-shell.tsx` に抽出する (Sidebar + 主領域 `#studio-content` + スクロール方針 prop)。Catalog 固有ロジックは `AppShell` に保持。完了条件: `AppShell` が `StudioShell` 経由で描画され、`npx tsc --noEmit` が通る。
- [ ] **T2** 共通ページヘッダー帯 (戻りリンク / 題名 / 補足 / 主CTA スロット) のスタイルを `web/src/app/panel.tsx` に追加する。完了条件: 4画面が同一ヘッダー帯スタイルを参照する。
- [ ] **T3** `web/pages/chart.tsx` / `web/pages/publish.tsx` / `web/pages/new-song.tsx` を `StudioShell` に組み込み、hex 直書き (`#1b1b1b` 等) を既存セマンティックトークンに置換する。Chart は `overflow: hidden` の作業領域を維持。完了条件: 4ルートで Sidebar が表示され、Editor の描画が欠けない。
- [ ] **T4** 導線 E2E 仕様 `web/tests/e2e/studio-flow.spec.ts` (Playwright, Catalog→Editor→Review & Publish→Publish→Catalog再読込) を追加する。完了条件: dev server に対して実行可能なコードがリポジトリに残る。
- [ ] **T5** 検証: `cd web && npx tsc --noEmit && npx vp build && npm test` を実行し、FHD (1920×1080) 実ブラウザで 4 ルート + `?state=` 分岐を目視する。完了条件: 全緑 + 目視結果を最終報告に記録。

## Dependencies
親 #61。topbar / chart-editor / song-publisher / new-song components は各 Worker 排他 — 呼ぶだけ。

## Rollback
PR の revert。
