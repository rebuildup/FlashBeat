# FlashBeat 展示アプリの更新 (運営者向け)

展示用 Windows / macOS 版 FlashBeat の配布・更新の仕組みと、`tools/update/Update.ps1` /
`tools/update/update-macos.sh` の説明です。
オーナー要件 (2026-11-07 版・#41 の「起動時自動更新の禁止」を置き換え、#118 で**適用も明示式**に変更) に基づきます。

## 方針

- **展示アプリは GitHub Release の配布成果物**を配置したものです。
  - Windows: `C:\Users\rebui\Desktop\FlashBeat-Exhibit\`
  - macOS: `FlashBeat.app` (Desktop 等、運営者が決めた場所)
  `D:\` や worktree の `Builds/` は展示に使いません。
- **運営者は `FlashBeat.exe` / `FlashBeat.app` を直接起動するだけ**です。常設ランチャー・`Play.cmd` / `Update.cmd` / `Rollback.cmd`・独自更新サーバーはありません。**更新元は GitHub Releases のみ**です。
- **起動時**: アプリが GitHub Releases の候補を非同期で**確認**します。新版があれば **Title シーン (ホーム) に『アップデートあり vX.Y.Z』の通知を出します** (issue #118: 更新は明示式)。**通知を押さない限りダウンロードも適用も行われません**。ゲームは止めません。候補は**更新チャンネル**で決まります (下述)。展示用ビルド (stable) は **prerelease (テスト用 Release) を更新候補にしません**。Windows 版は `-win64-`、macOS 版は `-macos-` の Release だけを候補にします。
- **ダウンロード / 適用 (ユーザー操作)**: 通知上で **F3 キーまたは通知クリック** → ZIP と `.sha256` を staging に取得して SHA-256 検証 (進捗 % を表示)。検証が通ると通知は『更新の準備ができました。終了して適用 [F3]』に変わり、**もう一度 F3 / クリックで確認**すると配布物内のアップデータが分離プロセスで起動されます (アプリ終了待ち → 別ディレクトリ展開 → 検証 → 一括切替 → 新プロセス再起動)。**適用するとアプリは再起動します**。
  - Windows: `tools\update\Update.ps1`
  - macOS: `FlashBeat.app/Contents/Resources/tools/update/update-macos.sh`
- **押さなければ更新しない**: F4 / × でこのセッションは通知を隠せます。確認せずに普通終了した場合は何も適用されず、staging 済み ZIP は残ります (次回起動時にまた通知が出ます)。**`OnApplicationQuit` からの自動適用は存在しません** (issue #118 で廃止)。
- **失敗時は現行版を残します**。SHA 不一致 / ネットワーク失敗 / 二重起動では既存配置を一切変更せず、ゲームは止めません。
- **旧版は 1 世代だけ残します** (`<install>\.flashbeat-update\backup\FlashBeat-backup-<installKey>-<runId>`、最新のもの)。それより古い世代は次回適用時に削除します。
  削除対象は **更新対象のインストール自身が所有すると確認できた退避だけ** です
  (同じインストールフォルダ内の別名領域や、所有不明の退避は消しません。Issue #185 参照)。
- **配布 ZIP に第三者楽曲の音声・動画を含めません** (`Assets/Game/Resources/Musics`、`Assets/Game/Resources/Videos/UnityYoutube`、`loading.mp4` はビルド時に退避して除外。ゲーム内の音声は yt-dlp で解決した YouTube ストリームから再生します)。
- 管理者権限は不要です (インストール先フォルダ内への書き込み権限があることのみ必要)。
- **更新関連のファイルはすべてインストール先フォルダの内側に置きます** (issue #670)。
  `%APPDATA%` / `AppData\LocalLow` / `~/Library/Application Support` / システム temp /
  インストール先の親・兄弟フォルダには、更新処理は一切書き込みません。
  旧レイアウト (親フォルダの `.flashbeat-update`、`FlashBeat-backup-*`、
  `persistentDataPath` 配下の `update/`) に残るファイルは**移動も削除もせず**、
  ログに「手動削除を推奨」と出すだけです (データ保全)。

## ビルド版識別子と Release タグ

- ビルド時、`BuildScript.cs` が `PlayerSettings.bundleVersion` に **`<バージョン>+<git短SHA>`** を埋め込みます (`Assets/Game/Scripts/Update/UpdateChecker.cs` が `Application.version` で読む)。基準バージョンは **リポジトリ直下の `VERSION` ファイル** (`X.Y.Z`) で、環境変数 `FLASHBEAT_VERSION_BASE` があればそれが優先です。どちらも無い / `X.Y.Z` でない場合は **ビルドが失敗します** (以前の暗黙の既定 `0.1.0` は廃止)。
  **Release を出すたびに `VERSION` を上げます** (例: `0.1.23` → `VERSION` を書き換えてビルド → タグ `v0.1.23-win64-<sha>` / `v0.1.23-macos-<sha>`)。
  自動更新はバージョンの大小で判定するため、同じバージョンで別 SHA の Release を作ると自動更新の経路が壊れます。
- Release タグは **`v<バージョン>-<platform>-<git短SHA>`** 形式 (platform = `win64` / `macos`、例: `v0.1.1-win64-abc1234`, `v0.1.1-macos-abc1234`)。アセットは `FlashBeat-<バージョン>-<platform>-<sha>.zip` と隣接する `.sha256`。
  **プラットフォームごとに別 Release** とし、UpdateChecker は実行中のプラットフォームのタグ/アセットだけを見ます。
- **適用候補の選び方**: GitHub API の返却順は新しい Release が先とは限らない (実測) ため、UpdateChecker は候補から **version を主キー・published_at を副キーに最大のもの**を選びます。draft は対象外、prerelease は stable チャンネルの候補から外れます (preview チャンネルでは対象)。
- UpdateChecker は 候補のバージョンと現行ビルドのバージョンを比較し、**バージョンが厳密に大きいときだけ**更新します。
  同じバージョンで別のビルド (SHA 違い) は「新旧」を判定できないため自動更新しません (古いビルドへ戻すのを防ぐため)。
  その場合は Player.log に「同じバージョン ○○ の別ビルドがあります」と出るので、運営者が `Update.ps1 -Tag` / `update-macos.sh -Tag` で適用します。
  古いバージョンへのダウングレードも行いません。

## 更新チャンネル (stable / preview)

更新候補の Release は**ビルド時に決まるチャンネル**で絞り込みます。

|チャンネル|対象|prerelease|
|---|---|---|
|`stable`|展示用ビルド (既定)|対象外 (テスト用 Release は自動適用されない)|
|`preview`|開発・検証用ビルド|対象|

- チャンネルは `BuildScript.cs` がビルド出力の `StreamingAssets/update-channel.txt` (Windows: `FlashBeat_Data\StreamingAssets\`、macOS: `FlashBeat.app/Contents/Resources/Data/StreamingAssets/`) に書き出します (`stable` / `preview`)。`UpdateChecker` は起動時にこのファイルを読んで候補を絞ります。
- ファイルが無い・読めない・未知の値のときは **safe 側 (`stable`)** に倒します。展示用ビルドがテスト用 prerelease を自動適用しないのはこの境界で保証します。
- `preview` ビルドは環境変数 `FLASHBEAT_UPDATE_CHANNEL=preview` を付けてビルドします (例: `FLASHBEAT_UPDATE_CHANNEL=preview Unity -batchmode -quit -projectPath . -executeMethod BuildScript.BuildStandaloneWindows64`)。通常のビルド (環境変数なし) は `stable` です。
- 起動時の Player.log に `[UpdateChecker] 更新チャンネル: stable (prerelease を更新候補に含める: no)` と出ます。展示PC でチャンネルを確認したいときは `Player.log` を見てください。
- 運営者が `Update.ps1 -Tag <タグ>` を手動実行する経路はチャンネルに関係なく任意の Release (prerelease 含む) を適用できます。展示中の緊急差し替え用です。

## 運営者が見える動き

|タイミング|動き|
|---|---|
|起動|(裏で) Releases latest を**確認だけ**。新版があれば Title シーンに『アップデートあり vX.Y.Z』通知。ゲームは通常どおり遊べる|
|Title 通知|**F3 / 通知クリック**でダウンロード+SHA 検証 (進捗 % 表示)。**F4 / × でこのセッションは非表示** (押さなければ更新しない)|
|staging 完了後|通知が『更新の準備ができました。終了して適用 [F3]』に変わり、**F3 / クリックで確認したときだけ**適用+再起動|
|Option|staging 済みの新版があるとき「更新を適用して再起動 (<タグ>)」ボタンが出る (押すと適用+再起動)|
|終了|**確認していない場合は何も適用されない** (staging は残り、次回また通知)。確認済み (F3 押下) のときは終了後に更新が走り、**新 exe が自動で再起動**される|
|再起動|新版になっている。旧版は `<install>\.flashbeat-update\backup\FlashBeat-backup-*` として 1 世代残る|

更新の進行と結果は、**インストール先フォルダ内** の `.flashbeat-update/` 配下の責務別ディレクトリに記録されます (issue #574 / #670)。

|内容|Windows|macOS|
|---|---|---|
|staging (ZIP + manifest)|`<install>\.flashbeat-update\staging\`|同左 (`.app` は更新対象外のため通常は使われない)|
|last-result.json|`<install>\.flashbeat-update\results\last-result.json`|同左|
|updater ログ|`<install>\.flashbeat-update\logs\FlashBeat-update-<runId>.log` (最新 20 世代)|同左|
|作業 / 退避 / updater 一時領域|`<install>\.flashbeat-update\{work,backup,updater}\`|同左|

`persistentDataPath` (`%USERPROFILE%\AppData\LocalLow\...` / `~/Library/Application Support/...`) は
スコア・設定など更新と無関係のデータ用であり、**更新関連アーティファクトはもう置きません** (issue #670)。
旧レイアウトの残骸は検出して警告ログを出すだけで、移動・削除はしません。

## Update.ps1

`tools/update/Update.ps1` は GitHub Releases から ZIP を取得・検証・適用するスクリプトです。配布 ZIP にも同梱され (`tools\update\Update.ps1`)、アプリの自動適用フローは**配布物内のスクリプト**を起動します。

```powershell
# 運営者が明示的に実行 (タグ指定・ZIP と .sha256 を Releases から自動取得)
powershell -ExecutionPolicy Bypass -File .\tools\update\Update.ps1 `
    -InstallDir C:\FlashBeat -Tag v0.1.1-win64-abc1234

# アプリからの適用 (ユーザーが通知/Option で確認した後に UpdateChecker が起動する形)
powershell -NoProfile -ExecutionPolicy Bypass -File "C:\FlashBeat\tools\update\Update.ps1" `
    -InstallDir "C:\FlashBeat" -Apply -Relaunch -WaitPid 12345 `
    -ZipPath "C:\FlashBeat\.flashbeat-update\staging\FlashBeat-0.1.1-win64-abc1234.zip" `
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
|`-WorkDir` / `-LogPath` / `-LogDir` / `-ResultDir`|作業フォルダ / ログ出力先 (ファイル or ディレクトリ) / last-result.json の出力先。**すべてインストール先フォルダ内でなければ拒否されます** (issue #670)。未指定時は `<install>\.flashbeat-update\{work,logs,results}` を使います。|
|`-Apply`|アプリからの自動適用モード。スクリプトが配布物内 (インストール先) から起動されることを許可する。|
|`-WaitPid <PID>`|指定 PID の FlashBeat 終了を待ってから更新 (最大 300 秒)。待機後に他の FlashBeat が実行中 (二重起動) なら中止。|
|`-Relaunch`|更新成功後に `FlashBeat.exe` を再起動する。|

### 動作

1. `-WaitPid` 指定時はそのプロセスの終了を待ちます (二重起動なら終了コード 6 で中止)。
2. インストール先に `FlashBeat.exe` があれば更新モード、なければ新規インストール モード。
3. ZIP を取得し、SHA-256 を検証します (不一致なら終了コード 3 で中止)。
4. 別ディレクトリに完全展開し、`FlashBeat.exe` / `FlashBeat_Data` / `*.dll` を検査します。
5. 一括切替します。旧配置は退避し、**切替成功後も 1 世代残します**
   (それより古い退避は、**更新対象のインストール自身が所有すると確認できたものだけ**を削除します。
   詳細は下の「退避の所有権」)。
6. `-Relaunch` 指定時は新しい `FlashBeat.exe` を起動します。

### 退避 (バックアップ) の所有権 (Issue #185)

同じインストールフォルダ内に複数の FlashBeat 配置を
置いておくと、1 つの更新で **別のインストールの退避 (復旧データ) まで消してしまう**
問題がありました (Issue #185)。現在の `Update.ps1` は、退避を **インストール単位** で管理します。

|要素|内容|
|---|---|
|identity|正規化インストールパス + フォルダ名から作る安定 ID (`installKey`) + 正規名 (`FlashBeat`) と一致するか (`isCanonical`)|
|退避フォルダ|`<install>\.flashbeat-update\backup\FlashBeat-backup-<installKey>-<runId>` (`runId` = `yyyyMMdd-HHmmss-<pid>-<乱数>`)。旧レイアウト (インストール先の親フォルダ直下) の退避は**移動も削除もされず残る** (issue #670、警告ログのみ)|
|作業フォルダ|`<install>\.flashbeat-update\work\FlashBeat-update-work-<installKey>-<runId>`|
|所有 manifest|退避フォルダ内の `flashbeat-backup-owner.json` (インストールのパス・安定 ID・退避した版・作成日時)|

削除対象は **次の両方を満たす退避だけ** です。

1. 名前が **自インストールの名前空間内** (`FlashBeat-backup-<自 installKey>-<runId>`)
2. 所有 manifest が **自インストールの identity と一致** (安定 ID と正規化パスが両方一致)

したがって、

- **他インストールの退避**は残ります (保持理由 `other-namespace`)。
- **旧形式** (`FlashBeat-backup-<yyyyMMdd-HHmmss>`、manifest 無し = 所有不明) は
  **自動削除しません** (保持理由 `other-namespace` / `no-manifest`)。手動で消してください。
- manifest が壊れている / 所有が一致しない退避も残します
  (`invalid-manifest` / `owner-key-mismatch` / `owner-path-mismatch`)。
- 同じインストールについては **1 世代だけ残す** 契約は変わりません
  (今回の退避 1 世代 + 自インストールの旧世代は削除)。

保持・削除の判定はログ (`<install>\.flashbeat-update\logs\FlashBeat-update-<runId>.log`) に理由付きで出ます。

```text
[INFO] 退避を削除せず残します: C:\FlashBeat\.flashbeat-update\backup\FlashBeat-backup-flashbeat-0.1.14-20261108-010101-1111-aaaaaa (理由: other-namespace)
[INFO] 自インストールの旧世代の退避を削除しました: C:\FlashBeat\.flashbeat-update\backup\FlashBeat-backup-flashbeat-20261108-010101-1111-aaaaaa
```

> **注意 (旧レイアウトの残骸)**: issue #670 以降、更新処理はインストール先の
> **親フォルダには一切書き込みません**。以前のバージョンが作った
> `<親>\.flashbeat-update\` / `<親>\FlashBeat-backup-*` / `<親>\FlashBeat-update*.log` は
> 検出して「手動削除を推奨」の警告をログに出すだけで、**移動も削除もしません**。
> 不要であれば運営者が手動で消してください (中身は旧版の復旧データなので、
> 消す前に必要か確認してください)。

### 検証 (自動テスト)

`tools/update/tests/` に、PowerShell 5.1 / pwsh のどちらでも動く
Pester 非依存の検証スクリプトがあります。Windows 実機には依存せず、
macOS / Linux の pwsh でも同じテストが走ります。

```powershell
# スコープ (純粋関数 + 同じ親フォルダの 2 インストールの回帰)
pwsh -NoProfile -File tools/update/tests/Run-UpdateScopeTests.ps1

# 更新フロー結合 (実際に Update.ps1 を実行して切替・保持・失敗時挙動を確認)
pwsh -NoProfile -File tools/update/tests/Run-UpdateFlowTests.ps1

# macOS updater の install scope / 所有 manifest (bash・依存なし)
bash tools/update/tests/test-update-macos.sh
```

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

## update-macos.sh

`tools/update/update-macos.sh` は macOS 版のアップデータです。配布 ZIP では
`FlashBeat.app/Contents/Resources/tools/update/update-macos.sh` に同梱されます。

> **重要 (issue #670)**: `.app` バンドルは署名済みで、バンドル内への書き込みは
> 署名を壊すうえ境界モデル (「更新ファイルはインストール先フォルダ内」) とも
> 両立しないため、**`update-macos.sh` は `.app` を `-InstallDir` に受け取ると
> 安全側で拒否します** (非ゼロ終了・結果ファイルも境界外には書きません)。
> macOS 版のアプリ内更新フローも `UpdatePaths` がインストール先を解決できない
> (null) ため staging 自体を行いません。**macOS の更新は手動手順
> (「自動更新が不調なときの手動手順 > macOS」) を使ってください。**
> `.app` 以外の macOS レイアウトが必要になった場合は、署名・境界の両面から
> 別途設計が必要です。

以下はスクリプトの仕様 (非 `.app` レイアウト / テスト用途) です。

```sh
# 運営者が手動で適用 (タグ指定。手動実行は明示的に許可する)
# ※ -InstallDir に .app を渡すと拒否されます。非 .app レイアウト専用です。
FLASHBEAT_UPDATE_ALLOW_MANUAL=1 /bin/bash ./update-macos.sh \
    -InstallDir "$HOME/Desktop/FlashBeat" -Tag v0.1.14-macos-abc1234
```

### パラメータ

|パラメータ|説明|
|---|---|
|`-InstallDir`|更新する `FlashBeat.app` (親フォルダを渡すと中の `FlashBeat.app` を使う)。|
|`-ZipPath` / `-Tag`|取得元 (ローカル ZIP / GitHub Release タグ)。`-Tag` は GitHub から ZIP と `.sha256` を取得。|
|`-AssetName`|`-Tag` で取得するアセット名 (既定 `FlashBeat-<tag>.zip`)。|
|`-ExpectedSha256`|期待する SHA-256 (64 桁 hex)。未指定なら ZIP と同名の `.sha256` を参照。|
|`-Repo`|GitHub リポジトリ (`owner/repo`)。既定 `rebuildup/FlashBeat`。|
|`-ResultDir`|`last-result.json` の出力先 (既定 `<install>/.flashbeat-update/results`)。**インストール先フォルダ内でなければ拒否**。|
|`-LogDir` / `-LogPath`|ログ出力先ディレクトリ / ファイル (既定 `<install>/.flashbeat-update/logs/FlashBeat-update-<runId>.log`、最新 20 世代を保持)。**インストール先フォルダ内でなければ拒否**。|
|`-Apply`|アプリからの適用であることを示す。アプリは常に付ける。|
|`-WaitPid <PID>`|指定 PID の FlashBeat 終了を待ってから更新 (最大 300 秒)。待機後に他の FlashBeat が実行中なら中止。|
|`-Relaunch`|更新成功後に新しい配置を起動する。|

### 動作

0. `.app` を `-InstallDir` に受け取った場合は**ここで拒否**します (終了コード 1、
   境界外への書き込みは一切行いません)。
1. `-WaitPid` 指定時はそのプロセスの終了を待ちます (二重起動なら終了コード 6 で中止)。
2. ZIP を取得し、SHA-256 を検証します (不一致なら終了コード 3 で中止)。
   展開物にシンボリックリンク等のエントリがあれば拒否します (終了コード 4)。
3. `ditto -x -k` で `<install>/.flashbeat-update/work/` 配下の一時ディレクトリに展開し、
   実行ファイルとデータフォルダを検査します。
4. インストール先の中身を `<install>/.flashbeat-update/backup/FlashBeat-backup-<installKey>-<runId>/`
   へ退避してから新しい配置を置きます (失敗時は退避から復元)。
   切替時に `.flashbeat-update` 自身は退避対象から**除外**します
   (退避先がその内側にあるため、これを含めると backup が自身の中に移動して失敗します)。
   退避には所有 manifest (`flashbeat-backup-owner.json`、installKey + installPath) が付き、
   古い退避は Windows 版と同じ「名前空間 + manifest 一致」の判定で 1 世代だけ残します
   (他インストールの退避・所有不明のものは保持)。
   実行ログは `<install>/.flashbeat-update/logs/` に置かれ、
   インストール先の**外**に `FlashBeat-*` や `.flashbeat-update` は新規生成されません
   (旧レイアウトの残骸は検出して警告ログのみ、移動・削除はしません)。
5. `-Relaunch` 指定時は新しい配置を起動します。

終了コードは `Update.ps1` と同じ (0 成功 / 1 パラメータ / 2 通信 / 3 SHA / 4 ZIP / 5 内容 / 6 実行中 / 7 切替)。
結果は成功・失敗にかかわらず `last-result.json` に書かれますが、**インストール先フォルダ内にのみ**
書き込みます (境界外の `-ResultDir` は拒否)。

## 自動更新が不調なときの手動手順

### Windows

1. Releases から ZIP と `.sha256` を取得 (`gh release download <tag> -R rebuildup/FlashBeat`)。
2. SHA-256 を確認 (`Get-FileHash .\FlashBeat-<タグ>.zip -Algorithm SHA256` が `.sha256` と一致)。
3. ゲームを通常終了 → 既存フォルダを退避 → ZIP を展開して中身を丸ごと置換 → 退避を削除。
4. `FlashBeat.exe` を直接起動して題名画面まで進むことを確認。

### macOS

1. Releases から macOS の ZIP と `.sha256` を取得する。
2. `shasum -a 256 -c FlashBeat-<タグ>.zip.sha256` で照合する。
3. アプリを終了 → 現在の `FlashBeat.app` を退避 → ZIP を展開し、中の `FlashBeat.app` を置く。
4. `open FlashBeat.app` で起動して題名画面まで進むことを確認。
   (`update-macos.sh` は `.app` を更新対象として受け付けないため、自動更新は使えません。issue #670)

## 運用上の注意

- ゲームフォルダ内に運営者が別途置いたファイルは、更新後に残りません (フォルダ全体 / `.app` 全体を置換するため)。設定・スコアの保存先は Windows: `%USERPROFILE%\AppData\LocalLow\...`、macOS: `~/Library/Application Support/...` 側なので失われません。
- 更新の適用はユーザーが通知 (F3 / クリック) や Option ボタンで確認したときだけ行われる。終了の仕方 (通常終了 / タスクマネージャー / 強制終了) は適用の可否に影響しない。
- 配布 ZIP に `FlashBeat_BurstDebugInformation_DoNotShip/` は含めません (Unity の指示による ship 対象外)。
- macOS の旧版は `<install>/.flashbeat-update/backup/FlashBeat-backup-<installKey>-<runId>/` として 1 世代残ります (所有 manifest 付き、非 `.app` レイアウトの場合)。所有が確認できない旧形式の退避は自動削除されず残ります。`.app` の更新自体は issue #670 で拒否されるため、macOS は手動手順で更新します。
- テスト用 Release (prerelease) は、**stable チャンネル (展示用) の更新候補にはなりません**。検証したいときだけ preview チャンネルでビルドするか、運営者が `Update.ps1 -Tag` / `update-macos.sh -Tag` を手動実行します。

## 関連

- Release ZIP と SHA-256 の配布仕様: Issue #35
- 実機プレイ / 再起動 / API 疎通の証拠ゲート: Issue #22
- 実装: `Assets/Game/Scripts/Update/UpdateChecker.cs` (起動時確認・ユーザー起点のダウンロード/適用) と `Assets/Game/Scripts/UI/UpdateNoticeUI.cs` (Title の通知)、`Assets/Editor/BuildScript.cs` (版識別子の埋め込み・メディア除外・スクリプト同梱)、`tools/update/update-macos.sh` (macOS の適用)
