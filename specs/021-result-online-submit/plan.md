---
description: "FlashBeat lean implementation plan — Issue 021"
---

# Plan: リザルトをオンライン送信しランキング表示 (Issue #21)

**Issue**: #21 | **Branch**: `w/spec-wingate` | **Spec**: [`spec.md`](spec.md) | **Date**: 2026-11-10

## Architecture

main の現状 (送信 / 取得 / 3 状態表示は実装済) を踏まえた、**残差「実機 E2E の 1 往復」** の流れ。

```
[Windows 実機 (展示 PC) — オーナー実施]         [本番 API (Cloudflare / D1)]
 FlashBeat.exe (引数なし)
   Title → Select → (cloud 由来の曲を選ぶ: remoteId 必須)
            → Game (1 曲完走)
              └─ SongEndStateMachine (#226) が全ノーツを確定 → Completed
            → Result (V6ResultView)
              ├─ 結果描画 (即時。通信に依らない)      ← T2 の観測点
              ├─ nickname 入力 (実行時入力、trim + 1..16 文字)
              │
              ├─[coroutine 1] V6ResultView.cs:865
              │    RankingClient.PostScore(remoteId, chartVersion, nickname,
              │                            runScore, runPerfect, runGreat,
              │                            runBad, runMiss, runMaxCombo)
              │      RankingClient.cs:144 BuildScoreRequestJson
              │      RankingClient.cs:243 PostScore → Send(ScoreUrl(), body, result)
              │        │
              │        └─ POST {origin}/api/v1/scores
              │             ├─ 201 → ParseScoreResponse → rank を表示
              │             ├─ 422 → 判定数不一致 / score 不一致 (V6ResultLogic.cs:316)
              │             ├─ 429 → 10 秒以内の重複 (Retry-After)
              │             └─ 通信失敗 → V6ResultLogic.cs:261 / :443
              │                          "オフラインのようです  再送 [Enter] / スキップ [Esc]"
              │
              └─[coroutine 2] V6ResultView.cs:1333 FetchRanking
                   RankingClient.cs:277 GetLeaderboard(remoteId, chartVersion)
                     └─ GET {origin}/api/v1/leaderboards/{songId}?version=N
                          └─ top 10 (server 側 leaderboards/[songId].ts:22,51)

  [サーバ側 — #7 の領域、既存]
    scores.ts:115          computeUnityScore(P, G, B, chart.noteCount)
    _score-model.ts:176    sum !== noteCount → 422
    _score-model.ts:182    maxCombo > noteCount → 422

                                        ↑ 同じ origin / 同じ songId + version
  [2 台目]  別マシンのブラウザ (または別 Windows)
    GET {origin}/api/v1/leaderboards/{songId}?version=N
      → 当該 nickname + score が top 10 に含まれる
    (または Web Studio の Ranking 画面が同じ内容を表示)
```

## 実装順序

本 Issue に **新規実装は不要**。順序は「前提 → 観測 → 記録」。

1. **前提の確認** (cc 側, macOS):
   - 本番 API の生存確認 (`GET /api/v1/catalog` / `GET /api/v1/leaderboards/{songId}?version=1`)。
   - **送信対象の cloud 曲** (remoteId 付き) があることの確認。**local 27 曲には remoteId が無く送信対象外**。
   - `RankingClient.Origin()` の base URL が `CloudCatalogConfig.json` と一致していることを確認。
2. **Windows 実機での 1 ラン** (オーナー実施, cc は操作しない):
   - `FlashBeat.exe` を **引数なし** で起動 → Select で cloud 曲 → Game → 完走 → Result。
   - nickname を入力 → 送信。**10 秒以内に同一 payload で再送しない** (429 を避ける)。
   - Player.log を回収 (`%USERPROFILE%\AppData\LocalLow\rebuildup\FlashBeat\Player.log`)。
   - 観測点: (a) 結果画面が**即時**に表示されたか、(b) rank が表示されたか、
   (c) 自分の行がランキングに含まれるか、(d) submitting / error / empty のどの状態が出たか。
3. **2 台目のクライアントからの観測** (オーナー実施):
   - 別マシンのブラウザで `GET {origin}/api/v1/leaderboards/{songId}?version=N` を実行し、
   nickname とスコアが含まれることを確認。
4. **回帰確認** (オーナー実施):
   - local 曲で 1 曲選び、Result で「オフライン」等のオンライン失敗表示が **出ない** こと
   (端末内保存のみ)。
5. **記録** (cc 側):
   - Player.log の該当行と 2 台目のレスポンスを `docs/evidence/` に取り込み、
   `docs/evidence/index.md` §1 の S4 行を更新 (**別 Worker / cc の統合 PR で行う**。

## ブロッカー

| # | ブロッカー | 影響 | 解除方法 |
| --- | --- | --- | --- |
| B1 | **Windows 実機 (展示 PC)** | 手順 2–4 が全てブロック | **Windows 実機は展示 PC のため cc は実行しない。オーナー実施。** `docs/operations/win64-candidate-acceptance-2026-11-10.md` §3 |
| B2 | **#17 (S2) が未証明** → cloud 曲が無い | 送信対象が存在しない | #17 の E2E が先。**local 曲では #21 を観測できない** |
| B3 | **#7 (サーバ側ゲート) が未証明** | 本番 API が未稼働の可能性 | `script/with-secrets` 経由の read-only 生存確認 → 必要なら cc がデプロイ |
| B4 | **2 台目のクライアント** | 手順 3 ができない | **2 台目の Windows は必須ではない**。別マシンのブラウザで GET すれば足りる |
| B5 | **S4 は optional** | 他のゲートを阻害しない | `docs/evidence/index.md` §1 S4 行「S3 を維持」の運用方針に従う |

## テスト戦略

| 層 | 手段 | 本 Issue で可否 |
| --- | --- | --- |
| Unity 単体 | `Assets/Tests/Editor/RankingClientTests.cs` (11 テスト) / `Assets/Game/Tests/V6ResultSubmitTests.cs` / `V6ResultRegistrationTests.cs` / `V6ResultLogicTests.cs` | **緑を実機の代替にしない**。「JSON が正しい」ことの証明に過ぎない |
| サーバ単体 | `web/tests/score-model.test.ts` / `web/tests/api.test.ts` | **緑を実機の代替にしない**。「サーバが 422 を返す」ことの証明に過ぎない |
| API 実測 | `GET /api/v1/leaderboards/{songId}?version=N` (read-only) | **可** (cc 側) |
| Unity 実機 | 完走 → POST → rank 表示 → top 10 取得 | **本 Issue の受入そのもの**。オーナー実施 |
| 2 台目 | 別マシンのブラウザ / 別クライアントで同じ top 10 | **本 Issue の受入そのもの**。オーナー実施 |

**「EditMode 緑 ≠ 実機合格」** (`docs/audit/open-issues-audit.md` §0) を厳守する。
