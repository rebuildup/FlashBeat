# Release evidence index — Win64 exhibition build (#22)

Owner: issue **#22** (release gate). Machine-readable twin: [`release/check/stage-manifest.json`](../../release/check/stage-manifest.json).

This index records **what is actually proven** for every festival stage. A stage without a real Win64
play run is written as **未証明 (unproven)** — no green is claimed for work that was not observed.

```powershell
# verify every artifact, fingerprint and evidence file that exists on this machine
powershell -NoProfile -ExecutionPolicy Bypass -File release/check/verify-stage.ps1

# one stage only
powershell -NoProfile -ExecutionPolicy Bypass -File release/check/verify-stage.ps1 -Stage S0
```

## 1. Stage status

| Stage | Content | Status | Win64 artifact | Play evidence (select → stream → notes → result → retry) |
|---|---|---|---|---|
| **S0** | baseline build + one full song loop | **proven (partial)** | yes — fingerprint `7ef323adcf1855ea1f192d0f542c099ec6fdfe52540bdf7737b55014f2ba4d53` | chart/result/retry **proven**; YouTube stream + audio **未証明** |
| **S0-FONT** | S0 + Japanese HUD font fix (candidate for the exhibition) | **proven (partial)** | yes — fingerprint `e07ccba2cc406306254a50e0bfb5f43a197013aa3820fe907d28da60ffe1b242` | glyphs + chart/result **proven**; YouTube stream + audio **未証明** |
| **S1** | yt-dlp + Deno → Unity `VideoPlayer` (streamed playback) | **未証明** | build exists in the #13 worktree — fingerprint `b8c0acde7a318bc3f8d7929025743638042a3b0e2dec7836e418a2d80c60830f` | **未証明** — #13 has not published its Win64 run yet |
| **S2** | remote catalog / chart API (Cloudflare) | **未証明** | none | **未証明** — no deployed API, no client build |
| **S3** | chart editor + publisher → Win64 plays a published chart | **未証明** | none | **未証明** — no published chart version |
| **S4** | leaderboard (optional) | **未証明** | none | not started — keep the current playable build |

> 上表は **Windows 実機** のみを根拠とする。macOS で観測した v6 統合同ブランチの証跡は
> **§1.6** に分けて記載している（Win64 の代替ではない）。

`FlashBeat.exe` has the **same SHA-256 in every build** (it is the Unity launcher stub), so the
**folder fingerprint** is the artifact identity:

> SHA-256 over the UTF-8 bytes of `"<fileSha256>  <relative/path>"` lines, one per file, sorted by path.

## 1.5 展示フォルダ（運営者が起動する場所）

| 世代 | フォルダ | 中身 | `FlashBeat.exe` SHA-256 | フォルダ SHA-256 | ファイル数 / 総バイト | 配置 |
|---|---|---|---|---|---|---|
| **第 3 世代（展示用）** | `D:\4_apps\Unity\Build\FlashBeatExhibit_s2\` | **#13**（yt-dlp ストリーミング + **キャッシュにコンテナ拡張子を保持する修正**）+ **#27**（日本語フォント）の統合ビルド（code state `b77b63c`） | `3031bb5e15e85eb0eaa4de271b81e2c8f5e8eeb1254c3dcc0a70c4b950c39a60` | `2907102fcab0142e8d93b7bfb025d74a98cc274fe6ebbac55843cdca14cbedd9` | 199 / 1 588 795 255 | 2026-11-07 00:01 |
| 第 2 世代（旧・予備） | `D:\4_apps\Unity\Build\FlashBeatExhibit_s1\` | #13 + #27 の統合ビルド（キャッシュが拡張子を持たない版、code state `f53575c`）→ 映像は出ず音も同梱 wav | `3031bb5e15e85eb0eaa4de271b81e2c8f5e8eeb1254c3dcc0a70c4b950c39a60` | `cbae8f677eb60dfeaa6ca6676121d4266fa99583f2b76bfeacacebcbd0924758` | 199 / 1 588 787 091 | 2026-11-06 22:58 |
| 第 1 世代（旧・予備） | `D:\4_apps\Unity\Build\FlashBeatExhibit\` | #27 のフォント修正ビルド（#13 なし = **音が出ない**）。入手元は #35 の Release ZIP `FlashBeat-0.1.0-win64-743c88d.zip`（SHA-256 `3dbdfecbdb91d916178666045d0c62e1fae717f7df0b77e4f9ddc2af1c036a45`、展開前に照合） | `3031bb5e15e85eb0eaa4de271b81e2c8f5e8eeb1254c3dcc0a70c4b950c39a60` | `da338a6a8a73b23dbf6e0a7d26e498003e2f773bdb574323c416d9d17f6c563b` | 198 / 1 588 334 450 | 2026-11-06 22:53 |

* 運営者は **第 3 世代（`FlashBeatExhibit_s2\`）の `FlashBeat.exe` を直接起動** します。全世代とも **消しません**（上書きもしません）。
* 展示中は **読み取り専用**。更新は運営者の明示操作のみ。`Update.ps1`（#36）は別のインストール先を対象にし、
  展示フォルダは自動更新対象にしません。診断ログは各世代の `logs\` に集めます。
* 各フォルダの `EXHIBIT-MANIFEST.txt` に世代・ビルド SHA・exe SHA-256・フォルダ SHA-256 を記載しています。

**第 3 世代の実機証明（2026-11-07 00:01）**: Title → Select → まにまに → 判定 76（譜面全部、
実入力で Perfect 1 / Bad 2）→ Result → `R` リトライまで到達、起動 14.1 秒。
**映像あり**（`VideoPlayer prepared=True`、`STATUS … video[playing=True]` — #13 の
「キャッシュがコンテナ拡張子を保持する」修正が効いている）。
**音あり**（`STATUS … rms=0.44 … 0.56 peak=0.82`）。ただし音源は **同梱 wav**（`audioMode=Bundled`）:
ストリームの音声トラック（m4a）は `Failed to recognize video file format` で準備できず、
**「YouTube ストリームの音声そのもの」は未証明**。
**未証明**: ストリーム音声、実機 1280×720 fullscreen、S2/S3。
詳細: [`win64-2026-11-06-release-gate/README-gen3.md`](win64-2026-11-06-release-gate/README-gen3.md)（Run D）/
[`win64-2026-11-06-release-gate/README.md`](win64-2026-11-06-release-gate/README.md)（Run A/B/C = 第 1・2 世代）。

## 1.6 Mac 検証（2026-11-08・v6 統合中ブランチ）

**これは Win64 の代用ではない。** 展示は Windows 実機で行うため、macOS の実機ログは
「ソースがビルドでき、起動し、例外を出さない」証明に過ぎない。ここに書くのも
**実機で観測できたことだけ** で、観測できなかった項目は「未証明」と明記する。

| 項目 | 結果 | 根拠 |
|---|---|---|
| macOS スタンドアロンビルド | **成功** | `script/mac-verify build integration/v6-followup` → `Builds/macOS/FlashBeat.app` |
| 実機起動 | **成功**（プロセスは生存） | `script/mac-verify run` → `process alive: yes` |
| Player.log の例外/エラー行 | **0 行** | `grep -c -i -E 'exception\|error' Player.log` = 0 |
| シーン遷移 | **成功** | `[Perf] scene load end TitleScene 55ms` |
| 実解像度 | **2560×1664**（実ディスプレイの全画面表示） | AutoShot の実画像サイズ |
| v6 Title 描画 | **成功**（ロゴ・背景動画・自己ベスト・START が出る） | `shot-01-TitleScene.png` / `shot-02-TitleScene.png` |
| 更新通知 | **表示された**（`v0.1.0+aac497b → v0.1.14`、`更新して再起動 F3`） | `shot-02-TitleScene.png` |
| 選曲 → プレイ → Result → リトライ | **未証明**（この run では GameScene まで進まなかった） | `shot-00-Opening.png` / `shot-01-TitleScene.png` のみ |
| Win64 全画面表示 | **未証明**（Windows 実機は展示用につき未操作） | — |

証跡: [`mac-verify-run-2026-11-08/`](mac-verify-run-2026-11-08/)（`README.md` / `Player.log` / 3 枚のスクリーンショット）。

**Win64 CI が赤である理由**: 直近の `build-win64` 失敗は **ライセンス未設定** であって
**ソースのビルド失敗ではない**。該当ジョブは `license-precheck` で終了し、Unity は 1 度も起動していない:

```
[error]UNITY_EMAIL と UNITY_PASSWORD を Actions Secrets に登録してください。
```

切り分け手順と推奨手順は [`docs/operations/unity-ci-license.md`](../operations/unity-ci-license.md) を参照。

## 1.7 main 統合後の再検証（2026-11-09・PR #249 merge 済み）

PR #249（v6 統合 + Win64 CI 再構成）が `main` に merge された状態で、各ゲートを
**再実行**した結果。Windows 実機に関する記述は §1.6 と同じく **未証明** のまま。

| ゲート | 結果 | 根拠 |
|---|---|---|
| Unity EditMode テスト | **1071 / 1071 成功**（失敗 0 / skipped 0） | Unity 6000.5.6f1 / macOS batchmode、`TestResults.xml` を直接パース（`a4bda77`） |
| web テスト | **node 73 件 + vitest 399 件 すべて成功** | `cd web && bun run test` |
| 視覚ゲート `npm run visual` | **exit 1 (FAIL)** — `home@390x844` の横スクロール溢出 | PR 内で記録済みの失敗ベースライン（`specs/075-visual-gate/evidence/2026-11-08/README.md`）と一致。**新規の regressions ではなく未解決 issue**（#74 / #75 / #68 系） |
| ワークフロー構文 | YAML パース（node `yaml`）/ 全 bash `bash -n` / 全 pwsh `pwsh [scriptblock]::Create` すべて OK | `.github/workflows/build-macos.yml` / `build-win64.yml` |
| Personal ライセンス判定 | bash・pwsh 双方で Personal / Pro / BOM+base64 の実入力により確認 | `.ulf` の `DeveloperData Value="Personal"` / `SerialMasked Value="XXXX"` を検出して明示的に失敗 |
| CodeRabbit 指摘 | **8 thread すべて解消**（返信 + resolve 済み） | PR #249 |
| Windows 実機 / Win64 CI 緑 / Win64 成果物 (ZIP + SHA-256) | **未証明** | ライセンス未設定により `license-precheck` が blocker として停止。展示 PC は無操作 |
| Pencil デザイン全画面一致 | **未受入** | 契約照合表は実装コメントに残置済み。3 解像度の受入は Windows 実機に残ります |

## 1.8 macOS ビルドの Win64 配布物 (2026-11-09・PR #253)

**Windows 実機の代用ではない。** §1.6 と同じく、macOS で観測できたことだけを書いており、
展示 PC での実プレイは **未証明** のまま。

macOS の Unity 6000.5.6f1 (WindowsStandaloneSupport 入り) で `d8c1b39` を
Windows ターゲットとしてビルドし、`tools/release/package-win64.ps1` で
**正規のパッケージング経路** を使って ZIP + SHA-256 にしたもの。
手順・実測ログの全文: [`win64-build-from-macos.md`](win64-build-from-macos.md)。

identity model (§1 / `release/check/stage-manifest.json` の `fingerprint.definition`) に従い、
**成果物の同一性は ZIP の SHA-256 ではなくフォルダ fingerprint** で表す。
以下は `release/check/verify-stage.ps1` の `Get-FolderFingerprint` と **同一の定義** で実測した値:

| 項目 | 値 |
|---|---|
| フォルダ fingerprint (SHA-256) | `9f01a42acfe6a89ba4f2a798e13fd5c859b5f4b7625a5d3ed8566019318b6174` |
| `FlashBeat.exe` SHA-256 | `79cadd0ab0635f4275b58104273a1ec40201d624cd1e1a0b0cf8878ff8a34887` |
| ファイル数 / 総バイト | 203 / 408,136,773 bytes |
| ZIP SHA-256 | `d21d91737c7f630fe77a4a7cdc1ca0876cea23a894bd64a5c3aade8e9155b1f9` |
| ZIP サイズ | 274,824,993 bytes |
| ZIP エントリ数 / Managed DLL 数 | 224 / 138 |
| メディアポリシー | `song-free` (ZIP 内メディア 0 件 / `FlashBeat_BurstDebugInformation_DoNotShip` 0 件) |
| 更新チャンネル | `preview` |
| ビルド元 commit | `d8c1b39` (main / PR #251 merge 時点) |

* **再現性の実測**: 同一ビルド出力・同一 commit から 2 回パッケージして **SHA-256 が一致** した
  (2 回とも `d21d9173…`)。`version.json` の `createdAt` は実行時刻ではなく commit の日時から作る。
* **sidecar**: 同梱の `.zip.sha256` と `shasum -a 256 -c` で一致 (`OK`)。
* **未証明**: Windows 実機での起動・プレイ、GitHub Release への publish。

**`release/check/stage-manifest.json` には追加していない。**
同 manifest は「このマシンにアセットが存在する stage」だけを列挙し、`verify-stage.ps1` は
各 stage のアセットが **このマシンに存在すること** を要求する (存在しなければ `[FAIL]`)。
macOS ローカルの絶対パスを載せると Windows 実機や他のマシンで gate が壊れるため、
この成果物は **証跡として本 index に登録** し、stage には追加していない。
#35 の GitHub Release 化 (Windows 実機での受入後) の時点で、
Windows 実機上のパスと 함께 stage へ追加する。

## 2. Artifact index (SHA)

| Stage | Artifact (external, Git-free) | Build tree | `FlashBeat.exe` SHA-256 | Folder fingerprint | Files / bytes | Built (exe mtime) |
|---|---|---|---|---|---|---|
| S0 | `D:\4_apps\Unity\Build\FlashBeatBuild\Build0.1.0-win64-1c5137e` | `C:\Users\rebui\Desktop\FlashBeat.11\Builds\Windows` | `3031bb5e15e85eb0eaa4de271b81e2c8f5e8eeb1254c3dcc0a70c4b950c39a60` | `7ef323adcf1855ea1f192d0f542c099ec6fdfe52540bdf7737b55014f2ba4d53` | 199 / 1 555 304 119 | 2026-11-06 21:13:03 (archived 21:35:10) |
| S0-FONT | *not archived yet* | `C:\Users\rebui\Desktop\FlashBeat.27\Builds\Windows` | `3031bb5e15e85eb0eaa4de271b81e2c8f5e8eeb1254c3dcc0a70c4b950c39a60` | `e07ccba2cc406306254a50e0bfb5f43a197013aa3820fe907d28da60ffe1b242` | 199 / 1 588 771 187 | 2026-11-06 22:08:49 |
| S1 | *not archived yet* | `C:\Users\rebui\Desktop\FlashBeat.13\Builds\Windows` | `3031bb5e15e85eb0eaa4de271b81e2c8f5e8eeb1254c3dcc0a70c4b950c39a60` | `b8c0acde7a318bc3f8d7929025743638042a3b0e2dec7836e418a2d80c60830f` | 199 / 1 555 326 587 | 2026-11-06 22:03:48 |

The S0 archive copy and the #11 worktree build have the **same fingerprint** — the archived copy is
byte-identical to the build that produced the #11 evidence.

Measured 2026-11-06 22:26–22:28 (local machine). Fingerprints of worktree builds are **snapshots**:
if the owning worker rebuilds, the fingerprint changes and the index has to be refreshed.

## 3. Evidence files

Screenshots are LFS-tracked binaries and are **not copied into this branch**; they stay in the owning
branch / the external archive and are indexed here with their SHA-256.

macOS から作った Win64 配布物 (ZIP + SHA-256, PR #253) は **§1.8** に登録済みです:
[`win64-build-from-macos.md`](win64-build-from-macos.md)。

### S0 — branch `11`, commit `1c5137e`, PR [#26](https://github.com/rebuildup/FlashBeat/pull/26)

| Git ref | SHA-256 | Shows |
|---|---|---|
| `origin/11:docs/evidence/win64-2026-11-06/03-select-no2-manimani.png` | `54569f64488d52129114776bde25fa24cd6c386df038006069ed434f64fa6929` | select screen, No.2 まにまに |
| `origin/11:docs/evidence/win64-2026-11-06/04-game-playing.png` | `ce02b3c02d4c24d1752a5afac85115cb3d4b139c5b160ee754bbe4e4f1b11435` | GameScene with the chart running |
| `origin/11:docs/evidence/win64-2026-11-06/05-result.png` | `4b7ab9f18d1b33f95c97ee1f4e54ec6bad3460935a798f2c1654c3c64304369f` | result screen — Judge `Miss 76` (= the whole まにまに chart) |
| `origin/11:docs/evidence/win64-2026-11-06/06-retry-game-reloaded.png` | `3b8ed428d19bb581fc100ccc554fff9f0fb5e317b1e12547a59b7eb1d0c52704` | `R` reloaded the GameScene, counters reset |
| `origin/11:docs/evidence/win64-2026-11-06/07-result-run2-rostoreamblera.png` | `fb3954342e1776e218eed803119cdad9aa010c48d73dc95b744ac7a9acb42c51` | result screen of the 268-note chart ロストアンブレラ |
| `origin/11:docs/evidence/win64-2026-11-06/Player-run3-manimani.log` | `df6d9a6b05a34a1d3125949dc62a02541a4aad2d43f2879c8f47a5d0bb32a644` | player log of the まにまに run (redacted) |
| `origin/11:docs/evidence/win64-2026-11-06/Player-run2-rostoreamblera.log` | `d76023c943d170d97c3d673a279261f590188212a34f630cd2896bb128852b60` | player log of the ロストアンブレラ run (redacted) |
| `origin/11:docs/evidence/win64-2026-11-06/build-log-excerpt.txt` | `893d1ac9b7f1609de25e509647291b6f343e324f5770ec170b8682261659328c` | `[BuildScript] Build succeeded: 1554867382 bytes` |
| `origin/11:docs/evidence/win64-2026-11-06/drive-gameplay.ps1` | `7d82f35337a89732093fd1109085ec4568617d68f3fafd5b38015a09edd302a7` | the original driver (reused by `release/check/drive-gameplay.ps1`) |

### S0-FONT — branch `27`, commit `743c88d`, PR [#34](https://github.com/rebuildup/FlashBeat/pull/34)

| Git ref | SHA-256 | Shows |
|---|---|---|
| `origin/27:docs/evidence/win64-2026-11-06-font/01-select-song-titles.png` | `58aeb255d738f09113e9517aeb4f1de9fb56549fd1c1bf67b8c9564347003a9e` | select screen, Japanese titles rendered (was `□`) |
| `origin/27:docs/evidence/win64-2026-11-06-font/02-select-song2.png` | `143697c905fc3da1641c9a0b630c49e39b30f02a05afc055e04ae88793d8d7fd` | select screen, まにまに |
| `origin/27:docs/evidence/win64-2026-11-06-font/03-game-lyrics.png` | `aaaa866fc028673b3eb45777881851321f857923772c00e7b6e3fcf8925d89d4` | GameScene lyrics `誰もがそう思うでしょう` |
| `origin/27:docs/evidence/win64-2026-11-06-font/04-result-song-title-artist.png` | `49a6b3c1c15cb156ad0580adac785c5007156f97e97d4157a66a649a0de6441b` | result screen, まにまに / r-906 |
| `origin/27:docs/evidence/win64-2026-11-06-font/Player-font-run.log` | `4f7a0cff5fb8406198b58fdf322c92b3fc866cd6c18952ce28b707e5de64c521` | player log of the font run — see caveat below |
| `origin/27:docs/evidence/win64-2026-11-06-font/build-log-excerpt.txt` | `a80a55055a6e77025feda0c968437607108c0cc15505ad1a75c542c7ca4431af` | `[BuildScript] Build succeeded: 1588334450 bytes`, `error CS count: 0` |

**Caveat (found while indexing):** `Player-font-run.log` still contains a raw YouTube video id inside
the yt-dlp stderr (`[youtube] <id>: Requested format is not available`). A video id is public, not a
credential, and no signed URL is present — but it is inconsistent with the redaction used in the S0
logs. **#27 should redact it** (`release/check/redact-log.ps1` does this) before the log is published.
Recorded as a finding, not fixed here (the file belongs to #27).

### Release-gate runs by #22

| Run | Artifact | When | Result |
|---|---|---|---|
| *(see `docs/evidence/win64-2026-11-06-release-gate/`)* | S0 archive `Build0.1.0-win64-1c5137e` | 2026-11-06 | see `smoke-summary.md` in that folder |

## 4. ソース検査と実プレイ証拠の区別

「コードを読んだ結果」と「実際に動いて確認した結果」を混ぜないため分けて記録する。

### 4.1 ソース検査のみ（実プレイでは未確認）

| 項目 | 確認した場所 | 内容 |
|---|---|---|
| レーンキーマッピング | `Assets/Game/Scripts/Gameplay/Judge.cs`（`LaneKeys`） | 8 レーンのキー表（runbook §3 に転記） |
| 画面遷移のキー | `TitleSceneManager` / `SelectSceneManager` / `ResultSceneManager` | Title: `Space`/`O`/`E`、Select: `Space`/曲送り/曲戻し/`Esc`、Result: `Space`/`R` |
| ウィンドウ設定 | `ProjectSettings/ProjectSettings.asset` | `defaultScreenWidth/Height` 960×540、`fullscreenMode: 1`（FullScreenWindow）、`resizableWindow: 0`、`runInBackground: 1` → **既定は表示 Resolution の全画面**。1280×720 で観測するには `-screen-width/-screen-height/-screen-fullscreen 0` を付ける必要がある |
| 既定ログ位置 | `ProjectSettings` の `companyName: KimuraYusuke` / `productName: FlashBeat` | `%USERPROFILE%\AppData\LocalLow\KimuraYusuke\FlashBeat\Player.log` |
| 無音の構造的理由 | `Assets/Game/Scripts/Gameplay/MusicManager.cs` | `Resources/Musics/<song>` を `Music` にロードするが `audioS.Play()` を呼ばない → 音の乗りは映像トラック。映像が死ねば無音（#11 の所見） |

### 4.2 実プレイで観測済み

| 項目 | ステージ | 観測 |
|---|---|---|
| 選曲 → 譜面 → リザルト → リトライ | S0 / S0-FONT | スクリーンショット + ログ（判定数 = 譜面ノーツ数） |
| 日本語グリフ | S0-FONT | select / game / result が `□` なく表示（ログのフォントエラー 110 → 0） |
| 1280×720 windowed 描画 | S0（#22 の runbook 実行） | §3 の release-gate run を参照 |
| 起動時間（タイトル到達まで） | S0（#22 の runbook 実行） | 同上 |
| 音 / 音画同期 / ネットワーク断時の挙動 | **未証明** | S1 承認後まで「未検証」 |

## 5. Secret / redaction policy

* Player logs contain signed, time-limited googlevideo URLs. They are replaced with
  `<REDACTED-YOUTUBE-STREAM-URL>` before anything is committed or published.
* The committed S0 logs are already redacted (`<REDACTED-YOUTUBE-STREAM-URL>`, `videoId=<REDACTED>`,
  GPU id `<REDACTED>`); re-verified on 2026-11-06 with
  `powershell -File release/check/redact-log.ps1 -InPath <log> -Check`.
* No Cloudflare URL, token or credential is stored in this repository. The catalog API host is still the
  contract placeholder `https://YOUR-WORKER-DOMAIN` — nothing is deployed yet, so no real endpoint may
  be published here.

## 5. Open gaps of the release gate

1. **S1 audio is not proven.** In every S0-family run the player resolved the YouTube video but
   `WindowsVideoMedia error 0x80070005` (access denied) and `[MusicManager] VideoPlayer failed to
   prepare within 30s`; the song rides on the video track, so the build is **silent**. The S1 build
   (#13, PR #31) changes the media path, but no play evidence is published yet → **未証明**.
2. **S2/S3 have no Win64 artifact at all** → **未証明**.
3. **Fullscreen on a real 1280×720 panel is not proven** — this machine has 2560×1440 / 1920×1080 /
   2560×1600 displays only. The UX smoke therefore runs the player windowed at 1280×720
   (`-screen-width 1280 -screen-height 720 -screen-fullscreen 0`); the fullscreen rendering of the
   target panel stays **未証明** until the exhibition display is available.
4. The S0-FONT build is **not archived** to `D:\4_apps\Unity\Build\FlashBeatBuild` yet — packaging and
   the SHA-256 verified GitHub Release are **#35**'s scope, not #22's.

## 6. Web ↔ API ↔ Unity カタログ (P2 / Issue #41)

[`web-p2-2026-11-07/README.md`](web-p2-2026-11-07/README.md) — 本番
`https://flashbeat.rebuildup.dev` に対する実経路検証 (Issue #41 の P2)。

| 項目 | 状態 |
|---|---|
| Web Studio の publish → D1 | **証明済み** — スタジオの publish パスで本番 D1 に書き込むこと (編集ノーツの round-trip を含む) |
| 本番 API の契約一致 | **証明済み** — `web/tests/production-api.test.ts` 10 pass / 0 fail |
| Unity カタログの受け渡し | **コード + 応答の形で証明済み** — 取得経路 / キャッシュ / バージョン固定の実装と本番応答の検査 |
| Unity 実機の選曲 → 再生 → Result | **未証明** — Issue #48 / #31 の Worker の範囲 |

---

## 7. 2026-11-10 — macOS 実機の 1 曲完走と、main からの Win64 配布候補

**Windows 実機の代用ではない。** §1.6 / §1.8 と同じく、macOS で観測できたことだけを記録し、
展示 PC での実プレイは **未証明** のまま残す。

### 7.1 macOS 実機: 曲終了 → Result 到達 (PR #258)

`Assets/Game/Scripts/Common/AutoTestV6SongEnd.cs` + `script/mac-songend-harness.sh` による
**実機 (macOS プレイヤー) の自動実走**。克隆・時計注入・譜面の差し替えはしていない
(本物の曲・本物のメディア・本物の判定をそのまま通す)。

| 項目 | 実測値 |
|---|---|
| 再生曲 | 実カタログの最短曲「ツイッターランド」 (130 notes) |
| 譜面ノーツ数 / 実行時ノーツ数 | 130 / **130** (一致) |
| 確定 (settled) ノーツ数 | **130 / 130** |
| スコア / 最大コンボ | **992,307** / 129 |
| 曲終了状態 | `Completed` |
| 期限切れ強制完了だったか | **false** |
| 遷移先 | `ResultScene` (曲名と最終スコアが描画) |
| 実効解像度 | `screen=2560x1664 fullScreen=True` (**実ディスプレイ全画面**) |
| ハーネス | `RESULT pass=29 fail=0` / `script/mac-songend-harness.sh` が **exit 0** |
| EditMode | **1193 / 1193 passed / failed 0** |

証跡: [`233-song-end-harness.md`](233-song-end-harness.md)

### 7.2 Win64 配布候補 (main から生成)

| 項目 | 値 |
|---|---|
| 版 | `0.1.22` (`FLASHBEAT_VERSION_BASE=0.1.22` — 現行 Release `v0.1.21-win64-d581c4c` の次) |
| ソース SHA | `0bab5499dd6557b69577f885d17068987c1777b1` (`origin/main`) |
| 成果物 | `0.1.22+0bab549-win64-0bab549.zip` (274,854,582 bytes / 228 files) |
| ZIP SHA-256 | `a23c8c4af6983ffc6b19b1c69329fd2ebab3137f9d77e3a01598ca9c831b1c5d` |
| ビルド | `script/win64-build-mac.sh` (macOS ホスト + WindowsStandaloneSupport / Unity 6000.5.6f1) |
| 中身の確認 | `tools/update/Update.ps1` / `StreamingAssets/update-channel.txt` / `flashbeat-media-policy.json` あり。**同梱の音声・動画なし** |
| 状態 | **未証明** (Windows 実機での実行なし) |

手順 (SHA-256 照合 → 展開 → 3 解像度の実全画面 → 1 曲完走 7 項目) は
[`../operations/win64-candidate-acceptance-2026-11-10.md`](../operations/win64-candidate-acceptance-2026-11-10.md)。

### 7.3 この時点でなお未証明のもの

1. **Windows 実機での 1 曲完走**と **ノーツ・映像の視認**（展示 PC は未操作）。
2. **ストリーム音声 (m4a)** の再生（#13 / #4。Windows Media Foundation の制約）。
3. **3 解像度の実全画面**。macOS 側で証明できるのは実ディスプレイの 2560×1664 だけで、
   1920×1080 / 2560×1440 は **窓をリサイズした擬似全画面**であり証拠にならない。
4. **HOLD (ロングノーツ) の実機演奏**。`Assets/Game/Resources/*.json` の 27 曲すべての
   `notes[].type` は **1 (Tap) のみ**で、**type=2 (HOLD) のノーツが 1 つも存在しない**
   （`LongNote.TYPE_HOLD = 2`）。HOLD の実装は main にあるが、**到達可能な内容が無く**
   実機検証もできない。HOLD を含む検証用譜面（または実曲への HOLD 追加）が前提。
5. **#263 の修正後の再撮影**。PR #263 の独立レビューで、映像プレーンが prepare 前は
   白板のままになることと、根本原因の記載が誤っていることが判明し、修正中。
