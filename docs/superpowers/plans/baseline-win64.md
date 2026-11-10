# Windows x64 baseline — one complete song loop (Issue #11)

Captured: 2026-11-06
Issue: [#11](https://github.com/rebuildup/FlashBeat/issues/11) — *[S0/Win] Unity 既存1曲の起動・完走ベースラインと実行証拠*
Branch: `11` → base `release-0-1-0`
Built from source tree: `25fd896` (`Merge pull request #2 from rebuildup/1`) — the PR head adds **documentation only**; `git diff 25fd896..HEAD -- Assets/` is empty, so the binary below is the binary of this PR head.

## Environment

| Item | Value |
|---|---|
| OS | Windows 11 x64 (10.0.29698) |
| Unity | `6000.5.6f1 (0e0577a1a2ac)` — `C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Unity.exe` |
| GPU / API | NVIDIA GeForce RTX 4050 Laptop GPU, Direct3D 11 (`GfxDevice` from the player log) |
| Player scripting backend | Mono (`BuildOptions.None`) |
| yt-dlp | `2026.10.26` (mise install) — discovered by the player via `FLASHBEAT_YTDLP_PATH` (env override) or `PATH` |

## Prerequisites verified

- **Git LFS**: 120 tracked files, all present as real binaries (spot-checked PNG/WAV headers, not pointer stubs); `git lfs status` reports a clean tree.
- **Assets**: no missing scripts or missing assets in the build log (`has no script` / `MissingReference` diagnostics: 0).
- **Chart + BGM resources** for the two charts exercised below exist under `Assets/Game/Resources/`.

## Build

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Unity.exe' `
    -batchmode -quit `
    -projectPath 'C:\Users\rebui\Desktop\FlashBeat.11' `
    -executeMethod BuildScript.BuildStandaloneWindows64 `
    -logFile 'C:\tmp\unity_build.log'
```

Result (from `C:\tmp\unity_build.log`, excerpt in [`build-log-excerpt.txt`](../../evidence/win64-2026-11-06/build-log-excerpt.txt)):

```
16583:[BuildScript] Starting StandaloneWindows64 Build...
24500:[BuildScript] Build succeeded: 1554867382 bytes, output to C:\Users\rebui\Desktop\FlashBeat.11\Builds\Windows\FlashBeat.exe
```

- Compile diagnostics: **0 `error CS`**, 282 `CS0618` (obsolete API), 48 `CS0414` (unused field).
- Output: `Builds/Windows/FlashBeat.exe` (+ `FlashBeat_Data/`, `MonoBleedingEdge/`, `UnityPlayer.dll`), total ≈ 1.45 GiB.
- The previously working player on this machine (2024-03-13 build) was **not** overwritten; it is preserved under a separate name in `Builds/Windows_prev_verified_2024-03-13/`.

### Environment blocker that had to be cleared first (no repository change)

The first batch attempt aborted with exit code **198**:

```
No valid Unity Editor license found. Please activate your license.
```

Cause: the Unity Personal entitlement stored in `%LOCALAPPDATA%\Unity\licenses\UnityEntitlementLicense.xml` had an **expired offline validity period**, and the editor's licensing client could not refresh it (`Error: Access token is unavailable; failed to update`). Once Unity Hub re-issued the entitlement (new `IssueDate` on the license file, `Found 1 entitlements` in `Unity.Licensing.Client.log`), the *identical* command succeeded. No code, scene or project-settings change was required.

## Gameplay proof (built player, not the Editor)

Two full runs were executed against `Builds/Windows/FlashBeat.exe` with synthetic keyboard input
(`keybd_event` + `SetForegroundWindow`) and screen captures. The driver script and the raw,
unredacted player logs live outside the repository (`C:\tmp\fb_run2`, `C:\tmp\fb_run3`); the
redacted logs are attached under [`evidence/win64-2026-11-06/`](../../evidence/win64-2026-11-06/)
(transient signed `googlevideo.com` stream URLs are replaced with `<REDACTED-YOUTUBE-STREAM-URL>`).

### Run B — chart No.2 `まにまに` (BPM 174, 76 notes) — clean end-to-end run

| Step | Evidence |
|---|---|
| Title screen | `01-title.png` |
| Select screen, No.1 `ロストアンブレラ` highlighted | `02-select-no1.png` |
| `K` moved the selection to No.2 `まにまに` (BPM 174 / Total-Hits 383 / SongLevel 13) | `03-select-no2-manimani.png` |
| GameScene, chart running | `04-game-playing.png` |
| **Result** — assessment `C`, Judge `Perfect 0 / Great 0 / Bad 0 / Miss 76`, Score 0, `Retry` / `OK>>` buttons | `05-result.png` |
| `R` (retry) → GameScene reloaded (`Loading youtube video…`), counters reset | `06-retry-game-reloaded.png` |
| Full player log (scene loads, yt-dlp resolution, 76 judge events) | `Player-run3-manimani.log` |

`Miss 76` equals the **entire note count of the chart** (`Assets/Game/Resources/まにまに.json` contains
exactly 76 `"type"` entries), i.e. every note of the chart was judged and the run reached the result
screen on its own.

(The `Total-Hits 383` shown in the panel is the hardcoded `GManager.Shit[]` value, not the chart's
real note count — a pre-existing data inconsistency in the displayed stat, not a runtime defect.)

### Run A — chart No.1 `ロストアンブレラ` (BPM 274, 268 notes)

- Played through to the result screen: `07-result-run2-rostoreamblera.png` — Judge `Miss 268`, which
  again equals the full note count of `Assets/Game/Resources/ロストアンブレラ.json` (268 entries).
- `R` on the result screen re-entered the GameScene with `Score 0` / `Combo 0` (counters reset).
- Full player log: `Player-run2-rostoreamblera.log`.

**Consequence:** one complete song loop (select → chart play → result → retry) is proven on Win64
with the built player, on two different charts. All notes are `Miss` because the runs were
unattended (no lane keys were pressed) — that is expected for an automated baseline and is not a
gameplay defect.

## EditMode tests (supporting evidence only — not the acceptance gate)

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Unity.exe' -batchmode `
    -projectPath 'C:\Users\rebui\Desktop\FlashBeat.11' `
    -runTests -testPlatform editmode -testResults 'C:\tmp\TestResults.xml' `
    -logFile 'C:\tmp\unity_tests2.log'
```

```
<test-run id="2" testcasecount="36" result="Passed" total="36" passed="36" failed="0"
          inconclusive="0" skipped="0" duration="0.4181345" engine-version="3.5.0.0" />
```

36/36 EditMode tests pass on 6000.5.6f1 (0.42 s).

**Gotcha found while doing this:** the command documented in `CLAUDE.md`
(`-batchmode -quit … -runTests …`) exits with code 0 **without running any test** and without
writing `TestResults.xml` — `-quit` terminates the editor before the test runner starts. Dropping
`-quit` (the runner quits itself) makes it work. `CLAUDE.md` was updated accordingly.

## Findings that are *not* fixed by this issue (recorded for the follow-ups)

> **2026-11-10 追記**: 下の 1〜3 は `main` の実装で解消済みです。現在地は
> 下の「## 2026-11-10 追記 — 下の 1〜3 の現在地(issue #13 / #4)」を参照してください (1 は URL 再生経路を外してローカル再生に変更、2 は #140 の
> フォント対応、3 は #95 のロックステップ統一)。記録として当時の観測は残します。

1. **Video playback fails on this machine** (this is issue #13's subject):
   ```
   WindowsVideoMedia error 0x80070005 while reading https://…googlevideo.com/…
   Context: MFCreateSourceReaderFromURL
   [MusicManager] VideoPlayer failed to prepare within 30s for videoId=DeKLpgzh-qQ.
   ```
   yt-dlp itself resolves the stream fine (`[MusicManager] yt-dlp resolved videoId=… for '…'`),
   the failure is in the Windows Media Foundation URL reader. The chart/result/retry loop is not
   affected, but the **song audio rides on the video track** — `MusicManager` loads
   `Resources/Musics/<song>` into `Music` and never calls `audioS.Play()`, so with the video dead
   the build is currently silent. Audible playback was therefore **not** proven here.
2. **Japanese glyphs missing from the HUD font**: `The character with Unicode value \u30ED was not
   found in the [LiberationSans SDF] font asset` — the song/artist names render as `□` in the select
   and result panels. Cosmetic; gameplay logic unaffected.
3. **Parallel song arrays are out of lockstep**: `GManager.SongLong` / `SBPM` / `Slevel` / `Shit`
   have 30 entries while `SongName` / `Musician` / `SongURL` have 29 (`BuildSongs()` iterates
   `SongName.Length`, so nothing breaks today, but it violates the CLAUDE.md convention).
4. **Songs No.27 / No.28 are both `ニセモノ`** and there is no `Assets/Game/Resources/Musics/ニセモノ.wav`;
   the select screen shows them with Total-Hits 0. `MusicManager` does not null-check
   `Resources.Load("Musics/" + songName)`, so selecting those slots is not safe.

## 2026-11-10 追記 — 下の 1〜3 の現在地(issue #13 / #4)

> これは 2026-11-06 の観測を `main` (`44986de`、候補 `0.1.22` = `4445279`) の実装に
> 照合した結果です。**Windows 実機での再生は依然として未証明**です (結論 3 参照)。

### 1. 映像: URL を直接渡す経路はもう再生経路ではない

2026-11-06 の失敗 (`MFCreateSourceReaderFromURL` → `0x80070005`) は
**googlevideo の URL を `VideoPlayer` に直接渡す経路**の話でした。`main` ではその経路が
再生経路から外れ、**ダウンロードしたローカルファイルを再生**しています。

| 段 | 実装 | 何をするか |
|---|---|---|
| 0 | `MusicManager.cs:314` `ListFormatsAsync` → `:327` `YouTubeMediaCache.SelectPlan` | yt-dlp `-F` の結果から merged / 映像+音声 のどれを取るかを決める |
| 1 | `MusicManager.cs:332` → `ProbeDirectUrl` (`:591`, 判定 `:585`) | **動画ごとに 1 回だけ** googlevideo URL を `Prepare()` してみる。**診断専用** で、結果は再生に使わない (`PlayerPrefs` の `DirectUrlProbeKeyPrefix` で 1 回に制限) |
| 2 | `MusicManager.cs:341` `DownloadingMedia` → `YouTubeMediaCache.EnsureFileAsync` (`YouTubeMediaCache.cs:226`) | yt-dlp で同一バイト列を `persistentDataPath/FlashBeatMedia/<videoId>/` に落とす (キャッシュ再利用。ffmpeg があれば merged、無ければ映像と音声を別ファイル) |
| 3 | `MusicManager.cs:637` `PrepareLocalVideo` | ローカルファイルを `VideoSource.Url` で開く。開けなければ `file://` 形式で 1 回だけ再試行 (`:660`) |

回避の理由はコード内にも書いてあります (`YouTubeStreamResolver.cs:74`–`:79`):
Media Foundation は googlevideo の URL を拒否する (要求に必要な User-Agent / po-token を
`VideoPlayer` が送れない) ため、**同じバイト列をダウンロードしてローカル再生**して回避する。

したがって:

- Player.log の `0x80070005` / `MFCreateSourceReaderFromURL` は **故障ではなく probe の診断結果**。
  これ 1 行で「映像が出ない原因」と判断しないこと。
- 映像が出ないときは probe ではなく **段 2/3 のログ**を見る:
  `video file: … (cache|downloaded)` / `VideoPlayer prepared=… file=…` /
  `the player cannot open the cached file (…)`。

### 2. 音: 「`audioS.Play()` を呼ばないので無音」はもう当てはまらない

| `audioMode` | 開始処理 |
|---|---|
| `StreamCombined` (merged mp4) | `MusicManager.cs:1136` `songVideo.Play()`。音は映像トラックから `audioS` へ (`SetTargetAudioSource`) |
| `StreamSeparate` (映像+音声) | `:1147` `streamAudioPlayer.Play()` |
| `BundledWave` (同梱 wav / 復帰) | `:1151`–`:1153` と `SwitchToBundledAudio` (`:1170`, 中の `:1185`) で `audioS.Play()` |
| 失速時の切替 | `SwitchToBundledAudio` (`:1170`〜) |

どの経路を通ったかは起動ログの
`[MusicManager] PLAYBACK_START song=… source=<PlaybackSource> mediaTime=…` に残ります。
`docs/operations/win64-candidate-acceptance-2026-11-10.md` §3.4 の 5 行目はこの行で判定します。

### 3. まだ未証明 (Windows 実機が要る)

1. ダウンロードしたローカル mp4 を Media Foundation が開けるか (`VideoPlayer prepared=True`)。
2. その状態で音が鳴るか (`source=StreamCombined` / `StreamSeparate` / `BundledWave` のどれでもよい)。
3. `persistentDataPath` への書き込み権限 (展示 PC のユーザー権限) と、初回ダウンロードの待ち時間。

これらは macOS のテストでは代替できません。受入手順は
`docs/operations/win64-candidate-acceptance-2026-11-10.md` §3 です。

### 4. 2 と 3 も `main` では解消済み

- 2 (日本語が `□`): `4479114` (#140) が C# コード内文字列を SDF の文字セットに取り込み、
  `JapaneseFontAssetBuilder.EnsureDynamicFallback` (`Assets/Editor/JapaneseFontAssetBuilder.cs:249`)
  が動的フォールバックを付ける。`4479114` は候補 `4445279` の祖先 (確認済み)。
- 3 (並列配列の不整合): `main` では 7 配列すべて 29 件でロックステップ (#95)。
  `ChartDataIntegrityTests` が固定している。

## Repeat steps

```powershell
# 0. prerequisites
git lfs pull

# 1. build (Unity 6000.5.6f1)
& 'C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Unity.exe' -batchmode -quit `
    -projectPath 'C:\Users\rebui\Desktop\FlashBeat.11' `
    -executeMethod BuildScript.BuildStandaloneWindows64 -logFile 'C:\tmp\unity_build.log'
# expect: "[BuildScript] Build succeeded: …"

# 2. run the player with a log file
$env:FLASHBEAT_YTDLP_PATH = 'C:\Users\rebui\AppData\Local\mise\installs\yt-dlp\<version>\yt-dlp.exe'
& 'C:\Users\rebui\Desktop\FlashBeat.11\Builds\Windows\FlashBeat.exe' -logFile 'C:\tmp\fb_run\Player.log'

# 3. manual key path (window focused)
#    Opening (auto ~6 s) -> Title [Space] -> Select [K to change song] [Space]
#    -> GameScene [wait 8 s] [Space to start] -> plays -> Result [R = retry] [Space = select]
# 4. proof markers in the player log
#    "Your scene has been loaded" x2 per scene load   (Title / Select / Game / Result / Game-after-retry)
#    "[MusicManager] yt-dlp resolved videoId=… for '<song>'"
#    one "Miss" / "Perfect" / "Great" / "Bad" line per judged note
```

## Artifacts

| Artifact | Location |
|---|---|
| Built player | `Builds/Windows/` (gitignored, 1.45 GiB) |
| Previous known-good player, preserved under a different name | `Builds/Windows_prev_verified_2024-03-13/` |
| Build log excerpt | [`evidence/win64-2026-11-06/build-log-excerpt.txt`](../../evidence/win64-2026-11-06/build-log-excerpt.txt) |
| Player logs (redacted) | `evidence/win64-2026-11-06/Player-run2-rostoreamblera.log`, `Player-run3-manimani.log` |
| Screenshots | `evidence/win64-2026-11-06/0*.png` |
| Gameplay driver used for the runs | `evidence/win64-2026-11-06/drive-gameplay.ps1` |
