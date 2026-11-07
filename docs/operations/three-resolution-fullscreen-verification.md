# 3解像度フルスクリーン検証手順 (Issue #162 / #167 / #169)

対象 Issue: **#162** (P0/REBUILD 親)、**#169** (全画面・Canvas基盤)、**#167** (全ボタン実動作)。
期限: **2026-11-08 朝 (展示開始前)**。

## 0. 検証対象は 3 解像度だけ

| ID | 解像度 | 縦横比 | 入手方法 |
|---|---|---|---|
| FHD | 1920×1080 | 16:9 | 実ディスプレイ (展示PC / FHD モニタ) |
| 2K | 2560×1440 | 16:9 | 実ディスプレイ (2K モニタ) |
| MAC | 2560×1664 | 16:10 | 本機の内蔵 Liquid Retina (ネイティブ = 2560×1664) |

**FHD 未満 (1024×768 / 1280×720 等) は対象外**。旧記載の 1440×900 (Mac) は
Issue #162 により **誤りかつ無効** — 検証にも実装にも使わない。

## 1. 原則 (-windows をリサイズして擬似再現しない)

- 検証は **その解像度の実ディスプレイ上でアプリが全画面 (fullscreen) 表示している状態** で行う。
- ウィンドウをドラッグ / リサイズして「そのサイズに見せかける」検証は **无效**。
  ウィンドウ Jadged リサイズで得たスクリーンショットは証拠にならない。
- 実装側も同じ原則: `PlayerSettings.fullscreenMode` = FullScreenWindow、
  `defaultIsNativeResolution = 1` により **ディスプレイのネイティブ解像度** で全画面になる。
  `Screen.SetResolution` でウィンドウサイズを擬似的に合わせる実装は禁止。
- 起動直後の実効値を必ず **実測** する (`Screen.width/height`, `Screen.fullScreenMode`,
  `Screen.dpi`)。`FullscreenDiagnostics` が起動時ログに出すので、**Player.log** を採取する。

## 2. 実機 (Windows) の手順

1. 対象解像度のモニタ displaying PC に接続し、Windows の **推奨解像度** が
   上表の解像度になっていることを確認 (スケーリング 100% / 拡大縮小なし)。
2. 配布 ZIP を展開し、`FlashBeat.exe` を **引数なし** で起動する。
3. 起動後すぐに **画面全体** のスクリーンショットを採取する。
   - タスクバー / ウィンドウ枠 / 黒帯が 1 ピクセルでも見えたら **不合格**。
4. `Player.log` (`%USERPROFILE%\AppData\LocalLow\rebuildup\FlashBeat\Player.log`) から
   `[FullscreenDiagnostics]` 行を採取し、実効解像度と fullScreenMode を確認する。
5. 操作 E2E (押す → 実機能) を **操作ログ** として残す。

### 2.1 操作 E2E (各画面 / 各解像度で 1 回ずつ)

| 画面 | 操作 | 期待する実機能 |
|---|---|---|
| Title | 各ボタン | 遷移先が決まっている (機能のないボタン無い) |
| Select | 曲選択 / タイピングフィルタ / Ready / History / Ranking | 実データで絞り込み・表示。Ready は設定が **プレイ側** に渡る |
| Game | Space 開始 / ノーツ操作 | 音・動画・判定が出る |
| Pause | Setting / Display / Ranking / Keys | 実処理に接続。Ranking はオンライン or ローカル明記 |
| Option | 音量 / 速度 / キー | 保存され **次の起動** と **次のシーン** に反映 |
| Result | Register / Ranking / Retry | スコア保存・再選曲へ |

## 3. Mac (2560×1664) の手順

本機の内蔵ディスプレイはネイティブ 2560×1664 (16:10) のため、**条件3を実機検証できる**。

```sh
# ビルド (release-0-1-11 ワークツリー)
/Applications/Unity/Hub/Editor/6000.5.6f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -projectPath <worktree> \
  -executeMethod BuildScript.BuildStandaloneMacOS \
  -logFile Builds/macOS-build.log

# 起動 → 実全画面 → スクリーンショット
open Builds/macOS/FlashBeat.app
screencapture -x /tmp/flashbeat-mac-2560x1664.png
```

- `screencapture` の画像サイズが **2560×1664** であること (-menubar/-window を付けない全画面)。
- Dock / menu bar が隠れる (fullscreen) ことを確認。
- `~/Library/Logs/Unity/Player.log` に `[FullscreenDiagnostics]` が出る。

## 4. Web Studio の 3 解像度

Web は「実ディスプレイ」は_display ではなく **実ブラウザのビューポート** で再現する。

- 1920×1080 / 2560×1440 / 2560×1664 のビューポート尺寸で実ブラウザを起動し、
  入力欄・ドロップダウン・譜面エディタを **実際に操作** してスクリーンショットを採取する。
- 見た目の比較だけで終わらせない (入力・フォーカス・スクロール・重なり)。

## 5. 完了条件

- 3 解像度それぞれに「実全画面スクリーンショット + Player.log の実効解像度 + 操作ログ + commit SHA」。
- 1 曲完走 (選曲 → 演奏 → Result → Retry) のスクリーンショット。
- 1 つでも未確認があれば **未証明** と記録する (green を作らない)。
