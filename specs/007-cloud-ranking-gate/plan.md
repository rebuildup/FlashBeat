---
description: "FlashBeat lean implementation plan — Issue 007"
---

# Plan: 展示用クラウドランキング ゲート (Issue #7)

**Issue**: #7 | **Branch**: `w/spec-wingate` | **Spec**: [`spec.md`](spec.md) | **Date**: 2026-11-10

## Architecture

本 Issue の実体は **コードではなく「実機での 1 往復の観測と記録」** である。
既存のコード構成 (変更しない) と、誰が何を観測するかを以下に示す。

```
[Windows 実機 (展示 PC) — オーナー実施]        [別クライアント — オーナー実施]
 FlashBeat.exe (Win64, 引数なし)
   Title → Select → (cloud 由来の曲)
            → Game (1 曲完走)
            → Result
              ├─ nickname 入力 (実行時入力)
              ├─ V6ResultView.cs:865  RankingClient.PostScore(remoteId, chartVersion, nickname, P/G/B/M, maxCombo)
              │     └─ POST {origin}/api/v1/scores
              │           ├─ 201 → 受理 + rank
              │           ├─ 422 → 判定数不一致 / score 不一致
              │           ├─ 429 → 10 秒以内の重複 (Retry-After)
              │           └─ 通信失敗 → V6ResultLogic.cs:261 の失敗状態
              └─ V6ResultView.cs:1333 RankingClient.GetLeaderboard(remoteId, chartVersion)
                    └─ GET {origin}/api/v1/leaderboards/{songId}?version=N
                          └─ top 10 (server 側 .limit(10))

  [サーバ (Cloudflare / D1)]
    scores.ts:115  computeUnityScore(P, G, B, noteCount)   ← サーバが再計算
    _score-model.ts:176  sum !== noteCount → 422
    leaderboards/[songId].ts:22,51  LEADERBOARD_SIZE = 10

                                        ↑ 同じ origin / 同じ songId+version
  [2 台目]  別マシンのブラウザ
    GET {origin}/api/v1/leaderboards/{songId}?version=N
      → 当該 nickname が top 10 に含まれること
      (または: 別クライアント = 2 台目の Windows / Web Studio の Ranking 画面)
```

## 実装順序

この Issue に **新規実装は不要**。順序は「観測 → 記録 → 判定」だけ。

1. **前提の確認** (cc 側, macOS):
   - 本番 API の生存確認 (`GET /api/v1/catalog` が 200 を返すか)。
   - 対象の cloud 曲と `chartVersion` を確認する (ローカル曲には `remoteId` が無く送信対象外)。
   - `RankingClient.Origin()` の base URL が `CloudCatalogConfig.json` と一致していることを確認。
2. **Windows 実機での 1 ラン** (オーナー実施, cc は操作しない):
   - 実机的表示に `FlashBeat.exe` を **引数なし** で起動 (擬似全画面は禁止)。
   - Select で cloud 由来の曲を選ぶ → Game → 完走 → Result → nickname 入力 → 送信。
   - Player.log を `%USERPROFILE%\AppData\LocalLow\rebuildup\FlashBeat\Player.log` から回収。
   - 429 / 422 を避けるため、**同じ nickname で 10 秒以内に再送しない**。
3. **2 台目のクライアントからの観測** (オーナー実施):
   - 別マシンのブラウザで `GET {origin}/api/v1/leaderboards/{songId}?version=N` を実行し、nickname が含まれることを確認。
4. **記録** (cc 側):
   - Player.log の `[MusicManager]` / ランキング関連行と 2 台目のレスポンスを `docs/evidence/` に取り込み、`docs/evidence/index.md` §1 の S4 行を更新する (**この編集は cc が別 PR で行う。本 spec の所有範囲外**)。

## ブロッカー

| # | ブロッカー | 影響 | 解除方法 |
| --- | --- | --- | --- |
| B1 | **Windows 実機 (展示 PC) の利用** | 受入条件 2/3/4/5/6/7 が全てブロックされる | **Windows 実機は展示 PC のため cc は実行しない。オーナー実施。** 手順は `docs/operations/win64-candidate-acceptance-2026-11-10.md` §3.3 / §3.4 を参照 |
| B2 | **本番 API (S2/S4) の稼働** | 送信先が無く POST が失敗する | Cloudflare の deploy 状況の確認 (cc は `script/with-secrets` 経由で read-only 確認可) |
| B3 | **catalog が cloud 由来である**こと | ローカル曲 (remoteId 無し) は送信対象にならない | #17 (S2) の完了。`V6ResultView.cs` は `song.remoteId == null` で送信をスキップする |
| B4 | **「casual / unverified」の文言** | 受入条件 5 が未充足のまま残る | オーナーの文言決定 → 文言追加は #21 のスコープ (`V6RankingLogic.SourceLabel`) |
| B5 | **S4 は optional** | #7 が解なくても展示は成立 | `docs/evidence/index.md` §1 S4 行「S3 を維持」の運用方針に従う |

## テスト戦略

本 Issue の受入は **E2E のみ**。既存テストは「送信ullaのJSON が正しい」ことしか保証しない。

| 層 | 手段 | 本 Issue で可否 |
| --- | --- | --- |
| サーバ単体 | `web/tests/score-model.test.ts` / `web/tests/api.test.ts` | 既存・**緑を実機の代替にしない** |
| Unity 単体 | `Assets/Tests/Editor/RankingClientTests.cs` (11 テスト: nickname 検証 / POST body / leaderboard parse / openapi 契約一致 / songId パターン / PlayerPrefs roundtrip) | 既存・**緑を実機の代替にしない** |
| Unity 実機 | Win64 で完走 → POST → 2 台目で GET | **本 Issue の受入そのもの**。オーナー実施 |
| ブラウザ | 2 台目から `GET /api/v1/leaderboards/{songId}?version=N` | **本 Issue の受入そのもの**。オーナー実施 |

**「EditMode 緑 ≠ 実機合格」** (`docs/audit/open-issues-audit.md` §0「未検証の扱い」) を厳守する。
