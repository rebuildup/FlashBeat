# #96 補完: Result / Game / Pause の macOS 解像度行列 (2026-11-16)

## これは何の証拠か

`docs/evidence/96-241-resolution-matrix-2026-11-14/` の行列は
Opening / Title / Select / Option の 4 シーンだけを撮っており、
**Result / Game / Pause はハーネス対象外**だった (実プレイ状態が無いと
Result が空虚になり、Game は動画を伴うため)。

本ディレクトリはその不足分を、同じ解像度セットで補ったもの。
撮影は `script/mac-resolution-matrix-v6screens.sh` が
`AutoTestV6Screens` (`-autotest v6screens -autotest-scenes ResultScene,GameScene`)
で行う。ResultScene は `PrepareForAudit` / `OpenRegisterModalForAudit` で
最終状態と登録モーダルを再現し、GameScene は HUD / Pause / Loading を
監査 API で開いて撮る。実プレイ (動画・入力) は伴わない。

## 対象

- Player: `Builds/macOS/FlashBeat.app` (main `8402f33a`、PR #519 merge 後)
- 解像度: 1024x768 / 1280x720 / 1920x1080 / 2560x1440 / 2560x1664
- 各解像度 5 枚: `ResultScene` / `ResultScene-RegisterModal` /
  `GameScene-HUD` / `GameScene-Pause` / `GameScene-Loading`

## 判定結果

`summary.txt` のとおり **全 5 解像度 PASS**。
各 `autotest-v6screens-result.txt` の機械チェック
(フレーム収まり / レガシー混在 / Unicode 記号アイコン) も
**fail=0** (`RESULT pass=18 fail=0` など、解像度ごとに同値)。

## 言えないこと

- **画の良し悪し (崩れ・はみ出し・文字の潰れ) の最終判定は人の目視**。
  機械チェックは「フレーム契約内に収まっているか」等の形式条件だけを見る。
  1024x768 / 1280x720 は固定フレーム (1728x972) が画面より大きい契約どおりの
  「切れる」見え方になる (docs/evidence/96-241-resolution-matrix.md 参照)。
- **Windows 実機の合格ではない。** Win64 側の同等撮影 (T5) と
  1024x768 profile 判断 (T6) は引き続き未実施。
- 実プレイ中の Game 画面 (ノーツが流れる状態) ではなく、
  HUD / Pause / Loading の静止状態の記録である。
