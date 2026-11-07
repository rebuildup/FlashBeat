# Web Studio 視覚/動作修復の証跡 (#166 / #61 / #75)

- **Base:** `main` @ `1b1fe39`
- **Fix commit:** `d928cff` `fix(studio): compact toolbars, hint click-through, placed-note selection`
- **方法:** ローカル実機 (`void db migrate` + `void db seed` + `vp dev`) で実 Chrome
  (Playwright, headless) を操作。解像度はリリースゲート指定の 3 種
  **1920×1080 / 2560×1440 / 2560×1664** (DPR 1)。
- **対象:** app shell (`/`) と各ページ layout (`/chart`, `/publish`, `/new-song`)。

## 洗い出した欠陥と修正 (実操作で確認)

| # | 欠陥 (before) | 修正 (after) | 証跡 |
|---|---------------|--------------|------|
| 1 | Catalog 行の「Open chart」ヒントが押せる見た目なのに無機能: ヒント span と actions wrapper (zIndex 1) が引き伸ばされた行リンクの上に乗り、クリックを握りつぶす (`elementFromPoint` = span、URL 不変) | ヒントと actions wrapper を `pointerEvents: "none"` に、実リンク (Publish / YouTube) のみ `pointerEvents: "auto"`。ヒント中央のクリックが行リンクに到達し `/chart?songId&version` へ遷移 | `interaction/open-hint-click-navigates-to-chart.png` |
| 2 | エディターツールバー: Park UI select recipe の `root: width: full` + `flexShrink: 0` により Grid/Snap/View が各 1 行を占有し、ツールバーが 257px に膨張 (FHD でノートグリッドが約 200px 圧迫) | Select.Root に確定 basis `flex: "0 0 6.5rem"`、Song name/BPM を `flex: "1 1 …"`。ツールバー 257px → **83px** の 1 行 (1920/2560 の両方で測定) | `before/chart--*.png` vs `after/chart--*.png` |
| 3 | Topbar: Sort/Level のセレクトが同理由で各 1 行を占有し、バーが 211px (Search / Sort / Level の 3 段積み) | Select.Root に確定 basis `flex: "0 0 8rem"`。バー 211px → **91px**、Search・Sort・Level が 1 行 | `before/home--*.png` vs `after/home--*.png` |
| 4 | 譜面エディタ: 配置直後のノートが非選択のため、グリッドヒントが明示する「Del removes」が直後に効かない (もう一度クリックが必要) | Tap/Hold 両方の配置パスで新ノートを選択 (`setSelectedIndex(current.notes.length)`)。配置 → Del で即削除を実操作で確認 (139 → 140 → 139) | 実行ログ (PR 本文) |
| 5 | Catalog 行 (md 幅 768–1023px): Time セルがヘッダーと違い md で表示され、7 セルが 6 列に収まらず actions が 2 行目へ折り返し | Time セルをヘッダーと同じ lg のみ表示 (`numTime`) に。行 102px → 70px、actions が 1 行目に復帰 | `before/catalog-row-md-900.png` vs `after/catalog-row-md-900.png` |

## 監査で確認済み・問題なしと判断した項目

- 3 解像度とも横スクロール・viewport はみ出しは無し (`scrollWidth == clientWidth`)。
- `/` ショートカットの検索フォーカス、↑/↓ 行フォーカス、検索 no-match + Clear filters、
  Refresh、Sort/Level のセレクト動作、行リンク遷移。
- chart: ノート配置/移動/削除、Grid・Snap・View のセレクト、Export、
  Review & Publish → `/publish?draft=…&songId=…&version=…` 遷移。
- publish: メタデータフォーム、Review publish のダイアログ内容 (song/chart サマリ + token)。
- new-song: バリデーション (無効値で次へが進まないのは仕様)、ステッパー
  (未到達ステップが disabled なのは仕様)、有効入力で step 2 へ遷移。
- ブランドアイコンが壊れて見えた件はコード欠陥ではなく LFS 未取得
  (`git lfs pull` で解消)。コンポーネント契約どおり壊れ画像として表示されていた。

## テスト

- `npx vitest run` → **33 files / 313 tests passed**
- `node --test tests/` → **61 passed**
- `npx tsc --noEmit` → OK
- `npx vp check` → 修正ファイルはクリーン (ベースに存在した他ファイル 11 件の
  既存フォーマット指摘は所有パス外につき触らない)

## 画面別の before / after

| 画面 | before | after |
|------|--------|-------|
| `/` (Catalog) | `before/home--1920x1080.png` | `after/home--1920x1080.png` |
| `/chart` | `before/chart--1920x1080.png` | `after/chart--1920x1080.png` |
| `/publish` | `before/publish--1920x1080.png` | `after/publish--1920x1080.png` |
| `/new-song` | `before/new-song--1920x1080.png` | `after/new-song--1920x1080.png` |

2K (2560×1440) と Mac (2560×1664) も同名ファイルで収録。
