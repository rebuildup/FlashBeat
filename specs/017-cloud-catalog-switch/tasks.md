---
description: "FlashBeat lean tasks — Issue 017"
---

# Tasks: 固定楽曲配列 → Cloud Catalog 読込 (Issue #17)

**Issue**: #17 | **Branch**: `w/spec-wingate` | **Spec**: [`spec.md`](spec.md) / [`plan.md`](plan.md)

## Tasks

- [x] **T1** 契約に fit した小さな typed UnityWebRequest GET クライアントと、base URL の config 化、
  unavailable/empty/error でクラッシュしないことが実装済であることを確認する。
  — **done when**: 各項目に file:line が付いていること
  — **実測**: `Assets/Game/Scripts/Songs/CloudCatalog.cs:18` `public static class CloudCatalog` /
  `:26-29` `Config` (`enabled` / `baseUrl` / `refreshSeconds`) / `:89` `CatalogUrl` =
  `{base}/api/v1/catalog` / `:94` `ChartUrl` = `{base}/api/v1/charts/{songId}?version={n}` /
  `:110` `ParseCatalog` / `:182` `GetText` (UnityWebRequest) /
  `Assets/Game/Resources/CloudCatalogConfig.json`
  (`enabled: true` / `baseUrl: https://flashbeat.rebuildup.dev` / `chartVersion: 1` / `refreshSeconds: 8`) /
  `Assets/Game/Scripts/UI/SelectSceneManager.cs:380` `status == 200 ? ParseCatalog(body) : null` ガード。

- [x] **T2** 選曲画面入場時 + idle 5–10 秒周期の refresh、remote 曲名/メタデータの既存 UI への表示、
  realtime socket 無しが実装済であることを確認する。
  — **done when**: 各項目に file:line が付いていること
  — **実測**: `Assets/Game/Scripts/UI/SelectSceneManager.cs:67` `StartCatalogRefresh()` (入場時) /
  `:340` (再入ガード) / `:355` / `:361` `StartCoroutine(RefreshCatalogLoop(config))` /
  `:364` `RefreshCatalogLoop` / `:374` `RefreshCatalogOnce` /
  `:404` `v6Data.RefreshAsync()` (v6 選曲画面への反映) /
  `CloudCatalogConfig.json` `refreshSeconds: 8` (Issue 指定の 5–10 秒範囲内)。
  socket / WebSocket コードは `Assets/Game/Scripts/` 内に存在しない。

- [x] **T3** (songId, chartVersion) での exact version 譜面取得と snapshot 保持、
  並列配列を remote catalog の真値にしない構造が実装済であることを確認する。
  — **done when**: 各項目に file:line が付いていること
  — **実測**: `SelectSceneManager.cs:442` `CloudCatalog.ChartUrl(config.baseUrl, song.remoteId, version)` /
  `:444` `status == 200 ? CloudCatalog.ParseChartResponse(body) : null` /
  `Assets/Game/Scripts/Songs/CloudChartHolder.cs:16` `Hold` / `:30` `TryGet` /
  `:50` `TryGetForSong` / `:63` `Clear` /
  `Assets/Game/Scripts/Songs/GManager.cs:80-86` (「the cloud snapshot only *updates* the songs it knows」) /
  `:88` `ActiveSongs` / `:90` `ApplyRemoteCatalog` / `:132` `Merge` (`:166` 未知の cloud 曲 → `Append`)。

- [x] **T4** 動的 Youtube VideoPlayer を 1 個使う構造（曲ごとに object を作らない）が実装済である
  ことを確認する。
  — **done when**: 単一参照の file:line が記録されていること
  — **実測**: `Assets/Game/Scripts/Gameplay/MusicManager.cs:202` `YPlayer[GManager.songID].SetActive(true)` /
  `:207` `songVideo = YPlayer[GManager.songID].GetComponent<VideoPlayer>()`。
  新規曲のシーン配線は `Assets/Editor/MakeFileSceneManager.cs` の `YouTubeSceneWiring` が自動化
  (手動シーン編集不要)。**新規曲が `YPlayer[]` 配列の拡張を要するかは T8 で確認する**。

- [ ] **T5** 本番 API の生存確認 (`GET /api/v1/catalog` が 200 且つ非空、`GET /api/v1/charts/{id}?version=N` が 200)。
  — **done when**: API が応答し、catalog が 1 件以上取得でき、chart が引けることの記録が残ること
  — **備考**: `docs/evidence/index.md` §1 S2 行は "no deployed API" と記録しており、
  **これが未達だと S2 の前提 (#15 / #16) が未成立**。cc は read-only 確認のみ。**deploy しない**。

- [ ] **T6** Web Studio で staff token を runtime 入力し、`PUT /api/v1/admin/songs/{songId}` で
  **chartVersion を明示した**新しい有効な曲を公開する。
  — **done when**: 公開された songId と chartVersion が記録に残ること
  — **備考**: トークンはブラウザのタブ内メモリにのみ置き、**Issue / PR / ログに残さない**
  (`web/env.ts:15` `FLASHBEAT_ADMIN_TOKEN` は Worker runtime secret のみ)。
  publish 経路の実装は `web/src/components/song-publisher/song-publisher.tsx:29` /
  `web/src/lib/publish/client.ts:39` (既存、**本 spec のスコープ外**)。

- [ ] **T7** **再ビルドせずに**、Win64 既存成果物で新曲が選曲画面に出現することを観測する。
  — **done when**: `Player.log` に `[GManager] cloud catalog applied: local=… merged=… remote=…` 行があり、
  選曲画面に新曲 (正しいタイトル / アーティスト / 版) が乗っていること
  — **備考**: **Windows 実機は展示 PC のため cc は実行しない。オーナー実施。**
  `FlashBeat.exe` は **引数なし** で起動 (擬似全画面は禁止)。
  **再ビルドしないこと** が本条件の中核。

- [ ] **T8** 新曲を選曲 → ストリーム再生 → ノーツ/判定 → result を 1 ラン完走する。
  — **done when**: 判定数が公開した譜面のノーツ数と一致し、Result に到達したこと
  — **備考**: **Windows 実機は展示 PC のため cc は実行しない。オーナー実施。**
  実行した **chartVersion が意図した版** と一致すること
  (`CloudChartHolder` が (id, version) で保持している谱面を使ったことの確認)。
  前提: #13 (S1) のストリーム再生が成立していること。

- [ ] **T9** catalog 取得の失敗 / 空 / HTTP エラーで Unity がクラッシュせず、
  選曲画面が操作可能なまま残ることを観測する。
  — **done when**: 障害時に例外で落ちないことの記録 (Player.log のエラー状況) が残ること
  — **備考**: **Windows 実機は展示 PC のため cc は実行しない。オーナー実施。**
  ガードは `SelectSceneManager.cs:380` / `:444` に既にあるが、**実挙動は未観測**。

- [ ] **T10** 既存 27 曲 (local) の選曲・再生が引き続き可能なことを確認する (回帰)。
  — **done when**: 既存曲で 1 曲選曲・再生できることを確認したこと
  — **備考**: 併せて `GManager` の 7 並列配列 (29 エントリ) と `Resources/<song>.json` の
  29 エントリが **lockstep のまま** であることを `Assets/Tests/Editor/ChartDataIntegrityTests.cs` で確認する
  (**テストは実行可能だが、既存曲の実再生は Windows 実機が必要**。実機部分はオーナー実施)。

- [ ] **T11** S1 の last-good build が保持されていることを確認する (S2 が S1 を壊さない)。
  — **done when**: `docs/evidence/index.md` §1.5 の 3 世代の成果物がそのまま残っていること
  — **備考**: 読み取り確認のみ。**展示フォルダは読み取り参照**。

- [ ] **T12** 証跡 (Player.log の該当行 / 操作ログ / 成果物 SHA) を `docs/evidence/` に取り込み、
  `docs/evidence/index.md` §1 の S2 行と `release/check/stage-manifest.json` の S2 エントリを更新する。
  — **done when**: S2 が「未証明」から観測結果 (proven / proven-partial) に更新され、
  その根拠となるファイルが残っていること
  — **備考**: **`docs/evidence/index.md` と `release/check/**` は他 Worker が所有**しているため、
  cc は **cc 側の統合 PR で** 適用する (本 spec の所有範囲は `specs/017-*/` のみ)。
