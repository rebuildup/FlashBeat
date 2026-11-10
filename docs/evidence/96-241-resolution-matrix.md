# 解像度行列の実機証跡 (#96 / #241 / #88)

## これは何の証拠か

`script/mac-resolution-matrix.sh` は、macOS 実機で複数解像度の画面を撮り、
Issue の「画が崩れない」系の条件に使える入力を自動で揃えるハーネスである。

| 用途 | Issue | このハーネスが言えること |
|---|---|---|
| UI が崩れていないかの確認の**入力** (画の提供) | #96 (1024x768〜2560x1440 で Title/Result/Option/Opening が崩れない) | 各解像度で Opening/Title/Select/Option の PNG を確実に撮る。**崩れの有無の判定はしない** |
| 3 解像度での目視の**入力** | #241 (Option の 3 解像度で目視) | `RESOLUTIONS="1920x1080 2560x1440 2560x1664"` などで目視対象の画をまとめて撮れる |
| fullscreen 実測の補助 | #88 | `-screen-fullscreen 1` を付けて起動する (fullscreen のログ判定は下記「限界」参照) |

## 実行方法

```sh
# 既定: 1024x768 / 1280x720 / 1920x1080 / 2560x1440
script/mac-resolution-matrix.sh

# アプリと出力先を指定
script/mac-resolution-matrix.sh Builds/macOS/FlashBeat.app Builds/resolution-matrix-manual

# 解像度を差し替え (#241 の 3 解像度など)
RESOLUTIONS="1920x1080 2560x1440 2560x1664" script/mac-resolution-matrix.sh
```

各解像度で 1 回起動し、`AutoShot` (`Assets/Game/Scripts/Common/AutoShot.cs`) の
`-autoshot-scenes` モードで `OpeningScene / TitleScene / SelectScene / OptionScene`
の 4 枚を撮って終了する。起動は実行ファイル直叩きで、kill するのは自分の子プロセスだけ
(既存の `script/shot-v6.sh` と違い `open -n` も `pkill -f FlashBeat` も使わない)。

出力先の既定は `Builds/resolution-matrix-<日時>/`。配下は次の通り。

```
Builds/resolution-matrix-<日時>/
├── summary.txt              # "PASS/FAIL <解像度>" の 1 行サマリ
├── 1024x768/
│   ├── Player.log
│   ├── scene-00-OpeningScene.png
│   ├── scene-01-TitleScene.png
│   ├── scene-02-SelectScene.png
│   └── scene-03-OptionScene.png
├── 1280x720/ ...
├── 1920x1080/ ...
└── 2560x1440/ ...
```

## 出力の読み方

終了コードは自動判定のみを反映する。**全解像度で次の 3 条件を満たすときだけ 0**。

1. 期待枚数 (= scene 数、既定 4) の PNG が出た
2. `Player.log` の `[AutoShot] <path> (<W>x<H>)` が要求解像度と一致
3. 撮れた PNG の実寸 (`sips` で確認) が要求解像度と一致

`[res-matrix] <解像度> PASS/FAIL` と `summary.txt` がこの 3 条件の結果。
**画の良し悪し (崩れ・はみ出し・文字の潰れ) は自動判定しない**。PNG を開いて
人の目で確認するのが #96 / #241 の残条件そのもの。

fullscreen について: AutoShot の scene 一覧モードのログ行には `fullscreen=`
が付かない (付くのは scene 指定なしモードのみ)。ログに `fullscreen=` が現れた場合は
`True` であることを要求し、現れない場合は `n/a` として報告するだけである。
fullscreen の実測値そのものが必要な場合は `script/mac-playfield-evidence.sh`
(#233 ハーネス、結果ファイルに fullscreen を含む) を使うこと。

## このハーネスで言えないこと

- **Windows 実機の合格ではない。** macOS での撮影である。#96 の Windows 実機分は
  別途 (`docs/evidence/win64-*.md` の手順) 実機で取る必要がある。
- **画の良し悪しは人の判断。** ハーネスは「指定解像度の画が揃った」ことしか保証しない。
- **動画再生は含まない。** 既定の scene 一覧に GameScene が無く、YouTube 動画
  (VideoPlayer) の上に乗る演出は撮れない。
- **ResultScene を既定で撮らない。** 実プレイ状態 (スコア・コンボ・判定数) が無いと
  表示が空虚になり、崩れ確認として意味のある画にならないため。Result を見たい場合は
  実プレイを伴う既存ハーネス (`script/mac-songend-harness.sh` 等) の画を使うこと。

## v6 UI の固定フレーム契約と低解像度の見え方

v6 UI は `V6UiProfile` (`Assets/Game/Scripts/UI/V6/Common/V6UiProfile.cs`) の契約で
**スケールしない**: ワークフレームは 1728x972 固定で、どの解像度でも 1:1 表示し、
解像度ごとに変わるのは origin `((w - 1728) / 2, (h - 972) / 2)` (整数切り捨て) だけである。

したがって **1024x768 / 1280x720 ではフレーム (1728x972) が画面より大きく、
origin は負になる** (はみ出し前提の配置になる)。これは仕様であり、本ハーネスを
追加する PR では挙動を変えない。ハーネスが撮る PNG は「その解像度で実際に
どう見えるか」を記録する対象であり、低解像度で端が切れて見えることは契約通り。
スケール導入などの仕様変更はオーナー判断で行う。

## 取得状況

**本ドキュメント作成時点で画像は未取得。** ハーネスの実行は supervisor が行う
(本 PR は Player を起動しない)。取得後は `summary.txt` と PNG をこの配下に置き、
目視結果を #96 / #241 に記録すること。
