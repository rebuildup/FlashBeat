# FlashBeat 展示アプリの更新 (運営者向け)

展示用 Windows 版 FlashBeat の配布・更新の仕組みと、`tools/update/Update.ps1` の説明です。
オーナー要件 (2026-11-07 版・#41 の「起動時自動更新の禁止」を置き換え) に基づきます。

## 方針

- **展示アプリは GitHub Release の配布成果物**を `C:\Users\rebui\Desktop\FlashBeat-Exhibit\` に配置したものです。`D:\` や worktree の `Builds/` は展示に使いません。
- **運営者は `FlashBeat.exe` を直接起動するだけ**です。常設ランチャー・`Play.cmd` / `Update.cmd` / `Rollback.cmd`・独自更新サーバーはありません。**更新元は GitHub Releases のみ**です。
- **起動時**: アプリが GitHub Releases の最新 (draft を除く・prerelease を含む) を非同期で確認し、新版があれば ZIP と `.sha256` を `%LOCALAPPDATA%\FlashBeat\update\staging` に取得して SHA-256 検証します。ゲームは止めません。
- **終了時**: staging 済みの新版があるとき、アプリは `tools\update\Update.ps1` を `-Apply -WaitPid <自分のPID> -Relaunch` で分離プロセス起動します。スクリプトは「アプリ終了待ち → 別ディレクトリ展開 → 検証 → 一括切替 → 新 exe 再起動」を行います。
- **アプリを終了して再起動すると更新済み**になっています。staging が無い (最新・通信失敗・検証失敗) ときは何も起きず、普段どおり終了します。
- **失敗時は現行版を残します**。SHA 不一致 / ネットワーク失敗 / 二重起動では既存配置を一切変更せず、ゲームは止めません。
- **旧版は 1 世代だけ残します** (`FlashBeat-backup-*`、最新のもの)。それより古い世代は次回適用時に削除します。
- **配布 ZIP に第三者楽曲の音声・動画を含めません** (`Assets/Game/Resources/Musics`、`Assets/Game/Resources/Videos/UnityYoutube`、`loading.mp4` はビルド時に退避して除外。ゲーム内の音声は yt-dlp で解決した YouTube ストリームから再生します)。
- 管理者権限は不要です (インストール先の親フォルダに書き込み権限があることのみ必要)。

## ビルド版識別子と Release タグ

- ビルド時、`BuildScript.cs` が `PlayerSettings.bundleVersion` に **`<バージョン>+<git短SHA>`** を埋め込みます (`Assets/Game/Scripts/Update/UpdateChecker.cs` が `Application.version` で読む)。基準バージョンは既定 `0.1.0`。バージョンアップの Release を作るときは環境変数 `FLASHBEAT_VERSION_BASE` で上書きします (例: `v0.1.1` Release → `FLASHBEAT_VERSION_BASE=0.1.1` でビルド)。
- Release タグは **`v<バージョン>-win64-<git短SHA>`** 形式 (例: `v0.1.1-win64-abc1234`)。アセットは `FlashBeat-<バージョン>-win64-<sha>.zip` と隣接する `.sha256`。
- **適用候補の選び方**: GitHub API の返却順は新しい Release が先とは限らない (実測) ため、UpdateChecker は全候補から **version を主キー・published_at を副キーに最大のもの**を選びます。draft は対象外、prerelease は対象です。
- UpdateChecker は 候補の (バージョン, SHA) と現行ビルドを比較し、バージョンが新しいか・同バージョンで SHA が違うとき新版とみなします。古いバージョンへのダウングレードは行いません。

## 運営者が見える動き

|タイミング|動き|
|---|---|
|起動|(裏で) Releases latest を確認。新版があればダウンロード+SHA 検証。ゲームは通常どおり遊べる|
|Option|staging 済みの新版があるとき「更新を適用して再起動 (<タグ>)」ボタンが出る (押すと即適用+再起動)|
|終了|staging 済みの新版があるとき、終了後に更新が走り、**新 exe が自動で再起動**される|
|再起動|新版になっている。旧版は `FlashBeat-backup-*` として 1 世代残る|

更新の進行と結果は `%LOCALAPPDATA%\FlashBeat\update\staging\` と、インストール先の親フォルダの `FlashBeat-update.log` に記録されます。

## Update.ps1

`tools/update/Update.ps1` は GitHub Releases から ZIP を取得・検証・適用するスクリプトです。配布 ZIP にも同梱され (`tools\update\Update.ps1`)、アプリの自動適用フローは**配布物内のスクリプト**を起動します。

```powershell
# 運営者が明示的に実行 (タグ指定・ZIP と .sha256 を Releases から自動取得)
powershell -ExecutionPolicy Bypass -File .\tools\update\Update.ps1 `
    -InstallDir C:\FlashBeat -Tag v0.1.1-win64-abc1234

# アプリからの自動適用 (UpdateChecker が終了時に起動する形)
powershell -NoProfile -ExecutionPolicy Bypass -File "C:\FlashBeat\tools\update\Update.ps1" `
    -InstallDir "C:\FlashBeat" -Apply -Relaunch -WaitPid 12345 `
    -ZipPath "$env:LOCALAPPDATA\FlashBeat\update\staging\FlashBeat-0.1.1-win64-abc1234.zip" `
    -ExpectedSha256 1a2b3c...
```

### パラメータ

|パラメータ|説明|
|---|---|
|`-InstallDir`|インストール先フォルダ。`FlashBeat.exe` があれば更新、なければ**新規インストール** (Release から取得して配置)。|
|`-ZipPath` / `-ReleaseUrl` / `-Tag`|取得元 (ローカル ZIP / アセット URL / タグ)。`-Tag` は ZIP と `.sha256` を `gh release download` で取得。|
|`-AssetName`|`-Tag` / `-ReleaseUrl` で取得するアセット名。|
|`-ExpectedSha256`|期待する SHA-256 (64 桁 hex)。未指定なら ZIP と同名の `.sha256` を参照。|
|`-Repo`|GitHub リポジトリ (`owner/repo`)。未指定なら git remote から自動判定。|
|`-WorkDir` / `-LogPath`|作業フォルダ / ログ出力先。|
|`-Apply`|アプリからの自動適用モード。スクリプトが配布物内 (インストール先) から起動されることを許可する。|
|`-WaitPid <PID>`|指定 PID の FlashBeat 終了を待ってから更新 (最大 300 秒)。待機後に他の FlashBeat が実行中 (二重起動) なら中止。|
|`-Relaunch`|更新成功後に `FlashBeat.exe` を再起動する。|

### 動作

1. `-WaitPid` 指定時はそのプロセスの終了を待ちます (二重起動なら終了コード 6 で中止)。
2. インストール先に `FlashBeat.exe` があれば更新モード、なければ新規インストール モード。
3. ZIP を取得し、SHA-256 を検証します (不一致なら終了コード 3 で中止)。
4. 別ディレクトリに完全展開し、`FlashBeat.exe` / `FlashBeat_Data` / `*.dll` を検査します。
5. 一括切替します。旧配置は退避し、**切替成功後も 1 世代残します** (それより古い退避は削除)。
6. `-Relaunch` 指定時は新しい `FlashBeat.exe` を起動します。

### 終了コード

|コード|意味|既存配置|
|---|---|---|
|0|更新成功|更新済み|
|1|パラメータ / 前提不備|変更なし|
|2|ネットワーク / ダウンロード失敗|変更なし|
|3|SHA-256 不一致|変更なし|
|4|ZIP 破損 / 展開失敗|変更なし|
|5|展開物の内容検査失敗 (exe / Data / dll 不足)|変更なし|
|6|FlashBeat 実行中 (二重起動 / 待機タイムアウト)|変更なし|
|7|切替 / ファイル操作の失敗|退避データから復元を試みる|

## 自動更新が不調なときの手動手順

1. Releases から ZIP と `.sha256` を取得 (`gh release download <tag> -R rebuildup/FlashBeat`)。
2. SHA-256 を確認 (`Get-FileHash .\FlashBeat-<タグ>.zip -Algorithm SHA256` が `.sha256` と一致)。
3. ゲームを通常終了 → 既存フォルダを退避 → ZIP を展開して中身を丸ごと置換 → 退避を削除。
4. `FlashBeat.exe` を直接起動して題名画面まで進むことを確認。

## 運用上の注意

- ゲームフォルダ内に運営者が別途置いたファイルは、更新後に残りません (フォルダ全体を置換するため)。設定・スコアの保存先は `%USERPROFILE%\AppData\LocalLow\...` 側なので失われません。
- タスクマネージャーでの強制終了では更新は走りません (OnApplicationQuit が呼ばれないため)。通常終了 (ウィンドウを閉じる) で終了してください。
- 配布 ZIP に `FlashBeat_BurstDebugInformation_DoNotShip/` は含めません (Unity の指示による ship 対象外)。
- テスト用 Release (prerelease) でも自動更新は動きます (draft は認証なしでは見えないため対象外)。検証後のテスト Release は削除して構いません。

## 関連

- Release ZIP と SHA-256 の配布仕様: Issue #35
- 実機プレイ / 再起動 / API 疎通の証拠ゲート: Issue #22
- 自動更新の実装: `Assets/Game/Scripts/Update/UpdateChecker.cs` (起動時確認+staging+終了時起動)、`Assets/Editor/BuildScript.cs` (版識別子の埋め込み・メディア除外・スクリプト同梱)
