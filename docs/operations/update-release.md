# Win64 / macOS リリース運用手順 (Issue #35)

展示会場の Windows 機・Mac に Unity 本体 (`FlashBeat.exe` / `FlashBeat.app`) のパッチを配布する手順。
アプリ自体の起動・更新フロー (起動時非同期確認 → **Title 通知 → ユーザー明示でダウンロード/適用**、issue #118) は Issue #36 (`tools/update/**`) が実装する。
本ドキュメントは **配布物の仕様 = #36 の UpdateChecker / アップデータが消費する契約** を固定する。
常設ランチャー・独自更新サーバーは作らない。更新元は GitHub Releases のみ。

## 対応プラットフォーム

| プラットフォーム | ビルド入口 | 配布アセット | 適用スクリプト |
|---|---|---|---|
| Windows 64 | `BuildScript.BuildStandaloneWindows64` | `FlashBeat-<semver>-win64-<sha>.zip` | `tools/update/Update.ps1` |
| macOS | `BuildScript.BuildStandaloneMacOS` | `FlashBeat-<semver>-macos-<sha>.zip` | `tools/update/update-macos.sh` |

Release は **プラットフォーム別** に作る (`v<semver>-win64-<sha>` と `v<semver>-macos-<sha>` は別 Release)。
UpdateChecker は実行中のプラットフォームのタグ・アセットだけを候補にするため、
Windows 版と macOS 版が互いの更新候補になることはない。

## 配布仕様 (#36 との契約・固定)

### タグ / アセット命名

- タグ: `v<semver>-<platform>-<shortsha>` (例: `v0.1.1-win64-743c88d`, `v0.1.1-macos-743c88d`)
  - `<semver>` = `X.Y.Z`、`<platform>` = `win64` / `macos`、`<shortsha>` = ビルド元 commit の短縮 SHA (7〜40 hex)
- アセット: `FlashBeat-<semver>-<platform>-<shortsha>.zip` と `FlashBeat-<semver>-<platform>-<shortsha>.zip.sha256` の 2 点
- ZIP ルートに `version.json` を同梱 (パッケージスクリプトが自動生成):

  ```json
  {
    "version": "0.1.1",
    "buildSha": "<40 桁フル commit SHA>",
    "tag": "v0.1.1-macos-743c88d",
    "platform": "macos",
    "mediaPolicy": "bundled | free | song-free",
    "createdAt": "2026-11-06T15:39:27Z"
  }
  ```

- UpdateChecker は実行中ビルドの版識別子 (`Application.version` = `bundleVersion` = `<version>+<shortsha>`) を読み、
  同じプラットフォームの GitHub Releases 候補タグと比較して新版を判定する
  (`version.json` は人が読むための同梱メタデータで、判定には使わない)。

### .sha256 sidecar 形式

- `<64 桁小文字 hex>  <ファイル名>` + LF、UTF-8 (BOM 無し)。`sha256sum -c` / `shasum -a 256 -c` / `Get-FileHash` に対応。

### 候補の判定 (実装に合わせる)

- `GET https://api.github.com/repos/rebuildup/FlashBeat/releases?per_page=30` (認証不要) を使う。
  GitHub の返却順は新しい順とは限らない (実測) ため、`/releases/latest` の 1 件には依存しない。
- 候補の採用条件: `draft = false` かつ `tag_name` が
  `^v\d+\.\d+\.\d+-(win64|macos)-[0-9a-f]{7,40}$` に一致し、
  実行中のプラットフォーム token と一致し、対応 ZIP + `.sha256` の 2 アセットが存在すること。
  **prerelease はチャンネルで決まります** (stable は対象外、preview は対象)。
- 候補の順序付けは **version を主キー・published_at を副キーに最大**。現行ビルドより新しいものだけを更新扱いにする (ダウングレードしない)。
- 不一致・取得失敗・SHA 検証失敗時は更新を中止し現行版を残す (ゲーム動作は止めない)。

### メディア排除 (第三者楽曲)

- **配布 ZIP に第三者楽曲の音声・動画を含めない** (オーナー要件 2026-11-07)。
- ルーズなメディア (`*.wav/*.mp3/*.mp4/...`) がビルド出力にあればスクリプトが検知して失敗する。
- `Assets/Game/Resources/**` のメディアは Unity が `resources.resource` に焼き込むため ZIP レベルでは除去不能。
  そこでビルド (`BuildScript.ExcludeMediaFromBuild`) が `Resources/Musics` と `Resources/Videos/UnityYoutube`
  (第三者楽曲の音声・動画) をビルド中だけ退避し、焼き込まないようにしている。
- **不含の証明**: ビルドは `flashbeat-media-policy.json` (除外したパス) を配布物に出力する。
  `-MediaPolicy song-free` はこの manifest を確認し、第三者楽曲メディアが外されていないと失敗する。
  さらに `resources.resource` のサイズ上限 (既定 300MB) でも検出する (漏れていれば 1.4GB になる)。
- ゲーム自身の背景アニメーション (`Resources/Videos/*.mp4`、約 196MB) は自作素材なので同梱する
  (media-rights-audit 3: 権利ではなくサイズの問題)。

### 配布対象の Release ルール (Issue #77 / 監査結果)

- **media policy 導入前 (2026-11-06 頃) のビルドは配布リストに載せない**。
  第三者楽曲メディアを `resources.resource` に焼き込んだ公開 Release は
  `v0.1.0-win64-743c88d` の **1 件だけ** (実測: `resources.resource` = 1,399,081,540 bytes。
  他 16 件はすべて 196,446,346 bytes)。
- 展示用 ZIP のダウンロードページには **stable チャンネルの最新安定版だけ** を載せる。
  prerelease / 旧世代 ZIP は掲載しない。
- **新しい Release を出したら発行前に `tools/release/audit-release-media.py` を実行**し、
  同梱メディア 0 件と `flashbeat-media-policy.json` の存在を確認する。
  判定手順・全 Release の実測結果・推奨は
  [`docs/operations/release-media-audit.md`](release-media-audit.md) に置き、
  新しい Release を出したらその表に追記する。

```bash
GITHUB_TOKEN=$(gh auth token) python3 tools/release/audit-release-media.py
```

### 更新フローの契約 (実装は #36 / 明示式化は #118)

1. 起動時に latest を非同期**確認**するだけ (ブロックしない。失敗はログのみ)
2. 新版があるとき、**Title シーン (ホーム) にだけ**『アップデートあり vX.Y.Z』の通知を出す
   (コード構築 overlay。F3 キー / 通知クリックでダウンロード開始、F4 / × でこのセッションは非表示)
3. **ユーザーがトリガーしたときだけ** ZIP + `.sha256` を staging へ取得し SHA-256 検証 (進捗 % を通知内に表示)
   - Windows: `%LOCALAPPDATA%\FlashBeat\update\staging`
   - macOS: `~/Library/Application Support/FlashBeat/update/staging`
4. staging 完了後、通知は『更新の準備ができました。終了して適用 [F3]』に変わる。
   ユーザーが F3 / クリック (または Option の適用ボタン) で**確認したときだけ**、配布物内の
   アップデータを分離プロセスで起動する:
   - Windows: `tools\update\Update.ps1` (アプリ PID の終了待機 → 別ディレクトリへ展開 → 検証 → 一括切替 → 新 exe 再起動)
   - macOS: `Contents/Resources/tools/update/update-macos.sh` (アプリ PID の終了待機 → 一時ディレクトリへ展開 → 検証 → `FlashBeat.app` を退避して切替 → 新 app 再起動)
5. **押さなければ更新しない**: 確認なしで普通に終了した場合は何も適用されない
   (staging 済み ZIP は残り、次回起動時にまた通知が出る)。`OnApplicationQuit` からの自動適用は存在しない
6. 旧版は 1 世代残す (切戻し用)
7. 失敗時 (SHA 不一致 / ネットワーク断 / 二重起動) は一行メッセージを表示して更新を中止し現行版を維持
   (ゲーム動作は止めない)

### 更新結果ファイル (last-result.json、issue #125)

`tools\update\Update.ps1` / `update-macos.sh` は**すべての終了経路**で
`<update dir>\last-result.json` に機械可読な結果を書く
(アプリからの自動適用でも手実行でも。書けない場合も更新処理自体は失敗させない)。
`<update dir>` は Windows: `%LOCALAPPDATA%\FlashBeat\update`、
macOS: `~/Library/Application Support/FlashBeat/update`。

```json
{
  "result": "success | failed | aborted",
  "exitCode": 0,
  "fromVersion": "0.1.2",
  "toVersion": "0.1.3",
  "tag": "v0.1.3-win64-abc1234",
  "message": "SHA-256 が一致しません。ZIP が破損、または想定と違うファイルです。",
  "timeUtc": "2026-11-07T00:00:00Z"
}
```

- `result` の対応: `aborted` = 前提不備で何もしていない (終了コード 1 パラメータ/前提不備、
  6 FlashBeat 実行中)。`failed` = 適用を試みて失敗 (2 通信 / 3 ダイジェスト / 4 ZIP / 5 内容 /
  7 切替、想定外エラー)。`success` は切替と配置後検査が**完了したあとにだけ**書く
  (再起動の成否には依存しない)。
- `message` は `FlashBeat-update.log` と同じ日本語の理由。
- アプリは次回起動時にこのファイルを読み、**Title シーンで 1 回だけ**通知する:
  - `failed` / `aborted`: 『前回の更新に失敗しました: \<message\> - 現在の版 vX を使用中 / F3 で再試行』
  - `success`: 『更新しました vX』
- 表示した時点でファイルを `last-result-shown.json` へリネームし、次回起動以降は出さない
  (ゲームをブロックしない。F3 での再試行は新版がまだ見つかっているときだけ効く)。
- したがってスクリプト起動時のアプリのログは
  `update script started (result unknown until next start)` であり、**成否はこの時点では確定しない**。
  確認は結果ファイル (または `FlashBeat-update.log`) で行う。
- staging / 適用の直前に、対象 ZIP の版が現行ビルドより新しいことを再確認する
  (`UpdateChecker.IsStagedTagNewerThanCurrent`)。旧版・同一バージョンの別ビルドは
  staging も適用もしない (ダウングレード防止)。

## 取得先

- GitHub Releases: `https://github.com/rebuildup/FlashBeat/releases`
- 現行版: [`v0.1.14-win64-617c2b6`](https://github.com/rebuildup/FlashBeat/releases/tag/v0.1.14-win64-617c2b6)
  - `FlashBeat-0.1.14-win64-617c2b6.zip`
  - SHA-256: `3ecf9d8ea65a435b4e99b70a0ed8691859968bc2c06da6e56b4366750e124171`
  - ビルド元 commit: `617c2b6ad2da6b6735f511f50f85ba7597036329`
    (Issue #168: プレイフィールドのフラット描画化 (unlit + シャドウ無効) + 判定エフェクトを踏んだレーンへ表示)
  - メディア方針: `song-free` (第三者楽曲は非同梱、`flashbeat-media-policy.json` で証明)
  - チャンネル: `stable`
  - 実機検証: **未証明** (Windows 展示機での実プレイは未実施。
    EditMode 407/407 成功 + `FlatRenderCheck` `audit: lights=0 casters=0 receivers=0` / `failures 0` のみ)。
    Issue #22 の承認チェックリスト上は「部分承認」相当で記録。
- 前版 (参考): [`v0.1.13-win64-5ee86a9`](https://github.com/rebuildup/FlashBeat/releases/tag/v0.1.13-win64-5ee86a9)
  - `FlashBeat-0.1.13-win64-5ee86a9.zip`
  - SHA-256: `8bcfafcf7cbde66cad977d164cdefdece709f989266948d4a5eb52c309f21548`
  - ビルド元 commit: `5ee86a95efc5d2b3dd022506aa4c180e9ee50e6a`
    (v5 UI / キーガイド / 読み込み画面 / Issue #148 キーキャップ / 初期解像度修正 / Update.ps1 の BOM 修正)
  - 実機検証: Windows 展示機相当で ZIP 展開 → 起動 → Title まで到達 / overlay 5/5 / keyconfig 30/30 を確認。
- macOS 現行版: GitHub Release は未発行。`~/Desktop/FlashBeat-Mac` の
  ブートストラップ `0.1.14+716a25f` (commit `716a25f`) が起点で、以降の macOS 版は
  `v<semver>-macos-<sha>` Release とアプリ内更新で配布する
  (手動ビルドした `.app` を Desktop へ置く運用は廃止。旧 `v0.1.5+8e712b3` は同フォルダに
  `FlashBeat-backup-*.app` として退避)。
- 前々版 (参考): [`v0.1.11-win64-5f94d12`](https://github.com/rebuildup/FlashBeat/releases/tag/v0.1.11-win64-5f94d12)
  - Web Studio 併入版。`v0.1.11-win64-98137a5` は同じ 0.1.11 の別ビルド (prerelease)。
    展示機には **0.1.13 が適用済み** (2026-11-07)。
- 旧版 (参考): [`v0.1.0-win64-743c88d`](https://github.com/rebuildup/FlashBeat/releases/tag/v0.1.0-win64-743c88d)
  - `FlashBeat-0.1.0-win64-743c88d.zip`
  - SHA-256: `3dbdfecbdb91d916178666045d0c62e1fae717f7df0b77e4f9ddc2af1c036a45`
  - ビルド元 commit: `743c88d14a1931d4637c34b1ec5d1bec5e502d44` (#27 フォント修正済み)
  - **注記**: このリリースは第三者楽曲を `resources.resource` に焼き込みで含む (`version.json` 無し・旧仕様)。
    実測 (`resources.resource` = 1,399,081,540 bytes / ZIP = 1,459,717,135 bytes) と
    17 件の判定結果・推奨は [`docs/operations/release-media-audit.md`](release-media-audit.md) に記録済み。
    扱い (公開維持 / 差し替え / 非公開化) はオーナー決定事項 — ワーカーは触らない。
    2026-11-08 時点でこの Release は `prerelease = true` (stable チャンネルの更新候補にはならない)。
- アセット命名: `FlashBeat-<semver>-<platform>-<shortsha>.zip` + `.zip.sha256`
  - 例: `FlashBeat-0.1.0-win64-743c88d.zip` / `FlashBeat-0.1.14-macos-abc1234.zip`
- 展示運営者はブラウザでダウンロードするだけ。GH/CLI/PowerShell・管理者権限はプレイヤーに要求しない。

## ZIP の中身

### Windows 64

Unity Win64 ビルド出力フォルダ**一式** (exe 単体ではない) + `version.json`:

- `FlashBeat.exe` / `UnityPlayer.dll` / `UnityCrashHandler64.exe`
- `FlashBeat_Data/` (Managed DLL 群・リソース一式)
- `MonoBleedingEdge/`, `D3D12/`, `DirectML.dll`, `dstorage*.dll`
- `tools/update/Update.ps1` (アプリが適用時に起動する。BuildScript が同梱)
- `version.json` (配布仕様参照)
- 除外: `FlashBeat_BurstDebugInformation_DoNotShip/` (Unity の指示により ship 対象外)、ルーズなメディアファイル (第三者楽曲)

### macOS

`FlashBeat.app` + `version.json`:

- `FlashBeat.app/Contents/MacOS/FlashBeat`
- `FlashBeat.app/Contents/Resources/Data/` (Managed DLL 群・リソース一式)
- `FlashBeat.app/Contents/Resources/tools/update/update-macos.sh` (アプリが適用時に起動する)
- `FlashBeat.app/Contents/Resources/flashbeat-media-policy.json`
- `FlashBeat.app/Contents/Frameworks/`, `Contents/MonoBleedingEdge/` 等 (Unity の標準構成)
- `version.json` (ZIP ルート)
- 除外: ルーズなメディアファイル (第三者楽曲)。`resources.resource` のサイズ上限で焼き込みも検知する。
- ZIP は `ditto -c -k --sequesterRsrc --keepParent` で作成し、symlink・実行権限・bundle メタデータを保持する。

## SHA-256 検証手順

Windows:

```powershell
# ZIP と .sha256 を同じフォルダに置いてから
Get-Content .\FlashBeat-<version>.zip.sha256   # 例: "abc123...  FlashBeat-0.1.0-win64-743c88d.zip"
Get-FileHash .\FlashBeat-<version>.zip -Algorithm SHA256
# 2 つのハッシュが一致すること。不一致ならダウンロード破損 → 再ダウンロード。
```

macOS:

```sh
# ZIP と .sha256 を同じフォルダに置いてから
shasum -a 256 -c FlashBeat-<version>.zip.sha256
# "FlashBeat-<version>.zip: OK" が出れば一致。不一致なら再ダウンロード。
```

## パッチ版の発行手順

1. ビルドする (Unity 6000.5.6f1)。
   - Windows: `Build → Build Windows 64` または `BuildScript.BuildStandaloneWindows64`
   - macOS: `Build → Build macOS` または `BuildScript.BuildStandaloneMacOS`
   基準バージョンは環境変数 `FLASHBEAT_VERSION_BASE` で指定する (既定 `0.1.0`)。**前回の Release より大きくする**。
   展示用のビルドは**既定のまま (stable チャンネル)** で、`FLASHBEAT_UPDATE_CHANNEL=preview` を付けたビルドだけが
   テスト用 prerelease を取り込む。公開前に Player.log の
   `[UpdateChecker] 更新チャンネル: stable ... / プラットフォーム: win64|macos` を確認して証跡に残す。
   macOS の Deployment Target は `ProjectSettings` の `macOSTargetOSVersion` (12.0)。
2. 実機 smoke (起動→曲選択→プレイ→リザルト) を確認。**実機証拠がなければリリース完了としない** (Issue #22 がゲート)。
   macOS は `docs/operations/mac-verify.md` の手順で確認する。
3. パッケージ化 (ZIP + `.sha256` sidecar と内容検査):

   ```powershell
   # Windows
   powershell -NoProfile -ExecutionPolicy Bypass -File tools/release/package-win64.ps1 `
       -SourceDir <Unity出力フォルダ> -OutDir <distフォルダ> `
       -Version <semver>-win64-<buildSHA> -CommitSha <full SHA> -MediaPolicy song-free
   ```

   ```sh
   # macOS
   tools/release/package-macos.sh \
       --source Builds/macOS/FlashBeat.app \
       --out <distフォルダ> \
       --version <semver>-macos-<buildSHA> \
       --commit-sha <full SHA> \
       --media-policy song-free
   ```

   - **配布する `.ps1` は UTF-8 BOM 付き** (`.editorconfig` の `charset = utf-8-bom` 準拠)。
     Windows PowerShell 5.1 は BOM なし UTF-8 を ANSI (cp932) として解釈するため、
     日本語コメントのバイト列が文字列を壊しスクリプト全体がパースエラーで起動しなくなる
     (2026-11-07 実機発生 → `tools/update/Update.ps1` に BOM を付与して修正)。
   - `update-macos.sh` は LF・BOM 無しで配布する (BOM があると `#!/bin/bash` が解釈できない)。
4. Release 作成 (プラットフォームごとに別 Release)。オーナー承認済みなら `--draft` を外して即公開。
   未承認なら `--draft` で作り 5 へ。タグは実在しない新タグを使い、draft の間はタグが push されない。
   タグ / アセット名には必ずプラットフォーム (`win64` / `macos`) を含める:

   ```powershell
   gh release create v<semver>-<platform>-<shortsha> --target <build commit SHA (フル SHA)> `
       --title "FlashBeat <semver> (<platform>)" --latest `
       --notes "SHA-256: <hash>`nSource commit: <full SHA>`nPlatform: <platform>`n実機検証: <証跡リンク or 未証明>"
   gh release upload v<semver>-<platform>-<shortsha> `
       FlashBeat-<semver>-<platform>-<shortsha>.zip `
       FlashBeat-<semver>-<platform>-<shortsha>.zip.sha256
   ```

   - 同じ `<semver>` でも `win64` と `macos` は別 Release。`--latest` はそれぞれの Release に付けてよい
     (UpdateChecker は `--latest` ではなく一覧からプラットフォームで絞る)。
5. **公開 (publish) の権限境界**: 原則オーナー手動承認。オーナーが明示的に承認した場合のみ
   ワーカーが直接公開してよい (2026-11-06 の v0.1.0-win64-743c88d はオーナー指示により即時公開):

   ```powershell
   # オーナー承認後に実行
   gh release edit v<semver>-<platform>-<shortsha> --draft=false
   ```

6. 不具合が出た場合は既存 Release を差し替えず、**修正版を新しいドラフト Release として発行**する。
   ただし **基準バージョンは必ず上げる** (`FLASHBEAT_VERSION_BASE=0.1.2` → タグ `v0.1.2-<platform>-<sha>`)。
   自動更新はバージョンの大小で判定するため、同じバージョンで別 SHA の Release を作ると
   「同じバージョンの別ビルド」として扱われ、自動更新が効かなくなる (運営者が手動適用する運用になる)。
   旧版は archive に残して proven artifact を保持する。

## ブートストラップ (macOS 版の初回導入)

macOS 版は「手動ビルドを配って終わり」ではなく、Win64 と同じ Release 更新経路に載せる。
配布の起点となる 1 個体だけは Release を経由できないため、リポジトリのビルドを
ブートストラップとして手動配置する (`~/Desktop/FlashBeat-Mac/FlashBeat.app`)。

- ブートストラップの版: `0.1.14+716a25f` (commit `716a25f` を `FLASHBEAT_VERSION_BASE=0.1.14` でビルド)。
- 以後の更新はアプリ内 (Title 通知 → ZIP → SHA-256 検証 → `.app` 差し替え → 再起動) で完結する。
- **最初の macOS Release は `0.1.14` 以降の semver で発行する。** 自動更新はバージョンの大小で
  判定するため、同じ `0.1.14` で別 SHA の Release を作ってもブートストラップは更新されない
  (手動適用になる)。ブートストラップを差し替えた場合も同じ制約が付く。
- 差し替え時のバックアップは `.app` と同じ階層に `FlashBeat-backup-YYYYmmddHHMMSS.app`
  として 1 世代だけ残る (更新スクリプトと同じ命名規則)。

## 更新の種別 (Stage 区別)

| 種別 | 対象 | 配布方法 |
|---|---|---|
| 本体更新 (S0/S1/S2/S3 の Unity 側変更) | exe / FlashBeat_Data / FlashBeat.app | 本手順の ZIP + Release |
| コンテンツのみの更新 (楽曲カタログ・譜面) | Cloudflare 側 | Unity 本体 ZIP とは独立に即時反映 (ZIP 再発行不要) |

## 既存バイナリ互換

- パッチ ZIP はフォルダ / `.app` 全置換。レジストリ書き込み・常駐プロセス・既存の起動経路変更なし。
- 本体が依存する backend API **v1 の契約を壊す変更はリリースしない**。
  破壊的変更が必要な場合は v2 を追加し、v1 を deprecated 期間中維持してから削除する。
- 旧 Unity 本体が新 API を呼んでも壊れないこと (未知フィールド追加は可、必須フィールドの意味変更は不可)。
