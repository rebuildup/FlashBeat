---
description: "FlashBeat lean spec — Issue 017"
---

# Spec: 固定楽曲配列 → Cloud Catalog 読込へ切替 (S2) — 実ブラウザ / 実 API の E2E

**Issue**: #17 | **Branch**: `w/spec-wingate` | **Date**: 2026-11-10 | **Status**: Draft

**Input**: `[S2/Win] Unity の固定楽曲配列を Cloud Catalog 読込へ切替`

## Objective

Issue #17 は「Unity の固定楽曲配列 (並列配列) を Cloud Catalog 読込へ切替」することを求める。
`main` には **catalog / chart の取得経路と、並列配列を cloud で上書きする merge 構造** が既に
実装されている。本 spec の目的は、**残った唯一の未充足条件 =「公開直後の新曲が再ビルドなしで
Unity 上で出現し再生できる」ことの E2E 証明**、および Issue 本文が要求する
**PUT → 再ビルドなし → 選曲 → ストリーム → ノーツ/判定 → result** の一連を、
作業単位として固定することにある。

## Decisions (凍結する設計)

| 項目 | 決定 |
| --- | --- |
| 並列配列の位置づけ | `GManager` の 7 並列配列は **固定カタログの初期値**であり、**remote catalog の真値ではない**。`GManager.cs:88` `ActiveSongs` は `mergedSongs` を返し、cloud snapshot は **知っている曲だけを更新する** (`GManager.cs:80` コメント / `:93` `Merge(Songs, remoteSongs)`)。**この merge 構造を維持する** (並列配列を削除しない) |
| 譜面の取得単位 | `(songId, chartVersion)` の **exact version** で取得する。`CloudCatalog.cs:94` `ChartUrl(baseUrl, songId, chartVersion)` = `{base}/api/v1/charts/{songId}?version={chartVersion}`。**「最新版」で代用しない** |
| 取得 snapshot の保持 | 選曲時に取得した chart JSON は `CloudChartHolder` (`:16` `Hold` / `:30` `TryGet` / `:50` `TryGetForSong` / `:63` `Clear`) で保持し、**1 回のプレイは同一版で完走させる**。プレイ中に catalog が更新されても谱面は差し替えない |
| 経路の頑健性 | catalog / chart の取得失敗・空・例外で **クラッシュしない**。`SelectSceneManager.cs:380` `status == 200 ? CloudCatalog.ParseCatalog(body) : null` のガードがある。**raw の例外を握りつぶさない** (ログは残す) |
| refresh の周期 | 選曲画面入場時 + idle 中の周期 refresh。`CloudCatalogConfig.json` の `refreshSeconds: 8` は Issue 本文の「5–10 秒」範囲内。**realtime socket は使わない** |
| base URL の外部化 | `Assets/Game/Resources/CloudCatalogConfig.json` の `baseUrl` (`https://flashbeat.rebuildup.dev`) / `enabled` / `refreshSeconds`。**バイナリに API の秘密 信息は入れない** (token  Breton無い。publish 用の staff token は Web Studio 側の runtime 入力) |
| VideoPlayer の扱い | `MusicManager` は `songVideo` を 1 つだけ持ち、`YPlayer[GManager.songID]` を初期化時に一度だけ張る (`MusicManager.cs:202` / `:207`)。**曲ごとに GameObject を作らない**。新規song 追加時のシーン配線は `YouTubeSceneWiring` (Editor ツール) が自動化済 — **手動シーン編集は不要** |
| 実機操作 | **Windows 実機は展示 PC のため cc は実行しない。オーナー実施。** `CloudCatalogTests.cs` 緑は「remote catalog を読める」ことの証明であって「公開直後の新曲が再ビルドなしで再生できる」ことの証明ではない |
| 既存 local 曲 | 27 曲 (local `Resources/<song>.json`) は ** playable のまま維持** する。cloud への切替は **追加** であり、既存カタログの削除ではない (Issue 本文 "Keep necessary local game state only") |
| 実装の境界 | 本 spec はドキュメントのみ。**新規コードは書かない**。未充足分は **E2E 手順と証跡** |

## 非目標 (Out of scope)

- Web Studio 側の編集ツール / publish フロー (#19 / #6 / #135 / #73 の責務)。
- ランキング (#21 / #7 の責務)。
-  Edmund 展示版のパッチ配布ゲート (#22 の責務)。
- `GManager` の 7 並列配列の削除・再構成 (merge 構造は現状維持)。
- **`Assets/**` / `web/**` のコード変更** (本 spec はドキュメントのみ)。

## Acceptance criteria

1. 管理者トークンをブラウザで runtime 入力し、`PUT /api/v1/admin/songs/{songId}` で
   **新しい有効な曲 (chartVersion を明示)** を公開できる (Web 側は #19 / #135 が担当)。
2. **Unity を再ビルドせずに**、その新曲が選曲画面に出現する (タイトル / アーティスト / 版が正しい)。
3. その曲を選曲して **ストリームが再生**され、ノーツが降りて判定が積み上がり、Result に到達する。
4. 1 ランの **chartVersion が意図した版** と一致すること (別の版にフォールバックしていない)。
5. 判定数が公開した譜面のノーツ数と一致し、サーバ側の noteCount 検査を通過すること (S4 の前提)。
6. catalog 取得が失敗 / 空 / HTTP エラーのとき、**Unity がクラッシュせず**選曲画面が操作可能なまま残る。
7. 既存 27 曲 (local) が引き続き選曲・再生できる (回帰なし)。
8. 実行した成果物 (Win64 build の SHA-256) と実行ログが保存され、参照先 (URL / songId / chartVersion) が記録に残る。
9. **S1 の last-good build が保持** されている (S2 が S1 を壊さない)。

## 現在の実装状況 (2026-11-10 時点)

`origin/main` = `405555c` を実測した結果。

### 充足 (コード)

| Issue タスク | 根拠 (file:line) | 判定 |
| --- | --- | --- |
| T1: 契約に fit した小さな typed UnityWebRequest GET クライアント、base URL を config 化、unavailable/empty/error で crash しない | `Assets/Game/Scripts/Songs/CloudCatalog.cs:18` `public static class CloudCatalog` / `:26-29` `Config` (`enabled` / `baseUrl` / `refreshSeconds`) / `:89` `CatalogUrl` = `{base}/api/v1/catalog` / `:94` `ChartUrl` / `:110` `ParseCatalog` / `:182` `GetText` (UnityWebRequest) / `Assets/Game/Resources/CloudCatalogConfig.json` (`enabled: true` / `baseUrl: https://flashbeat.rebuildup.dev` / `refreshSeconds: 8`) / `Assets/Game/Scripts/UI/SelectSceneManager.cs:380` `status == 200 ? CloudCatalog.ParseCatalog(body) : null` | 充足 |
| T2: 選曲画面入場時 + idle 中 5–10 秒周期で更新、remote の曲名/メタデータを既存 UI に表示、realtime socket 無し | `Assets/Game/Scripts/UI/SelectSceneManager.cs:67` `StartCatalogRefresh()` (入場時) / `:340` / `:355` `StartCatalogRefresh` / `:361` `StartCoroutine(RefreshCatalogLoop(config))` / `:364` `RefreshCatalogLoop` / `:374` `RefreshCatalogOnce` / `:404` `v6Data.RefreshAsync()` (v6 選曲画面への反映) / `CloudCatalogConfig.json` `refreshSeconds: 8` (Issue 指定の 5–10 秒内) / socket コードなし | 充足 (コード) |
| T3: (songId, chartVersion) で譜面を取得し完走まで snapshot を保持、**Resources / 並列配列を remote catalog の真値にしない** | `SelectSceneManager.cs:442` `CloudCatalog.GetText(CloudCatalog.ChartUrl(config.baseUrl, song.remoteId, version), …)` / `:444` `status == 200 ? CloudCatalog.ParseChartResponse(body) : null` / `Assets/Game/Scripts/Songs/CloudChartHolder.cs:16` `Hold` / `:30` `TryGet` / `:50` `TryGetForSong` / `:63` `Clear` / `Assets/Game/Scripts/Songs/GManager.cs:80` (「the cloud snapshot only *updates* the songs it knows」) / `:86` `mergedSongs` / `:88` `ActiveSongs` / `:93` `Merge(Songs, remoteSongs)` | 充足 (コード) |
| T4: 動的 Youtube VideoPlayer を 1 個使う（曲ごとに object を作らない） | `Assets/Game/Scripts/Gameplay/MusicManager.cs:202` `YPlayer[GManager.songID].SetActive(true)` / `:207` `songVideo = YPlayer[GManager.songID].GetComponent<VideoPlayer>()` (単一参照) / 新規曲のシーン配線は `Assets/Editor/MakeFileSceneManager.cs` の `YouTubeSceneWiring` が自動化 | 充足 |
| 既存 local 27 曲.select 可能 (回帰の非-target) | `GManager.cs:407` `totalSong => ActiveSongs.Length - 1` / `UnplayableTitles` で `ニセモノ` 2 件のみ選曲不可 | 充足 |

### 未充足 / 未検証

| # | 条件 | 判定 | 根拠 |
| --- | --- | --- | --- |
| 1 | T5: **Win64 E2E — PUT で新曲を公開 → 再ビルドなし → 選曲 → ストリーム → ノーツ/判定 → result → artifact + SHA** | **未充足** | `docs/evidence/index.md` §1 の **S2 = 未証明** (`release/check/stage-manifest.json` の S2 エントリも `status: "unproven"`、成果物 path 空)。**この一連の観測記録が 1 件も無い** |
| 2 | Gate: S2 が単独で demo でき、**S1 の last-good build が保持** | **未充足 (S2 側)** | `docs/evidence/index.md` §1.5 に 3 世代の成果物が保持されている点は充足。**S2 側 (dynamic catalog 付き) の demo は未証明** |
| 3 | 公開直後の新曲 (新規 songId) が remoteId 付きで Unity に載る | **未検証 (コードは充足)** | `Assets/Game/Scripts/Songs/GManager.cs:132` `Merge(localSongs, remoteSongs)` は (a) 同じ remoteId → 行を更新 (`:149-153` `Overlay`)、(b) 内蔵曲と対応 → 内蔵行を更新 (`:157-162`)、(c) **未知の cloud 曲 → 末尾に追加** (`:166` `Append`) の 3 分岐。**新規 songId の追加は merge で対応する設計**。テスト `Assets/Tests/Editor/CatalogMergeTests.cs` / `SongCatalogRobustnessTests.cs` / `ScoreCatalogIntegrityTests.cs` が merge の不変条件を固定しているが、**実機・実 API での新規曲追加の挙動は未観測** |
| 4 | 谱面取得失敗時にクラッシュしない | **未検証 (コードは充足)** | `SelectSceneManager.cs:444` のガードはあるが、**実ネットワーク断での挙動は未観測** |
| 5 | 公開した chartVersion と再生した谱面が一致する | **未検証** | `CloudChartHolder` は (id, version) で保持するが、**実 run で意図した版が使われたことの記録が無い** |
| 6 | 本番 API の到達性 | **未検証** | `CloudCatalogConfig.json` の `baseUrl` = `https://flashbeat.rebuildup.dev`。**S2 の API がデプロイ済みで応答するかの実測記録が無い** (`docs/evidence/index.md` §1 S2 行 "no deployed API, no client build") |

### ブロッカー

- **Windows 実機 (展示 PC)** が要る (受入条件 1–5)。
- **本番 API のデプロイ** (#16 / #15 の公開 API) が要る (`docs/evidence/index.md` §1 S2 行が "no deployed API" を明記)。
- **staff token の runtime 入力** ができる人 (Web Studio 側の publish)。**token はブラウザのタブ内メモリにのみ置き、コミットにも Web バンドルにも入れない**。
- **Issue #13 (S1) が未証明** のため、#17 の「ストリームが再生される」部分は #13 の実機に依存する。

### 参考

- `docs/audit/open-issues-audit.md` §3.4 の #17 節が本 spec の根拠。**§3.4 の判定 (充足 4 / 未充足 1 / 未検証 1) を本 spec がそのまま踏襲した**。
- `docs/evidence/index.md` §1 のステージ表が S2 の合否判定の唯一の情報源。
