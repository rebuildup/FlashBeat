# #381 フレームタイム計測 — ベースライン (2026-11-12)

Issue #393–#396 の作業に入る前の、**修正していない main (`571bd357`) の実測**。
改善量はこの数値との差分で語る。数値が無い項目は「未計測」と書く。

## 1. 条件

| 項目 | 値 |
|---|---|
| ビルド | macOS `FlashBeat.app`、`0.1.23+571bd357` (Unity 6000.5.6f1) |
| ソース | `origin/main` = `571bd357` |
| 曲 | ツイッターランド (44 秒 / 130 ノーツ) |
| 起動 | `script/mac-fbperf.sh` (実ディスプレイ全画面、`-fbperf` + `-autotest v6songend`) |
| 計測器 | `FrameTimeProbe` (5 秒窓、`Time.unscaledDeltaTime`) |
| 解像度 | 3420×2148 (Mac 実ディスプレイ、FullScreenWindow) |

`-fbperf` はフレーム時間 (CPU+GPU の融合した壁時計) しか出さない。
**GPU 時間の内訳は未計測** (Profiler 未接続)。

## 2. 実測値 (生ログ)

```
[PerfProbe] event=window scene=GameScene frames=302 avgFps=60.2 p50=16.7 p95=16.7 p99=16.7 worst=16.7 gc0=5 gcKBPerFrame=3.5
[PerfProbe] event=spike scene=GameScene frameMs=47855.6 t=56.32
[PerfProbe] event=window scene=GameScene frames=47 avgFps=1.0 p50=16.7 p95=16.7 p99=47855.6 worst=47855.6 gc0=2 gcKBPerFrame=-20.0
[PerfProbe] event=spike scene=GameScene frameMs=435.3 t=56.91
[PerfProbe] event=spike scene=GameScene frameMs=275.7 t=57.42
[PerfProbe] event=window scene=ResultScene frames=69 avgFps=20.7 p50=16.7 p95=127.0 p99=435.3 worst=435.3 gc0=4 gcKBPerFrame=-29.5
```

## 3. 読み取れること

| 指標 | 値 | 意味 |
|---|---|---|
| 演奏中の平均 fps | **60.2** | 定常状態は既に 60fps に張り付いている |
| p50 / p95 / p99 | 16.7 / 16.7 / 16.7 ms | **窓 1 では 1 フレームも乱れていない** |
| gen0 GC (5 秒窓) | 5 回 | 5 秒で 5 回 = 約 1 回/秒 |
| マネージド確保 | 3.5 KB/frame | 毎フレーム 3.5 KB を確保している |

**この曲では、定常状態に改善の余地がほぼ無い。** 平均 60.2 / p95 16.7 は
60fps の上限に張り付いており、ここを「改善した」と言える余地は小さい。
#393–#396 の効果が見えるのは、**この曲より重い条件**か、**遷移・停止**の側。

## 4. 重要な発見: 47.9 秒の停止は演奏中ではない

`frameMs=47855.6` は t=56.32。ログの並びを見ると

```
[AutoTestV6SongEnd] PASS the screenshot 03-gameplay was written   (line 137)
[PerfProbe] event=window scene=GameScene frames=302 ... 60.2 fps  (line 140)
[PerfProbe] event=spike scene=GameScene frameMs=47855.6 t=56.32   (line 141)
...
[AutoTestV6SongEnd] PASS the screenshot 04-result was written      (line 175)
```

停止は **`04-result` のスクリーンショット撮影の直前**に起きている。曲の演奏中ではなく、
**Result へ遷移する直前の撮影処理**で止まっている。したがって

- 「演奏中に 8.4 秒止まる」という以前の記録とは **別の現象**である可能性が高い。
- 撮影処理 (`ScreenCapture.CaptureScreenshotAsTexture` 等) が同期で 48 秒ブロックして
  いるなら、それは **計測ハーネス側の問題**で、製品の演奏中フレームレートではない。

この切り分けは #395 の担当。ここでは「演奏中の定常 fps は 60.2 で、47.9 秒停止は
撮影の直前に起きている」という事実だけを残す。**原因は未特定。**

## 5. 未計測

| 項目 | 状態 |
|---|---|
| GPU 時間 (Render Thread) | 未計測 (Profiler 未接続) |
| ノーツ数依存のコスト (Dogbite 603 ノーツ) | 未計測 (#394) |
| Title / Select の探索コスト | 未計測 (#396) |
| `V6GameHudView.Refresh` 1 回あたりの CPU 時間 | 未計測 (#393) |
| Win64 実機 | 未計測 (展示端末を操作しない方針) |

## 6. 証跡

- `script/mac-fbperf.sh` (計測手順。同じ条件で再実行できる)
- 生ログ: 実行ごとの `Player.log` と `summary.txt`

再実行:

```sh
script/mac-fbperf.sh Builds/macOS/FlashBeat.app <outdir> ツイッターランド 100
```

## 7. 計測の妥当性 — 環境負荷で数値が壊れる (2026-11-12 追記)

同じビルドでも、**マシンの負荷で数値が 7 倍以上ぶれる**ことが分かった。
記録として残す。

| 実行 | 条件 | GameScene avgFps | p50 | gcKBPerFrame |
|---|---|---|---|---|
| baseline 1 | 負荷 小 | **60.2** | 16.7 ms | 3.5 |
| after 1 | 負荷 大 (load avg 10〜12) | 8.3〜9.2 | 109〜113 ms | 5.7〜11.4 |
| baseline 2 | 負荷 大 (load avg 10〜12) | **60.2** | 16.7 ms | 0.9 |

after 1 の 8.4fps は **修正のせいではない**。同じ負荷帯で baseline を測り直したら
60.2fps のままだった。したがって after 1 の数値は **無効**として捨てる。
`uptime` の load average が 10 を超えていた時間帯で、Unity のビルドや他の作業が
同居していた。

**この計測の前提**: 比較は「同じ負荷帯で、before と after を続けて測る」こと。
負荷を記録しない計測は比較に使えない。`script/mac-fbperf.sh` の出力に
load average を残すべき (未対応)。

また、`avgFps` だけを見ると「窓 1 つだけ 60fps で、残りが 8fps」のような
混在を見落とす。**窓の数と p50 を必ず併記する**。
