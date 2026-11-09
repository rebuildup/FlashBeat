---
description: "FlashBeat lean tasks — Issue 007"
---

# Tasks: 展示用クラウドランキング ゲート (Issue #7)

**Issue**: #7 | **Branch**: `w/spec-wingate` | **Spec**: [`spec.md`](spec.md) / [`plan.md`](plan.md)

## Tasks

- [x] **T1** サーバ側の受入バックエンド (score 再計算・noteCount 検査・top 10・nickname 制約・重複 429) が
  `main` に存在することを確認し、受入条件との対応表を `spec.md` の「現在の実装状況」に記録する。
  — **done when**: サーバ API と Unity クライアントの各受入条件に「充足 / 未充足 / 未検証」の
  判定が付き、根拠が file:line で示されていること
  — **実測**: `web/routes/api/v1/scores.ts:115` (computeUnityScore) /
  `web/routes/api/v1/_score-model.ts:176` (sum !== noteCount → 422) / `:182` (maxCombo > noteCount) /
  `:213` (maxScore = fround(noteCount * 5)) /
  `web/routes/api/v1/leaderboards/[songId].ts:22,51` (LEADERBOARD_SIZE = 10) /
  D1 migration `20261106124714_heavy_harrier.sql` の `scores` / `chart_versions`。
  `web/tests/score-model.test.ts` / `web/tests/api.test.ts` は既存。

- [x] **T2** Unity 側の送信・取得経路 (POST / GET / nickname / 3 状態表示) が `main` に存在することを確認し、
  同じ表に記録する。
  — **done when**: Unity 側の受入条件に判定と file:line が付いていること
  — **実測**: `Assets/Game/Scripts/UI/RankingClient.cs:135` (ValidateNickname) / `:144`
  (BuildScoreRequestJson) / `:243` (PostScore) / `:277` (GetLeaderboard) /
  `Assets/Game/Scripts/UI/V6/Result/V6ResultView.cs:865` (PostScore 呼び出し) / `:1333`
  (GetLeaderboard 呼び出し) / `Assets/Game/Scripts/UI/ResultRankingOverlay.cs:186,307,325` (v5 側) /
  `Assets/Game/Scripts/UI/V5/SelectView.cs:1703` /
  `Assets/Game/Scripts/UI/V6/Result/V6ResultLogic.cs:255,261,443` (送信中 / 輸送失敗 / 再送表示) /
  `Assets/Tests/Editor/RankingClientTests.cs` 6 テスト
  (`:26` / `:33` / `:50` / `:100` / `:142` / `:152` / `:165`)。
  **これらは「JSON が正しい」ことの証明であり「実プレーの結果が POST される」ことの証明ではない。**

- [ ] **T3** 「casual / unverified」ラベルの文言をオーナー確定する。
  — **done when**: 表示文言 (または「表示しない」判断) が決められていること
  — **現状**: `Assets/Game/Scripts/UI/V6/Ranking/V6RankingLogic.cs:135` `SourceLabel` は
  `"Local"` / `"Online"` のみ。`grep -rn "casual\|unverified" Assets/Game/Scripts/UI/` は 0 件。
  **cc が独断で UI 文言を入れない**。文言追加は #21 のスコープ。

- [ ] **T4** 本番 API の生存確認 (送信先の `GET /api/v1/catalog` が 200 を返すこと)。
  — **done when**: 送信先の origin が応答し、catalog が 1 件以上取得できること
  — **備考**: 本番 API の existence は #22 / #17 の E2E と併せて確認する。
  **cc は read-only の確認のみ** (`script/with-secrets` 経由)。deploy は行わない。

- [ ] **T5** Windows 実機で 1 曲完走 → Result → nickname 入力 → POST 1 回 (201 受理) を観測する。
  — **done when**: Player.log とサーバ側の記録に、**その run の** POST 受理が残ること
  — **備考**: **Windows 実機は展示 PC のため cc は実行しない。オーナー実施。**
  手順は `docs/operations/win64-candidate-acceptance-2026-11-10.md` §3.3 / §3.4。
  `FlashBeat.exe` は **引数なし** で起動 (擬似全画面は禁止)。
  429 を避けるため **同一 nickname の 10 秒以内の再送はしない**。
  対象は **cloud 由来の曲** (ローカル曲には remoteId が無く送信対象外)。

- [ ] **T6** 2 台目のクライアントから `GET /api/v1/leaderboards/{songId}?version=N` を実行し、
  T5 の nickname が top 10 に含まれることを確認する。
  — **done when**: レスポンスに T5 の nickname とスコアが含まれる記録が残ること
  — **備考**: **Windows 実機は展示 PC のため cc は実行しない。オーナー実施。**
  2 台目の Windows ではなく **別マシンのブラウザ** でも差し替え可。
  検証は `curl` / ブラウザのどちらでも良い (同一 API の GET)。

- [ ] **T7** 判定数不一致 (422) と重複送信 (429) の通し確認。
  — **done when**: 実機 / 実 API 経由で 422 と 429 が返り、Unity 側に失敗表示が出ることを確認したこと
  — **備考**: 422 は「意図的に noteCount と違う P/G/B/M を送る」操作が必要なため、
  実機操作ではなく **API への直接リクエスト** で行うのが安全。
  429 は T5 の直後に同じ payload で再送すれば自然に発生しうる。

- [ ] **T8** T5–T7 の証跡を `docs/evidence/` に取り込み、`docs/evidence/index.md` §1 の S4 行を
  「未証明」から更新する。
  — **done when**: evidence に Player.log の該当行・2 台目のレスポンス・422/429 の応答が残り、
  index.md の S4 行が観測結果と一致していること
  — **備考**: **`docs/evidence/index.md` は他 Worker が所有**しているため、cc は
  **cc 側の統合 PR で** 適用する (本 spec の所有範囲は `specs/007-*/` のみ)。

- [ ] **T9** S4 が失敗しても S3 の playable build が保持されていることを確認する。
  — **done when**: 展示用フォルダの成果物が S3 のものを指していることを確認したこと
  — **備考**: #7 本文「If time is short keep S3」に対応する運用確認。
  展示フォルダは読み取り参照。
