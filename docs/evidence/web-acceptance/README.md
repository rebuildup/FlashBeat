# Web Studio 受け入れ監査の証跡 (Issue #61)

実ブラウザ (Playwright / Chromium) による実操作監査の証跡。ローカル dev server
(`vp dev` + ローカル D1 を `void db migrate && void db seed` で初期化) に対して実行し、
本番 API への書き込みは行っていない。

- 実行コマンド:
  `cd web && E2E_BASE_URL=http://localhost:5183 npx playwright test -c tests/e2e/acceptance-audit.config.ts`
- 対象: 4 ルート (`/`, `/chart?songId=who&version=1`, `/publish?songId=who&version=1`,
  `/new-song`) × 3 解像度 (1920×1080 / 2560×1440 / 2560×1664)
- 結果: **12/12 パス**。横スクロール・画面外はみ出し要素・実行時エラーなし。
- 操作ログ: `operations.json` (102 操作、fail 0)

## スクリーンショット一覧

各解像度ディレクトリ (`1920x1080/`, `2560x1440/`, `2560x1664/`) に同名の PNG がある。

| ファイル | 操作と状態 |
| --- | --- |
| `catalog--initial.png` | Catalog 初期表示。シード済み曲 `Who` の行・サムネイル・列整列を確認 |
| `catalog--search-no-match.png` | 検索に「存在しない曲名xyz」を入力 → 「No song matches the filters」状態 |
| `catalog--error-state.png` | `/?state=error` の強制エラー状態と Try again ボタン |
| `chart--initial.png` | Editor (`/chart?songId=who&version=1`)。`Who · v1 · 139 notes` を確認 |
| `publish--initial.png` | Publish 画面。メタデータフォームとエディタ領域 |
| `publish--sidebar-open.png` | 「Open the publish sidebar」クリック後。メタデータフォームが展開 |
| `publish--publish-dialog.png` | 「Review publish」クリック後の確認ダイアログ (Cancel で閉じて未送信) |
| `new-song--initial.png` | New song フローの初期表示 |
| `new-song--step-1.png` | ステッパー step 1 (曲情報) |

## 監査で実施した操作 (抜粋)

- Catalog: 検索入力 → no-match 状態 → Clear filters 復帰 / レベルフィルタ
  (Lv 10–19 で no-match) / ソート切替 / Refresh 再読込 / `/` キーで検索フォーカス /
  行リンクの可視性 / `?state=error` + Try again
- Publish: サイドバー開閉 / Title フィールド編集 / Review publish → ダイアログ表示 →
  Cancel (送信はしない)
- Chart: 選曲の identity 表示 / Review & Publish ボタンの存在
- New song: step 1 の表示
- 全ルート: ナビリンク 4 種の可視性 / 横スクロールなし / 画面外はみ出しなし /
  pageerror・console error なし

## 監査で見つかり修正したもの

- Catalog のテーブル行に #156 マージ時の旧 `rowBody` が残り、タイトル/アーティストが
  2 回描画されて各セルが 1 列右にズレていた
  (`web/src/app/catalog-panel.tsx` — 削除 + 回帰テスト `catalog-panel.test.tsx` 5 件)。
- `tests/e2e/studio-flow.spec.ts` の行リンク locator が md+ で非表示のカードビュー側を
  掴み、spec が壊れていた (可視側テーブルに限定して修復)。

## 既知の環境メモ

- `vp dev` 起動直後の `/publish`・`/new-song` が一度空の `#app` になる状態が観測された
  (React 未マウント、エラーなし)。再読込で解消。本番ビルド (`vp preview`) では再現せず、
  アプリコードの不具合ではなく dev server の一過性の状態と判断。
- Playwright の既定のスクリーンショット (`caret: "hide"`) はフォーカス中 input の
  `caret-color` を書き換えるため、hydration 中だと React の属性不一致警告が出る。
  監査 spec は `caret: "initial"` を使用。
