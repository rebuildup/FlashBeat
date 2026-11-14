# #374 Select 受入証跡 (Mac)

対象: Select 画面の初回ロード (スケルトン)・再取得・空状態・エラー状態の
自動再現と、外周線・中央組版・スケルトン点滅のデザイン正本照合。

## ハーネス

`-autotest v6screens -autotest-scenes SelectScene` (`AutoTestV6Screens`) が
以下を実行する (Issue #374 で追加):

- `RefreshAsync()` を 3 連続で呼び、各回 `ListModeForAudit == "rows"` を確認
  (行がある再取得でスケルトンへ戻らない = 点滅しない)。
- `SetAuditCatalogFetching(true)` + 不一致クエリで `"skeleton"`、
  既存行ありで `"rows"` を確認。
- `CommitReady()` を選択がフィルタで隠れた状態で呼び、`HasError` と
  `StatusMessage` が出ることを確認 (エラー状態)。
- 不一致クエリの空状態を撮影。エラー後はクエリを戻して `RefreshAsync()` し、
  `HasError` が消えるまで待ってから後続の Empty/Settings/History/Ranking を撮る。

監査用の公開面 (製品動作は変えない):

- `V6SelectDataSource.SetAuditCatalogFetching(bool)` — 取得中フラグの直接操作。
- `V6SelectView.ListModeForAudit` / `VisibleSongCount` / `Data`。

## 実行結果 (最終ビルド)

`script/mac-select-evidence.sh --offline` (`-autotest v6screens
-autotest-scenes SelectScene`、実機フルスクリーン):

| ディレクトリ | 解像度 | 結果 |
| --- | --- | --- |
| `mac-native-2560x1664/` | ネイティブ (3420x2148 表示) | **pass=27 fail=0** |
| `fhd-1920x1080/` | FHD | **pass=27 fail=0** |
| `qhd-2560x1440/` | 2K 相当 | **pass=27 fail=0** |

状態アサーションは全解像度で PASS:

- Refetch #1-#3: rows 維持 (スケルトンへ戻らない)
- Skeleton: 初回ロード相当で `"skeleton"`
- LoadingKeepsRows: 既存行ありの取得中は `"rows"`
- Error: `HasError` + `StatusMessage`
- Empty: 0 件で `"empty"`

スクリーンショット各 11 枚 (`v6screen-SelectScene-*.png`)。

注: `ScreenCapture.CaptureScreenshot` の非同期書き込みが初回実行で
散発的に 0.6 秒の待機を超えたため、`AutoTestV6Screens.Screenshot` を
存在ポーリング (上限 5 秒) に修正した (本ブランチ)。

## 残課題

- なし (Mac 3 解像度で状態再現と証跡が揃った)。オーナー実機での
  目視確認は Issue 側の受け入れフローに委ねる。
