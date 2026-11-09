---
description: "FlashBeat lean tasks — Issue 022"
---

# Tasks: 各ステップ Win64 展示版 smoke / パッチ配布ゲート (Issue #22)

**Issue**: #22 | **Branch**: `w/spec-wingate` | **Spec**: [`spec.md`](spec.md) / [`plan.md`](plan.md)

## Tasks

- [x] **T1** ステージ表・fingerprint・検証スクリプト・承認チェックリスト・展示 runbook が
  `main` に存在することを確認し、ゲート定義として充足と判定する。
  — **done when**: 各定義の file:line が記録されていること
  — **実測**: `release/check/stage-manifest.json` (`fingerprint.algorithm` = SHA-256 over
  `<fileSha256>  <relative/path>` / `artifactRoots` / `exhibitDeployment` の activeGeneration 3) /
  `release/check/verify-stage.ps1` (fingerprint と evidence hash の再計算 + `docs/evidence/index.md`
  の言及検査) / `release/check/drive-gameplay.ps1` / `release/check/redact-log.ps1` /
  `release/check/README.md` / `docs/operations/release-approval-checklist.md` (§A–§H) /
  `docs/operations/festival-runbook.md` (§1 展示フォルダ / §2 起動方法 / §4 smoke 10 項目 /
  §6 異常時の復旧 / §8 成果物置き場 / §9 未検証の Windows 作業) /
  `docs/operations/exhibit-runbook.md` / `docs/operations/update-patch.md` /
  `docs/operations/release-gate.md` / `docs/operations/unity-ci-license.md`。

- [x] **T2** パッチ運用 (ゲームを閉じて 1 回適用 → `FlashBeat.exe` を直接起動。展示中の強制更新なし。
  ロールバック経路なし。壊れた更新は別パッチで出版) と、
  承認の独立性 (本番 main と Cloudflare を独立に承認) が運用として定義されていることを確認する。
  — **done when**: 定義の出典 file:line が記録されていること
  — **実測**: `docs/operations/festival-runbook.md:37` (§2 起動方法 = `FlashBeat.exe` 直接実行) /
  冒頭 (「ランチャー / `Play.cmd` / `Update.cmd` / 起動時自動更新 / ロールバックコマンドは
  **存在しません**」) / `:101` (§5 パッチを適用する手順) /
  `docs/operations/update-patch.md` (「`OnApplicationQuit` からの自動適用は存在しません」— #118 で廃止) /
  `docs/operations/release-approval-checklist.md` §A (「片方の承認を他方の承認に読み替えない」) /
  §H (禁止事項)。

- [x] **T3** 証明済み Win64 artifact の保存 (3 世代) と、runbook による直起動構成の文書化を
  確認する。
  — **done when**: 3 世代の成果物と runbook の記載が確認できること
  — **実測**: `release/check/stage-manifest.json` の `exhibitDeployment` (activeGeneration 3、3 世代の
  path / content / sourceBuild / buildResult) / `docs/evidence/index.md` §1.5 の世代表 /
  `docs/operations/festival-runbook.md:12` (§1 展示フォルダの正確なパス) /
  `docs/operations/exhibit-handoff-2026-11-08.md`。

- [ ] **T4** S1 / S2 / S3 / S4 それぞれの**実 Win64 play 証拠**と正確な SHA を取得する
  (select → YouTube ストリーム → notes/result → retry、ログ/スクリーンショット + CF URL/chartVersion)。
  — **done when**: 各ステージの `proven` / `unproven` が観測結果と一致し、
  根拠となるファイルが `docs/evidence/` に残っていること
  — **備考**: 現状 `docs/evidence/index.md` §1 は **S0 = proven (partial)** /
  **S0-FONT = proven (partial)**、**S1 / S2 / S3 / S4 = 未証明**。
  `release/check/stage-manifest.json` の S1–S4 エントリも `status: "unproven"`。
  **各ステージの実装側ゲート (#13 / #17 / #6 / #7 / #21) が先に通る必要がある**。
  **Windows 実機は展示 PC のため cc は実行しない。オーナー実施。**

- [ ] **T5** 1280×720 の高速 UX smoke 10 項目を記入する
  (日本語グリフ / 入力マッピング / 音画同期 / ネットワークエラー状態)。
  — **done when**: `docs/operations/festival-runbook.md` §4 の 10 項目の判定欄が記入され、
  **source チェックと実ゲーム証跡が区別**されていること
  — **備考**: §4 の判定欄は現状 **未記入** (全 10 項目 ☐)。
  `:99` (項目 10) は「実機 1280×720 パネルでの fullscreen は **未検証**」と記載。
  `:189` (§9 項目 2) のウィンドウモード (`-screen-width 1280 -screen-height 720 -screen-fullscreen 0`)
  は **実機 fullscreen の代替にならない**。
  **Windows 実機は展示 PC のため cc は実行しない。オーナー実施。**

- [ ] **T6** 各ゲートに**文書化された直接起動可能な `FlashBeat.exe`** が存在することを確認する。
  — **done when**: 各ステージの成果物のパスと SHA が記録され、直接起動の指示が runbook にあること
  — **備考**: 第 3 世代 (`FlashBeatExhibit_s2\`) は実在し、`release-approval-checklist.md` §F に
  「2026-11-07 / 実機で完走 / Title → Select → まにまに → 判定 76 → Result → `R` リトライ /
  映像あり / 音あり (ただし `audioMode=Bundled`、**ストリーム音声は未証明**)」の記録がある。
  **S1–S3 の段階ごとの直接起動可能ビルドは未証明**。
  **読み取り確認のみ。展示フォルダは読み取り参照。**

- [ ] **T7** macOS 側の候補 Win64 build を作成し、SHA-256 sidecar と build-info を添付する。
  — **done when**: ZIP / `.zip.sha256` / `.build-info.txt` が揃い、sha256 が一致することが確認できること
  — **備考**: `FLASHBEAT_VERSION_BASE=0.1.22 bash script/win64-build-mac.sh origin/main <outdir>`。
  現行 Release は `v0.1.21-win64-d581c4c` で、**同じ版だと更新が効かない**ため **0.1.22** とする。
  **sha256 一致は「Windows で動く」ことの証明ではない**。
  手順は `docs/operations/win64-candidate-acceptance-2026-11-10.md` §1 / §2。

- [ ] **T8** 2K モニタ (2560×1440) の実機撮影を取得する (or 「未証明」と記録する)。
  — **done when**: 2560×1440 の実全画面スクリーンショットが残るか、
  確保できなかった旨が「未証明」として記録されていること
  — **備考**: `docs/operations/three-resolution-fullscreen-verification.md` §0 の指示
  「2K モニタが確保できない場合は **『未証明』と記録**」に従う。
  **Web のビューポート確認は代替にならない。**
  **Windows 実機は展示 PC のため cc は実行しない。オーナー実施。**

- [ ] **T9** コミットする Player.log を `release/check/redact-log.ps1` でマスクし、
  `-Check` でマスク漏れが無いことを確認する。
  — **done when**: マスク済みログのみがコミト対象になり、署名 URL / video id が残っていないこと
  — **備考**: `release/check/README.md` の典型手順。
  `docs/evidence/index.md` の注意書き (コミット済みログはマスク済み) との整合を取る。

- [ ] **T10** backend API v1 と既存バイナリの互換を確認する (旧 Unity build が v1 API を実呼びできる)。
  — **done when**: 旧 build からの API 呼び出しが成功した記録 (または v1 の互換性設計の記録) が残ること
  — **備考**: `contracts/openapi.v1.yaml` が存在し、
  `Assets/Tests/Editor/RankingClientTests.cs:142` `Urls_FollowTheOpenApiContract` が
  URL の一致を固定しているが、**旧 build の実呼び出しの記録は無い**。

- [ ] **T11** Win64 CI のライセンスをSecrets に登録する (CI を赤から戻す)。
  — **done when**: `build-win64` ジョブが `license-precheck` で停止しなくなること
  — **備考**: 手順は `docs/operations/unity-ci-license.md`。
  **cc は Actions Secrets を触らない** (Infisical 経由の注入は `script/with-secrets`)。
  **これは「ソースのビルド失敗」ではなく「ライセンス未設定」による停止**である点に注意。

- [ ] **T12** 承認記録 (`release-approval-checklist.md` §C / §D / §F) を記入する。
  — **done when**: §C / §D の実機項目が記入され、§F の承認記録表に
  「**未証明**」または「承認」のどちらかが **根拠つきで** 記載されていること
  — **備考**: 文書自身の原則「1 項目でも確認できていないものは『未証明』と書き、
  green を作りません」に従う。§F の S0-FONT / S1 / S2 / S3 / S4 は現在すべて「未承認」。
  **チェックリストが存在することやテストが緑であることはゲート通過の根拠にならない。**

- [ ] **T13** 展示用フォルダが読み取り参照され、**未検証の後続パッチが現在の展示ビルドを
  置き換えていない**ことを確認する。
  — **done when**: 展示フォルダの中身が承認済み世代を指していることを確認したこと
  — **備考**: `docs/operations/festival-runbook.md`「展示中は **読み取り参照**」/
  `docs/operations/update-patch.md`「1 世代だけ残します」。
  **読み取り確認のみ。cc は展示 PC を操作しない。**

- [ ] **T14** ステージ表 (`docs/evidence/index.md` §1) と `release/check/stage-manifest.json` の
  各ステージ `status` / `proven` / `unproven` を観測結果に合わせて更新する。
  — **done when**: S0–S4 の判定が実際の観測と一致し、根拠となるファイルが残っていること
  — **備考**: **`docs/evidence/index.md` と `release/check/**` は他 Worker が所有**しているため、
  cc は **cc 側の統合 PR で** 適用する (本 spec の所有範囲は `specs/022-*/` のみ)。
