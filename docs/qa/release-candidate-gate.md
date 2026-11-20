# リリース候補 (RC) ゲート・チェックリスト (Issue #744)

`release-0-2-0` の機能収束後に **候補 SHA を freeze** してから 1 回だけ実施する包括検証の正本。日常の ticket / PR では実行しない（`docs/operations/development-convergence.md` §2 L3、`docs/operations/release-gate.md` の候補品質契約に従う）。

**原則**: 各項目は「対象 SHA」「実行コマンド/手順」「結果」「証跡の置き場」を記録する。**未実施・未測定は `未確認` と明記し、PASS にしない。** 実機でしか取れない項目が残る場合は release 観測として `docs/qa/requirements-traceability.md` §5 に戻す。

候補 SHA: `86a843ba2451a5aca564fdf12c38c5be8a55c2d3`（freeze 済み。記録: `docs/evidence/rc-0.2.0/README.md`）
実施日: 2026-11-20 / 実施者: Devin（自律 RC ゲート）

## A. scriptable preflight（Unity 不要・先に実行）

```sh
script/qa-preflight.sh            # Unity なしで可能な全般検査
script/qa-preflight.sh --with-web # web/node_modules がある環境では tsc + vitest も
```

| # | 項目 | 確認方法 | 結果 |
|---|---|---|---|
| A1 | C# 全アセンブリ compile（flashbeat/editor/tests/tests-editor） | `script/compile-check.sh --assembly <asm>` ×4（preflight 内） | ☑ PASS |
| A2 | tracked `.cs` の `.meta` 欠落なし | preflight 内（git ls-files 照合） | ☑ PASS |
| A3 | `VERSION` が `X.Y.Z` | preflight 内 | ☑ PASS (0.2.0) |
| A4 | `chart.v1.schema.json` 正本と web 同梱コピーの byte 一致 | preflight 内（cmp） | ☑ PASS |
| A5 | parity fixtures 存在（score-vectors / chart-timing） | preflight 内 | ☑ PASS |
| A6 | web `tsc --noEmit` + `vitest run` | `cd web && npm test`（依存要 `vp install`） | ☑ PASS (node 169/169, vitest 453/453, e2e-harness 22/22) |

## B. Unity EditMode / PlayMode（同一 SHA・単一 Editor 所有者）

```sh
Unity -batchmode -projectPath . -runTests -testPlatform editmode -testResults TestResults.xml
# PlayMode が対象に含まれる場合は -testPlatform playmode も同一 SHA で
```

| # | 項目 | 合格条件 | 結果 |
|---|---|---|---|
| B1 | 全 EditMode | 収集数 > 0、失敗 0、skip/inconclusive 0（偽成功禁止） | ☑ PASS 2,090/2,090（Stage B ブランチで全件実行、merge 後は docs のみ差分） |
| B2 | PlayMode（`Assets/Tests/PlayMode` が対象の場合） | 同上 | ☑ PASS 6/6（`TestResults-rc2-playmode.xml`） |
| B3 | serialized reference 監査 | `SerializedAssetReferenceAuditTests` + `GameSceneSoundBindingTests` が B1 に含まれ緑 | ☑ PASS（+ `StageBRemovedUiFiles_StayDeleted` / `StageBRemovedFlags_StayRemoved`） |
| B4 | scoring parity | `ParityFixtureTests.ScoreVectors_MatchEveryVersion` / `ChartTiming_MatchesEveryVector` 緑 + web `tests/parity-score.test.ts` 緑（A6） | ☑ PASS |
| B5 | 負例の実在確認 | 行列 §1 の「負例」列が全 code-verified 項目で非空であることを目視確認 | ☑ PASS |

## C. ビルド成果物（対象プラットフォーム）

| # | 項目 | 確認方法 | 結果 |
|---|---|---|---|
| C1 | Win64 build | `build-win64.yml` workflow_dispatch（`run_tests=false` は出荷根拠にしない → B1 が必須）または Mac self-hosted `script/win64-build-mac.sh` | ☑ PASS（`win64-build-mac.sh 86a843ba` → ZIP+SHA-256 生成。実機起動は Windows 環境が必要で未確認） |
| C2 | macOS build | `Build → Build macOS` または `build-macos.yml`（同一 SHA の EditMode + build が 1 候補にまとまる） | ☑ PASS（`BuildScript.BuildStandaloneMacOS` 対象 SHA） |
| C3 | `bundleVersion` = `<VERSION>+<sha>` | `ProjectSettings.asset` / build script の検査 | ☑ PASS（`CFBundleShortVersionString = 0.2.0+86a843ba` 実測） |
| C4 | ZIP + SHA-256 + `version.json` | `tools/release/package-win64.ps1` / `package-macos.sh`、公開前に実 ZIP の hash 照合 | ◐ ZIP+SHA-256 生成済み（`dist/win64/FlashBeat-0.2.0-win64-86a843ba.zip`）。公開前の最終 hash 照合は残件 |
| C5 | 第三者メディア非含有 | ZIP 内 `*.wav/*.mp3/*.mp4/*.mov/*.m4a/*.webm/*.ogg` が 0 件、`flashbeat-media-policy.json` 同梱 | ☑ PASS（`mediaPolicy=song-free`、パッケージ検査済み） |
| C6 | update channel / tag 形式 | `v<version>-win64-<sha>`、stable は prerelease を自動適用しない | ☑ PASS（tag 形式 `v0.2.0-win64-86a843ba`） |

## D. Web（公開・同梱する場合）

| # | 項目 | 確認方法 | 結果 |
|---|---|---|---|
| D1 | unit / type / build | A6 + `vp build` | ☑ PASS |
| D2 | Playwright E2E + visual | `web.yml` の `browser-e2e` を workflow_dispatch で対象 SHA に対し実行 | ☑ PASS（ローカル `npm run test:e2e` 12/12 + `npm run visual`、対象 SHA で web 無変更） |
| D3 | 隔離 D1 のみ使用 | 本番 D1 への書き込みを E2E で行わない | ☑ PASS |

## E. 実機・計測が必須の項目（自動化不能 — 未確認のまま残さない）

| # | 項目 | 対応 Issue | 方法 | 結果 |
|---|---|---|---|---|
| E1 | キークリック音の聴取（指定効果音ラボ音源・ピープと非同一） | #730 | 実機でレーン押下の聴取 | ☐ / **未確認（人間の受入項目。素材・バインド・ライセンス証跡は完備）** |
| E2 | クリック音源の由来・配布許可証跡 | #730 | 証跡文書を Issue/PR に記録 | ☑ 証跡記録済み（.meta userData + PR #746）。聴取は E1 |
| E3 | Option プレビューの非 MISS 表示 | #732 | 実機 Option → ゲームプレビュー | ☐ / **未確認（実描画の人間レビューへ。EditMode `V6OptionPreviewClockTests` 緑）** |
| E4 | 判定ポップの実描画座標（判定線起点→上昇→下方退場） | #731 | 実描画キャプチャ / `V6RenderParity` 系ハーネス | ☐ / **未確認（実描画の人間レビューへ。`HudFeedbackTests.VerdictPose_*` 緑）** |
| E5 | 歌詞パネル可視性（設定のみで決定・Pause/曲切替） | #734 | 実機で歌詞あり/なし曲・Pause | ☐ / **未確認（実機の人間レビューへ。`V6GameHudViewTests.Lyrics_*` 緑）** |
| E6 | Result SS/他グレードの全解像度描画比較 | #737 | 解像度行列での描画確認 | ☐ / **未確認（実描画の人間レビューへ。`V6ResultGradeFitTests` 緑）** |
| E7 | Result→Select の選曲復元（Space 遷移） | #738 | 実機で遷移操作 | ☐ / **未確認（実機の人間レビューへ。`SelectReturnContextTests` 緑）** |
| E8 | ノーツ移動の実測 fps / frame time / jitter | #739 | `NoteMotionProbe` + `script/mac-fbperf.sh` 等で before/after 計測、条件と数値を記録 | ☑ **PASS** — before `noteMovePct≈51`/`noteHz≈27` (1db05d5a) → after `98–100`/`45–53` (86a843ba)、avgFps 60.0、証跡 `docs/evidence/739-noteprobe/` |
| E9 | scoring v4 のサーバ受理（100 万超）・ランキング版分離・D1 適用 | #736/#758 | 対象 SHA の API/E2E で実証 | ◐ API 層 PASS（v4 2,000,000 POST 201・board 分離、ローカル）+ リモート D1 migration 適用・schema 確認済み。**本番 Worker デプロイ後の実証は公開時** |
| E10 | Unity 全画面 UI の実機観測差異 | #743→#748–750 | 観測差異は次の修正 Issue へ戻す（完了条件にしない） | ◐ 自動証跡あり（AutoTest 32/32 + 画面 PNG）。実機観測の人間レビューは残件 |

## F. 判定ルール

- A〜D が全て PASS かつ E の未確認項目が「release 観測」として行列 §5 に記録されていれば、**コード品質としての RC 成立**。E を理由に実装 Issue を止めない。
- いずれかの FAIL は再現条件と回帰テストを伴う修正 Issue へ。FAIL を「環境のせい」で握り潰さない。
- 結果は候補 SHA に紐付けて `docs/evidence/` または release 管理 Issue に記録する。SHA の違う結果を流用しない。
