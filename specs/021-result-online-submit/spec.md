---
description: "FlashBeat lean spec — Issue 021"
---

# Spec: リザルトをオンライン送信しランキング表示 (S4 optional) — 実機 E2E と 2 台目表示

**Issue**: #21 | **Branch**: `w/spec-wingate` | **Date**: 2026-11-10 | **Status**: Draft

**Input**: `[S4/Win/optional] Unity リザルトをオンライン送信しランキング表示`

## Objective

Issue #21 は「Unity のリザルト画面からオンラインへスコアを送り、ランキングを表示する」ことを求める。
`main` には **送信経路 (POST)、取得経路 (GET top 10)、nickname 入力、
submitting / error / leaderboard-empty の 3 状態表示** が既に実装されている。

本 spec の目的は、**残った未充足条件**、すなわち

1. **譜面完走後に 1 回だけ POST が飛ぶ**ことの実機証明 (end での missed notes 修正の挙動を含む)、
2. **結果がネットワーク遅延に関わらず即表示**され、top 10 が非同期取得されることの実機証明、
3. **2 台目のデバイスから同じランキングが見える**ことの証明、

を 1 つの作業単位に固定することにある。サーバ側の受入 (#7) は別 Issue だが、
**本 Issue の証拠は #7 の証拠と同一の観測**を共有するため、両者は pair で進む。

## Decisions (凍結する設計)

| 項目 | 決定 |
| --- | --- |
| 送信のタイミング | **譜面完走後、1 回だけ** POST する。`V6ResultView.cs:865` の `RankingClient.PostScore(...)` は coroutine として 1 回だけ走る。**リトライは明示操作** (通信失敗時に「再送 [Enter]」) であり、自動連打での多重送信はしない (サーバ側で 429 になる) |
| songId / chartVersion の固定 | 実行中の run から固定する。`V6ResultView.cs:864` は `song.remoteId` と `chartVersion` を渡す。`chartVersion` は `CloudChartHolder` (`Hold` / `TryGetForSong`) が保持した譜面スナップショットの版であり、**catalog の最新値で上書きしない** (プレイ中に catalog が更新されても/run の版で送る) |
| ローカル曲 | `remoteId` が無い local 曲 (27 曲) は **オンライン送信の対象外**。`V6ResultView.cs` は送信対象が無い場合に **オンラインの失敗表示を出さず** 端末内保存のみを行う。**この挙動は維持する** |
| 結果表示と送信の分離 | **結果はネットワーク遅延に関わらず即表示**する。送信は別 coroutine (`V6ResultView.cs:865`) で、`GetLeaderboard` も別 coroutine (`:1333` `FetchRanking`) で非同期取得する。**リザルト描画を通信完了でブロックしない** |
| ローカルハイスコア | `ScorePersistence` / `SaveLoadManager` / `GManager.Hiscore` は **端末内記録**であり、**サーバの権威ではない**。サーバの top 10 が常に優先して表示される。**変更しない** |
| UI framework | 既存 uGUI / TextMeshPro + v5 の `UISizeProfile` 体系を維持。**UI framework の変更はしない** (Issue 本文 "no UI framework change") |
| nickname | trim + 1..16 文字 (`RankingClient.cs:135` `ValidateNickname`)。**アカウント・ログイン・アンチチートは実装しない** (Issue 本文 "No login, no anti-cheat") |
| 実機操作 | **Windows 実機は展示 PC のため cc は実行しない。オーナー実施。** `RankingClientTests.cs` の緑は「JSON が正しい」ことの証明であって「実プレーの結果が実際に POST される」ことの証明ではない |
| optional の位置づけ | #21 は **optional until S3 stable** (Issue 本文)。**S3 の playable build を壊さない**。失敗しても S3 を維持する |
| 実装の境界 | 本 spec はドキュメントのみ。**新規コードは書かない**。未充足分は **実機 E2E と証跡** |

## 非目標 (Out of scope)

- サーバ API の実装・変更 (`web/routes/api/v1/scores.ts` / `leaderboards/**` は #7 側)。
- ログイン / アカウント / アンチチート (Issue 本文 "No login, no anti-cheat")。
- v6 Result 画面の作り直し (`V6ResultView` / `V6ResultLogic` は既存。#240 の責務)。
- Web Studio 側のランキング UI 作り直し。
- **`Assets/**` / `web/**` のコード変更** (本 spec はドキュメントのみ)。

## Acceptance criteria

1. 譜面完走後、**1 回だけ** POST が飛び、サーバが 201 で受理する (実機での記録)。
2. 送信する payload が **実行中の run** の songId / chartVersion / P / G / B / M / maxCombo を含み、
   **判定数の合計が譜面のノーツ数** と一致している (サーバの 422 が返らない)。
3. **リザルト画面はネットワークが遅い / 失敗していても即座に表示**される (通信待ちで固まらない)。
4. 送信成功後、**同じ画面** に自分の順位 (rank) が表示される。
5. **top 10 が非同期取得** され、リランキングされる (送信直後の取得でも自分の行が含まれる)。
6. 送信中 / 通信エラー / ランキング空 の 3 状態が既存 uGUI / TMP で表示され、
   エラー時は再送 / スキップの操作 offered がある。
7. **2 台目のデバイス** (別 Windows / 別ブラウザ) から同じランキングの top 10 が同じ nickname を含めて表示されること。
8. ローカル PlayerPrefs のハイスコアは **サーバの権威として扱わない** (サーバの top 10 が優先)。
9. 既存 local 27 曲では **オンライン送信の失敗表示が出ない** (端末内保存のみ) となること。
10. S4 が失敗しても **S3 の playable build が保持** される。

## 現在の実装状況 (2026-11-10 時点)

`origin/main` = `405555c` を実測した結果。

### 充足 (コード)

| Issue タスク | 根拠 (file:line) | 判定 |
| --- | --- | --- |
| T1 (一部): 譜面完走後に nickname と P/G/B/M で 1 回 POST、songId と chartVersion を実行中の run から固定 | `Assets/Game/Scripts/UI/RankingClient.cs:144` `BuildScoreRequestJson(songId, chartVersion, nickname, score, perfect, great, bad, miss, maxCombo)` / `:243` `PostScore` (onDone は 1 回だけ) / `Assets/Game/Scripts/UI/V6/Result/V6ResultView.cs:865` `RankingClient.PostScore(song.remoteId, chartVersion, nickname, runScore, runPerfect, runGreat, runBad, runMiss, runMaxCombo, …)` / v5 側 `Assets/Game/Scripts/UI/ResultRankingOverlay.cs:307` `PostScore` | 充足 (コード) |
| T2: 結果はネットワーク遅延に関わらず即表示、非同期で top10 GET、ローカルハイスコアを server の権威にしない | `V6ResultView.cs:865` (送信は coroutine) / `:1333` `RankingClient.GetLeaderboard(remoteId, chartVersion, …)` → `FetchRanking` (別 coroutine) / `Assets/Game/Scripts/Persistence/ScorePersistence.cs` + `SaveLoadManager.cs` は PlayerPrefs に保存するのみ | 充足 (コード) |
| T3: submitting / error / leaderboard-empty の状態を既存 uGUI/TMP で表示、UI framework 変更なし | `Assets/Game/Scripts/UI/V6/Result/V6ResultLogic.cs:255` (送信中) / `:261` (輸送失敗) / `:316` (201 / 429 / オフライン / その他 HTTP の区分) / `:437` `"送信中…"` / `:443` `"オフラインのようです  再送 [Enter] / スキップ [Esc]"` / `:376` (ローカル保存用メッセージはオンライン語彙を含まない) / `:180` `V6RankingLogic.EmptyMessage` (`"NO DATA"` / `"記録がありません"`) | 充足 (コード) |
| No scope: login / anti-cheat | 該当実装なし | 充足 (規律) |
| 契約整合 | `Assets/Tests/Editor/RankingClientTests.cs:142` `Urls_FollowTheOpenApiContract` (`contracts/openapi.v1.yaml` との一致を固定) / `:152` `IsValidSongId_FollowsTheServerPattern` / `:26` / `:33` / `:50` / `:100` / `:165` (計 11 テスト) | 充足 |

### 未充足 / 未検証

| # | 条件 | 判定 | 根拠 |
| --- | --- | --- | --- |
| 1 | T4: **End-to-end streamed play → submit → 2 台目のデバイスで表示**、artifact 保存 | **未充足** | `docs/evidence/index.md` §1 の **S4 = 未証明**。`release/check/stage-manifest.json` の S4 エントリも `status: "unproven"`。**実 Win64 の POST 記録も、2 台目の表示記録も 1 件も無い** |
| 2 | Gate: 実 run 後にスコアボードが見える、S3 build は fallback のまま | **未充足 (実機)** | 同上 |
| 3 | **end での missed notes の修正**が実機で正しく起こる | **未検証 (コードは充足)** | 譜面完走 → ノーツ全確定 → 送信、という順序は `SongEndStateMachine` (#226) と `V6ResultView` の送信経路が担保する。**実機での end 時挙動 (未判定ノーツの扱い) は未確認** |
| 4 | 結果表示が通信でブロックされないことの実機確認 | **未検証 (コードは充足)** | コード上は分離されている (`V6ResultView.cs:865` / `:1333` は別 coroutine)。**実機での体感 (通信遅い環境で即表示) は未観測** |
| 5 | 自分の rank が表示されることの実機確認 | **未検証 (コードは充足)** | `RankingClient.cs` の `ParseScoreResponse` が rank を持つ。**実機での rank 表示は未観測** |
| 6 | 3 状態 (submitting / error / leaderboard-empty) の実表示 | **未検証 (コードは充足)** | `V6ResultLogic.cs:437` / `:443` の文字列は存在するが、**実機での見え方 (文言が実際に画面に出る / 空状態のレイアウト) は未確認** |
| 7 | local 曲でオンライン送信の失敗表示が出ない | **未検証 (コードは充足)** | `V6ResultView.cs` の送信対象判定 (`song.remoteId == null` なら `SaveLocalName()` のみ) がある。**実機での 27 曲それぞれは未観測** |
| 8 | cloud 由来であることの前提 | **未充足 (依存)** | #17 (S2) の E2E が未了。**local 曲しか選べない状態では #21 の実機観測ができない** |

### ブロッカー

- **Windows 実機 (展示 PC)** が要る。cc は展示 PC を操作しない。
- **#17 (S2)** が未証明 → **送信対象の cloud 曲が無い**。
- **#7 (サーバ側ゲート)** が未証明 → 本番 API の稼働確認が要る。
- **2 台目のデバイス** — 展示 PC は 1 台だが、**2 台目の Windows は必須ではない** (別ブラウザで.rankings を確認できれば足りる。 Issue #7 本文 "second browser/client")。

### 参考

- `docs/audit/open-issues-audit.md` §3.4 の #21 節 (充足 3 / 未充足 1 / 未検証 1) が本 spec の根拠。
- 本 Issue は #7 (parent #3 → S4) の子であり、**#7 と同じ観測を共有する**。
