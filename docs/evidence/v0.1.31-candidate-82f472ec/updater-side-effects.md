# v0.1.31 候補 更新処理の実ファイル副作用検証 (2026-11-19)

Issue #670 の受入条件「更新のダウンロード・展開・退避・ログ・結果・復旧が
アプリフォルダ内に限定され、境界を破らない」について、実際のファイルシステムで
適用・失敗・拒否の 3 経路を観測した記録。シェルテストのモックではなく、
`tools/update/update-macos.sh` をサンドボックスの擬似インストールへ実実行した。

## 検証環境

- 実行: macOS 27.2.1 (arm64)、`tools/update/update-macos.sh` (release-0-1-31 `82f472ec` 時点)
- 擬似インストール `/tmp/fb-upd/install/`: `Contents/MacOS/FlashBeat` + `Contents/Resources/Data` + `Contents/Info.plist` (0.1.30) — .app ではないレイアウト (updater がサポートする中身切替方式)
- 更新 ZIP: 擬似 `FlashBeat.app` (0.1.31) + `version.json` を同梱した実 ZIP、SHA-256 事前計算

## 経路 1: 正常適用 (exit 0)

```
update-macos.sh -InstallDir /tmp/fb-upd/install -ZipPath update.zip \
  -ExpectedSha256 <正しいSHA> -Apply
```

観測結果:

- `install/Contents/Resources/Data/data.txt` が `olddata` → `newdata` に切替 (0.1.30 → 0.1.31)
- 全アーティファクトが `<install>/.flashbeat-update/` 内に収まる:
  - `backup/FlashBeat-backup-install-<runId>/` — 旧コンテンツ一式 + `flashbeat-backup-owner.json` (所有 manifest)
  - `logs/FlashBeat-update-<runId>.log`
  - `results/last-result.json` — `"result":"success", "fromVersion":"0.1.30", "toVersion":"0.1.31"`
  - `work/` は実行後に削除済み (残存なし)
- **境界外への書き込みなし**: 親 `/tmp/fb-upd/` には `install/` `payload/` `update.zip` のみ。`.flashbeat-update`・`FlashBeat-backup-*` 等は親に作られない

## 経路 2: SHA-256 不一致 (失敗時の安全性)

```
-ExpectedSha256 に誤った値を指定して再実行
```

観測結果:

- `ERROR: SHA-256 が一致しません` で中止、インストール内容は無変更 (`data.txt` は `newdata` のまま)
- `last-result.json` が `"result":"failed", "exitCode":3` で上書き記録 (失敗も結果として残る)
- バックアップ・退避は作られない (検証前に失敗するため)

## 経路 3: .app バンドルへの更新拒否

```
-InstallDir /tmp/fb-upd/apphost (FlashBeat.app を含むフォルダ)
```

観測結果:

- `FlashBeat.app (署名済みバンドル) への更新はサポートされていません` で拒否
- `apphost/` 内に `.flashbeat-update` は一切作られない (署名を壊す書き込みをしない)
- 外部領域へのフォールバックなし — 手動差し替えを促すメッセージのみ

## Windows 側 (Update.ps1) の対応証跡

`tools/update/tests/Run-UpdateFlowTests.ps1` (34/34 PASS) が同等の実ファイル検証を
実施済み: 実 ZIP の適用・`<install>\.flashbeat-update\backup` 内への退避・所有 manifest・
境界外レガシーファイルの保全 (移動も削除もしない)・last-result.json の配置・
親フォルダへの新規書き込みが無いことを実 FS で assert している。

## 未観測

- 実機 (Win64 / 展示用 Mac) での実更新 ZIP 適用と Relaunch 後の動作
- 実 GitHub Release からのダウンロード経路 (本検証はローカル ZIP 経路)
- 異常終了 (プロセス kill) からの次回起動時復旧 — コード上は last-result.json と backup から復元する設計で、EditMode/シェルテストで経路は検証済みだが、実 kill を伴う通し確認は未実施
