---
description: "FlashBeat lean implementation plan — Issue 134"
---

# Plan: リザルトでスコア登録と曲別ランキング表示 — Issue #134

**Issue**: #134 | **Branch**: `w/spec-gameui` | **Spec**: [`spec.md`](spec.md) | **Date**: 2026-11-10

## Architecture

```
[ 実装 (main 済み・変更しない) ]

ResultScene
  └─ V6ResultView.cs:171  StartRankingFetch()
        └─ :1296 → RankingClient.GetLeaderboard(remoteId, chartVersion)  (:1333)
             └─ RankingClient.cs:277  UnityWebRequest GET  → :181 ParseLeaderboard
                  └─ Top10 + 自分の順位 を V6 の表へ描画

  フッター "Op Register" (:571) → :742 OpenModal()
        └─ :991 HandleModalInput()
             ├─ :1023 Input.inputString から 1 文字取り (IME 非依存)
             ├─ Esc → スキップ (送信なし)
             └─ Enter → :829 ValidateNicknameInput → :865 RankingClient.PostScore()
                    ├─ 201        → 登録成功 (:959 SaveNickname)
                    ├─ 429        → 既存登録 (duplicate)
                    ├─ オフライン → 4 区分 (ClassifySubmitResult)
                    └─ その他     → 4 区分
                    ※ ローカル曲 (送信不可) は通信せず V6LocalNameStore に保存
                       (specs/240-result-registration)

RankingClient.cs
  :111 ScoreUrl() / :116 LeaderboardUrl(songId, chartVersion)
  :144 BuildScoreRequestJson / :165 ParseScoreResponse / :181 ParseLeaderboard
  :205 SaveNickname / :225 LoadNickname / :243 PostScore / :277 GetLeaderboard

サーバー: web/routes/api/v1/scores.ts
  → computeUnityScore で再計算 → 不一致は 422 (クライアント値を鵜呑みしない)

[ 残作業 ]
  ① 実機での実登録 (Windows 実機 = 展示 PC → オーナー実施)
  ② 2 台目クライアントでの順位表示 (2 台の環境が必要)
  ③ 展示 patch line b677f8f への移植 / main を正とするかの判断 (オーナー判断)
  ④ 判定表 (OK / NG / 未検証) の記録
```

## 実装順序

1. **既存実装とテストの確認 (変更なし)**
   - `Assets/Tests/Editor/RankingClientTests.cs` の 11 件が緑であることを確認する。
   - `specs/240-result-registration` の受入記録 (`docs/evidence/240-result-registration.md`) で、ローカル/オンラインの分岐が復元済みであることを確認する。
2. **サーバー側の契約確認 (読み取りのみ)**
   - `web/routes/api/v1/scores.ts` の `computeUnityScore` と 422 条件を読む。
   - **サーバーは変更しない**。
3. **実機での実登録 (オーナー実施)**
   - Win64 ビルド (main から `script/win64-build-mac.sh` で生成した配布物) を SHA-256 照合して展開する。
   - Result 画面 → 『ランキング登録』→ ニックネーム入力 → Enter → 成功メッセージ → ボード更新、を確認。
   - Esc スキップ → 送信されないことを確認。
   - スクリーンショットと `Player.log` を `docs/evidence/134-result-registration-ranking/` に残す。
4. **2 台目クライアントでの順位表示**
   - 1 台目で登録 → 2 台目 (別プレイヤー or 別マシン) で同じ曲を開く → 自分の順位が見えることを確認する。
   - **1 台では「順位|leaderboard に登録された」ことしか証明できない** ので、2 台目観測を省略しない。
5. **オフライン / 失敗ケース**
   - ネットワークを切った状態で登録 → ゲームが止まらず、4 区分のメッセージが出ることを確認する。
6. **展示 patch line の扱い**
   - `git ls-tree -r --name-only b677f8f | grep -c ranking` = 0 を記録する。
   - 移植の判断は **オーナーに委ねる**。cc は移植も判断もしない。
7. **判定表の作成**
   - 各受入条件を OK / NG / 未検証 の 3 値で記録する。**未検証を「合格」に書き換えない**。

## ブロッカー

| ブロッカー | 内容 | 対処 |
|---|---|---|
| **Windows 実機 (展示 PC)** | 実登録・実 API 送信は実機必須。**Windows 実機は展示 PC のため cc は実行しない。オーナー実施** | 手順書と候補 SHA を渡し、判定を「未検証」で固定 |
| **実サーバー到達** | `RankingClient.Origin()` は `CloudCatalogConfig.json` の `baseUrl` (`https://flashbeat.rebuildup.dev`) を使う。**本番 API への書き込み**になる | 書き込みが許容されるかはオーナー判断。本番への実送信は **cc が自発的に行わない** |
| **2 台目のクライアント** | 順位の表示は他クライアントからの登録が必要 | 2 台の環境が必要。**未観測なら「未検証」** |
| **展示 patch line の正本** | `b677f8f` にランキング関連ファイルが 0 件。main を正とするか移植するかの判断が必要 | **オーナー判断** (`docs/audit/open-issues-audit.md` §4 U6) |
| **Windows Media Foundation** | ストリーム音声 (m4a) が鳴らないため、**映像・音声が無くても登録操作自体は成立する** ことを念頭に置く | 登録は通信だけなので影響なし。誤解しない |
| **`docs/evidence/index.md` の編集** | 他 Worker 所有 | 触らない |

## テスト戦略

| 種別 | 内容 | 備考 |
|---|---|---|
| EditMode (既存のみ) | `RankingClientTests.cs` (11 件)、`RankingPanelBackdropTests.cs`、`V6ResultLogicTests.cs`、`V6ResultRegistrationTests.cs`、`V6ResultSubmitTests.cs` | **新規テストは書かない** (本 Issue は検証 Issue) |
| 契約テスト (既存) | `Urls_FollowTheOpenApiContract` / `IsValidSongId_FollowsTheServerPattern` / `BuildScoreRequestJson_ContainsSubmissionFields` | **URL の書式と JSON の形だけ**を保証する。HTTP の結果は保証しない |
| 実機 (Windows) | Result → 登録 → ボード更新 / Esc スキップ / オフライン時 | **オーナー実施** |
| 2 クライアント | 登録 → 他クライアントで順位表示 | 2 台の環境が必要 |
| サーバー (読み取り) | `web/routes/api/v1/scores.ts` の `computeUnityScore` 読解 | 変更しない |
