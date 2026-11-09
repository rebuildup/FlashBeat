---
description: "FlashBeat lean implementation plan — Issue 058"
---

# Plan: カタログ合成と再生クロック修正の回帰確認 — Issue #58

**Issue**: #58 | **Branch**: `w/spec-gameui` | **Spec**: [`spec.md`](spec.md) | **Date**: 2026-11-10

## Architecture

```
[ 実装 (main 済み・変更しない) ]                    [ 残りの受入作業 ]

GManager.cs:88  ActiveSongs => mergedSongs         ① 選曲画面の実機撮影
  = Songs (固定 29) + RemoteSongs の上書き合成        macOS 実ディスプレイ 2560x1664 全画面
  :407 totalSong = ActiveSongs.Length - 1 = 28        v6 Select / ListVisibleRows = 9
  :70  UnplayableTitles = { noSong, ニセモノ }          → 先頭 / 途中 / 末尾 の 3 枚
     → 選曲可能 26 曲                                ② カタログ到達時の差分 (到達可否を記録)
                                                     ③ 1 曲フルプレイ (既存ハーネス再利用)
MediaTimeline.cs:71  HasSongEnded (duration - 1s)      AutoTestV6SongEnd + mac-songend-harness.sh
  :61  IsUsableDuration / :86 FallbackSongEnd          → 130/130 確定 / Completed / Result
SongEndStateMachine.cs:78  Playing→Finishing→Completed/ForcedByDeadline
                                                     ④ 判定表 (OK / NG / 未検証) を記録
EditMode: CatalogMergeTests / ChartDataIntegrityTests  ⑤ 不具合は起票のみ (修正しない)
         / PlaybackClockTests / MediaTimelineTests    ⑥ Windows 実機は手順書のみ (オーナー実施)

[ 既に main にある証跡 ]
docs/evidence/233-song-end-harness.md  … macOS 1 曲完走 (PR #258)
docs/evidence/index.md §1.7 / §7.1     … EditMode 1071/1071, macOS 完走
docs/evidence/win64-2026-11-06/02-select-no1.png … 旧ビルドの選曲画面 (v6 導入前)
```

## 実装順序

1. **基準の固定 (Issue 本文の読み替え)**
   - `GManager` の 7 並列配列のエントリ数と選曲可能数を実測 (`ChartDataIntegrityTests` の期待値と一致すること)。
   - Issue 本文の「30 曲」を削除せず、読み替え表として spec に記載済み。
2. **EditMode テストの再確認**
   - `script/compile-check.sh --assembly tests` → batchmode で EditMode 実行 → 成功/失敗件数を記録。
   - 失敗があれば **本 Issue では直さない**。起票する。
3. **選曲画面の撮影 (macOS 実ディスプレイ全画面)**
   - macOS ビルド (`script/mac-verify build` / `script/win64-build-mac.sh` のmacOS 版) で起動。
   - **実ディスプレイ 2560×1664 の全画面**で v6 Select を開く。
   - 先頭 (1 曲目) / 途中 (13 曲目付近) / 末尾 (選曲可能 26 曲目の最後まで) の 3 枚を撮る。
   - `ListVisibleRows = 9` なので「1 枚では全件が見えない」ことを screenshot の説明に明記する。
   - フィルタ (Level / Status / 検索) をEngageした状態で **選曲可能な曲总能到達できる** ことを確認する。
4. **カタログ到達時の差分**
   - `CloudCatalogConfig.json` の `baseUrl` に到達できるかを記録する (到達できなければ「未到達時の証跡」として残す。cloud 側を触らない)。
5. **1 曲フルプレイ (既存ハーネス)**
   - `script/mac-songend-harness.sh` を実行する。譜面もクロックも差し替えない。
   - 観測点: 譜面ノーツ数 / 実行時ノーツ数 / 確定数 / スコア / 最大コンボ / `SongEndMachine.State` / `ForcedByDeadline` / 遷移先 / 実効解像度。
   - スクリーンショットを `docs/evidence/58-catalog-clock-regression/` に保存する。
6. **判定表の作成**
   - 選曲画面 (3 解像度条件ではなく macOS 2560×1664) / 1 曲フルプレイ / Windows 実機 / ストリーム音声 を OK / NG / 未検証 で記録する。
   - **未検証を「合格」に書き換えない。**
7. **不具合の起票**
   - 見つかった不具合は Issue 化だけして、番号を spec の判定表に追記する。
8. **Windows 実機の引き渡し**
   - `docs/operations/win64-candidate-acceptance-2026-11-10.md` の手順 (SHA-256 照合 → 展開 → 実全画面 → 1 曲完走 7 項目) を参照として残す。

## ブロッカー

| ブロッカー | 内容 | 対処 |
|---|---|---|
| **Windows 実機 (展示 PC)** | 1 曲完走・実全画面・ストリーム音声は Windows 実機必須。**Windows 実機は展示 PC のため cc は実行しない。オーナー実施** | 手順書のみ用意し、判定を「未検証」で固定 |
| **cloud catalog の到達可否** | ネットワーク到達に依存 | 到達できなければ「未到達時の証跡」と明記。cloud 側の操作はしない |
| **ストリーム音声 (m4a)** | `#13` / `#4` の Windows Media Foundation 制約 | 本 Issue のスコープ外。**「同梱 wav が鳴る」だけを観測**し、m4a は「未証明」 |
| **v6 Select のスクロール撮影** | 26 曲が 1 画面 (9 行) に収まらない | 3 枚撮影で「全件到達可能」を示す。**1 枚 = 全件ではない**ことを spec に固定済み |
| **`docs/evidence/index.md` の編集** | 他 Worker の所有 | 触らない。証跡は `docs/evidence/58-catalog-clock-regression/` に置く |

## テスト戦略

| 種別 | 内容 | 備考 |
|---|---|---|
| EditMode (既存のみ) | `CatalogMergeTests` / `ChartDataIntegrityTests` / `ScoreCatalogIntegrityTests` / `SongCatalogRobustnessTests` / `ResourceChartUniquenessTests` / `PlaybackClockTests` / `MediaTimelineTests` / `SongEndStateMachineTests` | **新規テストは書かない** (本 Issue は検証 Issue) |
| EditMode 実行 | Unity batchmode (`-runTests -testPlatform editmode`)。**`-quit` を付けない** | 成功/失敗件数と失敗内容を `TestResults.xml` から記録 |
| 実機 (macOS) | `script/mac-songend-harness.sh` — 実カタログの最短曲「ツイッターランド」を最後まで再生 | **実ディスプレイ全画面**。窓リサイズによる擬似全画面は証拠にならない |
| 実機 (Windows) | 未実施 | **オーナー実施**。判定は「未検証」 |
| 画像比較 | なし (v6 Select のマスタ画像が未整備) | 将来 `specs/075-visual-gate` の web 側視覚ゲートに準ずれば再検討 |
