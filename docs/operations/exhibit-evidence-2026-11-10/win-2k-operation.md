# Win64 実機受入 操作ログ (2K / 2560×1440) — v0.1.22 / 7af9242d

> 実施: 2026-11-10 21:13〜22:25 (JST)
> 実施者: cc (Claude Code)。オーナー指示「v0.1.22 の受入検証に限り `device-windows` の操作禁止を解除する」
> に基づき、オーナー PC (`device-windows`) 上で実機検証を行った記録。
> 対象 PC は展示 PC と同一 (`D:\4_apps\Unity\Build\FlashBeatExhibit*` が展示環境)。展示環境には**一切書き込んでいない**。

## 0. 結論

| 項目 | 結果 |
|---|---|
| Windows 実機での起動・全画面 | **PASS** |
| 1 曲完走 (Title→Select→Game→Result→Retry) | **PASS** (2 曲 / 計 4 回完走) |
| 映像 (ローカル mp4) | **PASS** |
| 音声 | **PASS (信号ベース)** — 人間の聴取はしていない (§6) |
| 判定・スコア (実キー入力) | **PASS** — ただしコンボ 2 以上は未達 (§8) |
| 実測できた解像度 | **2560×1440 のみ**。FHD (1920×1080) / 2560×1600 は未検証 (§8) |
| 重大な不具合 | **なし**。中程度 2 件 (§7) |

## 1. 対象の同一性 (実機で再計算した値)

| 項目 | 実機での実測 |
|---|---|
| ZIP | `D:\FlashBeat-acceptance\FlashBeat-0.1.22-win64-7af9242d.zip` |
| ZIP サイズ | 274,849,480 bytes |
| ZIP SHA-256 | `33ACD1069987694549F5F594DC910B6DE79EADA51CEE5514378CF13B6BDB7A01` (指示値と一致) |
| `version.json` | `version=0.1.22` / `buildSha=7af9242d9ca9d580ad5078f085973a72120e5085` / `tag=v0.1.22-win64-7af9242d` / `platform=win64` / `mediaPolicy=song-free` / `createdAt=2026-11-10T09:18:56Z` |
| 更新チャンネル | `stable` (`FlashBeat_Data/StreamingAssets/update-channel.txt`) |
| 展開先 | `D:\FlashBeat-acceptance\v0.1.22-7af9242d\app` (203 files / 407,787,591 bytes) |
| **画面に出た版** | Title 右上に `v0.1.22+7af9242d` (`win-2k-title.png`) |

ZIP は別の候補に差し替えていない。ハッシュは展開前と展開後に 2 回実測し、sidecar (`.zip.sha256`) と一致した。

## 2. 環境

| 項目 | 値 |
|---|---|
| ホスト | `UUU` / Windows 11 Home Insider Preview build 29704 |
| CPU / GPU | Core i7-13700H / NVIDIA RTX 4050 Laptop (driver 32.0.16.2114) + Intel Iris Xe |
| セッション | セッション 1 (対話デスクトップ) |
| ディスプレイ (有効) | `\\.\DISPLAY5` 2560×1440 (primary, 60Hz) |
| ディスプレイ (無効) | `\\.\DISPLAY1` 1920×1080, `\\.\DISPLAY6` 2560×1600 |
| 既存の展示環境 | `D:\4_apps\Unity\Build\FlashBeatExhibit[,_s1〜s5]` (2026-11-06〜07 ビルド) — **未変更** |
| 受入用の展開先 | `D:\FlashBeat-acceptance\v0.1.22-7af9242d\app` — 展示環境とは別ディレクトリ |

検証開始時の状態: FlashBeat は起動していなかった (プロセスなし)。既存プロセスの強制終了は行っていない。

## 3. 試験方法 (何が自動で、何が人間か)

- **キー入力・マウス入力はすべて合成**: セッション 1 上の PowerShell エージェントが `keybd_event` / `mouse_event` で送出。人間は 1 度もキーを押していない。
- **画面は実ディスプレイの全画面キャプチャ** (`CopyFromScreen`)。ウィンドウの移動・リサイズによる擬似全画面は行っていない。
- **音は聴取していない**: (a) プロセス単位の WASAPI ピーク値、(b) アプリ内 `AudioLevelProbe` の `STATUS … rms=/peak=` 行、の 2 系統で判定した (§6)。
- 起動は `FlashBeat.exe` を**引数なし**で (環境変数 `FLASHBEAT_YTDLP_PATH` のみ、yt-dlp の解決に必要)。
- 実施中、ssh のコンソールウィンドウがフォーカスを奪うとキー入力がゲームに届かない。そのため実行中は ssh を張らず、キー操作はすべて 1 回のキュー投入で行った。

## 4. 操作ログ (時刻 / 操作 / 何が起きたか)

3 回起動した。pid と時刻はエージェントログの実測。

### 4.1 起動 1 (21:16:16, pid=45520)

| 時刻 | 操作 | 結果 |
|---|---|---|
| 21:13 | 画面情報を取得 | DISPLAY5 2560×1440 のみ active。他 2 面は inactive |
| 21:16 | `FlashBeat.exe` 起動 (引数なし) | 起動。35 秒後に Title |
| 21:18 | 画面撮影 | `win-2k-title.png` (`v0.1.22+7af9242d`)、`win-2k-fullscreen.png` |
| 21:18 | Space | Title → 選曲 |
| 21:19〜21:22 | ↓ を数回 | 選曲カーソル移動。**ツイッターランド** (No.15, BPM 142, NOTES 130, LEVEL 15, 0:44) を選択 |
| 21:27:22 | Space (準備画面) | **反応なし** (再生開始せず) → §7-1 |
| 21:29:21 | Space | 再生開始 (`PLAYBACK_START`) |
| 21:35〜21:37 | A S D F / J K L ; を繰り返し入力 (実キー) | Perfect/Great/Bad/Miss が発生。映像・ノーツ・判定ライン・キーガイドが見える (`win-fullplay-game.png`) |
| 21:30:5x | 曲終了 → Result | **Perfect 1 / Great 0 / Bad 0 / Miss 129 = 130 = NOTES 130**、SCORE 7,692 (初プレイなので 前回最高 +7,692) |
| 21:30:54 | Result で **R (Retry)** | 同じ曲の準備画面 (`win-fullplay-retry.png`) |
| 21:35:26 | Space | 再生開始 (2 周目) |
| 21:36:10 ごろ | 曲終了 → Result | **Perfect 3 / Great 2 / Bad 3 / Miss 122 = 130**、SCORE 36,923 (前回最高 +29,231 = 36,923 − 7,692) |
| 21:39:43 | Result で R (Retry) | 準備画面。直後の Space (21:39:46) は**無反応** (再準備中のため = §7-1) |
| 21:41:51 | Space | 再生開始 (3 周目) |
| 21:42:48 | 曲終了 → Result | **Perfect 5 / Great 1 / Bad 3 / Miss 121 = 130**、SCORE 47,692 (前回最高 +10,769 = 47,692 − 36,923)。`win-fullplay-result.png` |
| 21:45:52 | Result で Esc → 選曲へ | 選曲画面に戻る |
| 21:46 | ↓ で **Who** を選択 → Space → 準備 → Space | 再生開始。10 秒ほど再生 |
| 21:46:21 | Esc | ポーズ (`win-2k-pause-who.png`: LEVEL 5 / NOTES 139 / 曲長 1:08) |
| 21:47:40 | Backspace | 選曲へ |
| 21:47:45 | Esc → Title | Title |
| 21:47:50 | O | Option (Play タブ) |
| 21:48:44〜21:48:55 | Tab ×3 | Display → Sound → Data。4 タブすべて撮影 (`win-2k-option-*.png`)。Data タブの Media cache は 395.7 MB |
| 21:48:55 | Tab | **Play に戻らない** (タブは非循環) |
| 21:49:39 | ↓↓↓ + Enter | Key Layout を開くつもりが、**Media cache 行を実行**してしまい、キャッシュが **395.7 MB → 0 B** に (確認ダイアログなし) → §7-2。`win-2k-option-data.png` (実行前) / `win-2k-media-cache-cleared.png` (実行後) |
| 21:52〜21:54 | 選曲に戻されていたため Esc / O で Option を開き直し、Play タブで ↓↓↓ + Enter | キー設定エディタのオーバーレイが開き、Key Layout が **標準 → カスタム** に変化 |
| 21:54:47 | ↑ で Key Layout 行へ → ← | **確認ダイアログ**「すべてのレーンのキー割り当てを標準に戻します。現在のカスタム割り当ては消去されます (元に戻せません)。」(`win-2k-keylayout-confirm.png`) |
| 21:54:51 | Esc | ダイアログをキャンセル (**「戻す」は押していない** = リセットしていない) |
| 21:56:33〜 | Esc → Title → Esc | アプリ終了。クラッシュではない (Player.log に Physics cleanup / Input System shutdown / CodeReloadManager destroyed、Windows イベントログに Application Error / Hang / WER なし) |

### 4.2 起動 2 (22:04:39, pid=42208) — 起動直後の全画面・音声の採取

| 時刻 | 操作 | 結果 |
|---|---|---|
| 22:05:10 | Space (Title → 選曲) | 選曲 |
| 22:05:16 | Space | 準備画面（既定曲 **ロストアンブレラ**） |
| 22:05:27 | Space | 再生開始。`PLAYBACK_START song='ロストアンブレラ' videoId='DeKLpgzh-qQ' source=StreamCombined` |
| 22:05:30〜22:06:07 | A S D F / J K L ; 入力、途中で撮影 | 映像再生・音声あり (`STATUS … rms=0.08〜0.52 peak=0.6726 video[playing=True prepared=True]`)。`win-2k-play-lostumbrella.png` と同種の画面 |
| 22:06:07 | Esc → Backspace → Esc ×2 | ポーズ → 選曲 → Title → 終了 |

### 4.3 起動 3 (22:16:27, pid=38264) — Player.log を最初から最後まで保全したクリーンラン

**証拠の `win-2k-Player.log` はこの起動のもの**。起動から終了まで一続きで残っている。

| 時刻 | 操作 | 結果 |
|---|---|---|
| 22:16:32 | (自動) 起動 | `[FullscreenDiagnostics] event=startup, screen=2560x1440, currentResolution=2560x1440@59.951Hz, fullScreen=True, fullScreenMode=FullScreenWindow, dpi=96, displays=3, display0=…active:True, display1=…active:False, display2=…active:False` |
| 22:17:07 | Space | Title → 選曲 (`win-fullplay-select.png`: ロストアンブレラ NOTES 268 / LEVEL 8 / BPM 274 / BEST 23,134、28/28) |
| 22:17:14 | Space | 準備画面 (`Space to Start`) |
| 22:17:25 | Space | 再生開始 (`PLAYBACK_START … mediaTime=0.000 wallClock=2026-11-10T22:17:25.889 gameTime=48.921`) |
| 22:17:25〜22:20:09 | A S D F / J K L ; を約 100 秒入力、途中で 2 回撮影 | 映像・ノーツ・歌詞（`浸み込んでしまう前に` など、かな付き）が正常。`win-2k-play-lostumbrella.png` |
| 22:20:09 | (自動) 曲終了 | `[MusicManager] song completed: judged 268/268, mediaEnded=False, state=Completed` → `[Perf] scene load start ResultScene` |
| 22:20:10 | 画面撮影 | **Perfect 1 / Great 0 / Bad 0 / Miss 267 = 268 = NOTES 268**、SCORE 3,731、曲長 1:29 (`win-2k-result-lostumbrella.png`) |
| 22:21:09 | Space (OK) → Esc → Esc | 選曲 → Title → 終了（正常終了） |

## 5. 受入 10 項目の判定

| # | 項目 | 判定 | 根拠 |
|---|---|---|---|
| 1 | Title→Select→Game→Result→Retry | **PASS** | ツイッターランドを 3 回 (Retry ×2 で同曲を再開)、ロストアンブレラを 1 回、計 4 回完走。Retry は Result で `R` → 同じ曲の準備画面 (`win-fullplay-retry.png`) |
| 2 | 選曲した曲と開始した曲・譜面が一致 | **PASS** | 選曲画面の ロストアンブレラ (NOTES 268) → `PLAYBACK_START song='ロストアンブレラ' videoId='DeKLpgzh-qQ'` → Result の譜面情報も ロストアンブレラ / NOTES 268 / BPM 274。ツイッターランドも同様 (NOTES 130) |
| 3 | 8 レーン / ノーツ / 判定ライン / キーガイド | **PASS** | `win-fullplay-game.png`, `win-2k-play-lostumbrella.png`: 8 レーン、白いノーツ、下端の判定ライン、キーガイド `A S D F J K L ;` が視認できる |
| 4 | ローカル mp4 の映像 / 音声 | **PASS** | `video file: …\FlashBeatMedia\<id>\merged.mp4 (cache)`、`STATUS … video[playing=True prepared=True]`、画面に映像フレーム。音声は §6 |
| 5 | 実キー入力 → 判定 / スコア / コンボ | **PASS (条件付き)** | 合成キー入力で Perfect/Great/Bad/Miss が発生しスコアが増減。**コンボは 1 が最大** (§8) |
| 6 | 曲終了時のノーツ数と判定数の一致 | **PASS** | 130 = 130 (3 回) / **268 = 268** (`judged 268/268`) |
| 7 | Option の 4 タブ / キー設定 / 確認ダイアログ | **PASS** | 4 タブ撮影。Key Layout の `←` で確認ダイアログ (C-6)。キー設定エディタも起動。**Media cache だけは確認なしで実行** (§7-2) |
| 8 | Result からの Retry / 別の曲へ移動 | **PASS** | Retry で同曲再開、Esc → 選曲 → Who を選んで準備・再生 |
| 9 | 日本語フォント (豆腐・欠け・重なり) | **PASS** | Title/選曲/歌詞/Result の曲名・歌詞・かな付きルビがすべて表示。豆腐・文字欠けは観測していない |
| 10 | 全画面と実ディスプレイの解像度 | **PASS (2560×1440 のみ)** | `FullscreenDiagnostics` とスクリーンショットが 2560×1440。他解像度は未検証 (§8) |

`PlayerSettings` の設定値ではなく、起動時にアプリ自身が出した実効値 (`FullscreenDiagnostics`) と、
実際のスクリーンショット (2560×1440、タスクバー・枠・黒帯なし) を根拠にしている。

## 6. 実測値

### 全画面

```
[FullscreenDiagnostics] event=startup, screen=2560x1440, currentResolution=2560x1440@59.951Hz,
fullScreen=True, fullScreenMode=FullScreenWindow, dpi=96, safeArea=0,0,2560x1440, displays=3,
display0=system:2560x1440,rendering:2560x1440,active:True,
display1=system:2560x1600,rendering:2560x1600,active:False,
display2=system:1920x1080,rendering:1920x1080,active:False, quality=1, time=2026-11-10T13:16:32Z
```

スクリーンショットのピクセルサイズも 2560×1440 (`sips -g pixelWidth` = 2560)。

### 音声 (2 系統)

1. **プロセス単位 WASAPI** (pid=45520, 再生中 42 秒をサンプリング):
   `samples=676 max=1.0000 avg=0.9120 nonzero=674`。FlashBeat 自身のセッションだけを計測している。
2. **アプリ内 `AudioLevelProbe`** (`win-2k-Player.log` の STATUS 行):
   `audioMode=StreamCombined`、`rms=0.0802〜0.5200`、`peak=0.6726〜0.6748` が再生中ずっと非ゼロ、
   曲終了後 (`mediaTime=**88.07**`) も値が続く。

`videoPrepared=True` だけでは判定していない。上記 2 系統の信号が、再生区間にわたって非ゼロであることを根拠にした。
**ただし、実際に音を耳で聴いた人間はいない** (展示 PC に人間はおらず、スピーカー出力の物理確認はしていない)。

### 判定

| 曲 | Perfect | Great | Bad | Miss | 合計 | 譜面 NOTES | SCORE | 前回最高 |
|---|---|---|---|---|---|---|---|---|
| ツイッターランド (1 回目) | 1 | 0 | 0 | 129 | **130** | 130 | 7,692 | +7,692 |
| ツイッターランド (Retry 1) | 3 | 2 | 3 | 122 | **130** | 130 | 36,923 | +29,231 |
| ツイッターランド (Retry 2) | 5 | 1 | 3 | 121 | **130** | 130 | 47,692 | +10,769 |
| ロストアンブレラ | 1 | 0 | 0 | 267 | **268** | 268 | 3,731 | ±0 (BEST 23,134 を下回る) |

`前回最高` の差分が直前のスコアと 1 円まで一致する (7,692 → 36,923 → 47,692) ので、
ハイスコアの保存と比較表示も機能している。
なお、この検証で共有ユーザーデータのハイスコアが更新されたため、§9 の復元で元に戻した。

## 7. 不具合・観察

### 7-1 (中) 「Space to Start」の表示中に Space が無反応

準備画面は読み込み完了前から `Space to Start` を表示するが、開始判定は
`Assets/Game/Scripts/Gameplay/MusicManager.cs:939`
`bool canStart = (mediaReady || Time.time - sceneStartTime > FallbackStartTimeout) && notesReady;`
のため、メディア/ノーツの準備が終わる前の Space は**何のフィードバックもなく捨てられる**。

- 実測: 21:27:22 の Space は反応せず、21:29:21 の Space で開始した (画面は同じ `Space to Start`)。
- 影響: 展示では「押したのに始まらない」ので来場者が連打/離脱する。**受入は阻害しない** (再入力で開始する)。
- 起票候補: 「準備中であることを示す (進捗/`Loading…`) か、押下をバッファする」。

### 7-2 (中) Option → Data の「Media cache」が確認なしで実行される

`Assets/Game/Scripts/UI/V6/Option/V6OptionView.cs:974`
`new Row("Media cache", …, ClearMediaCache, …, danger: true, isAction: true)` は
Enter / クリックで即 `ClearMediaCache()` を呼ぶ。同じタブの「Clear save data」には
`OpenClearConfirm()` の確認ダイアログがあるのに、Media cache には無い。

- 実測: Key Layout を開くつもりの ↓↓↓+Enter で Media cache 行が実行され、**395.7 MB → 0 B**。
  `win-2k-option-data.png` (前) / `win-2k-media-cache-cleared.png` (後)。
- 影響: 展示機で誤操作すると 55 ファイル / 395.7 MB のキャッシュが消え、次回プレイ時に再ダウンロード
  (ネットワーク必須) になる。受入は阻害しないが、展示運用では事故になりうる。
- 起票候補: Clear save data と同じ確認ダイアログを付ける。

### 7-3 (低) 準備画面に曲名が無い

`Space to Start` の準備画面には曲名・譜面情報が無い。Retry 直後や選曲から入った直後に、
「今どの曲を始めようとしているか」が画面から分からない。

### 7-4 (文書) 受入手順書の Player.log パスが誤り

`win64-candidate-acceptance-2026-11-10.md` §3.3 は
`%USERPROFILE%\AppData\LocalLow\rebuildup\FlashBeat\Player.log` としているが、
実体は `companyName=KimuraYusuke` のため
`%USERPROFILE%\AppData\LocalLow\KimuraYusuke\FlashBeat\Player.log`。

### 7-5 (情報) Windows Media Foundation の警告

`First video frame not zero: 1 (0.033333s). Result may be out of sync.` と
`Color primaries 0 is unknown or unsupported by WindowsMediaFoundation.` が出るが、
映像・音声は正常に再生された。**`0x80070005` (URL リーダーの失敗) はこの経路では出ていない**
(ローカル mp4 を読むため)。

### 7-6 (情報) PlayerPrefs の解像度既定値が書き換わる

起動すると `Screenmanager Resolution Width/Height Default` が 960×540 → 1920×1080 に変わる
(Unity が全画面時の既定ウィンドウサイズを書き戻すため)。§9 で復元済み。

## 8. 未確認 (推測で埋めていない項目)

| 項目 | 状態 |
|---|---|
| FHD 1920×1080 の実機全画面 | **未検証**。DISPLAY1 は `active:False` (電源/接続されていない) で、その解像度の画面を出していない |
| 2560×1600 (DISPLAY6) の全画面 | **未検証**。同上 |
| 音声の人間による聴取 | **していない**。信号ベースの判定のみ (§6) |
| コンボ 2 以上 | 合成入力の間隔 (約 340 ms) では譜面に同期できず、**最大コンボ 1**。実入力でコンボが伸びるかは未確認 |
| キー設定エディタでの再割り当て確定 | オーバーレイを開いたところまで。キーを割り当てて保存する操作はしていない |
| 表示設定 (Key Guide / Lyrics) の再起動後の永続 (#343) | **未検証**。切り替えて再起動する操作をしていない |
| Result の Register (名前入力, #134) | **未実施** |
| Ready 連打時の Scene 遷移の重複 (#222) | **未検証**。Space は 1 回ずつ送っている |
| 更新通知 (#118) | Title に更新通知は出ていない。ただし v0.1.22 の stable がまだ存在しないため、**「出ないのが正しい」ことの確認にはならない** |
| カスタムキー割り当てでの実プレイ | 未検証 (標準のみ) |

## 9. 後片付け (共有ユーザーデータの復元)

FlashBeat のユーザーデータは**既存の展示インストールと共有** (`HKCU\Software\KimuraYusuke\FlashBeat` と
`C:\Users\rebui\AppData\LocalLow\KimuraYusuke\FlashBeat`)。検証前に取得したバックアップから復元した。

| 対象 | 検証前 | 検証後 (復元) |
|---|---|---|
| LocalLow (キャッシュ含む) | 415,836,958 bytes / メディアキャッシュ 55 files | 415,836,958 bytes / 55 files に復元 |
| Media cache (アプリ表示) | 395.7 MB (途中で 0 B になった) | 復元 (55 files) |
| `Screenmanager Resolution … Default` | 960×540 | 960×540 に復元 |

- 復元は `reg import` + `robocopy /MIR`。復元後はハイスコア・表示設定・解像度の既定値とも、
  検証前のバックアップと同じ値になっている (検証中に ツイッターランド のハイスコアが
  7,692 → 36,923 → 47,692 と更新されたが、復元で検証前の値に戻した)。
- 検証用に作ったスケジュールタスク `FlashBeatAcceptAgent` / `FlashBeatAcceptLaunch` は削除した。
- FlashBeat は終了済み。検証中に発生した ssh 由来のコンソールプロセスも終了させた。
- 成果物は `D:\FlashBeat-acceptance\` に残置 (app / shots / backup / agent)。**展示環境は未変更**。

## 10. ファイル対応

| ファイル | 内容 |
|---|---|
| `win-2k-fullscreen.png` | 起動直後の全画面 (2560×1440、枠・タスクバーなし) |
| `win-2k-title.png` | Title。右上に `v0.1.22+7af9242d` |
| `win-2k-Player.log` | **4.3 の起動 (22:16〜22:21)** の Player.log (`<USERPROFILE>` に伏せ字化、UTF-8) |
| `win-fullplay-select.png` | 選曲 (3 カラム、ロストアンブレラ NOTES 268 / LEVEL 8 / BEST 23,134、28/28) |
| `win-fullplay-select-who.png` | 選曲 (別の曲 Who を選んだ状態) |
| `win-fullplay-game.png` | プレイ中 (ツイッターランド、8 レーン / ノーツ / 判定ライン / キーガイド / 映像 / 歌詞) |
| `win-2k-play-lostumbrella.png` | プレイ中 (ロストアンブレラ、4.3 と同じ起動) |
| `win-fullplay-result.png` | Result (ツイッターランド: 5/1/3/121 = 130、47,692) |
| `win-2k-result-lostumbrella.png` | Result (ロストアンブレラ: 1/0/0/267 = 268、3,731) — 4.3 と同じ起動 |
| `win-fullplay-retry.png` | Retry 後の準備画面 (同曲、`Space to Start`、score 0) |
| `win-2k-pause-who.png` | ポーズ (Who: LEVEL 5 / NOTES 139 / 曲長 1:08) |
| `win-2k-option-play.png` ほか 3 枚 | Option の 4 タブ (Play / Display / Sound / Data) |
| `win-2k-keylayout-confirm.png` | Key Layout の確認ダイアログ (C-6) |
| `win-2k-media-cache-cleared.png` | Media cache が 0 B になった状態 (§7-2) |
| `win-2k-option-data.png` | Media cache が 395.7 MB だった状態 (実行前) |
