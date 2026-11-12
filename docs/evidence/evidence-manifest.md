# 実機レビュー証跡マニフェスト (Issue #388)

`docs/evidence/` に散らばっている実機スクリーンショットを、**機械的に照合できる索引**へ
変える。目的は #389 のレビュー報告が「変わった / 失敗した / 未確認」を人間の目視なしに
切り分けられる入力を持つこと。**この道具は UI の合否を判定しない。**

## 1. 何を作るか

```
docs/evidence/<set>/manifest-input.json   ← 主張と根拠の対応 (手で書く)
docs/evidence/<set>/manifest.json         ← 生成物 (機械が書く)
```

`manifest-input.json` は「どの Issue の、どの状態を、どの画像が支えるか」を並べたもの。
`manifest.json` はそこへ **ファイルの存在・sha256・サイズ** と **状態判定** を付けた生成物。

```sh
mise run evidence-manifest        # 生成
mise run evidence-manifest-test   # 単体テスト (負例を含む)
```

## 2. 状態の意味

| 状態 | 意味 |
|---|---|
| `creditable` | **この画像を、この主張の根拠として使ってよい**。ビルド・プラットフォーム・解像度が候補と一致し、ファイルが実在する |
| `unverified` | 判定に必要な項目 (ビルド / プラットフォーム / 解像度 / claim) が欠けている |
| `stale` | 別のビルドで撮られている (候補と SHA が違う) |
| `missing` | 画像がリポジトリに無い |
| `mismatch` | 参照している design ノードが design に無い |

### `creditable` は合格ではない

ここが最重要。`creditable` は「**根拠として使える**」だけを意味し、
「**画面が設計どおりに正しい**」ことは意味しない。視覚的な合否は design との比較
(#389) が決める。この区別を崩すと「スクリーンショットがある = 合格」という
未確認の PASS が生まれるので、状態名を `verified` ではなく `creditable` にしている。

## 3. 判定規則 (実装 = `evals/evidence-manifest/manifest.mts`)

上から順に評価し、最初に当たったものを採る。

1. `claim` か `state` が空 → `unverified`
2. 画像が無い → `missing`
3. `designNode` が design のノード一覧に無い → `mismatch`
4. `buildSha` が無い → `unverified`
5. `buildSha` が候補と違う → `stale`
6. `platform` が無い → `unverified`
7. `platform` が候補と違う → `unverified`
8. `resolution` が無い → `unverified`
9. それ以外 → `creditable`

規則 2 を 5 より先に置くのは、**存在しない画像はビルドの新旧に関係なくレビューできない**ため。
規則 7 は「macOS の証拠を Win64 に流用しない」「WSL を Windows ネイティブとして扱わない」を
コードで固定している。

## 4. 差分

`diffManifests(before, after)` が `added` / `removed` / `changed` (状態遷移) を返す。
#389 の報告はこの差分を使い、**変わった項目だけ**を人間に提示する。

## 5. 現在の適用結果 (#368 の証跡)

`docs/evidence/368-review-2026-11-12/manifest.json`:

| 状態 | 件数 |
|---|---|
| `creditable` | 26 |
| `unverified` | 0 |
| `stale` | 0 |
| `missing` | 0 |
| `mismatch` | 0 |

26 件はすべて macOS 3420×2224・ビルド `0.1.23+af183bf4` の撮影で、候補と一致する。
**これは「26 件の画面が設計どおり」を意味しない。** visual acceptance は #389 の仕事で、
現時点で未達の項目 (メニューバー 76 px、スケルトン点滅、ピクセル比較、登録の実送信) は
README と各 Issue に未達として残っている。

## 6. 制約

- **design ファイルは読まない。** `.pen` は暗号化されており Pencil が正本なので、
  ノード一覧は呼び出し側が渡す (`designNodes`)。
- **凍結済み展示候補を変更しない。** 生成物は `docs/evidence/` の下だけ。
- **未確認を PASS にしない。** 出力の終了コードは「全件 `creditable`」のときだけ 0。
  1 件でも他状態があれば 1 を返すので、CI は部分的なマニフェストを緑にできない。
