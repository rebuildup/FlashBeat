---
description: "FlashBeat lean spec — Issue 022"
---

# Spec: 各ステップ Win64 展示版 smoke / パッチ配布ゲート

**Issue**: #22 | **Branch**: `w/spec-wingate` | **Date**: 2026-11-10 | **Status**: Draft

**Input**: `[Release/Win] 各ステップ Win64 展示版 smoke / パッチ配布ゲート`

## Objective

Issue #22 は「**各ステップ (S0–S3, 任意で S4) ごとに、実 Win64 play の証拠と正確な SHA を要求し、
証明済み artifact と展示 runbook を維持する**」というリリースゲートである。

`main` には **ゲート定義**（`release/check/stage-manifest.json`、`verify-stage.ps1`、
`drive-gameplay.ps1`、`redact-log.ps1`、`docs/operations/release-approval-checklist.md`、
`docs/operations/festival-runbook.md`）が既に存在する。
本 spec の目的は、**残った未充足条件** —

1. **S1 / S2 / S3 / S4 の実 Win64 play 証拠**（S0 のみ proven-partial）、
2. **1280×720 の高速 UX smoke チェックリストが未記入**、
3. **S1–S3 の「直接起動可能な `FlashBeat.exe`」の文書化** —

を 1 つの作業単位に固定し、**「ゲート定義があるから通った，不代表」を明確にすること**にある。

Issue #22 は **PR readiness を確認するのみで merge しない**（`release-approval-checklist.md` §A）。
統合 (merge) は cc 側の責務であり、本 spec の範囲外。

## Decisions (凍結する設計)

| 項目 | 決定 |
| --- | --- |
| 起動方法 | **ユーザーは `FlashBeat.exe` を直接起動する。** ランチャー / `cmd` ラッパー / 起動時自動更新 / ロールバックコマンドは **存在しない** (Issue 本文 "No launcher, no user-facing rollback mechanism")。`docs/operations/festival-runbook.md` 冒頭の記載と一致 |
| パッチの運用 | 展示中に強制更新しない。**ゲームを閉じて 1 回適用 (#36) してから `FlashBeat.exe` を直接起動**。壊れた更新は **別パッチを出版** して直す。**ロールバック経路は足さない** (Issue 本文 T4) |
| 承認の独立性 | 本番 main と Cloudflare は **独立に承認** する。一方の承認を他方の承認に読み替えない (`release-approval-checklist.md` §A) |
| 証拠の区別 | **source チェックと実ゲーム証跡を区別する** (Issue 本文 T2)。チェックリストが存在すること、テストが緑であることは **ゲート通過の根拠にならない**。チェックボックス未記入は「未証明」 |
| 未検証パッチの扱い | **未検証の後続パッチが現在の展示ビルドを置き換えない** (Issue Acceptance)。展示フォルダは読み取り参照 |
| 1280×720 smoke | `docs/operations/festival-runbook.md` §4 の 10 項目。**source チェックと実ゲーム証跡を区別** して記入する。代替手順として `FlashBeat.exe -screen-width 1280 -screen-height 720 -screen-fullscreen 0` が runbook §4 に提示されているが、**実机的 fullscreen とは別物** |
| 候補ビルドの同一性 | macOS で作られた候補 ZIP は `.zip.sha256` sidecar で照合する。**sha256 一致は「Windows で動く」ことの証明ではない**。実機での確認 (手順書 `docs/operations/win64-candidate-acceptance-2026-11-10.md`) が未実施のあいだ **未検証** |
| 証跡の秘密情報 | コミットする Player.log は `release/check/redact-log.ps1` でマスクする (YouTube の署名 URL / video id)。**素のログをコミントしない** |
| 実機操作 | **Windows 実機は展示 PC のため cc は実行しない。オーナー実施。** macOS の完走は Win64 の代用にならない (`docs/evidence/index.md` §1.6) |
| 実装の境界 | 本 spec はドキュメントのみ。**ゲート定義は既に `main` に存在する**。本 spec が追加するのは「どの条件が未充足か」「誰が観測するか」の固定のみ |

## 非目標 (Out of scope)

- ゲーム本体 / UI / 再生経路の改修 (#96 / #13 / #233 の責務)。
- ランチャー・自動更新・ロールバックの実装 (**作らない**: Issue 本文が明示的に禁止)。
- GitHub Release の publish (#35 の責務) と patch ツール (#36 の責務)。**#22 は承認のみ**。
- ステージ表の **S0–S4 の判定そのものの変更** (evidence による更新は cc 側の統合 PR)。
- **`docs/evidence/**` と `release/check/**` の編集** (他 Worker が所有。本 spec の所有範囲は `specs/022-*/` のみ)。

## Acceptance criteria

1. **S0** は既に proven-partial のまま (譜面/結果/再試行は proven、YouTube ストリームと音声は未証明)。
2. **S1** (yt-dlp + Deno → VideoPlayer) に、**実 Win64 play の証拠** と正確な SHA が残る。
   選択 → YouTube ストリーム → notes/result → retry の 4 点が観測できること。
3. **S2** (remote catalog / chart API) に、**実 Win64 play の証拠** と SHA が残り、
   参照先 (CF URL / songId / chartVersion) が記録に残ること。
4. **S3** (chart editor + publisher → Win64 が公開谱を再生) に、**実 Win64 play の証拠** と SHA が残る。
5. **S4** (任意) に、**実 Win64 play の証拠** が残る (**未証明でも S3 を維持**するため問題ない)。
6. 各ステージの **1280×720 高速 UX smoke の 10 項目** (日本語グリフ / 入力マッピング / 音画同期 /
   ネットワークエラー状態) が記入され、**source チェックと実ゲーム証跡が区別**されていること。
7. 各ゲートに **文書化された直接起動可能な `FlashBeat.exe`** が存在する。
8. **未検証の後続パッチが現在の展示ビルドを置き換えていない** こと (展示フォルダは読み取り参照)。
9. **ランチャーもユーザー向けロールバックも存在しない** こと (現状のまま)。
10. 承認済み PR のみが release に merge され、**本番 main と Cloudflare が独立に承認**されていること
    (運用手順。cc が実施)。
11. backend API v1 が既存バイナリとの互換を保っていること (旧 Unity build が v1 API を叩ける)。

## 現在の実装状況 (2026-11-10 時点)

`origin/main` = `405555c` を実測した結果。

### 充足 (ゲート定義 / 運用 — コードまたは文書として存在する)

| Issue タスク | 根拠 (file:line) | 判定 |
| --- | --- | --- |
| T3: 証明済み Win64 artifact を保存し短い展示 runbook を維持 | `release/check/stage-manifest.json` の `exhibitDeployment` (activeGeneration 3、3 世代の path / content / sourceBuild / buildResult) / `docs/operations/festival-runbook.md` / `docs/operations/exhibit-runbook.md` / `docs/evidence/index.md` §1.5 (3 世代表) | 充足 |
| T4: パッチはテスト済みの新リリースを承認し、ゲームを閉じて 1 回適用 (#36) してから `FlashBeat.exe` を直接起動。展示中に強制更新しない、ロールバック経路を足さない、壊れた更新は別パッチで出版 | `docs/operations/festival-runbook.md` 冒頭 (「起動は `FlashBeat.exe` の直接起動だけ」「ランチャー / `Play.cmd` / `Update.cmd` / 起動時自動更新 / ロールバックコマンドは **存在しません**」) / `docs/operations/update-patch.md` (「`OnApplicationQuit` からの自動適用は存在しません」— issue #118 で廃止) | 充足 (運用) |
| T5: 承認済み PR のみを release に merge、本番 main と Cloudflare を独立に承認、backend API v1 互換 | `docs/operations/release-approval-checklist.md` §A (「片方の承認を他方の承認に読み替えない」) / §H (禁止事項) | 充足 (運用) |
| Acceptance: 未検証の後続パッチが現在の展示ビルドを置き換えない | `docs/operations/festival-runbook.md`「展示中は **読み取り参照**」/ `docs/operations/update-patch.md`「1 世代だけ残します」 | 充足 (運用) |
| Acceptance: ランチャー無し、ユーザー向けロールバック無し | `docs/operations/festival-runbook.md` 冒頭 / `docs/operations/update-patch.md` | 充足 |
| ステージ表と fingerprint の機械可読な双子 | `release/check/stage-manifest.json` (`fingerprint.algorithm` = SHA-256 over `<fileSha256>  <relative/path>` / `artifactRoots` / `exhibitDeployment`) / `release/check/verify-stage.ps1` (fingerprint と evidence hash の再計算 + `docs/evidence/index.md` が全 fingerprint に言及しているか検査) / `release/check/drive-gameplay.ps1` (ゲームプレイ smoke ドライバ) / `release/check/redact-log.ps1` (ログのマスク) | 充足 (定義) |

### 未充足 / 未検証

| # | 条件 | 判定 | 根拠 |
| --- | --- | --- | --- |
| 1 | T1: S0–S3 (S4 任意) ごとに**実 Win64 play 証跡と正確な SHA** — select → YouTube ストリーム → notes/result → retry、ログ/スクリーンショット + CF URL/chartVersion | **未充足** | `docs/evidence/index.md` §1 のステージ表: **S0 = proven (partial)**, **S0-FONT = proven (partial)**, **S1 / S2 / S3 / S4 = 未証明**。`release/check/stage-manifest.json` の S1 / S2 / S3 / S4 エントリもいずれも `status: "unproven"`、`proven: []`、`unproven: [...]` |
| 2 | T2: 1280×720 での高速 UX smoke (日本語グリフ、入力マッピング、音画同期、ネットワークエラー状態)、**source チェックと実ゲーム証跡を区別** | **未充足 (チェックリスト未実施)** | `docs/operations/festival-runbook.md` §4 の 10 項目は **チェックボックス未記入**。`:99` 自身が「実機 1280×720 パネルでの fullscreen は **未検証**」と記載。`:189` はウィンドウモード (`-screen-width 1280 -screen-height 720 -screen-fullscreen 0`) を代替手順として提示 (**実機の fullscreen とは別物**) |
| 3 | Acceptance: 各ゲートに**文書化された直接起動可能な `FlashBeat.exe`** が存在 | **部分充足** | `docs/evidence/index.md` §1.5 に第 3 世代の直接起動構成 (`FlashBeatExhibit_s2\`) が実在し、`docs/operations/release-approval-checklist.md` の承認記録に `2026-11-07` の「実機で完走」行がある (映像あり / 音あり / ただし `audioMode=Bundled`)。**S1–S3 の段階ごとの直接起動可能ビルドは未証明** |
| 4 | 承認記録のチェックボックス | **未充足** | `docs/operations/release-approval-checklist.md` §C / §D の実機項目が未記入。§F の承認記録表にも「**未証明**」の行が残る (S0-FONT / S1 / S2 / S3 / S4 がすべて「未承認」)。**文書自身の原則「1 項目でも確認できていないものは『未証明』と書き、green を作りません」に従い、green を作らない** |
| 5 | 候補 Win64 build の macOS 側成果物 | **未検証** | `docs/operations/win64-candidate-acceptance-2026-11-10.md` §0 は「**未検証**: Windows 実機での実行・全画面・1 曲完走・ストリーム音声**」と明記。`script/win64-build-mac.sh` の出力 (ZIP + `.zip.sha256` + `.build-info.txt`) は cc 側で作成可能だが、**Windows 実機での動作は未証明** |
| 6 | backend API v1 と既存バイナリの互換 | **未検証** | Issue T5 の要求。`contracts/openapi.v1.yaml` は存在し、`RankingClientTests.cs:142` `Urls_FollowTheOpenApiContract` が URL の一致を固定しているが、**旧 Unity build が v1 API を実呼びして通ることの記録は無い** |
| 7 | Win64 CI | **未充足 (外部要因)** | `docs/evidence/index.md` §1.6 は直近の `build-win64` 失敗が **ライセンス未設定** (`UNITY_EMAIL` / `UNITY_PASSWORD` が Actions Secrets に未登録) であり、**ソースのビルド失敗ではない** と明記。`docs/operations/unity-ci-license.md` に切り分け手順 |

### ブロッカー

- **Windows 実機 (展示 PC)** が全ての核心条件 (T1 / T2 / Acceptance 3) の前提。
- **S1 (#13) / S2 (#17) / S3 (#6) / S4 (#7, #21) の実装側ゲート**が未証明 — **#22 は証拠を待つ側**であり、実装を代行しない。
- **Win64 CI のライセンス** (`UNITY_EMAIL` / `UNITY_PASSWORD` の Secrets 登録) が無いと CI による自動ビルドが赤のまま。
- **2K モニタ (2560×1440) の確保** — `docs/operations/three-resolution-fullscreen-verification.md` §0 が「2K モニタが確保できない場合は **『未証明』と記録**」と指示。
- **cc は merge しない** — `release-approval-checklist.md` §A により #22 は PR readiness の確認のみ。

### 参考

- `docs/audit/open-issues-audit.md` §3.4 の #22 節 (充足 4 / 未充足 4 / 未検証 2) が本 spec の根拠。
- `docs/evidence/index.md` §1 のステージ表が **S0–S4 の合否の唯一の情報源**。
- macOS 側の候補ビルドと手順書は `docs/operations/win64-candidate-acceptance-2026-11-10.md` に cc が作成済み (本 spec の前提)。
