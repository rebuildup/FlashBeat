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

`FlashBeat.exe` has the **same SHA-256 in every build** (it is the Unity launcher stub), so the
**folder fingerprint** is the artifact identity:

> SHA-256 over the UTF-8 bytes of `"<fileSha256>  <relative/path>"` lines, one per file, sorted by path.

## 1.5 展示フォルダ（運営者が起動する場所）

```
D:\4_apps\Unity\Build\FlashBeatExhibit\      ← 運営者は FlashBeat.exe を直接起動する
```

| 項目 | 値 |
|---|---|
| 世代 | `743c88d`（#27 のフォント修正ビルド = PR #34 の Win64 出力） |
| 入手元 | #35 の Release ZIP `FlashBeat-0.1.0-win64-743c88d.zip`（GitHub タグ `v0.1.0-win64-743c88d`） |
| ZIP の SHA-256 | `3dbdfecbdb91d916178666045d0c62e1fae717f7df0b77e4f9ddc2af1c036a45`（展開前に照合済み） |
| `FlashBeat.exe` SHA-256 | `3031bb5e15e85eb0eaa4de271b81e2c8f5e8eeb1254c3dcc0a70c4b950c39a60` |
| フォルダ SHA-256 | `da338a6a8a73b23dbf6e0a7d26e498003e2f773bdb574323c416d9d17f6c563b` |
| ファイル数 / 総バイト | 198 / 1 588 334 450 |
| 配置日時 | 2026-11-06 22:53 |
| 運用 | 展示中は **読み取り専用**。更新は運営者の明示操作のみ。診断ログは `FlashBeatExhibit\logs\` に集める |
| 世代違い | 統合ビルド（#13 + #27）は別フォルダに置く。既存世代は上書き・削除しない |

この世代の既知の限界: **音 / YouTube ストリームは未証明**（#27 のビルドは旧 resolver のままで、
yt-dlp が `Requested format is not available`、Invidious fallback も失敗）。譜面 → リザルト → リトライの
loop は #27 自身の run と #11 の S0 run の証拠（`docs/evidence/win64-2026-11-06-font/`）が担保する。

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
