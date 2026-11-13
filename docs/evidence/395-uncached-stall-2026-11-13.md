# #395 未キャッシュ経路のフレーム停止 — 再現記録 (2026-11-13, 0.1.24)

展示機 (`device-windows`) の `FlashBeat-Exhibit` (0.1.24 / 9fd7c040) で `-autotest v6songend -fbperf` を実行。

## 条件

- 曲: ツイッターランド (`e_qQEU_uGjw`)
- メディアキャッシュを一時退避 (`FlashBeatMedia/e_qQEU_uGjw` → `.bak`) して **未キャッシュ経路** を検証。検証後は新規ダウンロードで復元、`.bak` は削除済み。

## 結果

- `v6songend` **pass=32 fail=0** (未キャッシュ経路の追加チェックを含む)
- キャッシュ済み経路 (同日・同一ビルド): worst frame **116.8ms** — 大きな停止なし
- **未キャッシュ経路で 12.1 秒の単発フレーム停止を再現**:

```
[Perf] media resolve 3671ms (yt-dlp -F)
[MusicManager] video file: ...\merged.mp4 (downloaded)
[Perf] notes instantiate 130 notes in 12142ms      ← 停止区間の壁時計
[MusicManager] VideoPlayer prepared=True file=merged.mp4 after 0.0s
[Perf] media ready 12138ms
[PerfProbe] event=spike scene=GameScene frameMs=12093.5 t=24.29
[PerfProbe] event=window scene=GameScene frames=45 avgFps=3.5 ... worst=12093.5
```

## 読み取り

- 停止は **メディア準備→Game 遷移のウィンドウ内** に発生。`notes instantiate` の計測は yield を挟むため壁時計であり、区間内で他のメインスレッド処理 (VideoPlayer Prepare / 初回デコード / シェーダ暖機等) が重なった可能性がある。単一フレーム 12.09 秒のブロック主体は未特定。
- Mac (同ビルド系・キャッシュ済み) では同遷移の worst=183.7ms で再現せず。Windows + 未キャッシュ条件でのみ観測。
- 過去の 8.4 秒停止 (Mac, #368 証跡) および展示機の 47.8 秒 stall 署名 (frameMs=47855, video frame 停滞, audio RMS=0) と同族の「準備中のメインスレッド長時間ブロック」と判断。

## 次の一手

- Unity Profiler (Development Build + deep profiling) または `[Perf]` の細分化計測で、12 秒フレーム内の内訳 (Instantiate 各バッチ / VideoPlayer Prepare / 初回フレームデコード / GC / シェーダコンパイル) を特定する。
- 候補仮説: VideoPlayer Prepare 中の Media Foundation 同期処理、または初回描画時のシェーダ variant コンパイル。推測での修正は行わない (#395 の方針通り)。

## 証跡

- リモート: `C:\Users\rebui\Desktop\fb-0124-fbperf\` (run.log / Player-fbperf.log は offset ずれで空。正本は `%LOCALAPPDATA%\..\Player.log` の当該区間)
