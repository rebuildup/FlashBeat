---
description: "FlashBeat lean spec — Issue 058"
---

# Spec: カタログ合成と再生クロック修正の回帰確認 (選曲画面証跡 + 1 曲フルプレイ)

**Issue**: #58 | **Branch**: `w/spec-gameui` | **Date**: 2026-11-10 | **Status**: Draft

## Input

`検証: カタログ合成 (30 曲表示) と再生クロック修正の回帰確認`

## Objective

#58 は **実装ではなく検証** の Issue である。実装 (#52 / #48 / #31) は main へラ-land 済みで、
残る受入条件は「**証跡 (スクリーンショットと実測ログ) が揃うこと**」だけである。

本 spec が定めるのは、**残りの受入条件をどの手順・どの解像度・どの観測点で満たすか** であり、
実装変更はしない (Issue 本文の「修正実装はしない。発見した不具合は Issue 化だけして報告する」に従う)。

3 つの受入条件を残す:

- (A) **選曲画面のスクリーンショット (全件が見えること)**
- (B) **1 曲フルプレイの実測** (ノーツ判定 / Result 到達 / Retry / 映像音声 / A/V 同期)
- (C) **基準の更新** — Issue 本文の「30 曲」は stale。`GManager` の実状 (29 / 26 / 2) に読み替える

## Decisions (凍結する設計)

| 項目 | 決定 |
| --- | --- |
| 曲数の基準 | Issue 本文の「30 曲」を **削除しない**。読み替え表として **GManager の 7 並列配列 = 29 エントリ / placeholder (index 0) = 1 / 選曲不可 (ニセモノ index 27, 28) = 2 / 選曲可能 = 26** を固定する。受入条件は「曲数が 30 であること」ではなく「**配列・カタログ・譜面が整合し、選曲可能な曲が全部選べる**」こと (`GManager.cs:88` / `:407` / `ChartDataIntegrityTests.cs`) |
| 選曲画面の証跡 (A) | **実ディスプレイ全画面**のスクリーンショットを macOS (2560×1664) で取得する。**窓リサイズによる擬似全画面は証拠にならない** (memory: 3解像度は実ディスプレイ全画面)。v6 Select は `ListVisibleRows = 9` (`V6SelectView.cs:42`) なので **1 枚では全件は見えない**。**先頭・末尾・途中**の 3 枚を撮って「全件がスクロールで到達できる」ことを示す |
| カタログ到達時の差分 (A) | 「カタログ到達時 / 未到達時 の両方」を撮る。ただし **cloud catalog の到達は環境依存** (到達できなければ未到達時の証跡として残し、云側を触らない) |
| 1 曲フルプレイ (B) | 既存ハーネス `Assets/Game/Scripts/Common/AutoTestV6SongEnd.cs` + `script/mac-songend-harness.sh` を **そのまま使う** (PR #258 で実証済み)。譜面もクロックも差し替えない。判定数・`SongEndMachine` 状態・`ResultScene` 到達・スクリーンショットを 1 回で観測する |
| 1 曲フルプレイ (B) の Windows 側 | Windows 実機は展示 PC のため **cc は実行しない**。手順書 (`docs/operations/win64-candidate-acceptance-2026-11-10.md`) を残し、オーナー実施とする |
| ストリーム音声 (m4a) | Issue の「YouTube 映像/音声が出るか」の中の **ストリーム音声** は `#13` / `#4` の未解決事項 (Windows Media Foundation が `googlevideo.com` を読めない) なので、**本 Issue では再検証しない**。「同梱 wav が鳴る」ことを観測して **「ストリーム音声は未証明」** と明記する |
| 不具合の扱い | 見つけた不具合は **Issue 化だけ** して報告する。**本 Issue では修正しない** (Issue 本文の制約) |
| 証跡の置き場所 | `docs/evidence/58-catalog-clock-regression/` に置く (新規ディレクトリ)。`docs/evidence/index.md` の更新は **他 Worker の所有** なので今回は触らない |

## 非目標 (Out of scope)

- カタログ合成 / 再生クロックの **実装変更** (`GManager` / `MediaTimeline` / `SongEndMachine`)。
- 譜面・設定 JSON の Resources 配置変更、`GManager` の 7 並列配列の追加・削除。
- `GManager.UnplayableTitles` の見直し (ニセモノ 2 件は index を保つため残す)。
- Windows 実機での 1 曲完走 (展示 PC は cc が自発的に触らない)。
- `docs/evidence/index.md` / `docs/audit/open-issues-audit.md` の編集 (他 Worker 所有)。

## Acceptance criteria

1. `GManager` の 7 並列配列のエントリ数・選曲可能数・placeholder 数が Issue 本文の読み替え表と一致し、整合性 EditMode テスト (`ChartDataIntegrityTests`) が緑である。
2. 選曲画面の実ディスプレイ全画面スクリーンショットが `docs/evidence/58-catalog-clock-regression/` にあり、**先頭・途中・末尾** の 3 枚で **選曲可能な 26 曲が全て到達できる** ことが確認できる (1 枚では全件は見えないことを spec として明記している)。
3. 選曲画面のスクリーンショットに「選曲不可の `ニセモノ` 2 件が出ない」ことが確認できる (or 出ないことがコードで保証されている: `V6SelectDataSource.cs:160,205` が `unplayable` を除外)。
4. macOS 実機 (実ディスプレイ全画面) で **1 曲フルプレイ** が完走し、譜面ノーツ数と確定ノーツ数が一致し、`SongEndMachine` が `Completed` (期限切れ強制完了でない) になり、`ResultScene` へ到達し、スクリーンショットが残る。
5. Retry (R) で同じ譜面が再び開始できることを確認する。
6. 映像・同梱 wav 音声の再生と A/V 同期が観測できる。**ストリーム音声 (m4a) は「未証明」と明記**する (本 Issue のスコープ外)。
7. 1 曲フルプレイ中に見つかった不具合は **別 Issue として起票** され、Issue 本文の報告欄に Issue 番号が記載される。
8. Windows 実機での 1 曲完走は **未実施** として明示され、手順書 (`docs/operations/win64-candidate-acceptance-2026-11-10.md`) が参照されている。
9. 秋の受入判定 (OK / NG / 未検証) が表形式で記録され、「未検証」が「合格」に書き換えられていない。

## 現在の実装状況 (2026-11-10 時点)

**充足済み (コード・テスト・証跡のいずれかが main にあるもの)**

| 受入条件 | 判定 | 根拠 (file:line / 証跡) |
|---|---|---|
| カタログ合成 (固定 + remote 上書き) | 充足 | `Assets/Game/Scripts/Songs/GManager.cs:88` (`ActiveSongs => mergedSongs`)、`:90-95` (`ApplyRemoteCatalog` / `Merge`)。テスト `Assets/Tests/Editor/CatalogMergeTests.cs:48,62,81,93,116,125` |
| 再生クロック (曲は必ず終わる / Ready 時にクロックが 0 に戻らない) | 充足 (コード) | `Assets/Game/Scripts/Gameplay/MediaTimeline.cs:61` (`IsUsableDuration`), `:71` (`HasSongEnded`), `:86` (`FallbackSongEnd`)。テスト `Assets/Tests/Editor/PlaybackClockTests.cs` / `MediaTimelineTests.cs` |
| 譜面整合性・カタログ契約の EditMode テスト | 充足 | `Assets/Tests/Editor/ChartDataIntegrityTests.cs` / `ScoreCatalogIntegrityTests.cs` / `SongCatalogRobustnessTests.cs` / `ResourceChartUniquenessTests.cs` |
| 7 並列配列のエントリ数 = 29 | 充足 | `Assets/Game/Scripts/Songs/GManager.cs:51-63` (実測: `SongName`/`Musician`/`SongURL` 29 エントリ。`SBPM`/`Slevel`/`Shit`/`SongLong` は末尾 1 件がコメントアウトされていて実効 29) |
| 選曲不可 = `ニセモノ` 2 件 (index 27 / 28) | 充足 | `GManager.cs:70` (`UnplayableTitles = { "noSong", "ニセモノ" }`)、`:331` (`unplayable = UnplayableTitles.Contains(SongName[i])`) |
| 選曲可能数 = 26 | 充足 (コード) | `GManager.cs:407` (`totalSong => ActiveSongs.Length - 1` = 28) から `ニセモノ` 2 件を除いて 26。`Assets/Game/Scripts/UI/V6/Select/Data/V6SelectDataSource.cs:160,205` が `unplayable` を除外 |
| EditMode テスト結果の記録 | 充足 | `docs/evidence/index.md` §1.7 (1071/1071 成功)、`docs/evidence/59-feature-acceptance.md` (§1: 1181/1181 成功) |
| 1 曲フルプレイの実測 (macOS) | **充足 (main に証跡あり)** | PR #258 / `docs/evidence/233-song-end-harness.md` / `docs/evidence/index.md` §7.1 — 「ツイッターランド」130/130 確定 / `Completed` / `forcedByDeadline=false` / `ResultScene` 到達 / スコア 992,307 / 実効解像度 2560×1664 全画面 / `RESULT pass=29 fail=0` |
| Windows 実機の選曲画面スクリーンショット (2560×1440) | 存在 (旧ビルド) | `docs/evidence/win64-2026-11-06/02-select-no1.png`, `03-select-no2-manimani.png` — ただし v6 Select 導入前のビルド。**現状 UI での証跡ではない** |

**未充足 (残りの作業)**

| 受入条件 | 判定 | 未充足の理由 |
|---|---|---|
| 選曲画面のスクリーンショット (全件が見えること) | **未充足** | 現状の証跡は v6 Select 導入前のもの (`docs/evidence/win64-2026-11-06/02-select-no1.png`)。macOS 実ディスプレイ全画面での v6 選曲画面の 3 枚 (先頭 / 途中 / 末尾) が無い |
| カタログ到達時の選曲画面 | **未充足** | `CloudCatalogConfig.json` の `baseUrl` は `https://flashbeat.rebuildup.dev` だが、**到達可否の実測記録が無い** |
| Windows 実機での 1 曲完走 | **未充足 (Windows 実機必須)** | `docs/evidence/index.md` §7.3-1 が「未証明」と明記。展示 PC は cc が自発的に触らない |
| ストリーム音声 (m4a) の再生 | **未充足 (#13 / #4 のスコープ)** | `docs/evidence/index.md` §7.3-2 が「未証明」と明記。Windows Media Foundation の制約 |
| 不具合の起票記録 | **一部充足** | `docs/evidence/59-feature-acceptance.md` §3 に #241 の起票・修正記録がある。**#58 の受入確認中に発見した不具合の起票記録は未作成** |
| Issue 本文のチェックリスト / 受入判定表 | **未充足** | 選曲画面の受入判定が OK / NG / 未検証 の形で残っていない (Issue 本文の「完了条件」の報告欄) |
