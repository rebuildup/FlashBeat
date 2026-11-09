---
description: "FlashBeat lean spec — Issue 007"
---

# Spec: 展示用クラウドランキング (Gate S4 optional) — 実 Win64 送信と 2 clients 観測

**Issue**: #7 | **Branch**: `w/spec-wingate` | **Date**: 2026-11-10 | **Status**: Draft

**Input**: `[Gate S4 optional] 展示用クラウドランキング`

## Objective

Issue #7 は **optional な統合ゲート** であり、S3 の playable build を出すために必須ではない。
Issue 本文が要求するゲートは次の 2 点に集約される:

1. **実 Win64 play が result を送信する** (実機での POST 成立)。
2. **2 番目のブラウザ / クライアントがそれを見る** (送信結果が別クライアントから観測できる)。

サーバ側 (score 再計算・noteCount 検査・top 10) と Unity 側 (送信・取得) のコードは
既に `main` にある。本 spec の目的は **残った「未観測」を 1 つの作業単位に固定し**、
実機で観測する責任所在を明示することにある。

ただし Issue #7 は **#21 (Unity 送信側)** を親に持つ。#21 は #17 (catalog) を前提とするため、
本 Issue の実行順は **#17 → #21 → #7** となる (#7 は #21 の実機証拠を束ねる上位ゲート)。

## Decisions (凍結する設計)

| 項目 | 決定 |
| --- | --- |
| ゲートの範囲 | S4 は **optional**。S3 の playable build を壊さない。`docs/evidence/index.md` §1 の S4 行が「未証明」のままでも **展示は S3 で成立**する (#7 本文「If time is short keep S3」) |
| サーバの権威 | スコアは **サーバが再計算** する (クライアント送信値を信用しない)。判定数 `perfect+great+bad+miss` は `chart_versions.noteCount` と **完全一致** しなければ 422。既存実装を使う (変更しない) |
| ニックネーム | アカウント無し。単純な編集可能ニックネーム。**nickname は trim + 1..16 文字の長さ制限のみ** (文字種の制限は無い。`RankingClient.cs:135`)。**songId** はサーバ側パターン `^[a-z0-9-]{3,64}$` に一致する必要あり (`RankingClient.cs:123`)。**アンチチートは実装しない** (#7 本文 "no real anti-cheat") |
| ランキングの表示 | top 10 を `songId` + `chartVersion` 単位で返す。**「casual / unverified」のラベル表示は未実装** (`V6RankingLogic.SourceLabel` は "Local" / "Online" のみ)。本 spec では **文言追加を必須にしない** (arbitrary な英語 UI 文言を cc が独断で入れないため)。ただし **Issue の受入条件 5 の文言は「未検証 / 要オーナー確認」** として残す |
| ローカル保存 | `ScorePersistence` / `GManager.Hiscore` (PlayerPrefs) は **端末内記録**であり、サーバの権威データではない (#7 本文 "No persistent local high-score authority")。**変更しない** |
| 2 客户端観測 | 「2 台目」は **別ブラウザ (Web Studio) か別 Windows クライアント**のどちらでもよい。**展示 PC を 2 台用意する要件はない** — 1 台目の Windows が送信し、**別マシンのブラウザ** で `GET /api/v1/leaderboards/{songId}?version=N` が当該 nickname を含む top 10 を返すこと、で足りる |
| 実機操作 | **Windows 実機は展示 PC のため cc は実行しない。オーナー実施。** macOS Player / EditMode 緑 / `score-model.test.ts` 緑は **実プレーの結果の送信・表示の証明にはならない** (`docs/audit/open-issues-audit.md` §3.4 未検証の区別) |
| 実装の境界 | 本 spec の未充足分は **証跡と実機観測だけ**。サーバ / Unity コードの新規実装は **既に `main` にある**ため本 spec では 새로書かない (新規コードが必要な場合は #21 / #17 のスコープ) |

## 非目標 (Out of scope)

- サーバ API の新規エンドポイント・スキーマ変更 (`web/routes/api/v1/scores.ts` / `leaderboards/**` は既存)。
- アンチチート・アカウント・ログインの実装 (#7 本文 "No login, no anti-cheat")。
- Web Studio 側のランキング UI 作り直し (v6 Ranking 画面は `V6RankingView` に既存)。
- S0–S3 のゲート (別 Issue #22)。
- **`docs/audit/open-issues-audit.md` / `docs/evidence/index.md` の編集** (cc / 別 Worker が所有)。

## Acceptance criteria

1. 実 Win64 build で 1 曲完走し、Result 画面から **1 回だけ** POST が飛び、サーバが受理 (201) した記録が Player.log / サーバログに残る。nickname は実行時入力。
2. 送信直後に **同じ Result 画面** に確定スコアと自分の順位 (rank) が表示される。**ネットワーク遅延で結果表示がブロックされない**。
3. 別クライアント (別マシンのブラウザ) から `GET /api/v1/leaderboards/{songId}?version={chartVersion}` を取得すると、**手順 1 で送った nickname とスコアが top 10 に含まれる**。
4. 判定数が譜面ノーツ数と一致しない送信は **422** で拒否され、Unity 側に失敗表示が出る (サーバ側検査の通し確認)。
5. 同じ `songId` + nickname + score の 10 秒以内の重複送信は **429** (Retry-After) で拒否される (既存仕様の通し確認)。
6. ランキングが空のとき / 通信エラーのとき / 送信中のとき、既存 uGUI / TextMeshPro で **submitting / error / leaderboard-empty** の 3 状態が表示される。UI framework は変更しない。
7. ローカル PlayerPrefs のハイスコアは **サーバの権威として扱わない** (サーバの top 10 が常に優先して表示される)。
8. `docs/evidence/` に手順 1–3 の証跡 (Player.log の該当行 + 2 台目のクライアントのレスポンス) が残り、`docs/evidence/index.md` の S4 行が「未証明」から更新される (更新の適用は cc が cc 側の PR で行う)。
9. S4 が失敗しても **S3 の playable build が保持** される (S4 の失敗が S3 の展示を壊さない)。

## 現在の実装状況 (2026-11-10 時点)

`origin/main` = `405555c` を実測した結果。**充足 = コードから確認できる** / **未充足 = 実機・実サービス観測が必要** の 2 値。Issue 本文の受入条件との対応も併記する。

### 充足 (コード)

| Issue 本文の受入条件 | 根拠 (file:line) | 判定 |
| --- | --- | --- |
| D1 に score submission と songId+chartVersion ごとの top 10 | `web/routes/api/v1/leaderboards/[songId].ts:22` `LEADERBOARD_SIZE = 10` / `:51` `.limit(LEADERBOARD_SIZE)` / D1 migration `20261106124714_heavy_harrier.sql` の `scores` / `chart_versions` テーブル | 充足 |
| サーバが score を再計算 | `web/routes/api/v1/scores.ts:115` `computeUnityScore(request.perfect, request.great, request.bad, chart.noteCount)` / `_score-model.ts:213` `maxScore = Math.fround(noteCount * 5)` | 充足 |
| judgment count == noteCount を検査 | `web/routes/api/v1/_score-model.ts:176` `if (sum !== noteCount)` → 422 (`scores.ts:59`) / `:182` `maxCombo > noteCount` → 422 | 充足 |
| 単純な編集可能ニックネーム、アカウント無し、アンチチート無し | `Assets/Game/Scripts/UI/RankingClient.cs:135` `ValidateNickname` (trim + 1..16 文字。文字種の制限は無く ASCII / かなとも受理) / `:123` `IsValidSongId` (サーバの songId パターン) / テスト `Assets/Tests/Editor/RankingClientTests.cs:26` `ValidateNickname_TrimsAndAcceptsAsciiAndKana` / `:33` `ValidateNickname_RejectsEmptyOrNullAndTooLong` | 充足 |
| ローカルハイスコアを server の権威にしない | `Assets/Game/Scripts/Persistence/ScorePersistence.cs` + `SaveLoadManager.cs` が PlayerPrefs に保存するのみ。`GManager.Hiscore` は PlayerPrefs | 充足 |
| 時間がなければ S3 を維持 (運用) | `docs/evidence/index.md` §1 S4 行 = "not started — keep the current playable build" | 充足 (運用) |

### 未充足 / 未検証

| # | 条件 | 判定 | 根拠 |
| --- | --- | --- | --- |
| 1 | ランキングを casual / unverified と表示 | **未充足** | `Assets/Game/Scripts/UI/V6/Ranking/V6RankingLogic.cs:135` `SourceLabel` は `"Local"` / `"Online"` を返すのみ。**"casual" / "unverified" の文字列は v6 / v5 どちらにも存在しない** (`grep -rn "casual\|unverified" Assets/Game/Scripts/UI/` → 0 件)。文言追加は **オーナー決定** (cc が独断で UI 文言を入れない) |
| 2 | Gate: 実 Win64 play が result を送信する | **未充足** | `docs/evidence/index.md` §1 の S4 = **未証明**。`release/check/stage-manifest.json` の S4 エントリも `status: "unproven"`。**実 Win64 POST の記録が 1 件も無い** |
| 3 | Gate: 2 番目のブラウザ / クライアントがそれを見る | **未充足** | 上記と同根。「2 台目のクライアントの観測記録」が `docs/evidence/` に存在しない |
| 4 | Unity 側の送信経路が実機で POST に到達する | **未検証 (コードは充足)** | `Assets/Game/Scripts/UI/V6/Result/V6ResultView.cs:865` `RankingClient.PostScore(song.remoteId, chartVersion, nickname, runScore, runPerfect, runGreat, runBad, runMiss, runMaxCombo, …)` / `RankingClient.cs:243` `PostScore` / `:277` `GetLeaderboard` / v5 側 `Assets/Game/Scripts/UI/ResultRankingOverlay.cs:307` `PostScore` / `:186`・`:325` `GetLeaderboard` / `Assets/Game/Scripts/UI/V5/SelectView.cs:1703` `GetLeaderboard`。**JSON が正しいことは RankingClientTests.cs (11 テスト) で証明済みだが、「実プレーの結果が実際に POST される」ことは証明していない** |
| 5 | 3 状態 (submitting / error / leaderboard-empty) 表示 | **未検証 (コードは充足)** | `Assets/Game/Scripts/UI/V6/Result/V6ResultLogic.cs:255` (送信中) / `:261` (輸送失敗) / `:443` `"オフラインのようです  再送 [Enter] / スキップ [Esc]"` / `:376` ローカル保存用メッセージ。**UI framework 変更は無い** |
| 6 | 結果表示がネットワーク遅延でブロックされない | **未検証 (コードは充足)** | `V6ResultView.cs:865` の送信は coroutine、`:1333` の取得は別 coroutine (`FetchRanking`) で、Result 描画と分離されている |
| 7 | 送信する `songId` / `chartVersion` が「実行中の run から固定」される | **未検証 (コードは充足)** | `V6ResultView.cs:864` は `song.remoteId` と `chartVersion` を渡している。`chartVersion` は `CloudChartHolder` (`Assets/Game/Scripts/Songs/CloudChartHolder.cs:16` `Hold` / `:50` `TryGetForSong`) で保持されたスナップショット由来。**実機での end 時 misse notes 修正の挙動は未確認** |

### ブロッカー

- **Windows 実機 (展示 PC)** が要る。cc は展示 PC を操作しない (cc は macOS 側のみ)。
- **本番 API の到達性** が要る (`RankingClient.Origin()` は `CloudCatalogConfig.json` 相当の base URL を使う。`https://flashbeat.rebuildup.dev` が S2/S4 で稼働しているか)。
- **catalog が cloud 由来である** ことが前提。ローカル曲 (remoteId 無し) は送信対象にならない (`V6ResultView.cs` の `song.remoteId == null` 判定で送信をスキップし、端末内保存のみ)。
