# macOS 実機受入 — `0.1.22+7af9242d` (2026-11-10)

`~/Desktop/FlashBeat-Mac/FlashBeat-0.1.22-7af9242d.app` (Win64 展示候補
`FlashBeat-0.1.22-win64-7af9242d.zip` と同一ソース SHA `7af9242d`) を、
**実ディスプレイ全画面** で起動して 選曲 → Game → Result まで通した記録。

- 実行: `script/mac-select-evidence.sh --app ~/Desktop/FlashBeat-Mac/FlashBeat-0.1.22-7af9242d.app --out docs/evidence/129-mac-0.1.22-7af9242d --timeout 420`
- 結果: **`[AutoTestV6SongEnd] RESULT pass=31 fail=0`** (exit 0)
- 実行時刻: 2026-11-10 20:27〜20:28 JST
- 曲: `ツイッターランド` (STEAKA / BPM 142 / 130 notes / 0:44)

## 1. 実測値

| 項目 | 値 | 出所 |
|---|---|---|
| 画面 | `screen=2560x1664, fullScreen=True, fullScreenMode=FullScreenWindow, displays=1` | `Player.log:31` `[FullscreenDiagnostics]` |
| display0 | `system:3420x2224, rendering:2560x1664` | 同上 |
| 選曲シーン | `[AutoTestV6SongEnd] PASS the select scene is loaded` / v6 select view 存在 / catalog 28 曲 | `Player.log:46-51` |
| 選んだ曲の引き継ぎ | `PASS the selection follows the picked song` / `PASS the started song is the picked one` | `Player.log:51,143` |
| 遷移 | `scene load end GameScene 89ms` / `scene load end ResultScene 66ms` | `Player.log:62,189` |
| メディア | `ready=True audioMode=StreamCombined videoPrepared=True failure=None` | `Player.log:158` |
| 判定 | `perfect=129 great=0 bad=0 miss=1` / `judged=130 settled=130 chartNotes=130 runtimeNotes=130` | `Player.log:192` |
| スコア | `score=992307 maxCombo=129` | 同上 |
| 曲終了 | `songEndState=Completed` (時間切れの強制終了ではない) | 同上 |

スクリーンショット (実寸 2560×1664、`sips` で確認済み):

| ファイル | 内容 |
|---|---|
| `autotest-v6songend-01-01-select.png` | v6 選曲 (3 カラム / 28 曲 / 検索 / Ready) |
| `autotest-v6songend-02-02-game.png` | Game 開始前 (ノーツ・レーン・ガイド) |
| `autotest-v6songend-03-03-gameplay.png` | 実プレイ中 |
| `autotest-v6songend-04-04-result.png` | Result (S / 992,307 / 判定表 / Retry・Register・OK) |

## 2. これで言えること

- **選曲 → Game → Result の遷移は、この候補ビルドで実ディスプレイ全画面のまま成立する**
  (シーン遷移の所要 `GameScene 89ms` / `ResultScene 66ms`)。
- **選んだ曲の identity が Game まで保たれる** (譜面 `130` = 実行時ノーツ `130`、
  選曲画面の表示と開始曲が一致)。
- 判定・スコア・コンボの集計が破綻しない (`judged == settled == chartNotes`)。
- macOS では**映像 + 音声が同じパイプラインで準備できる** (`audioMode=StreamCombined`)。
  これは #13 の Windows 側判定とは別物 (Windows の Media Foundation 経路は未検証のまま)。

## 3. これで言えないこと (証拠にしないこと)

- **本物のキー入力ではない**: ハーネスは内部で入力を与えている。キーボードの打鍵感・
  同時押し・HOLD の保持は #278 の別ハーネス領域。
- **Windows 実機の証拠ではない** (#222 条件 7 の Windows 分、#242 の対象外)。
- **3 解像度の証拠ではない**: これは MAC 枠 (2560×1664) の 1 枚。FHD 1920×1080 と
  2K 2560×1440 は実ディスプレイが無いため未取得。
- 人の目視による「崩れが無い」の判定ではない。PNG は人が読むための材料。

## 4. 再実行の手順

```
script/mac-select-evidence.sh \
  --app ~/Desktop/FlashBeat-Mac/FlashBeat-0.1.22-7af9242d.app \
  --out /tmp/macaccept-$(date +%Y%m%d-%H%M) --timeout 420
```

ハーネスは起動した **自分の子プロセスだけ** を停止する (`open -n` / `pkill` は使わない)。
他の FlashBeat には触れない。
