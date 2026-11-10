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
  Win64 ビルドでの描画結果を保証しない (下記の証跡画像は 2026-11-10 に macOS 実機で取得済み)。
  Windows 実機の確認は別途 #233 の受入で行う。
- **既定値は変わらない**。`V6UiMode.Playfield` の既定は `false` のまま。
  この比較は「切り替えたときに何が起きるか」の証跡であり、既定値の
  変更判断はオーナーが行う。

## 証跡画像

取得済み (2026-11-10)。macOS 14 インチ実機 (2560×1664)、Player は main `c9fb812` と
**ツリー同一**の `0be2d47` からビルドしたもの (プレイヤー内蔵バージョン表記
`v0.1.0+0be2d47`)。

| 画像 | 内容 |
|---|---|
| `219-playfield-compare/legacy-03-prestart.png` | `-playfield 0` / 開始前 (Space to Start) |
| `219-playfield-compare/legacy-06-play.png` | `-playfield 0` / プレイ中 |
| `219-playfield-compare/v6-03-prestart.png` | `-playfield 1` / 開始前 |
| `219-playfield-compare/v6-06-play.png` | `-playfield 1` / プレイ中 |

各 run の `autotest-v6-result.txt` も同梱 (`legacy-result.txt` / `v6-result.txt`)。

### 実測結果

- `legacy`: `[AutoTestV6] RESULT pass=29 fail=0`、`[AutoTestV6] playfield=0 (mode=v6playfield)`、PNG 7 枚 (2560×1664)
- `v6`: `[AutoTestV6] RESULT pass=29 fail=0`、`[AutoTestV6] playfield=1 (mode=v6playfield)`、PNG 7 枚 (2560×1664)
- スクリプト終了コード 0 (`[playfield-compare] OK`)

### 画像から読み取れる差分

- **レーン幅**: `-playfield 0` はレーンが画面幅いっぱい (左右端まで) に広がる。
  `-playfield 1` は判定線付近で内側に寄り、左右に余白ができる。
- **判定線の位置**: `-playfield 0` は画面最下部寄り (キーガイド直上)、
  `-playfield 1` はやや上に上がり、キーガイドとの間隔が広い。
- **ノーツの見え方**: どちらも奥が小さく手前が大きい遠近だが、`-playfield 1` の方が
  消失点が中央に寄り、レーンごとの傾きが強い。
- **HUD / 動画との重なり**: どちらもレーンの線はヘッダー帯 (スコア / ゲージ) の背後まで
  伸び、背景動画は全面に敷かれる。ノーツはいずれも動画の上で判読できる
  (4 枚とも 1 フレームの記録であり、曲を通した見え方の評価ではない)。

これは**差分の記録**であり、どちらが良いかの判断はしていない (既定値の変更はオーナー判断)。

### 未取得 / 未検証

- **Windows 実機 (Win64) の同じ比較は未実施** (#233 の受入と併せて実機で行う)。
- **目視の当否**: 4 枚は 1 フレームずつの記録で、曲を通した見え方の評価はしていない。

