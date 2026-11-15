# #389 独立 cold review の記録

対象 commit: `d58e4cb4` (branch `w/388-389-acceptance`)、レビュー実施日 2026-11-15。

## 方法

実装経緯を持たない独立レビュア (read-only subagent) に、Issue #389 の受入条件と
成果物 (`report.mts` / `manifest.mts` / `manifest-input.json` / `manifest.json` /
`review-notes.json` / 生成済み report / `design/flashbeat-ui.pen` / screen contract)
だけを渡して評価させた。ファイル変更は行わせていない。

## 結果

初回 verdict: **FAIL**。構造 (3 層分離・PASS/APPROVED 不使用・creditable ≠ 合格)
は健全と認められたが、以下の指摘があった。

| # | 重要度 | 指摘 | 対応 |
|---|---|---|---|
| 1 | major | creditable な全 claim に証跡への直接リンクが無い | report §2 に「証跡一覧 (creditable)」表を追加し、各 claim の `path` と design 参照を列挙 |
| 2 | major | reviewer の `unproven` が `complete` / 終了コードに反映されず、footer が「未確認なし」と表示され得た | `unproven` 件数を `ReviewReport` に追加し `complete` の条件に含めた。footer は `未確認あり (unproven N 件)` を表示。実 report は `unproven 18 件` で incomplete (exit 1) に戻った |
| 3 | minor | claim #370 の reviewer note が無い | `review-notes.json` に #370 の note を追加 (README §3 の記述に一致、unproven なし) |
| 4 | minor | `docs/evidence/evidence-manifest.md` §5 の件数が旧 input (26 件) のまま | 33 件・designNode 32/33 + exempt 1 件へ更新 |
| 5 | minor | 「実 Win64 での動作」の文言が platform 非依存 | creditable evidence の platform 集合から機械生成する文に変更 |
| 6 | minor | notes/decisions/resolutions 内の PASS/APPROVED を検査する経路が無い | `verdictLanguageViolations` を追加し、CLI は違反時に exit 2 で描画を拒否 |
| 7 | nit | `sameBuild` が version prefix を無視 | version + sha の両方一致を要求するよう修正 |
| 8 | nit | `--pen` 無しで designNode 照合が黙って skip | records が designNode を持つのに `--pen` が無い場合 stderr に warning |
| 9 | nit | `platformMatches` の分岐が実質等価比較のみ | 等価比較へ単純化 (コメントで意図を明記) |

指摘されなかったが確認された点:

- manifest.json は manifest-input.json と整合 (33 record、全 status creditable、
  sha256/bytes 付き)。
- 参照された design node id 18 種はすべて `design/flashbeat-ui.pen` (v2.22) に実在し、
  画面対応も妥当 (レビュアが pen 内の node context を個別確認)。
- resolutions (D-1〜D-4) は展示中止・Issue CLOSED を根拠にした解消済み判断で、
  open decision としては残っていない。
- 負の fixture (missing build / wrong OS / stale / design mismatch / 通常系) は
  manifest・report 両テストに存在する。

## 残る既知の限界 (cold review でも指摘・今回は対応見送り)

- `buildSha` / `platform` / `resolution` は manifest-input.json の手書き申告であり、
  PNG ヘッダや `369-launches.txt` 等との機械照合は無い (manifest の設計上の限界。
  入力の正しさは人間のレビュー責任)。
- `resolution` は存在チェックのみで、画像実寸との照合はしない。
- 証跡サブディレクトリ `cold-af183bf4/`・`review-fix-37ad42c4/` は別 SHA のため
  manifest 対象外 (README に記録済み)。
- `369-launch-1.png` と `369-launch-5.png` の sha256 が一致 (静止画ではあり得るが、
  「5 回連続起動の再現性」の証拠としては薄い。`369-launches.txt` のログ値が主証拠)。
