# Evidence: macOS から Win64 配布物 (ZIP + SHA-256) を再現性を持って作る (Issue #35)

- 日付: 2026-11-09 (独立レビュー指摘の解消と再計測も同日)
- 対象 commit: `d8c1b39` (main / PR #251 merge 時点)
- 実施場所: macOS (この Mac) / Unity 6000.5.6f1 / PowerShell 7.6.6 (mise 管理の pwsh) /
  bsdtar 3.5.3 (libarchive 3.7.4) / worktree `FlashBeat.macverify` @ `d8c1b39`
- 関連 PR: #253 (`tools/release/package-win64.ps1` を macOS (pwsh) からも実行可能にする)
- 関連 Issue: **#35** (Win64 パッチ ZIP を GitHub Releases に配布)

## なぜ必要だったか

展示 (U-16プログラミングコンテスト山口大会 2026 / 2026-11-14 / 宇部高専) 用に
**Win64 の配布物 (ZIP と SHA-256)** が必要 (§5)。
一方 Win64 CI (`build-win64.yml`) は **Unity ライセンス** で詰まっていた:

- リポジトリの Actions Secrets に登録されているのは `UNITY_LICENSE` (Personal の `.ulf`) だけ。
  `UNITY_EMAIL` / `UNITY_PASSWORD` は未登録。
- Personal の `.ulf` は GitHub-hosted ランナーでは machine binding mismatch が必ず起きる
  (実測 2026-11-08 / run 42470147986) ため、ワークフローは **Personal を明示的に拒否** する
  ようにした (PR #249)。
- つまり **CI を緑にするにはオーナーによる secret 登録が必要** で、
  その手を待たずに配布物を用意せざるを得なかった。

そこで **macOS 側から Win64 をビルドできないか** を調べたところ、
この Mac の Unity 6000.5.6f1 には **WindowsStandaloneSupport が入っていた**:

```
$ ls /Applications/Unity/Hub/Editor/6000.5.6f1/PlaybackEngines/
MacStandaloneSupport
WindowsStandaloneSupport
```

## 実測: macOS から Win64 ビルド

```
$ cd <この Mac の作業ツリー>
$ FLASHBEAT_VERSION_BASE=0.1.14 FLASHBEAT_UPDATE_CHANNEL=preview \
  /Applications/Unity/Hub/Editor/6000.5.6f1/Unity.app/Contents/MacOS/Unity \
    -batchmode -nographics -quit -projectPath <この Mac の作業ツリー> \
    -executeMethod BuildScript.BuildStandaloneWindows64 -logFile win64-build.log
exit=0
```

ログ抜粋 (実ログ `win64-build.log` からの逐語拔出 / 行番号つき):

```
389:  [BuildScript] bundleVersion = 0.1.14+d8c1b39
466:  [BuildScript] 第三者楽曲の音声・動画をビルド対象から除外しました (Musics / Videos/UnityYoutube / loading.mp4)
479:  [BuildScript] Starting StandaloneWindows64 Build...
9582: [BuildScript] Build succeeded: 407649127 bytes, output to <…>/Builds/Windows/FlashBeat.exe
9715: [BuildScript] 更新スクリプトをビルド出力へ複製しました: <…>/Builds/Windows/tools/update/Update.ps1
9728: [BuildScript] 更新チャンネル 'preview' を書き出しました: <…>/Builds/Windows/FlashBeat_Data/StreamingAssets/update-channel.txt
9741: [BuildScript] メディア除外マニフェストを出力しました: <…>/Builds/Windows/flashbeat-media-policy.json
```

出力の確認:

```
$ file Builds/Windows/FlashBeat.exe
Builds/Windows/FlashBeat.exe: PE32+ executable (GUI) x86-64, for MS Windows

$ du -sh Builds/Windows
390M    Builds/Windows
```

## 実測: パッケージング (ZIP + SHA-256)

`tools/release/package-win64.ps1` は当初 Windows 前提で macOS では `tar.exe` が無く失敗した。
PR #253 で `Resolve-TarCommand` (libarchive の tar を OS に応じて選択) と
`Join-PathSegments` (OS のパス区切りに依存しないパス結合) に直して再実行。
さらに独立レビュー (PR #253) で指摘された **再現性** と **tar 解決の脆さ** を解消した
(→「再現性」節 / 「tar 解決の堅牢化」節)。再実行の結果:

```
$ pwsh -NoProfile -ExecutionPolicy Bypass -File tools/release/package-win64.ps1 \
    -SourceDir <…>/Builds/Windows -OutDir <出力先> \
    -Version 0.1.14-win64-d8c1b39 -CommitSha d8c1b395e30ea8c4ec44e3e50d1e00914cc25744 \
    -MediaPolicy song-free
[media] 第三者楽曲メディアはビルド対象から除外済み (manifest: Assets/Game/Resources/Musics, Assets/Game/Resources/Videos/UnityYoutube)
[1/4] ZIP 作成中: <出力先>/FlashBeat-0.1.14-win64-d8c1b39.zip
      tar: /usr/bin/tar (-a) / 2 段構え (ustar → zip): 再現します
[2/4] ZIP 内容検査中
[3/4] SHA-256 計算中
[4/4] 完了: entries=224 managedDlls=138 mediaPolicy=song-free size=274,824,993 bytes
SHA-256: d21d91737c7f630fe77a4a7cdc1ca0876cea23a894bd64a5c3aade8e9155b1f9
Sidecar: <出力先>/FlashBeat-0.1.14-win64-d8c1b39.zip.sha256
createdAt (ビルド元 commit の日時, UTC): 2026-11-09T04:24:21Z
タグ (GitHub Release): v0.1.14-win64-d8c1b39
```

### 検査結果

| 項目 | 結果 |
| --- | --- |
| `file FlashBeat.exe` | `PE32+ executable (GUI) x86-64, for MS Windows` |
| `shasum -a 256 -c <zip>.sha256` | `OK` |
| `unzip -t <zip>` | `No errors detected in compressed data of …zip.` |
| SHA-256 | `d21d91737c7f630fe77a4a7cdc1ca0876cea23a894bd64a5c3aade8e9155b1f9` |
| ZIP サイズ | 274,824,993 bytes |
| ZIP エントリ数 | 224 (ファイル 203 + ディレクトリ 21) |
| Managed DLL 数 | 138 (ZIP 内とビルド出力で一致 — スクリプトの `Test-ZipContents` が検証) |
| ZIP ルート | `FlashBeat.exe` / `FlashBeat_Data/*` / `UnityPlayer.dll` / `version.json` あり |
| `version.json` | `{"version":"0.1.14","buildSha":"d8c1b39…","tag":"v0.1.14-win64-d8c1b39","platform":"win64","mediaPolicy":"song-free","createdAt":"2026-11-09T04:24:21Z"}` |
| メディアポリシー | `song-free` — 第三者楽曲の wav / 動画を含まないことを `flashbeat-media-policy.json` で証明 (ZIP 内のメディア拡張子エントリ 0 件) |
| `FlashBeat_BurstDebugInformation_DoNotShip` | ZIP に含まれない (0 件) |
| 更新チャンネル | `preview` (未検証の自動更新をしないため。`stable` は実機受入後) |

## 成果物の同一性 (release gate の identity model)

`docs/evidence/index.md` の契約に準じて、**成果物の同一性は ZIP の SHA-256 ではなく
フォルダ fingerprint** で表す (`FlashBeat.exe` はビルドごとに同じ launcher stub のため)。

実測 (`release/check/verify-stage.ps1` の `Get-FolderFingerprint` と同一の定義で計算):

| 項目 | 値 |
| --- | --- |
| フォルダ fingerprint (SHA-256) | `9f01a42acfe6a89ba4f2a798e13fd5c859b5f4b7625a5d3ed8566019318b6174` |
| `FlashBeat.exe` SHA-256 | `79cadd0ab0635f4275b58104273a1ec40201d624cd1e1a0b0cf8878ff8a34887` |
| ファイル数 / 総バイト | 203 / 408,136,773 bytes |

fingerprint の定義: ビルドフォルダ配下の各ファイルについて
`"<fileSha256>  <相対パス>"` を相対パス順で LF 連結し、その UTF-8 バイト列の SHA-256。
(この 203 には Unity が「出荷するな」と指定する
`FlashBeat_BurstDebugInformation_DoNotShip/Data/Plugins/x86_64/lib_burst_generated.txt` 1 ファイルを含む。
パッケージング時は **ZIP から除外** される。)

## 再現性: 同じ入力なら SHA-256 が一致する (重要)

### 修正前の実測と原因の訂正

，独立レビュー (PR #253) の指摘どおり、**当初この doc が原因帰属を間違えていた**。
訂正する:

- 誤った記述: 「ZIP にはファイル modification time が埋め込まれるため、同じビルド出力から
  再パッケージすると SHA-256 は変わります」。
- 実際の原因: **`version.json` の `createdAt` (実行時刻 `Get-Date()`) と `version.json` 自身の mtime** だけ。
  ビルド出力のファイル mtime は安定しており、原因になっていなかった。

実測 (修正前のスクリプト / 同一 `Builds/Windows` から 2 回実行):

```
entry count: 224 224
same order: True
entries with differing mtime: 1
    ('version.json', (2026, 11, 9, 19, 18, 8), (2026, 11, 9, 19, 18, 18))
entries with differing CRC (content): ['version.json']
```

→ **223 / 224 エントリは mtime も CRC も完全一致**。差分は `version.json` 1 件だけ。

### 修正内容 (PR #253)

1. **`createdAt` を決定的にした。** 実行時刻 (`Get-Date()`) ではなく
   **ビルド元 commit の committer date (UTC)** から作る。
   - `-CommitDate` (ISO 8601) を明示指定するか、無指定なら
     `git show -s --format=%cI <CommitSha>` から導出する
     (`d8c1b39` → `2026-11-09T13:24:21+09:00` → `createdAt = 2026-11-09T04:24:21Z`)。
   - `version.json` 自身の mtime も同じ commit 日時に固定する
     (tar は各エントリの mtime を ZIP に埋め込むため)。
2. **ZIP  creation を 2 段構えにした。** `ディスク → ustar → zip`。
   ustar には atime / ctime のフィールドが無いため、ZIP のローカルヘッダの
   UT 拡張フィールドに **実行時刻由来の atime / ctime が入らない**。
   (1 段構えだと `version.json` だけ mtime が違う ZIP になる)

### 再現性の実測 (修正後)

同一 `Builds/Windows` / 同一 commit から **2 回** パッケージした結果:

| 実行 | SHA-256 | サイズ | エントリ | Managed DLL |
| --- | --- | --- | --- | --- |
| 1 回目 | `d21d91737c7f630fe77a4a7cdc1ca0876cea23a894bd64a5c3aade8e9155b1f9` | 274,824,993 bytes | 224 | 138 |
| 2 回目 | `d21d91737c7f630fe77a4a7cdc1ca0876cea23a894bd64a5c3aade8e9155b1f9` | 274,824,993 bytes | 224 | 138 |

→ **SHA-256 が一致した** (同一入力 → 同一 SHA)。
`-CommitDate` を明示指定した場合 (`2026-11-09T13:24:21+09:00`) も同じ SHA になる。

したがって:

- 上に書いた SHA-256 は **この成果物に対する値** であり、
  「この SHA なら中身の完全性を保証できる」根拠です。
- **再現性検証 (同じビルド出力 + 同じ commit → 同じ SHA) は成立します。**
  再ビルド (別の commit) すると fingerprint も SHA も変わります。
- それでも **ZIP に同梱された `.sha256` sidecar** と突き合わせてください
  (`sha256sum -c` 互換)。sidecar は常に同梱の ZIP と 1 対 1 で対応します。
- 更新判定は SHA ではなく Release タグとバージョンを使います
  (`docs/operations/update-release.md` の配布仕様)。

## tar 解決の堅牢化 (独立レビュー S2)

`Resolve-TarCommand` には 2 つの穴があった。両方とも修正済み:

1. `Get-Command` は **Function / Alias を優先** するため、ユーザープロフィールの
   `function tar { … }` を掴まされ `.Source` が空文字になっていた。
   → **PATH を自前に走査して実行ファイルだけ** を探す実装に置き換えた
   (Function / Alias は実行ファイルではないので選ばれない)。
2. **zip を作れない tar** (GNU tar など) を PATH 先頭に置くと、拡張子 `.zip` の
   中身が tar archive になり、`ZipFile::OpenRead` の素の .NET 例外で落ちていた。
   → **実際に probe して zip を作れるか検証してから** 使い、
   できなければ **スクリプトの日本語診断** で失敗する。失敗時には
   **壊れた `.zip` を残さない** (release 手順は `*.zip` を glob で拾うため)。

### 実測: zip 非対応 tar を PATH 先頭に置いた場合

zip 非対応 tar の仿真 (GNU tar の `-a` は `.zip` でも zip にできないのと同じ結果になる) を
PATH 先頭に置いて実行:

```
[1/4] ZIP 作成中: <出力先>/FlashBeat-0.1.14-win64-d8c1b39.zip
Exception: …/tools/release/package-win64.ps1:179
  ZIP を作れない tar が選ばれました: /tmp/w253/faketar1/tar この tar は zip 形式に対応していません
  (拡張子 .zip の中身が tar .archive になります)。Windows 同梱の tar.exe (libarchive) または
  macOS / Linux の bsdtar を PATH の先頭に置いてください。 試した形式指定: -a / --format=zip
exit code = 1
```

出力先の実測:

```
$ ls -A <出力先> | wc -l
0            # 偽の .zip も .sha256 も残らない
```

### 実測: プロフィールの `function tar` がある状態で実行

```
function tar { "shadow" }
& tools/release/package-win64.ps1 … 
      tar: /usr/bin/tar (-a) / 2 段構え (ustar → zip): 再現します
[4/4] 完了: entries=224 managedDlls=138 mediaPolicy=song-free size=274,824,993 bytes
SHA-256: d21d91737c7f630fe77a4a7cdc1ca0876cea23a894bd64a5c3aade8e9155b1f9
```

→ 実際の `/usr/bin/tar` が選ばれ、**前回と同じ SHA** が出力された
(変更前は `&` が空オブジェクトで失敗していた)。

## パッケージング経路は 1 本 (独立レビュー S3)

macOS のパッケージング経路が 2 本ある状態 (`script/win64-build-mac.sh` と本 PR の pwsh 経路) は
**解消済み**。`script/win64-build-mac.sh` は **ビルドだけ** を行い、
パッケージングは **正規実装 `tools/release/package-win64.ps1` に委譲** する
(薄いラッパ) ようにした。これで:

- `version.json` の同梱
- `FlashBeat_BurstDebugInformation_DoNotShip` の除外
- media policy 検査 (`-MediaPolicy song-free`)
- `.sha256` sidecar の 2 点だけ (`.build-info.txt` などの 3 つ目のアセットは出さない)

が **1 つの実装** に集約される。release gate の契約
(`docs/operations/release-gate.md` §1.4 添付アセット / §1.5 メディア非同梱) に従う。

## 成果物の置き場所 (この Mac 上の記録)

リポジトリ外の個人環境。以下は **この Mac での記録** で、
リポジトリからは到達できない (再現手順は上の節にある):

```
<この Mac の作業ツリー>/Builds/Windows/            # ビルド出力 (展開用)
<この Mac の作業ツリー>/dist2/FlashBeat-0.1.14-win64-d8c1b39.zip
<この Mac の作業ツリー>/dist2/FlashBeat-0.1.14-win64-d8c1b39.zip.sha256
```

## 未検証 / 残課題

1. **Windows 実機での起動・プレイは未検証**。
   macOS からビルドした `FlashBeat.exe` が展示 PC で実際に動くかは **未確認**。
   展示 PC は無操作のため触っていない。Windows 実機での目視確認は
   Issue #233 / #242 の受入条件 (Title→Select→Game→Result→Retry) に含まれる **オーナー作業**。
2. **Win64 CI は依然として失敗** (`build-win64.yml` は未変更)。
   緑にするには GitHub Actions Secrets への `UNITY_EMAIL` / `UNITY_PASSWORD` 登録が必要。
   ただし **ライセンス待ちで配布物の作成自体が止まっていた問題は macOS 経路で解消済み**。
3. **CI のパッケージング step は一度も実行されていない**。
   直近の `build-win64` run はすべて `license-precheck` で止まり、Unity もパッケージングも
   走っていない。したがって **Windows 上でのパッケージング経路 (本 PR で `Resolve-TarCommand` を
   整理した経路) も未実行** であり、「Windows 上の挙動は変えません」は **推論** にすぎない。
4. **Windows 実機での再現性 (2 回パッケージして SHA 一致) は未検証**。
   上記の再現性の実測は **macOS (bsdtar 3.5.3)** でのもの。
   Windows 同梱の `tar.exe` でも同じ結果になるかは **未確認**。
   (スクリプトは Windows でも `tar.exe` が 2 段構え (`--format ustar` + `@archive`) に対応して
   いるかを probe する。非対応なら警告して 1 段構えにフォールバックする)
5. 本番データ (実際の wav / 実動画) は `song-free` ポリシーで除外されている。
   実機では `yt-dlp` で解決したストリームを再生する設計のため、配布物として正しい。
6. この成果物は **未検証のため GitHub Release には publish していない**。
   Release 化は Windows 実機での受入後に行う。
