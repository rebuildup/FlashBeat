---
description: "FlashBeat lean implementation plan — Issue 017"
---

# Plan: 固定楽曲配列 → Cloud Catalog 読込 (Issue #17)

**Issue**: #17 | **Branch**: `w/spec-wingate` | **Spec**: [`spec.md`](spec.md) | **Date**: 2026-11-10

## Architecture

main の現状 (catalog / chart の取得経路は実装済) を踏まえた、**残差「実 API の 1 往復 E2E」** の流れ。

```
[Web Studio (別マシン / ブラウザ) — publish 担当]   [本番 API (Cloudflare / D1)]
  song-publisher.tsx:290  token = operator の runtime 入力
  song-publisher.tsx:400  token を publish へ渡す
  publish-dialog.tsx:117 "Staff token" 入力欄
        │
        │ PUT /api/v1/admin/songs/{songId}   (bearer token)
        ▼
  song-publisher.tsx:29  → 公開 (chartVersion を明示)
        │
        │ GET /api/v1/catalog
        ▼
  ┌──────────────────────────── 本番 API ────────────────────────────┐
  │ catalog: songId / title / artist / videoId / chartVersion / ...   │
  │ charts : GET /api/v1/charts/{songId}?version={chartVersion}      │
  │ scores : GET /api/v1/leaderboards/{songId}?version=N (S4 用)      │
  └──────────────────────────────────────────────────────────────────┘
        ▲ GET /api/v1/catalog
        │                     ▲ GET /api/v1/charts/{songId}?version=N
        │                     │
[Windows 実機 (展示 PC) — オーナー実施]
  FlashBeat.exe (引数なし)
    Title → Select
      SelectSceneManager.cs:67   StartCatalogRefresh()      (入場時)
      SelectSceneManager.cs:361  RefreshCatalogLoop(config) (8 秒周期)
      SelectSceneManager.cs:374  RefreshCatalogOnce(config)
      SelectSceneManager.cs:378  CloudCatalog.GetText(CloudCatalog.CatalogUrl(baseUrl))
      SelectSceneManager.cs:380  status==200 ? ParseCatalog(body) : null
      SelectSceneManager.cs:404  v6Data.RefreshAsync()      (v6 選曲画面へ反映)
        │
        ├─ GManager.cs:90  ApplyRemoteCatalog(remoteSongs)
        │     GManager.cs:132 Merge(Songs, remoteSongs)
        │       ├─ 同じ remoteId   → 行を更新 (Overlay)
        │       ├─ 内蔵曲と対応     → 内蔵行を更新 (Overlay)
        │       └─ 未知の cloud 曲  → 末尾に追加 (Append)
        │     GManager.cs:88  ActiveSongs  (並列配列の固定値ではなく merge 結果)
        │
        ├─ Ready → SelectSceneManager.cs:442  chart を exact version で取得
        │     CloudCatalog.cs:94  ChartUrl(baseUrl, songId, chartVersion)
        │     SelectSceneManager.cs:444  status==200 ? ParseChartResponse(body) : null
        │     CloudChartHolder.cs:16 Hold(id, version, json)  → 1 ランは同一版で完走
        │
        ├─ Game: MusicManager.cs:202/:207  YPlayer[songID] を 1 回だけ初期化 (曲ごとに object を作らない)
        │       NotesManager が CloudChartHolder の谱面でノーツを生成
        │
        └─ Result → Retry
              Player.log に [GManager] cloud catalog applied: local=… merged=… remote=… 行
```

## 実装順序

本 Issue に **新規実装は不要**。順序は「準備 → 観測 → 記録」。

1. **公開 API の生存確認** (cc 側, macOS, read-only):
   - `GET {base}/api/v1/catalog` が 200 を返し、空でない数组を返すこと。
   - `GET {base}/api/v1/charts/{songId}?version=N` が 200 を返すこと (catalog の 1 行で検証)。
   - `CloudCatalogConfig.json` の `baseUrl` が実際のデプロイ先と一致していることを確認。
   - **存在しなければ S2 の前提 (#15 / #16) が未達**。cc はデプロイを行わない (read-only のみ)。
2. **新曲の publish** (Web Studio 担当 / オーナー):
   - `web/pages/publish.tsx` の既存フローで **staff token を runtime 入力** して `PUT /api/v1/admin/songs/{songId}`。
   - **chartVersion を明示** する。**token はブラウザのタブ内メモリにのみ置き、コミットにも Web バンドルにも入れない** (`web/env.ts:15` `FLASHBEAT_ADMIN_TOKEN` は Worker runtime secret のみ)。
3. **再ビルドなしで Unity 側の catalog 反映を観測** (オーナー / Windows 実機):
   - **再ビルドしない** 既存 Win64 成果物で `FlashBeat.exe` を **引数なし** 起動 → Select。
   - `Player.log` の `[GManager] cloud catalog applied: local=… merged=… remote=…` 行を採取。
   - 新曲が選曲画面に出現し、タイトル / アーティスト / 版が正しいことを確認。
4. **選曲 → ストリーム → ノーツ/判定 → result** (オーナー / Windows 実機):
   - 公開した新曲を選んで 1 ラン完走。判定数が公開した譜面のノーツ数と一致すること。
   - 実行した chartVersion が意図した版であること (`Player.log` の谱面取得行)。
5. **障害系の確認** (オーナー / Windows 実機):
   - API 遮断 / 404 / 空応答のいずれかで、**Unity がクラッシュせず** 選曲画面が操作可能なまま残ること。
6. **回帰確認**: 既存 27 曲 (local) が引き続き選曲・再生できること。
7. **記録** (cc 側): 証跡を `docs/evidence/` に取り込み、`docs/evidence/index.md` §1 の S2 行と
   `release/check/stage-manifest.json` の S2 エントリを更新 (**別 Worker / cc の統合 PR で行う。本 spec の所有範囲外**)。

## ブロッカー

| # | ブロッカー | 影響 | 解除方法 |
| --- | --- | --- | --- |
| B1 | **Windows 実機 (展示 PC)** | 手順 3–6 が全てブロック | **Windows 実機は展示 PC のため cc は実行しない。オーナー実施。** `docs/operations/win64-candidate-acceptance-2026-11-10.md` §3 |
| B2 | **本番 API のデプロイ** (#15 / #16) | 手順 1 が失敗し publish 先が無い | `docs/evidence/index.md` §1 S2 行が "no deployed API" と明記。**cc は read-only 確認のみ。deploy は cc が行う** (Infisical / Cloudflare の手順は `docs/operations/cloudflare-bootstrap.md`) |
| B3 | **staff token の入手と runtime 入力** | 手順 2 ができない | オーナーが token を持ちブラウザで入力。**トークンを Issue / PR / ログに残さない** |
| B4 | **#13 (S1) が未証明** | 手順 4 の「ストリームが再生される」部分が成立しない | #13 の実機証明が先 |
| B5 | **新規 songId の scene 配線** | 曲ごとに `YoutubePlayer` GameObject が必要な可能性 | `Assets/Editor/MakeFileSceneManager.cs` の `YouTubeSceneWiring` が自動配線する (手動編集不要)。**ただし新規追加が既存 `YPlayer[]` 配列の拡張を要するかは要確認** |

## テスト戦略

| 層 | 手段 | 本 Issue で可否 |
| --- | --- | --- |
| Unity 単体 | `Assets/Tests/Editor/CloudCatalogTests.cs` / `CatalogMergeTests.cs` / `SongCatalogRobustnessTests.cs` / `ScoreCatalogIntegrityTests.cs` (既存) | **緑を実機の代替にしない**。「remote catalog を読める」ことの証明に過ぎない |
| API 実測 | `GET /api/v1/catalog` / `GET /api/v1/charts/{id}?version=N` | **可** (read-only)。実 API の生存確認 |
| 公開 | `PUT /api/v1/admin/songs/{songId}` | 対象。**cc は token を持たない / 持たないべき**。オーナー実施 |
| Unity 実機 | 再ビルドなしで新曲が出現 → 選曲 → 再生 → result | **本 Issue の受入そのもの**。オーナー実施 |

**「EditMode 緑 ≠ 実機確認」** (`docs/audit/open-issues-audit.md` §0) を厳守する。
