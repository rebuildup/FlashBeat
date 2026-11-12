# 実行記録 — `review-fix-37ad42c4`

`w/v6-368-impl` の `37ad42c4` (RenderTexture の解放を直したコミット) でビルドした
macOS プレイヤーによるハーネス実行の生出力。

| 項目 | 値 |
|---|---|
| ビルド | macOS `FlashBeat.app`、`0.1.23+37ad42c4` |
| 実行 | `-autotest v6interact` / `-autotest v6songend` |
| 結果 | v6interact 46 PASS / 0 FAIL、v6songend 31 PASS / 0 FAIL (チュートリアル) |
| 画面 | 3420×2148 (ハーネス撮影) |

## 受入ビルドとの関係

**この記録は受入ビルド `0.1.23+af183bf4` のものではない。** そのため
`docs/evidence/368-review-2026-11-12/manifest.json` の対象には含めていない
(含めれば `stale` になる)。ここに置くのは、`37ad42c4` 時点で
インタラクションと曲終了が通っていたことを後から追えるようにするため。

受入ビルドの判定は README の §4 と `harness-runs.txt` が持つ。
