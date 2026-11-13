# #395 未キャッシュ経路の停止 — 0.1.26 系ビルドでの再検証 (2026-11-13)

0.1.24 で再現した 12.1 秒停止 (`docs/evidence/395-uncached-stall-2026-11-13.md`)
が、#430 の非同期化を含むビルドで再現するかを、計測強化版で確認した記録。

## 対象ビルド

- ソース: `fc1543fa` (`w/395-perf-marks`、origin/main + #395 帰属計測のみ)
- パッケージ: `FlashBeat-0.1.26-win64-fc1543fa.zip`
  (prerelease `v0.1.26-win64-fc1543fa-verify` として一時公開、検証後に削除可)
- 機体: 展示機 `device-windows` (Windows 11 / RTX 4050 / 2560x1440、対話セッションの
  スケジュールタスク経由で起動)

## 計測の追加 (#395 帰属用)

- `PerfMarks`: Update 内で最後に通過した地点を記録。
  `FrameTimeProbe` の spike ログに `mark=` と `gc0Total=` を付記
- `MusicManager`: `VideoPlayer.Prepare()/Play()` 呼び出し・映像配線/有効化・
  初回描画フレームに Stopwatch とマーカー
- `NotesManager`: ノーツ生成バッチの最遅バッチ計測と閾値超過バッチの記録

## 結果: 停止は再現しなかった

曲: ツイッターランド (`e_qQEU_uGjw`)。メディアキャッシュを退避した
**未キャッシュ経路**で `v6songend -fbperf` を実行 (pass=34/0)。

```
[Perf] media resolve 6562ms (yt-dlp -F)
[Perf] media files 15000ms            (ダウンロード + ffmpeg マージ)
[Perf] video Prepare() call 0ms
[Perf] video wire picture 1ms
[Perf] video activate 0ms
[Perf] media ready 15237ms
[Perf] video Play() call 0ms
[Perf] video first rendered frame at mediaTime=0.00
[Perf] notes instantiate 130 notes in 325ms slowestBatch=0.9ms@8
[PerfProbe] event=spike scene=GameScene frameMs=316.9 mark=notes-ready@0.32
```

- 単発最大フレーム **316.9ms** (notes-ready 直後のシーン確定処理)。
  それ以外の窓は worst≈33ms、avgFps≈60 で推移
- 0.1.24 の同条件 (12,093ms フレーム / notes instantiate 壁時計 12,142ms) とは
  桁が違い、**未キャッシュ経路の長時間停止は再現しなかった**

## 原因の帰属 (高い確度)

0.1.24 (`9fd7c040`) の `YouTubeStreamResolver.RunAsync` は
`proc.WaitForExit(timeoutMs)` を **Unity メインスレッド上で** 呼んでおり、
yt-dlp のダウンロード + ffmpeg マージ完了までフレームをブロックしていた
(#430 のコミット `874c7191` がまさにこの修正: 「一覧取得・ダウンロード全時間」)。

今回の計測で `media files` (15.0s) の区間に対応するブロックが消え、
Prepare/Play/配線/初回フレームの各呼び出しはいずれも 0-1ms だったことから、
12 秒停止の主体は **プロセス待機のメインスレッド・ブロッキング** であり、
#430 で解消されたと判断する。映像デコードやシェーダ初回コンパイルではない
(それらは仮に重くても呼び出し計測に現れ、かつ worst は 317ms に留まった)。

## 結論と残件

- #395 の再現条件 (0.1.24 + 未キャッシュ) では停止が再現せず、
  原因は #430 で解消済みと証拠付けできる → Issue は resolved-by-#430 でクローズ
- 帰属計測 (`PerfMarks` + spike への mark 付記) は今後の再発検出用に残す
- 同ビルドでの他ハーネス結果:
  - `v6hold` pass=41/0 (HOLD 3 個の開始・保持・終端を Win64 で観測 → #278 の
    Windows 受入も充足)
  - `v6screens` pass=46/0 (15 画面)
  - `v6interact` pass=47/0 (登録モーダル timing 修正後の全経路 PASS)

## 証跡

- `runner.log` — 4 ハーネスの起動/終了記録
- `songend/`, `hold/`, `v6screens/`, `v6interact/` — 各 `*-result.txt`
- Player.log の [Perf]/[PerfProbe] 行はリモート
  `%LOCALAPPDATA%\..\Player*.log` の当該区間 (共有パスのため本リポジトリには未収録)
