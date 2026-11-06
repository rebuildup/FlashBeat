# 展示 runbook（運営者向け）— FlashBeat Win64

対象 Issue: **#41**（展示成立と継続開発）の残りタスク「展示運営者向け runbook」。
読み手: 高専祭の展示 PC でゲームを起動する**運営者**。技術用語は最小限にする。

関連: [`festival-runbook.md`](festival-runbook.md)（旧世代の展示記録・詳細版） /
[`update-patch.md`](update-patch.md)（更新の仕組みと `Update.ps1` の仕様） /
[`update-release.md`](update-release.md)（配布物の Release 仕様） /
[`festival-development-path.md`](festival-development-path.md)（展示中の継続開発経路）。

---

## 0. 先に 5 行で

1. **起動は `FlashBeat.exe` をダブルクリックするだけです。**
2. **常設ランチナーも `Play.cmd` / `Update.cmd` / ロールバック UI も存在しません。** 探す必要はありません。
3. **譜面は Web Studio から D1 経由で入ります。** Unity を再ビルドする必要はありません。
4. **起動時に裏で GitHub Releases を確認**します。展示用のビルドは *stable* チャンネルなので、テスト用 Release（prerelease）は自動で適用されません。
5. **オフラインでもゲームは止まりません。** 取得できないときは内蔵の固定カタログへ落ちます。

---

## 1. 展示 PC 上の配置

### 1.1 展示フォルダ（運営者が触る場所）

```
C:\Users\rebui\Desktop\FlashBeat-Exhibit\
├─ FlashBeat.exe              ← 運営者はこれを直接起動する
├─ FlashBeat_Data\            （Managed DLL / 同梱リソース / StreamingAssets）
├─ MonoBleedingEdge\  D3D12\  UnityPlayer.dll  UnityCrashHandler64.exe  DirectML.dll  dstorage*.dll
├─ tools\update\Update.ps1    （配布物に同梱された更新スクリプト。§6 で使う）
└─ version.json               （同梱メタデータ。人が読む用）
```

* 根拠: [`update-patch.md`](update-patch.md) の「方針」— 展示アプリは GitHub Release の配布成果物を
  `C:\Users\rebui\Desktop\FlashBeat-Exhibit\` に配置したもの。`D:\` や worktree の `Builds/` は展示に使いません。
* **このフォルダの中身を直接編集しないでください**（更新はフォルダ丸ごと差し替えになり、置いたファイルは消えます。§8）。

### 1.2 ログ・退避・ステージングの場所

| もの | 場所 | 読むだけ / 触る |
|---|---|---|
| 更新の実行ログ | 展示フォルダの**親**フォルダ直下 `FlashBeat-update.log` | 読むだけ |
| 取得済み ZIP と manifest | `%LOCALAPPDATA%\FlashBeat\update\staging\` | 読むだけ（消してもよい） |
| 前の世代（更新前の退避） | 展示フォルダの**親**フォルダ直下の `FlashBeat-backup-<年月日-時刻>\` | §7 の復旧で使う |
| プレイヤーログ | `%USERPROFILE%\AppData\LocalLow\KimuraYusuke\FlashBeat\Player.log`（既定） | 読むだけ |

親フォルダ = `C:\Users\rebui\Desktop\`。退避とログはここに出ます（`Update.ps1` が
インストール先の**親**に作るため）。

---

## 2. 起動する

1. ファイルエクスプローラーで `C:\Users\rebui\Desktop\FlashBeat-Exhibit\` を開きます。
2. **`FlashBeat.exe` をダブルクリック**します（ショートカットから起動しても同じです）。
3. タイトル画面（`FlashBeat` ロゴ）まで **実測 14〜18 秒**（実機 run 3 回: 17.8 s / 14.0 s / 14.1 s）。
   初回はウイルス対策ソフトやディスクキャッシュでさらに時間がかかることがあります。
   **固まったように見える時間は、そのまま待ってください。**
4. 起動しない / 黒画面のまま → §7 の復旧。

運営者がログを取りたいとき（**起動経路は変えません**）:

```powershell
& 'C:\Users\rebui\Desktop\FlashBeat-Exhibit\FlashBeat.exe' `
    -logFile 'C:\Users\rebui\Desktop\FlashBeat-Exhibit\logs\Player-2026-11-07-2300.log'
```

操作（キーボード only）の早見表は [`festival-runbook.md` §3](festival-runbook.md) を参照してください。

---

## 3. 起動時に「裏」で起きること（自動更新）

運営者が操作する必要はありません。**起動したあと、ゲームを終了するときに自動で更新されます。**

| タイミング | 裏で起きること |
|---|---|
| 起動 | GitHub Releases の一覧を非同期で確認（15 秒タイムアウト）。**ゲームは止めません** |
| 起動 | 新版があれば ZIP と `.sha256` を `%LOCALAPPDATA%\FlashBeat\update\staging` に取得し、**SHA-256 を検証** |
| Option 画面 | staging 済みの新版があるときだけ「更新を適用して再起動 (<タグ>)」ボタンが**出ます**（押せば即適用+再起動） |
| 終了（ウィンドウを閉じる） | staging 済みの新版があれば `tools\update\Update.ps1` が別プロセスで起動され、**アプリ終了待ち → 別フォルダへ展開 → 検証 → 一括切替 → 新 exe の自動再起動** |
| 再起動 | 新版になっています。旧版は `FlashBeat-backup-*` として 1 世代だけ残ります |

補足（判断に必要な事実）:

- **自動更新の候補は「バージョンが厳密に大きい」Release のみ**です。運用ルールとして
  **修正版を出すたびに版数を上げて** Release します（同じバージョンで別 SHA の Release は公開しません）。
  詳細は [`update-release.md` の版数ルール](update-release.md)。
- **更新チャンネル**はビルド時に `FlashBeat_Data\StreamingAssets\update-channel.txt` へ書き出されます。
  展示用ビルドは `stable` = **prerelease（テスト用 Release）は自動適用されません**。
  ファイルが無い・読めない・未知の値のときは安全側に倒して `stable` になります。
  起動時の Player.log に `[UpdateChecker] 更新チャンネル: stable (prerelease を更新候補に含める: no)` と出ます。
- **失敗しても既存配置は壊れません。** SHA 不一致 / ネットワーク失敗 / 二重起動では
  既存フォルダを一切変更せず、ゲームはそのまま遊び続けます。
- **タスクマネージャーでの強制終了では更新は走りません**（`OnApplicationQuit` が呼ばれないため）。
  ウィンドウを閉じるという通常の終了操作をしてください。

---

## 4. 譜面とカタログ（Unity の再ビルドは不要）

- 譜面とカタログは **Web Studio から D1 経由で** ゲームに入ります。編集と公開は
  Mac 側の Web Studio で行い、公開（publish）した結果が本番 API に入ります
  （[`web/AGENTS.md`](../../web/AGENTS.md) / [`d1-migrations.md`](d1-migrations.md)）。
- Unity 側は API のベース URL を `Assets/Game/Resources/CloudCatalogConfig.json` の `baseUrl` から読みます。
  **このファイルを差し替えれば、再ビルドなしで参照先を変更できます。**
- 取得タイミング: **選曲画面を開いたとき**にカタログを取り、以後 `refreshSeconds` 秒ごと
  （同梱設定の既定は 8 秒）に取り直します。再生時に譜面本体
  （`GET /api/v1/charts/{songId}?version=N`）を取得します。
- つまり、**新しい曲を公開すれば、次の選曲画面から選択可能**になり、譜面データも入っています。

---

## 5. オフライン / 取得失敗時の挙動

| 事象 | 挙動 | ログ |
|---|---|---|
| カタログ取得失敗（オフライン / HTTP エラー / 空または壊れた応答） | **内蔵の固定カタログへフォールバック**して選曲画面は動き続けます。ゲームは止まりません | `[SelectScene] catalog unavailable (status=...) - using the built-in song list` |
| 譜面取得失敗（404 / ネットワーク断 / 壊れた応答） | **その曲の開始を取り消し**、選曲画面に留まります。ほかの流れは壊れません | `[SelectScene] chart for '<songId>' v<n> unavailable (status=...) - start cancelled` |
| API 設定が無い / 無効 | 固定カタログのまま（API を参照しません） | — |
| YouTube 映像・音声の取得失敗 | ログに `yt-dlp` / `VideoPlayer` の失敗が残ります。固定カタログの曲を選んで再開してください | `[MusicManager] ...` |

- 内蔵カタログの曲の譜面 JSON は同梱のままなので、**譜面データの取得はオフラインでも成立**します。
  映像・音声は YouTube からのストリームなので、そこだけネット接続が必要です。
- 展示中にネットが切れても、**再起動すれば元に戻ります**。前の世代で起動することもできます（§7）。

---

## 6. 展示中に運営者がパッチを適用する手順

**「ゲームを終了 → パッチを 1 回だけ適用 → `FlashBeat.exe` を直接起動」** が全部です。
訪問者セッション中の強制更新は行いません。

### 6.1 手順

1. **ゲームを通常終了**します（Result 画面の `Space` → 選曲 → `Esc` → タイトルで `E`）。
   実行中の `FlashBeat.exe` が残っていると適用は中止されます（終了コード 6）。強制終了はしません。
2. GitHub Releases（`https://github.com/rebuildup/FlashBeat/releases`）から
   **ZIP と `.sha256` の 2 つ**を取得します。ブラウザでダウンロードするだけです。
   ファイル名は `FlashBeat-<バージョン>-win64-<短縮SHA>.zip` と
   `FlashBeat-<バージョン>-win64-<短縮SHA>.zip.sha256` です。
3. **SHA-256 を検証**します（一致しなければ再ダウンロード）:

   ```powershell
   Get-Content .\FlashBeat-<バージョン>-win64-<短縮SHA>.zip.sha256
   Get-FileHash .\FlashBeat-<バージョン>-win64-<短縮SHA>.zip -Algorithm SHA256
   ```

4. **適用**します。2 通りあります。

   **(a) `Update.ps1` を使う（推奨）** — スクリプトを**展示フォルダの外**に置いた状態で実行します
   （展示フォルダ内の `tools\update\Update.ps1` を手動実行する場合は `-Apply` が必要。無いと終了コード 1 で中止します）。

   ```powershell
   # 入手した ZIP をローカルに置いた場合
   powershell -NoProfile -ExecutionPolicy Bypass -File C:\Users\rebui\Desktop\FlashBeat-Tools\Update.ps1 `
       -InstallDir 'C:\Users\rebui\Desktop\FlashBeat-Exhibit' `
       -ZipPath 'C:\Users\rebui\Desktop\Downloads\FlashBeat-<バージョン>-win64-<短縮SHA>.zip'
   ```

   ```powershell
   # タグ指定で Releases から取得する場合（この方法だけ gh CLI が必要。無いときは上の -ZipPath を使う）
   powershell -NoProfile -ExecutionPolicy Bypass -File C:\Users\rebui\Desktop\FlashBeat-Tools\Update.ps1 `
       -InstallDir 'C:\Users\rebui\Desktop\FlashBeat-Exhibit' `
       -Tag v<バージョン>-win64-<短縮SHA>
   ```

   **(b) 手動差し替え** — 上記が使えないとき:

   1. 既存フォルダ `FlashBeat-Exhibit\` を `FlashBeat-backup-<年月日-時刻>\` に**リネーム**（退避）。
   2. 展開した ZIP の中身を `FlashBeat-Exhibit\` へ**丸ごと置換**（中身を足すだけにはしません）。
   3. `FlashBeat.exe` を直接起動して題名画面まで進むことを確認。

5. **起動確認**: `FlashBeat.exe` を直接起動し、タイトル → 選曲 → 1 曲プレイ → リザルトまで通ることを確認します。

### 6.2 結果の見分け方

適用スクリプトは終了コードで結果を返します。**既存配置が変更されるのは成功（0）のときだけです。**

| コード | 意味 | 既存配置 |
|---|---|---|
| 0 | 更新成功 | 更新済み |
| 1 | パラメータ / 前提不備 | 変更なし |
| 2 | ネットワーク / ダウンロード失敗 | 変更なし |
| 3 | SHA-256 不一致 | 変更なし |
| 4 | ZIP 破損 / 展開失敗 | 変更なし |
| 5 | 展開物の内容検査失敗 | 変更なし |
| 6 | FlashBeat 実行中（二重起動 / 待機タイムアウト） | 変更なし |
| 7 | 切替 / ファイル操作の失敗 | 退避データから復元を試みる |

---

## 7. 起動できなくなったときの復旧

| 症状 | 対応 |
|---|---|
| 起動直後に黒画面 / 強制終了 | まず普通に再起動します（初回は 60〜90 秒かかることがあります） |
| 再起動しても壊れている | **前の世代で起動します**: 親フォルダの `FlashBeat-backup-<年月日-時刻>\FlashBeat.exe` を直接起動します（中身の他のファイルはそのままで構いません） |
| 前の世代も壊れている | **ZIP を再展開**します: Releases から ZIP + `.sha256` を取得 → SHA-256 検証 → 展示フォルダを退避名にリネーム → ZIP の中身で置換 → `FlashBeat.exe` を起動 |
| 切替の途中で失敗した | 退避フォルダ `FlashBeat-backup-*` には旧配置が残っています（スクリプトが復元を試み、7 のときはログに記録します） |
| 原因を調べたい | `C:\Users\rebui\Desktop\FlashBeat-update.log`（更新の記録）と Player.log（ゲーム側）を見ます。Issue に貼るときは必ずマスクします（§9） |

- **ロールバック UI / ロールバックコマンドは存在しません。** 復旧は「前の世代の exe を起動する」か
  「ZIP を再展開する」の 2 通りだけです。
- バージョンを戻す（ダウングレード）する仕組みはありません。古い版へ戻す必要がある場合は、
  別のバージョンとして Release を出す（版数は必ず上げてから公開します）運用です。

---

## 8. やってはいけないこと

- **展示中に Unity Editor を起動しない。** 展示 PC は展示専用です（理由と代替経路は
  [`festival-development-path.md`](festival-development-path.md) §2・§6）。
- **ビルド出力フォルダを直接触らない。** `Builds/`、worktree 内の出力、`D:\4_apps\Unity\Build\...`
  などの作業フォルダは展示物ではありません。展示に置くのは **GitHub Release の ZIP** だけです。
- **展示フォルダ（`FlashBeat-Exhibit\`）の中のファイルを編集しない。** 更新はフォルダ丸ごとの
  差し替えなので、置いたファイルは消えます。設定・スコアはゲームフォルダではなくユーザー側
  （PlayerPrefs / `%USERPROFILE%\AppData\LocalLow\...`）に保存されるので失われません。
- **`Play.cmd` / `Update.cmd` / `Rollback.cmd` / 常設ランチナーを探さない。** 存在しません（作らない方針）。
  更新の入口は「`FlashBeat.exe` を起動する」と「§6 の手順」だけです。
- **実行中の `FlashBeat.exe` を強制終了しない。** 更新が走りません。ウィンドウを閉じてください。
- **Release の一時 ZIP を展示フォルダの中に置かない。** `.sha256` は ZIP と同名・同フォルダに必要です。
- **秘密値（Cloudflare トークン、publish 用トークンなど）を展示 PC に置かない・出力しない。**
- **API や D1 に直接触らない。** 譜面の公開は Web Studio からの publish で行います。

---

## 9. 記録とエスカレーション

- 口頭だけが証拠になりません。復旧した・異常があった場合は **GitHub Issue にコメント**として残します。
- 対象 Issue の分け方:

| 対象 | Issue | 貼るもの |
|---|---|---|
| ビルドが起動しない / 固まる / 譜面の不整合 | [#11](https://github.com/rebuildup/FlashBeat/issues/11) | プレイヤーログ + スクリーンショット |
| 音が出ない / ストリームが再生されない | [#13](https://github.com/rebuildup/FlashBeat/issues/13) | プレイヤーログの `[MusicManager]` 行 + 曲名 |
| 日本語が `□` になる | [#27](https://github.com/rebuildup/FlashBeat/issues/27) | select / game / result のスクリーンショット |
| パッチの配布（Release 作成） | [#35](https://github.com/rebuildup/FlashBeat/issues/35) | 必要な SHA / 検証結果 |
| パッチの適用（`Update.ps1`） | [#36](https://github.com/rebuildup/FlashBeat/issues/36) | 適用ログ、適用前後の `FlashBeat.exe` の SHA-256 |
| リリース判定・証跡の矛盾 | [#22](https://github.com/rebuildup/FlashBeat/issues/22) | どの成果物・どの SHA についての判断かを明記 |
| 展示全体の運用 | [#41](https://github.com/rebuildup/FlashBeat/issues/41) | 状況と必要な決定 |

- ログを Issue に貼る前に必ずマスクします:

  ```powershell
  powershell -NoProfile -ExecutionPolicy Bypass -File release/check/redact-log.ps1 `
      -InPath 'C:\Users\rebui\Desktop\Player.log' `
      -OutPath 'C:\Users\rebui\Desktop\Player.redacted.log'
  powershell -NoProfile -ExecutionPolicy Bypass -File release/check/redact-log.ps1 `
      -InPath 'C:\Users\rebui\Desktop\Player.redacted.log' -Check
  ```

---

## 10. この runbook が確定していない点

| # | 未確定の点 | 現状の扱い |
|---|---|---|
| 1 | 展示フォルダのパス | 本書は `C:\Users\rebui\Desktop\FlashBeat-Exhibit\`（[`update-patch.md`](update-patch.md) の記載）を正とします。旧世代の `D:\4_apps\Unity\Build\FlashBeatExhibit*` は [`festival-runbook.md`](festival-runbook.md) に記録された旧配置で、参照のみ。**どちらが最終的な展示配置かは監督者の決定事項**です |
| 2 | 展示 PC に実際にどの版が置かれているか | 本書では版数を確定値として書きません。確認は `version.json` / 親の `FlashBeat-update.log` / 退避された旧世代を見ます |
| 3 | 自動更新の候補判定の実装 | 本書は「バージョンが厳密に大きい Release のみ」を前提に書いています。実装をこの方針に揃える対応は監督側の担当事項です（[`update-release.md`](update-release.md) の版数ルール参照） |
| 4 | `gh` CLI を展示 PC に置くか | `-Tag` 指定の適用には `gh` が必要です。置かない運用なら §6.1(b) の手動差し替え、または `-ZipPath` を使います |
| 5 | 展示 PC の台数 | 現状は Windows 実機 1 台（Unity Editor を含む）のため、展示と開発の両立には CI が必要です（[`festival-development-path.md`](festival-development-path.md)） |
