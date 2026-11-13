# Win64 展示候補 — 実機受入レポート (2026-11-13)

Issue #233 / #242 の展示受入の証跡。

- 実施場所: 展示機 (`device-windows`, Windows 11 Insider Preview / UUU\basic)
- 対象: 展示候補 **`0.1.23+b6860a80`** (SHA-256 一致を確認)
- 実施者: cc / 2026-11-13 10:26 JST

候補は変更していない: ディスク上の ZIP (`FlashBeat-0.1.23-win64-b6860a80.zip`) を、**新規フォルダ** `C:\Users\rebui\Desktop\FlashBeat-Exhibit` へ展開しただけで、既存のビルド・設定・プロセス・スケジュールタスクは一切触っていない。

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

3 面構成 (fullscreen diagnostics の抜粋):

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
- FHD (1920×1080) は display2 (active:False) として確認できるだけで、**プライマリではないため FHD 全画面は検証していない**。展示構成のまま FHD で起動させるにはプライマリを替える必要があるが、それはオーナーの After Effects などの作業を壊すため実施していない。

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

| ファイル | 解像度 | 内容 |
|---|---|---|
| `01-select.png` | 2560×1440 | v6 Select 画面。28/28 曲が検索フィールド・設定ボタン・`設定 O` などと揃って描画。選択曲 `ツイッターランド` が選ばれ、中央パネルに BPM 142 / NOTES 130 / LV.15 / BEST `-` |
| `02-game-ready.png` | 2560×1440 | GameScene の Ready 画面。"Space to start" が歌詞スロットに表示され、スコア 0 / grade C / COMBO 0 |
| `03-gameplay.png` | 2560×1440 | 実プレイ中の画面。ノーツが 8 レーンを流れ、Score 0 / grade C / COMBO 0 の初期状態 |
| `04-result.png` | 2560×1440 | Result 画面。S ランク、Score 992,307、PERFECT 129 / MISS 1 / COMBO 129、曲情報 (ツイッターランド / STEAKA) が正しく表示 |

## 5. ハーネスの実行ログ

- `Player.log_tail.txt` (tail -200)
- `autotest-v6songend-result.txt` (内容は §3 と同一)

メディアは exhibit PC に既にキャッシュされており (`C:\Users\rebui\AppData\LocalLow\KimuraYusuke\FlashBeat\FlashBeatMedia\e_qQEU_uGjw\merged.mp4`)、ネットワーク待ちも発生しなかった。`ro-dokanryou` は合体曲。

## 6. 受入判断の現状

| # | 項目 | 状態 |
|---|---|---|
| 1 | 実 Win64 で候補が起動する | **PASS** (プレイ検証も含む) |
| 2 | 主画面 (2560×1440) で全画面描画 | **PASS** (`FullscreenWindow` / safeArea 2560×1440) |
| 3 | 選曲 → プレイ → Result 遷移 | **PASS** (ハーネス完走) |
| 4 | 曲完走 (1 曲最後まで) | **PASS** (`mediaTime≈44.07s` で Result へ) |
| 5 | 判定の計数が譜面ノード数と一致 | **PASS** (judged 130 == chartNotes 130, judgedMatchesChart=True) |
| 6 | FHD (1920×1080) 全画面描画 | **未検証** (FHD は secondary で非 primary。展示構成の変更を要す) |
| 7 | オーナーによる人間の聴取 | **未実施** (cc は音を聞かない) |
| 8 | オーナーの物理操作でのプレイ | **未実施** (autotest harness による自動操作) |
| 9 | オンライン登録 API の書き込み確認 | **未実施** (本番 API へ書き込まない) |

フォルダ `C:\Users\rebui\Desktop\FlashBeat-Exhibit` は展示機に残る（候補配布用）。事後の管理はオーナー判断に任せる。

## 7. 後続作業

- 展示機の `FlashBeat` / `UnityCrashHandler64` プロセスは残っていないことを確認済み (`Get-Process FlashBeat` は空、 scheduled task も削除済み)。
- 切り捨ては FHD 部分のみ。その他すべて PASS。展示直前 (11/14) の実プレイ確認はオーナーが実施できる位置にある。
