# Tasks: Web 入力・操作系 (Issue #163)

**Issue**: #163 | **Branch**: `163` | **Base**: `release-0-1-11`

- [ ] **T1** `web/src/components/ui/input.tsx` に draft 保持の入力プリミティブ (`DraftInput`, `parseIntegerOrNull`) を追加する。完了条件: typing 中の空文字/先頭0/負号/全角数字が draft として表示され、blur/Enter で parse→commit または revert すること (`src/components/ui/input.test.tsx` が緑)。
- [ ] **T2** `web/src/components/ui/select.tsx` の `Control` を composite 化し Trigger を内包させる。完了条件: 呼出し側 JSX を変えずに `role="combobox"` のボタンが生まれ、クリック/Enter/Escape/矢印で開閉・選択できること (`src/components/ui/select.test.tsx` が緑)。
- [ ] **T3** `web/src/components/chart-editor/editor-toolbar.tsx` (BPM) と `web/src/components/song-publisher/song-metadata-form.tsx` (Duration) を DraftInput 化する。完了条件: `editor-toolbar.test.tsx` / `song-metadata-form.test.tsx` で Ctrl+A→Delete→再入力→blur の往復と Level Select の選択反映が緑。
- [ ] **T4** `web/tests/e2e/browser/run.ts` (+ README) に実ブラウザE2E (Ctrl/⌘+A→Delete→再入力, paste, IME, キーボード, 再選択) と `--layout` (1920x1080 / 2560x1440 / 2560x1664 の overflow チェック + スクリーンショット) を実装する。完了条件: Playwright 未環境で `node tests/e2e/browser/run.ts` が SKIPPED (exit 0)、あれば全 scenario pass。
- [ ] **T5** `web/src/app/topbar.tsx` の Catalog 検索/Sort/Level の操作テスト (`src/app/topbar.test.tsx`) を追加し、`npx tsc --noEmit` と `npm test` を通す。完了条件: 両コマンドが緑で、実行結果を PR に貼る。

## Dependencies

親 #61。Worker D (#164) / Worker E (#165) / Worker F とは所有パスが分離。Unity・DB・デプロイには依存しない。

## Rollback

branch `163` の PR を revert。所有パス単位で独立して取り消し可能。
