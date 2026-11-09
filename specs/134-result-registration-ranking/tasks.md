---
description: "FlashBeat lean tasks — Issue 134"
---

# Tasks: リザルトでスコア登録と曲別ランキング表示 (Issue #134)

**Issue**: #134 | **Branch**: `w/spec-gameui` | **Spec**: [`spec.md`](spec.md) / [`plan.md`](plan.md)

## Tasks

- [x] **T1** ランキング送信・取得の通信層が main にあることの確認。
  — **done when**: ファイル名・主要メンバが file:line 付きで記録されていること
  — **実測**: `Assets/Game/Scripts/UI/RankingClient.cs:111` (`ScoreUrl`)、`:116` (`LeaderboardUrl`)、`:123`
  (`IsValidSongId`)、`:135` (`ValidateNickname`)、`:144` (`BuildScoreRequestJson`)、`:165`
  (`ParseScoreResponse`)、`:181` (`ParseLeaderboard`)、`:205` (`SaveNickname`)、`:225` (`LoadNickname`)、`:243`
  (`PostScore`)、`:277` (`GetLeaderboard`)。旧 v5 経路は
  `Assets/Game/Scripts/UI/ResultRankingOverlay.cs:186` / `:307` / `:325`

- [x] **T2** v6 Result に登録モーダル (名前入力 → Enter 登録 / Esc スキップ) があることの確認。
  — **done when**: 入力・検証・送信・結果表示の各ステップが file:line 付きで記録されていること
  — **実測**: `Assets/Game/Scripts/UI/V6/Result/V6ResultView.cs:571` (Register ボタン)、
  `:742` (`OpenModal`)、`:829` (`V6ResultLogic.ValidateNicknameInput`)、`:865` (`RankingClient.PostScore`)、
  `:879` / `:806,815` (二重送信ガード)、`:959` (`SaveNickname`)、`:991` (`HandleModalInput`)、
  `:1021-1023` (`Input.inputString` から 1 文字取り)、`:1296` (`StartRankingFetch`) → `:1333`
  (`GetLeaderboard`)。`specs/240-result-registration` でローカル/オンライン分離も復元済み

- [x] **T3** オフライン / 失敗時にゲームが止まらないことの (コード・テスト上の) 確認。
  — **done when**: 4 区分の分類とガードが file:line 付きで記録されていること
  — **実測**: `V6ResultLogic.ClassifySubmitResult` (201 / 429 duplicate / オフライン / その他 HTTP)、
  `V6ResultView.cs:806,815` のガード、`Assets/Tests/Editor/RankingClientTests.cs` のパース失敗系、
  `Assets/Tests/Editor/V6ResultSubmitTests.cs`。ローカル曲 (送信不可) は通信せず
  `V6LocalNameStore` に保存 (`specs/240-result-registration/spec.md`)

- [x] **T4** EditMode テスト (11 件の `RankingClientTests` ほか) が main にあることの確認。
  — **done when**: テスト名と件数が記録されていること
  — **実測**: `Assets/Tests/Editor/RankingClientTests.cs:26` (`ValidateNickname_TrimsAndAcceptsAsciiAndKana`)、
  `:50` (`BuildScoreRequestJson_ContainsSubmissionFields`)、`:100` (`ParseLeaderboard_ReadsEntries`)、
  `:142` (`Urls_FollowTheOpenApiContract`)、`:152` (`IsValidSongId_FollowsTheServerPattern`)、
  `:165` (`Nickname_RoundTripsThroughPlayerPrefs`) ほか計 11 件。
  `Assets/Tests/Editor/RankingPanelBackdropTests.cs`、`Assets/Game/Tests/V6ResultLogicTests.cs`、
  `Assets/Game/Tests/V6ResultRegistrationTests.cs`、`Assets/Game/Tests/V6ResultSubmitTests.cs` も存在

- [x] **T5** サーバー側の再計算 (422) の存在確認 (読み取りのみ)。
  — **done when**: サーバー側のファイルと関数が記録され、変更が行われていないこと
  — **実測**: `web/routes/api/v1/scores.ts` の `computeUnityScore` がクライアント値を再計算し、
  一致しない場合 422 を返す。`web/routes/api/v1/leaderboards/**` がボードを返す。
  **cc はサーバーを変更しない**

- [ ] **T6** 実機での実登録 (ニックネーム入力 → Enter → 登録成功 → ボード更新) と
  Esc スキップ (送信されない) の確認。
  — **done when**: 実画像と `Player.log` が `docs/evidence/134-result-registration-ranking/` に残り、
  判定が「充足 (実機)」に更新されていること
  — **Windows 実機は展示 PC のため cc は実行しない。オーナー実施**。
  候補ビルドは main からの Win64 配布物 (`docs/evidence/index.md` §7.2 の SHA 照合手順)

- [ ] **T7** オフライン / HTTP 失敗時にゲームが止まらないことの実機確認。
  — **done when**: ネットワークを切った状態で登録 → 4 区分のメッセージが出て Result から出られることの
  記録があること
  — **Windows 実機は展示 PC のため cc は実行しない。オーナー実施**

- [ ] **T8** 2 台目のクライアントで自分の順位が表示されることの確認。
  — **done when**: 1 台目の登録が 2 台目のボードに現れ、自分の順位行が存在することの記録があること
  — **2 台の環境が必要**。**Windows 実機は展示 PC のため cc は実行しない。オーナー実施**。
  1 台だけでは「登録された」ことしか証明できない

- [ ] **T9** 展示 patch line (`b677f8f`) への移植か、main を正とするかの **判断を記録**する。
  — **done when**: 移植の要否が判断として記録され、未決のまま放置されていないこと
  — **実測 (現状)**: `git ls-tree -r --name-only b677f8f | grep -c ranking` = **0**
  (`RankingClient.cs` / `ResultRankingOverlay.cs` が無い)。
  `docs/audit/open-issues-audit.md` §4 U6 が「展示 line と main のどちらに寄せるかはオーナー判断」と明記。
  **cc は判断しない。オーナー判断待ち**

- [ ] **T10** 受入判定表 (OK / NG / 未検証) の作成。
  — **done when**: 6 つの受入条件が OK / NG / 未検証 のいずれかで判定され、
  根拠が file:line または証跡ファイルで示されていること。
  **未検証が「合格」に書き換えられていないこと**
  — **Windows 実機 / 2 台目クライアント / 本番 API 書き込みが必要なものは「未検証 (オーナー実施)」で固定**
