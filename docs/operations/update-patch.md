# FlashBeat パッチ適用手順 (運営者向け)

展示運営者が **必要なときだけ** Windows 版 FlashBeat のパッチを手動で適用する手順と、そのための小さなスクリプト `tools/update/Update.ps1` の説明です。

## 方針

- **通常のゲーム起動はいつも `FlashBeat.exe` の直接実行**です。ショートカット・ゲーム UI・起動経路は変更しません。
- **更新は運営者が明示的に実行する操作**です。起動時の自動チェック・常駐・更新要求 UI は存在しません。
- `Play.cmd` / `Update.cmd` / `Rollback.cmd` / 常設ランチャー / ロールバック UI・コマンド / 永続的なバージョン倉庫は **作りません**。
- **ロールバックは実装しません**。不具合があった場合は「新しいパッチを 1 つ作る」運用で解決します (#35 が Release として配布)。
- ネットワーク・ZIP・ダイジェスト・内容検査のいずれかが失敗した場合、**既存配置は一切変更しません**。
- 管理者権限は不要です (インストール先の親フォルダに書き込み権限があることのみ必要)。

## 最短の手動手順 (自動更新が不調でもこれだけで更新できます)

1. **ZIP を取得**
   - Releases ページから取得する、または
   - `gh release download <tag> -R rebuildup/FlashBeat` で取得する
2. **SHA-256 を確認**
   - `Get-FileHash .\FlashBeat-<tag>.zip -Algorithm SHA256` (または `certutil -hashfile .\FlashBeat-<tag>.zip SHA256`) を計算し、
   - リリースの隣接アセット `FlashBeat-<tag>.zip.sha256`、またはリリース本文に記載された値と **一致すること**を確認します。
3. **フォルダを丸ごと置換**
   - ゲームを **通常終了** します (実行中は置換できません)。
   - 既存フォルダを退避 (例: `FlashBeat-旧`)、ZIP を新しいフォルダへ展開、展開物の **中身で丸ごと置換**、退避フォルダを削除します。
   - 置換後は `FlashBeat_Data/` `*.dll` `FlashBeat.exe` が揃っていることを確認します。
4. **`FlashBeat.exe` を直接起動**して題名画面まで進むことを確認します。

手順 3 の退避フォルダは **置換途中の破損回避のためだけ** に使い、置換が正常終了したら破棄します (履歴として残しません)。

## Update.ps1 の使い方

`tools/update/Update.ps1` は上記 1〜3 を機械的に 수행する運営者用スクリプトです。**ゲームの起動は行いません** (起動確認は運営者が手動で `FlashBeat.exe` を直接実行)。

```powershell
# ローカルの ZIP を使う場合
powershell -ExecutionPolicy Bypass -File .\tools\update\Update.ps1 `
    -InstallDir C:\FlashBeat `
    -ZipPath C:\dl\FlashBeat-v0.1.0-win64-743c88d.zip `
    -ExpectedSha256 3dbdfecbdb91d916178666045d0c62e1fae717f7df0b77e4f9ddc2af1c036a45

# GitHub Releases のタグから取得する場合 (ZIP と *.sha256 を自動取得)
powershell -ExecutionPolicy Bypass -File .\tools\update\Update.ps1 `
    -InstallDir C:\FlashBeat -Tag v0.1.0-win64-743c88d

# Releases のアセット URL を直接指定する場合
powershell -ExecutionPolicy Bypass -File .\tools\update\Update.ps1 `
    -InstallDir C:\FlashBeat -ReleaseUrl https://github.com/rebuildup/FlashBeat/releases/download/v0.1.0-win64-743c88d/FlashBeat-0.1.0-win64-743c88d.zip
```

### パラメータ

|パラメータ|説明|
|---|---|
|`-InstallDir`|更新対象のインストールフォルダ (`FlashBeat.exe` を含む)。既定はカレントディレクトリ。|
|`-ZipPath`|ローカルにあるリリース ZIP。|
|`-ReleaseUrl`|Releases のアセット URL。直接ダウンロードします。|
|`-Tag`|Releases のタグ名。`gh release download` で ZIP を取得します (ZIP アセットが 1 個なら自動選択)。|
|`-AssetName`|`-Tag` / `-ReleaseUrl` で使うアセット名。|
|`-ExpectedSha256`|期待する SHA-256 (64 桁 hex)。未指定なら ZIP と同名の `.sha256` を参照します。|
|`-Repo`|GitHub リポジトリ (`owner/repo`)。未指定なら本スクリプトのあるリポジトリの git remote から自動判定します。|
|`-WorkDir`|ダウンロード・展開に使う作業フォルダ。|
|`-LogPath`|運営者ログの出力先。既定はインストール先の親フォルダの `FlashBeat-update.log`。|

### 動作

1. インストール先に `FlashBeat.exe` があることを確認します (既存インストールへの更新専用)。
2. `FlashBeat.exe` / `UnityPlayer` / `UnityCrashHandler64` が実行中なら **終了コード 6 で中止**します。強制終了はしません。
3. ZIP を取得します (ローカル / URL / `gh release download`)。
4. SHA-256 を計算し期待値と照合します。不一致なら **終了コード 3 で中止**します。
5. 別ディレクトリ (作業フォルダ) に **完全展開** し、`FlashBeat.exe` / `FlashBeat_Data` / `*.dll` の存在を検査します。不足なら **終了コード 5 で中止**します。
6. 検証済みの展開物と既存配置を **一括切替** します (旧配置は退避 → 切替完了後に破棄)。
7. 終了コード 0 で終了します。`FlashBeat.exe` を直接起動して確認してください。

どの検査にも失敗した場合は既存配置を変更しません。作業フォルダは中止時に削除されます (失敗時の退避データのみ、復元が必要な場合に備えて残します)。

### 終了コード (運用表)

|コード|意味|既存配置|
|---|---|---|
|0|更新成功|更新済み|
|1|パラメータ / 前提不備|変更なし|
|2|ネットワーク / ダウンロード失敗|変更なし|
|3|SHA-256 不一致|変更なし|
|4|ZIP 破損 / 展開失敗|変更なし|
|5|展開物の内容検査失敗 (exe / Data / dll 不足)|変更なし|
|6|FlashBeat 実行中|変更なし|
|7|切替 / ファイル操作の失敗|退避データから復元を試みる|

## 運用上の注意

- **ゲームを通常終了してください。** タスクマネージャーでの強制終了では Unity のクラッシュハンドラー (`UnityCrashHandler64.exe`) がゲームを **再起動** します。再起動されると更新は中止 (終了代码 6) されます。ウィンドウを閉じて終了を確認してから実行してください。
- ゲームフォルダ内に運営者が別途置いたファイルは、更新後に残りません (フォルダ全体を置換するため)。設定の保存先は `%USERPROFILE%\AppData\LocalLow\...` 側なので、スコア・設定は更新によって失われません。
- 既存 Release には `FlashBeat_BurstDebugInformation_DoNotShip/` が含まれません (Unity の指示による ship 対象外)。
- 展開フォルダ直下で起動すると `DirectoryNotFoundException: ...Assets\Resources\ロストアンブレラ.json` が 1 件記録されます (`OptionSceneManager.saveSettings` が相対パスで設定を書き込む既知の不具合)。起動・題名描画・シーンフェードインは正常で、このバージョンでは設定が永続化されません (#22 / #27 の対象)。
- 実機プレイ完走 (select → play → result) の証拠は Issue #22 がゲートしています。本手順の適用 (＝ZIP 取得・検証・置換・起動) とは別の証拠です。

## 関連

- Release ZIP と SHA-256 の配布仕様: Issue #35 (`tools/release/**`, `docs/operations/update-release.md`)
- 実機プレイ / 再起動 / API 疎通の証拠ゲート: Issue #22
- 実機適用のためのスクリプト: `tools/update/Update.ps1`
