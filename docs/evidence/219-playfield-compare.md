# Issue #219: プレイフィールド ON/OFF 実機比較 (証跡)

`V6UiMode.Playfield` を `-playfield 1` で有効にした場合と、既定 (`false`、
元の 3D レーン) の場合を同じ autotest ハーネスで実機撮影し、比較できるように
するための手順と記録。

## 実行コマンド

```sh
script/mac-playfield-compare.sh [FlashBeat.app のパス] [出力先]
```

- app パスを省略した場合は `Builds/macOS/FlashBeat.app` を使う。
- 内部で 2 回プレイヤーを起動する:
  - `legacy/` … `-autotest v6playfield -playfield 0` (既定の 3D レーン)
  - `v6/`     … `-autotest v6playfield -playfield 1` (v6 遠近法プレイフィールド)
- 各 run の `autotest-v6-result.txt` が `fail=0` でないとスクリプトは
  終了コード 1 を返す。
- 起動ログは各 run ディレクトリの `Player.log`。ハーネスが使った
  Playfield 値は `[AutoTestV6] playfield=N (mode=v6playfield)` の 1 行で
  確認できる。

## 見るポイント

両 run の `autotest-v6-*.png` を並べて、次の差分を確認する:

1. **レーン本数**: `-playfield 1` 側で 8 レーンが透視描画されること。
   `false` 側は元の 3D レーンのまま。
2. **ノーツの見え方**: 遠近法側はレーン奥に向かってノーツが小さく
   (手前で大きく) 描かれること。`false` 側の平行投影っぽい見え方と
   異なること。
3. **判定行の位置**: ノーツを判定する基準線がレーン手前に来ていること
   (奥に流れ去ったノーツが判定線をまたいでいないこと)。
4. **動画との重なり**: 背景動画の上にプレイフィールドと HUD が重なって
   見え、譜面が動画に埋もれないこと。

## この比較で言えないこと

- **Windows 実機は未検証**。この手順は macOS Player での収集を前提にしており、
  Win64 ビルドでの描画結果を保証しない (証跡画像自体は下記のとおり**まだ未取得**)。
  収集後も、Windows 実機の確認は別途 #233 の受入で行う。
- **既定値は変わらない**。`V6UiMode.Playfield` の既定は `false` のまま。
  この比較は「切り替えたときに何が起きるか」の証跡であり、既定値の
  変更判断はオーナーが行う。

## 証跡画像

**未取得** (2026-11-10 時点)。macOS Player での実行は supervisor が行う。
取得後、この節に run ディレクトリへのパスを追記する。
