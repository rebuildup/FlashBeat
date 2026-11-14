# 解像度行列の実機証跡 2026-11-14 (#96 / #241 / #88)

`script/mac-resolution-matrix.sh` を **CI ビルドの macOS player** で実行した記録。
ハーネスと判定基準の説明は `docs/evidence/96-241-resolution-matrix.md` を参照。

## 対象ビルド

- ソース: main `ec98878f` (PR #473 merge 地点)
- ビルド: self-hosted macOS CI (run 43583703274) の artifact
  `flashbeat-macos-ec98878f...` — ZIP の SHA-256 を manifest と照合済み
  (`b5ca5446…28706`)
- 配置: `/tmp/fb-mac-artifact/app/FlashBeat.app` (ローカル展開、未署名 dev 相当)

後続の merge (#474 V6Slider / #475 vmmap 訂正 / #476 V6CursorInput) は
いずれも EditMode 全件 Pass + 独立レビュー APPROVE の挙動不変リファクタで、
本証跡の画面内容に影響しない。

## 実行条件

```sh
RESOLUTIONS="1024x768 1280x720 1920x1080 2560x1440 2560x1664" \
  script/mac-resolution-matrix.sh <app> docs/evidence/96-241-resolution-matrix-2026-11-14
```

各解像度で `-autoshot-scenes` により Opening / Title / Select / Option の
4 シーンを撮影 (Result は実プレイ状態が必要なためハーネス既定の対象外)。

## 結果

`summary.txt`: **5 解像度すべて PASS**
(期待枚数・`[AutoShot]` の解像度一致・PNG 実寸一致の 3 条件)。

| 解像度 | PNG |
|---|---|
| 1024x768 | scene-00〜03 (4 枚) |
| 1280x720 | scene-00〜03 (4 枚) |
| 1920x1080 | scene-00〜03 (4 枚) |
| 2560x1440 | scene-00〜03 (4 枚) |
| 2560x1664 | scene-00〜03 (4 枚) |

各ディレクトリに `Player.log` を併置。

## この証跡が言えること / 言えないこと

- 言える: macOS 実機で 5 解像度の Opening/Title/Select/Option が
  AutoShot の解像度契約どおりに描画・撮影できたこと。
- 言えない: 画の良し悪し (崩れの有無) の判定は人の目視項目 (#241)。
  Result / Game / Pause は対象外。**macOS は Win64 の代替にならない** —
  Windows 実機での同等撮影は引き続き未実施 (#96 T5)。
