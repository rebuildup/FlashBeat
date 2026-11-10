---
description: "FlashBeat lean tasks — Issue 021"
---

# Tasks: リザルトをオンライン送信しランキング表示 (Issue #21)

**Issue**: #21 | **Branch**: `w/spec-wingate` | **Spec**: [`spec.md`](spec.md) / [`plan.md`](plan.md)

## Tasks

- [x] **T1** 譜面完走後に nickname と P/G/B/M で 1 回 POST を送り、songId と chartVersion を
  実行中の run から固定する経路が実装済であることを確認する。
  — **done when**: 呼び出し箇所と payload 構築箇所に file:line が付いていること
  — **実測**: `Assets/Game/Scripts/UI/RankingClient.cs:144` `BuildScoreRequestJson(songId, chartVersion,
  nickname, score, perfect, great, bad, miss, maxCombo)` / `:243` `PostScore` (onDone は 1 回だけ) /
  `:111` `ScoreUrl` / `:116` `LeaderboardUrl` / `:123` `IsValidSongId` / `:135` `ValidateNickname` /
  `Assets/Game/Scripts/UI/V6/Result/V6ResultView.cs:865` `RankingClient.PostScore(song.remoteId,
  chartVersion, nickname, runScore, runPerfect, runGreat, runBad, runMiss, runMaxCombo, …)` /
  v5 側 `Assets/Game/Scripts/UI/ResultRankingOverlay.cs:307` `PostScore`。
  テスト `Assets/Tests/Editor/RankingClientTests.cs:50` `BuildScoreRequestJson_ContainsSubmissionFields`。

- [x] **T2** 結果がネットワーク遅延に関わらず即表示され、非同期で top 10 を取得し、
  ローカルハイスコアを server の権威にしない構造が実装済であることを確認する。
  — **done when**: 描画と送信が分離されていること、取得が別 coroutine であることが file:line で示されること
  — **実測**: `V6ResultView.cs:865` (送信は coroutine 1) / `:1333` `RankingClient.GetLeaderboard(remoteId,
  chartVersion, r => { result = r; })` → `FetchRanking` (coroutine 2) /
  `Assets/Game/Scripts/UI/RankingClient.cs:277` `GetLeaderboard` /
  `Assets/Game/Scripts/Persistence/ScorePersistence.cs` + `SaveLoadManager.cs` は PlayerPrefs のみ
  (サーバの権威データではない)。`GManager.Hiscore` も PlayerPrefs。

- [x] **T3** submitting / error / leaderboard-empty の 3 状態が既存 uGUI / TMP で表示され、
  UI framework 変更が無いことを確認する。
  — **done when**: 3 状態すべてに対応する文言 / 状態が file:line で示されること
  — **実測**: `Assets/Game/Scripts/UI/V6/Result/V6ResultLogic.cs:255` (送信中) / `:261` (輸送失敗) /
  `:316` (201 / 429 / オフライン / その他 HTTP の区分) / `:376` (ローカル保存用メッセージはオンライン語彙を含まない) /
  `:437` `"送信中…"` / `:443` `"オフラインのようです  再送 [Enter] / スキップ [Esc]"` /
  `Assets/Game/Scripts/UI/V6/Ranking/V6RankingLogic.cs:166` `EmptyMessage` / `:180` `"NO DATA"` /
  `:202` `"記録がありません"`。UI framework (uGUI / TMP) の変更は無い。

- [x] **T4** 契約 (openapi.v1.yaml) との URL 一致と、nickname / songId / leaderboard parse が
  既存テストで固定されていることを確認する。
  — **done when**: テスト名と file:line が記録されていること
  — **実測**: `Assets/Tests/Editor/RankingClientTests.cs:26` `ValidateNickname_TrimsAndAcceptsAsciiAndKana` /
  `:33` `ValidateNickname_RejectsEmptyOrNullAndTooLong` / `:50` / `:100` `ParseLeaderboard_ReadsEntries` /
  `:142` `Urls_FollowTheOpenApiContract` / `:152` `IsValidSongId_FollowsTheServerPattern` /
  `:165` `Nickname_RoundTripsThroughPlayerPrefs` (計 11 テスト)。
  関連 `Assets/Game/Tests/V6ResultSubmitTests.cs` / `V6ResultRegistrationTests.cs` / `V6ResultLogicTests.cs`。
  **これらは「JSON が正しい」ことの証明であり「実プレーの結果が POST される」ことの証明ではない。**

- [ ] **T5** 本番 API の生存確認 (`GET /api/v1/catalog` と `GET /api/v1/leaderboards/{songId}?version=N` が応答)。
  — **done when**: API が応答し、送信対象になる cloud 曲 (remoteId 付き) が特定できること
  — **備考**: **cloud 曲が無いと本 Issue の実機観測ができない** (#17 が未証明のため現状)。
  cc は read-only 確認のみ。**deploy しない**。

- [ ] **T6** Windows 実機で 1 曲完走し、Result から nickname 入力 → POST 1 回 (201 受理) を観測する。
  — **done when**: Player.log とサーバ側の記録に、**その run の** POST 受理が残ること
  — **備考**: **Windows 実機は展示 PC のため cc は実行しない。オーナー実施。**
  `FlashBeat.exe` は **引数なし** で起動 (擬似全画面は禁止)。
  **10 秒以内に同一 payload で再送しない** (429 回避)。
  対象は **cloud 由来の曲** (local 曲は送信対象外)。

- [ ] **T7** 結果画面が**ネットワーク待ちでブロックされない**ことを実機で確認する。
  — **done when**: 通信が遅い / 切れている状況でも リザルトが即座に表示され、
  送信中 → 失敗 の状態遷移が観察できること
  — **備考**: コード上は `V6ResultView.cs:865` と `:1333` が別 coroutine。
  **実機での確認は未了**。**Windows 実機は展示 PC のため cc は実行しない。オーナー実施。**

- [ ] **T8** 送信成功後に自分の rank と top 10 が同じ画面に表示されることを確認する。
  — **done when**: rank が表示され、top 10 に自分の行が含まれるスクリーンショット/ログが残ること
  — **備考**: **Windows 実機は展示 PC のため cc は実行しない。オーナー実施。**

- [ ] **T9** 2 台目のクライアントから同じランキングの top 10 が取得でき、自分の nickname が含まれることを確認する。
  — **done when**: 2 台目のレスポンスに nickname と score が含まれる記録が残ること
  — **備考**: **Windows 実機は展示 PC のため cc は実行しない。オーナー実施。**
  **2 台目の Windows は必須ではない** — 別マシンのブラウザで `GET /api/v1/leaderboards/{songId}?version=N`
  を実行すれば足りる (Issue #7 本文 "second browser/client")。

- [ ] **T10** submitting / error / leaderboard-empty の 3 状態が実機で観測できることを確認する。
  — **done when**: 3 状態それぞれの表示が記録 (スクリーンショット / ログ) に残ること
  — **備考**: error 状態は通信を切る、empty 状態は未公開の版を引くなどして作る。
  **Windows 実機は展示 PC のため cc は実行しない。オーナー実施。**

- [ ] **T11** 既存 local 27 曲でオンライン失敗表示が出ないこと (端末内保存のみ) を確認する。
  — **done when**: local 曲の Result に「オフライン」等の表示が出ないことを確認したこと
  — **備考**: `V6ResultView.cs` の送信対象判定 (`song.remoteId == null` → `SaveLocalName()` のみ) の実機確認。
  曲全部ではなく **代表 1 曲** で十分。
  **Windows 実機は展示 PC のため cc は実行しない。オーナー実施。**

- [ ] **T12** S4 が失敗しても S3 の playable build が保持されていることを確認する。
  — **done when**: 展示用フォルダの成果物が S3 のものを指していることを確認したこと
  — **備考**: Issue 本文 "S3 build remains fallback" に対応する運用確認。**読み取り確認のみ**。

- [ ] **T13** 証跡 (Player.log の該当行 / 2 台目のレスポンス / スクリーンショット) を `docs/evidence/` に
  取り込み、`docs/evidence/index.md` §1 の S4 行と `release/check/stage-manifest.json` の S4 エントリを
  更新する。
  — **done when**: S4 の判定が観測結果と一致し、根拠となるファイルが残っていること
  — **備考**: **`docs/evidence/index.md` と `release/check/**` は他 Worker が所有**しているため、
  cc は **cc 側の統合 PR で** 適用する (本 spec の所有範囲は `specs/021-*/` のみ)。
