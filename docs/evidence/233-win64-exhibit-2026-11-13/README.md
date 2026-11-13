# Win64 展示候補 — 実機受入レポート (2026-11-13)

Issue #233 / #242 の展示受入の証跡。

- 実施場所: 展示機 (`device-windows`, Windows 11 Insider Preview / UUU\basic)
- 対象: 展示候補 **`0.1.23+b6860a80`** (SHA-256 一致を確認)
- 実施者: cc / 2026-11-13 10:26 JST (2K), 2026-11-13 11:26 JST (真の FHD 実機)

候補は変更していない: ディスク上の ZIP (`FlashBeat-0.1.23-win64-b6860a80.zip`) を、**新規フォルダ** `C:\Users\rebui\Desktop\FlashBeat-Exhibit` へ展開しただけで、既存のビルド・設定・プロセス・スケジュールタスクは一切触っていない。

> **訂正履歴 (2026-11-13 12:00 JST)**
> 当初「FHD 検証は `-screen-width 1920 -screen-height 1080 -screen-fullscreen 1` で実施した」と記録したが、
> この引数による起動は **強制的にウィンドウを 1920x1080 にするものであって、物理 FHD モニターでの実画面検証ではない**。
> オーナー指摘 (「実際にFHD解像度のモニターで試す必要があります。設定でいじるだけではだめです」) を受け、
> **物理 DISPLAY1 (1920×1080) を一時的に主画面 (0,0) へ移動して引数なし起動**した検証を追加し、
> §6 を真の FHD 実機検証に書き換えた。強制引数による旧記録 (05〜08-fhd-*.png) は無効として残し、内容は記載しない。

## 1. 成果物の同一性 (展開直後)

| 項目 | 値 |
|---|---|
| ZIP SHA-256 | `9ee7e4a6ff89c4548e7bf4dc5836a5ab08b57878baea2c148e5a6e6309283d20` — 文書と一致 |
| `version.json` | `0.1.23` / `buildSha=b6860a80…` / tag=`v0.1.23-win64-b6860a80` / platform=win64 / mediaPolicy=`song-free` |
| `FlashBeat.exe` | 667,648 bytes |
| 更新チャンネル | `stable` (AssetBundle 内 `StreamingAssets/update-channel.txt`) |
| 同梱メディア | 0 件 (policy 通り) |

展開フォルダは `C:\Users\rebui\Desktop\FlashBeat-Exhibit`。中身は `D3D12/`, `FlashBeat_Data/`, `MonoBleedingEdge/`, `tools/`, `DirectML.dll`, `FlashBeat.exe`, `UnityPlayer.dll`, `version.json`, `flashbeat-media-policy.json`。

## 2. 実フルスクリーン (主ディスプレイ)

3 面構成 (fullscreen diagnostics の抜粋、2K 起動時):

```
[FullscreenDiagnostics] event=startup,
  screen=2560x1440, currentResolution=2560x1440@59.951Hz,
  fullScreen=True, fullScreenMode=FullScreenWindow, dpi=96,
  safeArea=0,0,2560x1440, displays=3,
  display0=system:2560x1440,rendering:2560x1440,active:True,
  display1=system:2560x1600,rendering:2560x1600,active:False,
  display2=system:1920x1080,rendering:1920x1080,active:False,
  quality=1, time=2026-11-13T01:27:58Z
```

- **主ディスプレイ = RTX 4050 Laptop GPU の 2560×1440** (2K)。FULLSCREEN=true、FullScreenWindow、dpi=96。
- FHD は **§6 のとおり物理 DISPLAY1 を一時的に主画面にして引数なし起動で検証した** (復旧済み)。

## 3. 実プレイ (-autotest v6songend)

展示機の Media cache を使い、`ツイッターランド` (BPM 142 / LV.15 / 130 notes の最短曲) で `select → game → gameplay → result` を一周。

```
[AutoTestV6SongEnd] RESULT pass=31 fail=0
song=ツイッターランド
songEndState=Completed
forcedByDeadline=False
scene=ResultScene
perfect=129 great=0 bad=0 miss=1
judged=130
settled=130
chartNotes=130 runtimeNotes=130
score=992307 combo=129 maxCombo=129 comboPeak=129
rankingMode=Online
judgedMatchesChart=True
```

- **31 PASS / 0 FAIL**。全曲完走、画面遷移は Select → Ready → Game → Result、judge 計 130/130 (chartNotes と一致)。
- `mediaTime` は 0.00 → 42.27s まで進行し、曲が最後まで流れた。`video[playing=True frame=4158 prepared=True]` は終端付近まで同じフレームで、フレームが進まず時計が先に進む現象は発生していない (この RUN では)。
- メディアは YouTube からの取得で、`video file: ...\merged.mp4` として既に Media cache に存在したことを確認 (曲を跨いだ永続キャッシュ)。
- シーン遷移 Chart → Game → Result。Ready での Space はハーネスが押している。
- RankingMode=Online: オンライン順位登録は行っていない (書き込みなし)。

## 4. 画面キャプチャ

### 2K 主画面 (2560×1440) — 01〜04

| ファイル | 解像度 | 内容 |
|---|---|---|
| `01-select.png` | 2560×1440 | v6 Select 画面。28/28 曲が検索フィールド・設定ボタン・`設定 O` などと揃って描画。選択曲 `ツイッターランド` が選ばれ、中央パネルに BPM 142 / NOTES 130 / LV.15 / BEST `-` |
| `02-game-ready.png` | 2560×1440 | GameScene の Ready 画面。"Space to start" が歌詞スロットに表示され、スコア 0 / grade C / COMBO 0 |
| `03-gameplay.png` | 2560×1440 | 実プレイ中の画面。ノーツが 8 レーンを流れ、Score 0 / grade C / COMBO 0 の初期状態 |
| `04-result.png` | 2560×1440 | Result 画面。S ランク、Score 992,307、PERFECT 129 / MISS 1 / COMBO 129、曲情報 (ツイッターランド / STEAKA) が正しく表示 |

### 旧記録 — 05〜08 (**無効**)

`05-fhd-select.png` 〜 `08-fhd-result.png` は `-screen-width 1920 -screen-height 1080 -screen-fullscreen 1` を付けた
**強制ウィンドウ起動**の画像であり、物理 FHD モニター上の実画面検証ではない。**PASS の根拠としては無効**。

### 真の FHD 実機 (物理 DISPLAY1, 1920×1080) — 09〜13

| ファイル | 解像度 | 内容 |
|---|---|---|
| `09-fhdreal-select.png` | 1920×1080 | Select 画面 (FHD プライマリ、引数なし起動)。**BEST 数値が `992,30 / 7` のように 2 行へ折り返す**表示崩れを再現 (2K では起きない) |
| `10-fhdreal-game-ready.png` | 1920×1080 | Ready 画面 |
| `11-fhdreal-gameplay.png` | 1920×1080 | 実プレイ画面 |
| `12-fhdreal-result.png` | 1920×1080 | Result 画面。2K と同一レイアウトで描画 |
| `13-fhdreal-ingame.png` | 1920×1080 | 実プレイ中 (Score 7,692 / COMBO 1 / Perfect 表示) の画面 |

## 5. ハーネスの実行ログ

- `Player.log` — 2K 起動時のログ
- `Player-fhdreal.log` — 真の FHD (DISPLAY1 プライマリ) 起動時のログ
- `autotest-v6songend-result.txt` (内容は §3 と同一)

メディアは exhibit PC に既にキャッシュされており (`C:\Users\rebui\AppData\LocalLow\KimuraYusuke\FlashBeat\FlashBeatMedia\e_qQEU_uGjw\merged.mp4`)、ネットワーク待ちも発生しなかった。`ro-dokanryou` は合体曲。

## 6. 真の FHD 実機検証 (1920×1080, DISPLAY1 プライマリ)

オーナー指摘を受け、**物理 DISPLAY1 (Intel Iris Xe, 1920×1080) を一時的に主画面へ移動**して引数なしで起動する手順で検証した。

手順 (展示機のディスプレイ配置を一時変更):

1. 事前に現在の配置を保存: `C:\Users\rebui\Desktop\display-config-backup-2026-11-13.txt` (query-displays3.ps1 の出力)
2. `SetDisplayConfig(SDC_APPLY | SDC_USE_SUPPLIED_DISPLAY_CONFIG)` で SOURCE 位置を移動:
   - FHD (adapter `0001553E.0` srcId=0): `(-1932,1422)` → **`(0,0)`** (主画面)
   - 2K (adapter `00017E26.0` srcId=0): `(0,0)` → `(1920,0)`
   - 2.5K (adapter `00017E26.0` srcId=1): `(-2560,-178)` → `(1920,1440)`
   - 検証は先に `SDC_VALIDATE` で実施済み (result=0)。`SDC_SAVE_TO_DATABASE` は付けず、再起動で戻る一時適用とした。
3. `FlashBeat.exe` を **引数なし**で起動 (`-autotest v6songend` のみ、`-screen-*` は一切付けない)
4. 完走後に同ツールで元の位置へ restore (DISPLAY5 が主画面へ戻ることを確認済み)

起動時のログ (真の FHD 実機):

```
[FullscreenDiagnostics] event=startup,
  screen=1920x1080, currentResolution=1920x1080@143.999225380574Hz,
  fullScreen=True, fullScreenMode=FullScreenWindow, dpi=96,
  safeArea=0,0,1920x1080, displays=3,
  display0=system:1920x1080,rendering:1920x1080,active:True,
  display1=system:2560x1440,rendering:2560x1440,active:False,
  display2=system:2560x1600,rendering:2560x1600,active:False,
  quality=1, time=2026-11-13T02:26:33Z
```

`-autotest v6songend` の結果 (真の FHD 実機):

```
[AutoTestV6SongEnd] RESULT pass=31 fail=0
song=ツイッターランド
songEndState=Completed
forcedByDeadline=False
scene=ResultScene
perfect=129 great=0 bad=0 miss=1
judged=130 settled=130 chartNotes=130 runtimeNotes=130
score=992307 combo=129 maxCombo=129 comboPeak=129
rankingMode=Online
judgedMatchesChart=True
```

**確認できたこと**

- 物理 DISPLAY1 が主画面 (display0, active:True) の状態で、**引数なし起動のまま 1920×1080 全画面 (FullScreenWindow) 描画**された
- Select → Ready → Game → Result の遷移と一曲完走 (31 PASS / 0 FAIL)
- 全画像が 1920×1080 で取得できた

**確認できた表示の崩れ**

- Select 画面の BEST 数値 (`992,307`) が FHD で **「992,30 / 7」のように 2 行へ折り返す** (09-fhdreal-select.png)。2K では起きない。
  v6 Select の BEST スロットに収まり切らないケースで、**#379 の「スコア折返し」の実機再現**である。

**実施後の復旧**

- `switch-display.ps1 -Mode restore` を実行し、DISPLAY5 が主画面 (0,0) に戻ったこと、
  DISPLAY1 が (-1932,1422)、DISPLAY6 が (-2560,-178) に戻ったことを確認済み。
- 展示機の FlashBeat プロセス・スケジュールタスクは残っていない。

## 8. 追加実機検証 (2026-11-13 12:00〜12:15 JST, 同一候補・同一フォルダ)

§6 の FHD 検証後、同じ展示フォルダ (`C:\Users\rebui\Desktop\FlashBeat-Exhibit`) で
さらに 3 系統の検証を行った。候補・フォルダ・環境設定は変更していない
(メディアキャッシュの一時退避を 1 件だけ行い、再ダウンロードで復元済み。§8.3)。

### 8.1 全画面監査 `-autotest v6screens` (2K 主画面)

| 項目 | 値 |
|---|---|
| 結果 | **pass=46 fail=0** / symbols=59 / screenshots=15 |
| 解像度 | **2560×1440 ネイティブ** (`resolution=2560x1440`、PNG 実測 2560×1440) |
| 証跡 | `v6screens-2k/` (15 枚 + `autotest-v6screens-result.txt`) |

対象画面: Title / Select (3 タブ + Empty + Settings) / Option (4 タブ) /
Result (+ RegisterModal) / Game (HUD / Pause / Loading)。
v6 の機械チェック (フレーム収まり・レガシー混在・記号アイコン) は全画面 FAIL 0。

### 8.2 全画面監査の途中経過 — 一時的な 1920×1080 描画 (記録)

12:01 JST の初回 `v6screens` 実行では、**DISPLAY5 (2560×1440) 上で
`rendering:1920x1080` の引き伸ばし描画**になる状態を 2 回観測した
(`screen=1920x1080 / display0=system:2560x1440,rendering:1920x1080,active:True`)。
ディスプレイ構成 (`query-displays3.ps1`) は退避前のバックアップと完全一致しており、
原因はドライバ/セッション側の一時状態と判断。**その後の起動ではネイティブ
2560×1440 に戻った** (12:05 JST の強制解像度起動 → 12:06 JST の引数なし起動で確認、
§8.3 の楽曲完走も 2560×1440)。

- 当該ランの証跡は `v6screens-1920x1080-transient/` に隔離 (PASS だが 1080p 座標系のため
  2K の証拠としては使わない)。
- **展示当日の注意**: 起動直後に画面が「にじんで」見える場合は、この状態の可能性がある。
  その場合はアプリを再起動するか、解像度を一度変更→戻すとネイティブに戻る (実測)。

### 8.3 未キャッシュ曲のメディア取得 → 完走 (`-autotest-song まにまに`)

展示機のメディアキャッシュは全 28 曲分が既に存在したため、**キャッシュを 1 件だけ
一時退避** (`9O2VyUM5MlQ` = まにまに, 48 秒 / 76 notes) して未キャッシュ経路を検証した。

| 項目 | 値 |
|---|---|
| yt-dlp 解決 | `yt-dlp -F videoId=9O2VyUM5MlQ formats=38 ok` → itag=18 640x360 mp4 |
| ダウンロード | `merged.mp4` を `FlashBeatMedia\9O2VyUM5MlQ\` へ取得 (約 9 MB) |
| 音声 | `audioMode=StreamCombined`、STATUS 行の rms=0.08〜0.44 / peak≈0.54 (ストリーム音声が出ている) |
| 結果 | **pass=31 fail=0** / songEndState=Completed / forcedByDeadline=False / judged=76==chartNotes / score=973,684 |
| 所要 | 起動 12:06:57 → PLAYBACK_START 12:07:22 (約 25 秒、ダウンロード+準備+遷移を含む) |
| 復元 | 検証後、新規ダウンロードで `9O2VyUM5MlQ` は復元済み。一時退避 `.bak` は削除済み |

証跡: `songend-manimani-uncached/` (4 枚 + result.txt + `Player-manimani-uncached.log`)。

**意味**: 展示で来場者が任意の曲を選んでも、キャッシュに無い曲は yt-dlp 経由で
約 25 秒 (この回線・この曲) で取得→プレイ可能になることが Win64 実機で確認できた。
ネットワーク不通時の挙動は未検証 (下記 §9)。

## 9. 受入判断の現状 (更新)

| # | 項目 | 状態 |
|---|---|---|
| 1 | 実 Win64 で候補が起動する | **PASS** (プレイ検証も含む) |
| 2 | 主画面 (2560×1440) で全画面描画 | **PASS** (`FullscreenWindow` / safeArea 2560×1440) |
| 3 | 選曲 → プレイ → Result 遷移 | **PASS** (ハーネス完走) |
| 4 | 曲完走 (1 曲最後まで) | **PASS** (`mediaTime≈44.07s` で Result へ)。追加で未キャッシュ曲 `まにまに` も完走 (§8.3) |
| 5 | 判定の計数が譜面ノード数と一致 | **PASS** (judged 130 == chartNotes 130, judgedMatchesChart=True。まにまにも 76==76) |
| 5b | 全画面の視覚監査 (v6screens) | **PASS** — 2K ネイティブで pass=46 fail=0 / 15 画面 (§8.1) |
| 5c | 未キャッシュ時のメディア取得 | **PASS** — yt-dlp ダウンロード→完走 約 25 秒 (§8.3) |
| 6 | FHD (1920×1080) 全画面描画 | **PASS** — **物理 DISPLAY1 を一時的に主画面へ移動した上で引数なし起動**し、`FullscreenWindow` / `display0=1920x1080,active:True` を確認。31 PASS / 0 FAIL。**BEST 数値の折返し表示あり** (09-fhdreal-select.png) |
| 7 | オーナーによる人間の聴取 | **未実施** (cc は音を聞かない。信号 rms/peak は両曲で確認済み) |
| 8 | オーナーの物理操作でのプレイ | **未実施** (autotest harness による自動操作) |
| 8b | Result → Retry の実遷移 | **未実施** (ハーネスは Result で終了。オーナー実機で 1 回確認) |
| 8c | Option 設定の保存方向 (画面→PlayerPrefs→再起動) | **未実施** (候補に v6interact ハーネス無し。オーナー実機で確認) |
| 9 | オンライン登録 API の書き込み確認 | **未実施** (本番 API へ書き込まない) |
| 10 | ネットワーク不通時のエラー復帰 | **未実施** (回線を切る検証は環境変更を伴うため未実施) |

フォルダ `C:\Users\rebui\Desktop\FlashBeat-Exhibit` は展示機に残る（候補配布用）。事後の管理はオーナー判断に任せる。

## 10. 後続作業

- 展示機の `FlashBeat` / `UnityCrashHandler64` プロセスは残っていないことを確認済み (`Get-Process FlashBeat` は空、scheduled task も削除済み)。
- ディスプレイ配置は検証前の状態 (DISPLAY5 主画面) に復旧済み。
- **BEST 数値の FHD 折返し** (#379) の対処は未着手。展示用の凍結候補 (`b6860a80`) は変更できないため、修正は次期ビルドで行う。
- 展示直前 (11/14) の実プレイ確認はオーナーが実施できる位置にある。
