---
description: "FlashBeat lean tasks — Issue 058"
---

# Tasks: カタログ合成と再生クロック修正の回帰確認 (Issue #58)

**Issue**: #58 | **Branch**: `w/spec-gameui` | **Spec**: [`spec.md`](spec.md) / [`plan.md`](plan.md)

## Tasks

- [x] **T1** 基準の固定: `GManager` の 7 並列配列のエントリ数・placeholder・選曲不可・選曲可能数を実測し、
  Issue 本文の「30 曲」を読み替える (29 エントリ / placeholder 1 / 選曲不可 2 / 選曲可能 26)。
  — **done when**: 実測値が `GManager.cs:51-63,70,407` と `ChartDataIntegrityTests` に一致し、
  spec に file:line 付きで記載されていること
  — **実測**: `Assets/Game/Scripts/Songs/GManager.cs:51-63` の 7 配列は実効 29 エントリ
  (`SBPM`/`Slevel`/`Shit`/`SongLong` の末尾 1 件はコメントアウト)。
  `:70` `UnplayableTitles = { "noSong", "ニセモノ" }`、`:407` `totalSong => ActiveSongs.Length - 1` = 28、
  `:331` で `unplayable` を判定。選曲可能 = 28 - 2 = **26**。spec.md の Decisions / 実装状況に記録済み

- [x] **T2** EditMode テストの現状把握: カタログ合成 / 再生クロック / 譜面整合性の EditMode テストが main にあり、
  失敗 0 であることが記録に残っていること。
  — **done when**: テスト名と件数が file:line 付きで spec に列挙され、
  直近の実行結果が証跡として参照できること
  — **実測**: `Assets/Tests/Editor/CatalogMergeTests.cs` (6 件: `:48,62,81,93,116,125`)、
  `ChartDataIntegrityTests.cs`、`ScoreCatalogIntegrityTests.cs`、`SongCatalogRobustnessTests.cs`、
  `ResourceChartUniquenessTests.cs`、`PlaybackClockTests.cs`、`MediaTimelineTests.cs`。
  実行結果は `docs/evidence/index.md` §1.7 (1071/1071 成功) と
  `docs/evidence/59-feature-acceptance.md` §1 (1181/1181 成功) に記録済み

- [ ] **T3** 選曲画面の実機撮影 (macOS 実ディスプレイ全画面): v6 Select を開き、
  先頭 / 途中 / 末尾 の 3 枚を `docs/evidence/58-catalog-clock-regression/` に保存する。
  併せて「選曲可能な 26 曲にスクロールで全件到達できる」ことを確認する。
  — **done when**: 実ディスプレイ 2560×1664 全画面のスクリーンショット 3 枚が残り、
  各画像の実効解像度が 2560×1664 であること
  — **macOS 実機 (実ディスプレイ全画面) で実行可能**。
  窓リサイズによる擬似全画面は証拠にならない。`ListVisibleRows = 9`
  (`Assets/Game/Scripts/UI/V6/Select/View/V6SelectView.cs:42`) なので 1 枚では全件が見えない

- [ ] **T4** カタログ到達 / 未到達の両方で選曲画面を確認する。
  — **done when**: 到達できた場合は到達時の画像と到達ログが、
  到達できなかった場合は「未到達 (cloud 側を触らなかった)」という記録として残ること
  — **macOS 実機 (実ディスプレイ全画面) で実行可能**。
  `Assets/Game/Resources/CloudCatalogConfig.json` の `baseUrl` = `https://flashbeat.rebuildup.dev`

- [ ] **T5** 1 曲フルプレイの実測 (macOS): 既存ハーネス
  `Assets/Game/Scripts/Common/AutoTestV6SongEnd.cs` + `script/mac-songend-harness.sh` を実行し、
  判定数 / `SongEndMachine` 状態 / `ResultScene` 到達 / スクリーンショットを記録する。
  併せて Retry (R) で再開始できることを確認する。
  — **done when**: 実機ログに「譜面ノーツ数 = 実行時ノーツ数 = 確定数」「Completed」
  「forcedByDeadline = false」「遷移先 = ResultScene」「実効解像度 = 2560x1664 fullScreen=True」
  が残り、スクリーンショットが保存されていること
  — **macOS 実機 (実ディスプレイ全画面) で実行可能**。
  譜面もクロックも差し替えない (PR #258 の実測条件を引き継ぐ)

- [ ] **T6** 映像・同梱 wav 音声の再生と A/V 同期の観測 (1 曲プレイ中)。
  — **done when**: 映像が再生され、同梱 wav が聞こえ、A/V が同期していることの観測記録があること。
  **ストリーム音声 (m4a) は「未証明」と明記**する (`#13` / `#4` のスコープ)
  — **macOS 実機 (実ディスプレイ全画面) で実行可能** (ストリーム音声の Windows 側は本 Issue の外)

- [ ] **T7** 受入判定表 (OK / NG / 未検証) を作成し、spec の判定表に反映する。
  **未検証を「合格」に書き換えない。**
  — **done when**: 選曲画面 / 1 曲フルプレイ / Windows 実機 / ストリーム音声 の各項目が
  OK / NG / 未検証 のいずれかで判定され、根拠が file:line または証跡ファイルで示されていること
  — **Windows 実機は展示 PC のため cc は実行しない。オーナー実施**。
  macOS で観測できた項目だけを macOS で確定する

- [ ] **T8** 不具合の起票 (見つかった場合のみ): 別 Issue として起票し、番号を判定表に追記する。
  **本 Issue では修正しない。**
  — **done when**: 起票した Issue 番号が判定表に載っていること (不具合が無ければ「無し」と明記)

- [ ] **T9** Windows 実機での 1 曲完走の手順書を参照する形にし、判定を「未検証」で固定する。
  — **done when**: `docs/operations/win64-candidate-acceptance-2026-11-10.md` の手順が
  判定表の Windows 行に参照され、判定が「未検証 (オーナー実施)」になっていること
  — **Windows 実機は展示 PC のため cc は実行しない。オーナー実施**
