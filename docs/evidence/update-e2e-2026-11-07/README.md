# 自動更新 E2E 実機検証 (2026-11-07)

> **注記 (Issue #118 適用前の記録)**: 本記録は **#118「更新は明示式」を適用する前の挙動**の記録で、
> 手順 5〜6 の「通常終了で `OnApplicationQuit` から自動適用」・手順 4 の「自動 staging」は
> 当時の仕様である。現在の設計には **`OnApplicationQuit` からの自動適用・自動 staging は存在しない**
> (ダウンロードも適用もユーザー操作が必要 — 現状の根拠は
> [`docs/evidence/118-explicit-update.md`](../118-explicit-update.md) を参照)。

機械: Windows 実機 (RTX 4050 Laptop / Windows 11)。機械上の FlashBeat.exe を手動で起動・終了したのみ。

## 手順と結果 (時系列)

| 手順 | 結果 |
|---|---|
| 1. 旧版を `Desktop\FlashBeat-E2E` に配置 | `Update.ps1 -ZipPath` (ローカル ZIP) で配置。`FlashBeat-0.1.0-win64-6fb6810.zip` SHA-256 `7f2a22bf…` 検証済み |
| 2. テスト用 Release 作成 | `v0.1.1-win64-6fb6810` (prerelease)。ZIP `FlashBeat-0.1.1-win64-6fb6810.zip` (251MB) + `.sha256` |
| 3. 旧版を起動 | `FlashBeat.exe` を直接起動 (PID 50960)。Player.log: `[UpdateChecker] 新版を確認: v0.1.1-win64-6fb6810 (現行 0.1.0+6fb6810)` |
| 4. 自動 staging | 約 35 秒で `C:\Users\rebui\AppData\Local\FlashBeat\update\staging\` に ZIP+manifest を取得。`[UpdateChecker] 新版の staging が完了しました: v0.1.1-win64-6fb6810 (sha256=4b756fe4…)` |
| 5. 通常終了 | WM_CLOSE (ウィンドウを閉じる相当)。`OnApplicationQuit` から `Update.ps1 -Apply -Relaunch -WaitPid 50960` を起動 |
| 6. 自動適用 | `FlashBeat-update.log`: PID 終了待ち → SHA-256 再検証 (`4b756fe4…` 一致) → 展開 (219 項目) → 一括切替 → 旧版を 1 世代残す (`FlashBeat-backup-20261107-015213`) → 新 exe 再起動 (PID 58164) |
| 7. 再起動後の検証 | Player.log: `[UpdateChecker] 最新です (build=0.1.1+6fb6810, latest=v0.1.1-win64-6fb6810)`。`FlashBeat_Data/globalgamemanagers` 内の版識別子 = `0.1.1+6fb6810` (`0.1.0+…` は不在)。`FlashBeat.exe` の SHA-256 = ビルド D の exe と一致 (`3031bb5e15e85eb0eaa4de271b81e2c8f5e8eeb1254c3dcc0a70c4b950c39a60`) |

## 兼ねて確認した追加シナリオ

- **gh 経由の配布** (Release タグ指定): `Update.ps1 -InstallDir Desktop\FlashBeat-Exhibit -Tag v0.1.1-win64-6fb6810` → `gh release download` で ZIP+.sha256 取得 → SHA 検証 → 適用成功。
- **旧版 1 世代保持**: 適用のたびに直前の世代を `FlashBeat-backup-*` に残し、それより古い世代を自動削除。
- **旧版の自動更新不可は仕様**: テスト旧版 (743c88d Release 相当) には UpdateChecker が存在しないため自己更新できない。初回配備は Update.ps1 (ローカル ZIP / タグ指定) で行う。

## 既知の注意点

- Update.ps1 のバックアップ保持は「親ディレクトリの `FlashBeat-backup-*`」を対象にするため、`Desktop` 直下に複数のインストール (FlashBeat-Exhibit / FlashBeat-E2E など) があると互いのバックアップを 1 世代に丸める。単一展示フォルダ運用では問題なし。
- 2026-11-07 01:40:51、別ワーカー (#22) が `Desktop\FlashBeat-Exhibit` へ自前ビルドを配備した記録あり (`version.json` / `version.txt` マーカー)。本検証では競合を避けるため `Desktop\FlashBeat-E2E` を使用し、検証完了後に `FlashBeat-Exhibit` を Release (v0.1.1) から配備し直した。
