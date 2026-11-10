---
description: "FlashBeat lean implementation plan — Issue 022"
---

# Plan: 各ステップ Win64 展示版 smoke / パッチ配布ゲート (Issue #22)

**Issue**: #22 | **Branch**: `w/spec-wingate` | **Spec**: [`spec.md`](spec.md) | **Date**: 2026-11-10

## Architecture

本 Issue は **コードではなく「ゲートの運用と証跡」**。main に存在する定義と、
各ステージの未充足箇所を示す。

```
[macOS / cc — 準備と検証]
  script/win64-build-mac.sh <ref> <outdir>
    → <outdir>/FlashBeat-<ver>-win64-<sha>.zip
    → <outdir>/FlashBeat-<ver>-win64-<sha>.zip.sha256   ← 同一性の照合用
    → <outdir>/FlashBeat-<ver>-win64-<sha>.build-info.txt
  docs/operations/win64-candidate-acceptance-2026-11-10.md   (cc が作成済み)
    §1  SHA-256 照合 / §2 展開 / §3 実機 3 解像度 / §4 判定の注意 / §5 cc の次の一手

[Windows 実機 (展示 PC) — オーナー実施]
  FlashBeat.exe (引数なし・直接起動。ランチナー無し)
    ├─ 1 曲完走 (select → stream → notes → result → retry)
    ├─ Player.log  (%LOCALAPPDATA%..\LocalLow\rebuildup\FlashBeat\Player.log)
    ├─ 1280×720 smoke 10 項目  (festival-runbook.md §4)
    └─ スクリーンショット
        │
        │ release/check/redact-log.ps1  (YouTube URL / video id のマスク)
        ▼
[ゲート定義 (main に存在)]
  release/check/stage-manifest.json
    ├─ fingerprint.algorithm = SHA-256 over "<fileSha256>  <relative/path>"
    ├─ artifactRoots / exhibitDeployment (activeGeneration 3 / 3 世代)
    └─ stages[]: S0 / S0-FONT / S1 / S2 / S3 / S4
          ├─ status: proven-partial / unproven
          ├─ proven[] / unproven[]
          └─ artifact path + fingerprint
  release/check/verify-stage.ps1   fingerprint と evidence hash の再計算 + index.md 言及検査
  release/check/drive-gameplay.ps1  ゲームプレイ smoke ドライバ (run-summary.json 出力)
  release/check/redact-log.ps1      ログのマスク / -Check
        │
        ▼
[承認記録]
  docs/operations/release-approval-checklist.md
    §A 前提と権限の境界 (本番 main と Cloudflare は独立承認 / #22 は merge しない)
    §B 成果物チェック / §C プレイ証拠 / §D 1280×720 UX スモーク
    §E 秘密情報チェック / §F 承認記録 / §G 承認後 / §H 禁止事項
    「1 項目でも確認できていないものは『未証明』と書き、green を作りません」
        │
        ▼
[ステージ表 (唯一の情報源)]
  docs/evidence/index.md §1
    S0 proven(partial) / S0-FONT proven(partial) / S1 未証明 / S2 未証明 / S3 未証明 / S4 未証明
```

## 実装順序

本 Issue に **新規コードは不要**。順序は「候補準備 → 実機 smoke → 記録 → 承認」。

1. **候補ビルドの準備** (cc 側, macOS):
   - `FLASHBEAT_VERSION_BASE=0.1.22 bash script/win64-build-mac.sh origin/main <outdir>` を実行し、
   ZIP / `.zip.sha256` / `.build-info.txt` を作る。
   - **注意**: 現行 Release は `v0.1.21-win64-d581c4c`。自動更新はバージョンの大小で判定するため、
   **同じ版 (0.1.21) で別 SHA の Release を作っても更新が効かない**。次の配布は **0.1.22** にする。
   - **これは「Windows で動く」ことの証明ではない** (`win64-candidate-acceptance-2026-11-10.md` §1)。
2. **SHA-256 の照合** (オーナー側): 展開前に `Get-FileHash -Algorithm SHA256` で
   `.zip.sha256` と一致することを確認する。
3. **実機 smoke** (オーナー実施, Windows):
   - `FlashBeat.exe` を **引数なし** で起動 → **実ディスプレイ全画面** で撮影。
   - **擬似全画面 (リサイズ) は禁止** (`docs/operations/exhibit-handoff-2026-11-08.md` §1)。
   - 1 曲完走 → Player.log 回収 → 1280×720 smoke 10 項目を記入。
   - `release/check/drive-gameplay.ps1` を **windowed 1280×720** で実行し `run-summary.json` を得る。
4. **ログのマスキング** (cc 側): `release/check/redact-log.ps1` で署名 URL / video id をマスクし、
   `-Check` でマスク漏れが無いことを確認してからコミット対象にする。
5. **ステージ表の更新** (cc 側, 統合 PR):
   - 観測できた項目だけ `proven` へ、残りは `unproven` のまま残す。
   - **未観測を green にしない** (`docs/evidence/index.md` §0 の原則)。
6. **承認記録の記入** (cc 側 / オーナー): `release-approval-checklist.md` §C / §D を記入し、
   §F の承認記録表に「**未証明**」か「承認」のどちらかを **根拠つきで** 書く。
7. **各ステージの S1–S4 の証拠を待つ** (他 Issue): #13 / #17 / #6 / #7, #21 の実機証拠が
   揃った時点で、それぞれ §1 のステージ表を更新する。
8. **cc が merge を承認** (cc 側): `release-approval-checklist.md` §A に従い、**#22 自体は merge しない**。

## ブロッカー

| # | ブロッカー | 影響 | 解除方法 |
| --- | --- | --- | --- |
| B1 | **Windows 実機 (展示 PC)** | T1 / T2 / Acceptance 3 の全て | **Windows 実機は展示 PC のため cc は実行しない。オーナー実施。** 手順は `docs/operations/win64-candidate-acceptance-2026-11-10.md` §3 |
| B2 | **S1 (#13) が未証明** | S1 のステージ証拠が上げられない | #13 の実機証明 (ストリーム音声) が先 |
| B3 | **S2 (#17) が未証明** | S2 のステージ証拠が上げられない | #17 の E2E (PUT → 再ビルドなし → 再生) が先 |
| B4 | **S3 (#6) が未証明** | S3 のステージ証拠が上げられない | #6 (Web Studio → Unity 公開谱再生) が先 |
| B5 | **S4 (#7, #21) が未証明** | S4 のステージ証拠が上げられない | **optional**。未証明でも S3 を維持すれば展示は成立 |
| B6 | **Win64 CI のライセンス未設定** | CI による自動ビルドが赤 | `UNITY_EMAIL` / `UNITY_PASSWORD` を Actions Secrets に登録 (手順 `docs/operations/unity-ci-license.md`)。**cc はSecrets を触らない** |
| B7 | **2K モニタ (2560×1440) の確保** | 2K の実機撮影ができない | 確保できない場合は **「未証明」と記録** (`three-resolution-fullscreen-verification.md` §0) |
| B8 | **1280×720 の実機パネルが無い** | runbook §4 項目 10 が「実機 fullscreen 未検証」のまま | ウィンドウモードは代替にならない。**未検証として記録** |

## テスト戦略

本 Issue の「テスト」は **人手による実機 smoke** である。

| 層 | 手段 | 本 Issue で可否 |
| --- | --- | --- |
| 機械検証 | `release/check/verify-stage.ps1` (fingerprint と evidence hash の再計算) / `redact-log.ps1 -Check` | **可** (cc 側)。**artifact が一致ことの証明であって、動作の証明ではない** |
| ゲームプレイ smoke | `release/check/drive-gameplay.ps1` → `run-summary.json` | **可** (オーナー / Windows) |
|  checklists | `docs/operations/festival-runbook.md` §4 の 10 項目 / `release-approval-checklist.md` §C / §D | **未記入**。記入が「ゲート通過」の根拠 |
| 実機目視 | 1 曲完走 + スクリーンショット | **本 Issue の受入そのもの**。オーナー実施 |

**「チェックリストが存在すること」「テストが緑であること」はゲート通過の根拠にならない**
(`docs/audit/open-issues-audit.md` §0「未検証の扱い」)。
