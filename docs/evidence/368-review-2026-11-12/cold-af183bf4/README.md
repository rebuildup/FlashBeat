# 実行記録 — `cold-af183bf4`

受入ビルド `af183bf4` の **コールド状態** (メディアキャッシュを捨てた後) での
`-autotest v6songend` の生出力。

| 項目 | 値 |
|---|---|
| ビルド | macOS `FlashBeat.app`、`0.1.23+af183bf4` |
| 実行 | `-autotest v6songend` (春嵐 / 未キャッシュ) |
| 結果 | 31 PASS / 0 FAIL、曲終了は `Completed` / `forcedByDeadline=False` |
| 画面 | 3420×2148 (ハーネス撮影) |

## 受入ビルドとの関係

この記録は受入ビルド `af183bf4` のもので、`review-fix-37ad42c4/` (別ビルド) とは
**別の実行**である。同じ曲でも画像の sha256 は一致しない (時刻と描画フレームが違う)。

`loadingObserved=False` / `loadingVisibleSeconds=0.00` は、この実行では
ローディング画面が観測窓に入らなかったことを意味する。ローディング画面そのものの
証跡は `378-loading-video.png` と `378-transition-*.png` が持つ。
