---
description: "FlashBeat lean spec — Issue 134"
---

# Spec: リザルトでスコア登録 (名前入力) と曲別ランキング表示

**Issue**: #134 | **Branch**: `w/spec-gameui` | **Date**: 2026-11-10 | **Status**: Draft

## Input

`[Ranking/P0] Unity: リザルトでスコア登録 (名前入力) と曲別ランキング表示 (展示 patch line へも移植)`

## Objective

#134 は「**リザルト画面でスコアの登録操作ができ、その曲のランキング (Top10 + 自分の順位) が見られる**」
という 2 つの受入条件を主张する。**main には実装・EditMode テスト・v6 の登録モーダルまで揃っている**。

残るのは 3 つ:

- (A) **実機での実登録・実 API 送信** (2 クライアント観測を含む) — 環境依存
- (B) **展示 patch line (`b677f8f`) への移植** — 誰に寄せるかの判断待ち
- (C) **2 台目クライアントでの順位表示** — 2 台の環境が必要

本 spec は (A)〜(C) を「どの手順で、どの観測点で、どの環境が必要か」を固定し、
**既に充足している部分と未充足部分を混同しない** ための判定表を提供する。

## Decisions (凍結する設計)

| 項目 | 決定 |
| --- | --- |
| 登録 UI の正本 | v6 Result の登録モーダル `Assets/Game/Scripts/UI/V6/Result/V6ResultView.cs` (`:742` `OpenModal`、`:991` `HandleModalInput`、`:1023` `Input.inputString`、`:865` `RankingClient.PostScore`、`:959` `RankingClient.SaveNickname`、`:1296` `StartRankingFetch`)。`specs/240-result-registration` で v5 と同じ PlayerPrefs キー `"V5PlayerName"` の契約も復元済み |
| 通信層 | `Assets/Game/Scripts/UI/RankingClient.cs` (`:111` `ScoreUrl`、`:116` `LeaderboardUrl`、`:144` `BuildScoreRequestJson`、`:181` `ParseLeaderboard`、`:243` `PostScore`、`:277` `GetLeaderboard`)。**新しい通信経路は作らない** |
| サーバー | `web/routes/api/v1/scores.ts` が `computeUnityScore` で **サーバー側で再計算** し、一致しないクライアント値は 422。**クライアントのスコアを鵜呑みしない** |
| オフライン / 失敗時 | ゲームを止めない。`V6ResultLogic.ClassifySubmitResult` (201 / 429 duplicate / オフライン / その他 HTTP) の 4 区分を維持。ローカル曲は送信せず `V6LocalNameStore` に保存する (`specs/240-result-registration`) |
| 実機受入の前提 | **実 API 送信が成立するには、(1) 実機、(2) 実サーバー到達、(3) 2 台目のクライアント** の 3 つが必要。**Windows 実機は展示 PC のため cc は実行しない。オーナー実施** |
| 展示 patch line への移植 | 展示 patch line = `b677f8f` (`git ls-tree -r --name-only b677f8f` に `RankingClient.cs` / `ResultRankingOverlay.cs` は **0 件**)。移植の要否は **どちらの line を正とするかのオーナー判断** 待ち (`docs/audit/open-issues-audit.md` §4 U6 / §7)。**cc は判断しない** |
| 証跡の置き場所 | `docs/evidence/134-result-registration-ranking/` に置く (新規)。`docs/evidence/index.md` / `docs/audit/open-issues-audit.md` の編集は **他 Worker 所有** なので触らない |
| 実装の変更 | 本 Issue は **検証 Issue**。発見した不具合は起票だけして報告する |

## 非目標 (Out of scope)

- `RankingClient` / `ResultRankingOverlay` / `V6ResultView` / `V6ResultLogic` の実装変更。
- 新しい API エンドポイント / 新しい保存経路 / 新しいスコア集計モデルの新設。
- サーバー (`web/`) の変更。
- 展示 patch line への移植の **実施** (判断待ち)。
- `docs/evidence/index.md` / `docs/audit/open-issues-audit.md` の編集 (他 Worker 所有)。

## Acceptance criteria

1. リザルト画面から『ランキング登録』を操作でき、ニックネーム入力 → Enter で登録 → Esc でスキップ、という **入力契約** が実機で成立する。
2. 登録後、その曲の **Top10 + 自分の順位** が表示される (オンライン語彙 / ローカル語彙の区別は `V6ResultLogic.RegistrationMode` に従う)。
3. **オフライン / HTTP 失敗でもゲームが止まらない** (4 区分のメッセージが出て、Result 画面から出られる)。
4. 実 API 送信が実際に受け付けられ、サーバー側で再計算されたスコアが一致する (不一致は 422 になる)。
5. **2 台目のクライアント**から同じ曲の順位ボードを見て、自分の順位が表示される。
6. 既に main にある EditMode テスト (11 件の `RankingClientTests` ほか) が緑である。
7. 展示 patch line (`b677f8f`) へ移植するか、main を正とするかの **判断が記録されている** (未決のまま放置しない)。
8. 受入判定が OK / NG / 未検証 の 3 値で記録され、**未検証が「合格」に書き換えられていない**。

## 現在の実装状況 (2026-11-10 時点)

**充足済み**

| 受入条件 | 判定 | 根拠 (file:line / 証跡) |
|---|---|---|
| リザルトで『ランキング登録』(ニックネーム入力 / Enter 登録 / Esc スキップ) | 充足 (コード) | `Assets/Game/Scripts/UI/V6/Result/V6ResultView.cs:742` (`OpenModal`)、`:991` (`HandleModalInput`)、`:1021-1023` (`Input.inputString` から 1 文字取り)、`:571` (フッター "Op Register" ボタン)、`:959` (`RankingClient.SaveNickname`)、`:960` (`submittedThisResult = true`)、`:879` / `:806,815` (二重送信ガード) |
| その曲の Top10 + 自分の順位を表示 | 充足 (コード) | `V6ResultView.cs:1296` (`StartRankingFetch`) → `:1333` (`RankingClient.GetLeaderboard`)。旧 v5 経路は `Assets/Game/Scripts/UI/ResultRankingOverlay.cs:186` (`GetLeaderboard`)、`:307` (`PostScore`)、`:325` (再取得) |
| シーン非依存のコード構築オーバーレイ (KeyConfigUI と同方式) | 充足 | `Assets/Game/Scripts/UI/ResultRankingOverlay.cs` は独立 MonoBehaviour。テスト `Assets/Tests/Editor/RankingPanelBackdropTests.cs` |
| オフライン / 失敗でもゲームを止めない | 充足 (コード) | `V6ResultLogic.ClassifySubmitResult` (201 / 429 duplicate / オフライン / その他 HTTP の 4 区分)、`V6ResultView.cs:806,815` のガード。ローカル曲 (送信不可) は `V6LocalNameStore` に保存 (`specs/240-result-registration/spec.md`) |
| EditMode テスト | 充足 | `Assets/Tests/Editor/RankingClientTests.cs` (11 件: `:26` `ValidateNickname_TrimsAndAcceptsAsciiAndKana`、`:50` `BuildScoreRequestJson_ContainsSubmissionFields`、`:100` `ParseLeaderboard_ReadsEntries`、`:142` `Urls_FollowTheOpenApiContract`、`:152` `IsValidSongId_FollowsTheServerPattern`、`:165` `Nickname_RoundTripsThroughPlayerPrefs` ほか) |
| サーバー側の再計算 | 充足 | `web/routes/api/v1/scores.ts` が `computeUnityScore` で再計算し、一致しないクライアント値に 422 |

**未充足**

| 受入条件 | 判定 | 未充足の理由 |
|---|---|---|
| 実機での実登録・実 API 送信 | **未充足 (実機 + 実サーバー必須)** | 通信は `UnityWebRequest` を使うため実機 (または実機相当の実行) が必要。**Windows 実機は展示 PC のため cc は実行しない。オーナー実施**。`Urls_FollowTheOpenApiContract` は URL の書式検証のみで、**HTTP 通信の結果は保証しない** |
| 2 台目のクライアントでの順位表示 | **未充足 (2 台の環境が必要)** | `docs/audit/open-issues-audit.md` の #134 行が「実 Win64 での送信 → 2 台目での表示」を未検証と判定 |
| 展示 patch line (`b677f8f`) への移植 | **未充足 (判断待ち)** | `git ls-tree -r --name-only b677f8f | grep -c ranking` = **0**。移植するか main を正とするかは **オーナー判断** (`docs/audit/open-issues-audit.md` §4 U6 が「展示 line と main のどちらに寄せるかはオーナー判断」と明記) |
| 実機の証跡 (スクリーンショット / Player.log) | **未充足** | `docs/evidence/` に #134 用の実機証跡ディレクトリが無い |
| 関連ブランチ `origin/134-patch` の main 未マージ分 | **参考** | `origin/134-patch` に `615db66` / `5067329` があるが、`git log origin/main..origin/134-patch` は空 (内容は main に取り込まれ済み)。`git ls-tree -r --name-only origin/134-patch` に `RankingClient.cs` / `ResultRankingOverlay.cs` / `RankingClientTests.cs` はある |
