# Release-gate runs — 2026-11-06 (issue #22)

Runs that the release gate itself executed on this machine, on top of the evidence the stage workers
produced. Everything here was **observed on the built player**, not in the Editor.

Machine: Windows 11 x64 (10.0.29698), Unity 6000.5.6f1, NVIDIA GeForce RTX 4050 Laptop GPU,
displays 2560×1440 / 1920×1080 / 2560×1600 (**no 1280×720 panel on this machine**).

Driver: [`release/check/drive-gameplay.ps1`](../../../release/check/drive-gameplay.ps1)
(the #11 driver, reused with the artifact path / output directory / window geometry as parameters).

---

## Run A — exhibit folder, generation `743c88d` (実機 / 完全完走)

| 項目 | 値 |
|---|---|
| 実行した exe | `D:\4_apps\Unity\Build\FlashBeatExhibit\FlashBeat.exe` |
| フォルダ SHA-256 | `da338a6a8a73b23dbf6e0a7d26e498003e2f773bdb574323c416d9d17f6c563b` |
| `FlashBeat.exe` SHA-256 | `3031bb5e15e85eb0eaa4de271b81e2c8f5e8eeb1254c3dcc0a70c4b950c39a60` |
| 世代 | `743c88d`（#27 のフォント修正ビルド = PR #34 の Win64 出力、#35 が Release ZIP 化） |
| 起動方法 | `FlashBeat.exe` を**直接起動**（`-logFile` 付き。`-screen-width 1280 -screen-height 720 -screen-fullscreen 0`） |
| 実行時刻 | 2026-11-06 22:54:34 JST |
| 起動 → タイトル | **17.8 秒**（実測） |
| 実到達 | Title → Select（曲 No.2 まにまに）→ GameScene → **判定 76**（= まにまにの譜面ノーツ数全部）→ **Result** → `R` で **Retry**（シーンロード計 10 回） |
| ウィンドウ | windowed 1280×720（クライアント領域 1280×720 で全スクリーンショット取得） |
| 音 | **無音**。ログに `[MusicManager] yt-dlp resolver failed … Requested format is not available` → `YouTube prepare failed`（Invidious fallback も失敗）。この世代は旧 resolver のままで **YouTube ストリーム未再生 = 未証明** |
| 判定 | 全ノート `Miss`（レーンキーを押さない無人実行のため。**既知の未証明項目**で「音が鳴らない」と同じ扱い） |

証跡（**Git 外**。スクリーンショットは LFS バイナリなので Git には入れない）:

| ファイル | SHA-256 | 内容 |
|---|---|---|
| `D:\4_apps\Unity\Build\FlashBeatEvidence\2026-11-06-exhibit-743c88d\01_title.png` | `1ba6b34ddb32cc2788a20d941094b205dd21cc981d76ccad1e982f0aa0224c07` | タイトル |
| `…\02_select.png` | `f36a5f11d5e225f9102973db2d79673160fa93b72c689203c279d37cb4387387` | 選曲画面 |
| `…\03_select.png` | `f43cfaec2ba112013dfe85e1a3643488763fc8e63bc0b8ec50f45c665b98d0cf` | 選曲画面（安定後） |
| `…\04_select_song2.png` | `c4b29396da0b161ad78f769e0e95352a3396f1edc5d4fd4c5e17705032f541b6` | No.2 へ移動（日本語タイトル表示） |
| `…\05_game.png` | `4933f2499e70b66c5d503feb3ffcfbb91949f8f5fae0f06b2cc02eea03fcaa52` | GameScene |
| `…\06_game_playing.png` | `5816f9fc6b467ed9c8dfdf327d0f64fd315524939858512bd7198a7cc53fadc6` | 譜面進行中 |
| `…\07_result.png` | `858882674c0ec8b3fd65426759429c9733181c2c6cf9531379385005cc4d8d95` | リザルト画面 |
| `…\08_retry.png` | `5b167e809268531cc562e30f23a2728e55587a94a9caba69f0426cb08a58215d` | `R` によるリトライ後の GameScene |
| `…\Player.log` | `76e69aed3c7ccfd07532923ce394a4681ce7ad633cd389ad81b347e1e7fe11cd` | 生のプレイヤーログ（**未コミット**。video id を含むため、コミットするのはマスク済み版のみ） |
| `…\Player.redacted.log` | `44e485721885a1e55d4f02e1e341fc42dee147f4fc9de6ede661e3a937ac427d` | マスク済みログ（本ブランチに `docs/evidence/win64-2026-11-06-release-gate/Player-exhibit-743c88d.redacted.log` として commit） |
| `…\run-summary.json` | `67460c6c6de6996c634fb1aeac4e4a242a4adbd1d7ff182c669b4daf1099f43e` | ドライバのサマリ（startup 17.8 s / result 到達 / retry 到達 / 判定 76） |

マスク結果: video id 4 箇所・yt-dlp stderr の video id 2 箇所を置換。`redact-log.ps1 -Check` は
`signed media urls 0 / unexpected urls 0 / video ids 0 / yt-dlp stderr ids 0 → OK`。

## Run B — S0 外部アーカイブ（`Build0.1.0-win64-1c5137e`）

| 項目 | 値 |
|---|---|
| 実行した exe | `D:\4_apps\Unity\Build\FlashBeatBuild\Build0.1.0-win64-1c5137e\FlashBeat.exe` |
| フォルダ SHA-256 | `7ef323adcf1855ea1f192d0f542c099ec6fdfe52540bdf7737b55014f2ba4d53` |
| 実行時刻 | 2026-11-06 22:44 JST |
| 実到達 | Title → Select → No.2 → GameScene（**Result には到達せず**） |
| 中断理由 | 同じ PC で #13 のワーカーが並行して player を自動操作していた（`FlashBeat.exe` が 2 つ同時に起動）。2 つの driver が 1 つのデスクトップでキーボードとフォーカスを奪い合うため、この run は **信任できない** と判断して中断。統合ビルドを優先すべきという指示が出たため、exhibit フォルダを先に整えた |
| ログ事実 | この世代も yt-dlp が `Requested format is not available`（旧 resolver）→ Invidious も失敗 → 無音。判定 0（中断が早すぎ） |

証跡: `D:\4_apps\Unity\Build\FlashBeatEvidence\2026-11-06-S0-archive\`
（`01_title.png` `165cbb34a3631d091239154ffd938b439d457ae2670a58817c09774c1ae788c5` /
`02_select.png` `681c05b4263d1a2fe3f1bbb1673db9079d765b6b324c07bae535387abbb47994` /
`03_select.png` `4bbf4cdee07235e6fe450540e0ec2871ee86c8638f99942e80e2c9c5140e6bd4` /
`04_select_song2.png` `10840195812b00dbb6af2b1e24a478883ce8c713f65c04fb89f0be9b9465a072` /
`05_game.png` `cbd89f7b6017a54f9e0cddac1402fe0e6929d32845bc186f3d4660ce73c30430` /
`Player.log` `4a82ae1352bcf0efd1aecc8f8b42748c10fe99b0139efdb1086ea46f6f6c0a2b`）

## 統合ビルド（#13 + #27）

`origin/13` と `origin/27` を `--no-ff` でブランチ 22 に merge し（squash / rebase / force push なし）、
その HEAD（code state `f53575c`）から Win64 ビルドを生成した:

```
[BuildScript] Build succeeded: 1588350354 bytes, output to C:\Users\rebui\Desktop\FlashBeat.22\Builds\Windows\FlashBeat.exe
error CS count : 0
warning CS count : 352
```

（ログ抜粋: [`build-log-excerpt-integration.txt`](build-log-excerpt-integration.txt)）

## Run C — 展示第 2 世代 `FlashBeatExhibit_s1`（実機 / 完全完走 / **音あり**）

| 項目 | 値 |
|---|---|
| 実行した exe | `D:\4_apps\Unity\Build\FlashBeatExhibit_s1\FlashBeat.exe`（第 2 世代 = #13 + #27 統合ビルド） |
| フォルダ SHA-256 | `cbae8f677eb60dfeaa6ca6676121d4266fa99583f2b76bfeacacebcbd0924758` |
| `FlashBeat.exe` SHA-256 | `3031bb5e15e85eb0eaa4de271b81e2c8f5e8eeb1254c3dcc0a70c4b950c39a60` |
| ビルド | `Build succeeded: 1588350354 bytes` / 0 error CS / Unity 6000.5.6f1 |
| 実行時刻 | 2026-11-06 23:03:17 JST |
| 起動 → タイトル | **14.0 秒**（実測） |
| 実到達 | Title → Select（曲 No.2 まにまに）→ GameScene → **判定 76**（= まにまにの譜面ノーツ数全部）→ **Result** → `R` で **Retry**（シーンロード計 10 回） |
| ウィンドウ | windowed 1280×720（クライアント領域 1280×720） |
| **音** | **鳴っている（実証）**。ログの `[MusicManager] STATUS mediaTime=… audioMode=Bundled rms=0.30 peak=0.47` … `rms=0.37 peak=0.52`（RMS > 0 = 音出し中）。音源は `audioMode=Bundled`（=`Resources/Musics/<曲>.wav`） |
| **映像（YouTube ストリーム）** | **未証明**。yt-dlp は 37 format を取得し `video-only mp4 itag=232 1280x720 + audio-only m4a itag=140` を選び `persistentDataPath` にキャッシュしたが、Unity の `VideoPlayer` が **拡張子なしファイル**を認識できず `Failed to recognize video file format` → `VideoPlayer prepared=False` / `stream audio prepared=False`。結果として **音は同梱 wav フォールバック**、映像は出ない |
| 再生の開始条件 | #13 ビルドはメディア準備後に `press Space to start` を要求する。**準備前に Space を押しても始まらない**（本次 run は準備完了後に Space を 1 回押して開始） |
| ノーツ判定 | `PLAYBACK_START song='まにまに' videoId='<REDACTED-VIDEO-ID>' audioMode=Bundled mediaTime=0.000` → `STATUS … miss=76`（譜面全部） |

証跡（**Git 外**）:

| ファイル | SHA-256 | 内容 |
|---|---|---|
| `D:\4_apps\Unity\Build\FlashBeatEvidence\2026-11-06-exhibit-s1\01_title.png` | `116383228607eb9037a00f86da172377b053d74207c95a02b3dfc11ae06e8143` | タイトル |
| `…\02_select.png` | `3b15f075ad01a6304797fe752f6c967b57a0f8b8c75df60e0689563285179bf2` | 選曲画面 |
| `…\03_select.png` | `15a1216a2c63e7dd661acba108878ce01d2dd865353ddd41abb11a128108f5b8` | 選曲画面（安定後） |
| `…\04_select_song2.png` | `be6b1d3b07b90baad8fdd971646deecbfe84eac2e27d93aa0e85b2996beb8dfb` | No.2 へ移動（日本語タイトル表示） |
| `…\05_game.png` | `268f62f0585cc4d10a1cb7ee341fc22149df930be1871b047259a436a9697256` | GameScene（メディア準備中） |
| `…\06_game_playing.png` | `0366be35a5da4f50d25f076d095d25b048e7e1d1f1eeef72b5042ab3ad8e78dc` | 譜面進行中 |
| `…\07_result.png` | `78d2dedcf21145c49834c019772c80ff1ea9faf604fbc467246f225fb02db0fc` | リザルト画面 |
| `…\08_retry.png` | `7c27d9b0bb662a2fc479978ceccbd4af70bdd7d9b17acc774a0fa2c8a5ae197f` | `R` によるリトライ後の GameScene |
| `…\Player.log` | `d4bc5c261686eb3297fa245a0379bc47d1051de60f4a08a04f6c8d0507fde035` | 生のプレイヤーログ（**未コミット**。video id を含む） |
| `…\Player.redacted.log` | `1e3d09c76706ecec0917cbd036bda0febf6bab8ca071e733bb52f368aab18350` | マスク済みログ（本ブランチに `docs/evidence/win64-2026-11-06-release-gate/Player-exhibit-s1.redacted.log` として commit） |
| `…\run-summary.json` | `18b66ffb12ec3d60a4bad0e2027b84027529cedac5579058cc2975e09ea795f8` | ドライバサマリ（startup 14.0 s / result 到達 / retry 到達 / 判定 76） |

マスク結果: video id 2 箇所を置換。`redact-log.ps1 -Check` は
`signed media urls 0 / unexpected urls 0 / video ids 0 / yt-dlp stderr ids 0 → OK`。

### 共有マシンの注記

この run の間、同じ PC で #13 のワーカーが 3 個の player を同時に起動していました（自動操作）。
そのため driver は `-AllowConcurrentPlayers` で起動し、**開始の Space は準備完了後に手動で 1 回** 送りました。
他の自動化に干渉しないよう、driver はフォーカスが自分のウィンドウでない場合に **中断** します。
