# リリース候補 (RC) ゲート・チェックリスト (Issue #744)

`release-0-2-0` の機能収束後に **候補 SHA を freeze** してから 1 回だけ実施する包括検証の正本。日常の ticket / PR では実行しない（`docs/operations/development-convergence.md` §2 L3、`docs/operations/release-gate.md` の候補品質契約に従う）。

**原則**: 各項目は「対象 SHA」「実行コマンド/手順」「結果」「証跡の置き場」を記録する。**未実施・未測定は `未確認` と明記し、PASS にしない。** 実機でしか取れない項目が残る場合は release 観測として `docs/qa/requirements-traceability.md` §5 に戻す。

候補 SHA: `____________`（freeze 時に記入）
実施日: `____________` / 実施者: `____________`

## A. scriptable preflight（Unity 不要・先に実行）

```sh
script/qa-preflight.sh            # Unity なしで可能な全般検査
script/qa-preflight.sh --with-web # web/node_modules がある環境では tsc + vitest も
```

| # | 項目 | 確認方法 | 結果 |
|---|---|---|---|
| A1 | C# 全アセンブリ compile（flashbeat/editor/tests/tests-editor） | `script/compile-check.sh --assembly <asm>` ×4（preflight 内） | ☐ |
| A2 | tracked `.cs` の `.meta` 欠落なし | preflight 内（git ls-files 照合） | ☐ |
| A3 | `VERSION` が `X.Y.Z` | preflight 内 | ☐ |
| A4 | `chart.v1.schema.json` 正本と web 同梱コピーの byte 一致 | preflight 内（cmp） | ☐ |
| A5 | parity fixtures 存在（score-vectors / chart-timing） | preflight 内 | ☐ |
| A6 | web `tsc --noEmit` + `vitest run` | `cd web && npm test`（依存要 `vp install`） | ☐ |

## B. Unity EditMode / PlayMode（同一 SHA・単一 Editor 所有者）

```sh
Unity -batchmode -projectPath . -runTests -testPlatform editmode -testResults TestResults.xml
# PlayMode が対象に含まれる場合は -testPlatform playmode も同一 SHA で
```

| # | 項目 | 合格条件 | 結果 |
|---|---|---|---|
| B1 | 全 EditMode | 収集数 > 0、失敗 0、skip/inconclusive 0（偽成功禁止） | ☐ |
| B2 | PlayMode（`Assets/Tests/PlayMode` が対象の場合） | 同上 | ☐ |
| B3 | serialized reference 監査 | `SerializedAssetReferenceAuditTests` + `GameSceneSoundBindingTests` が B1 に含まれ緑 | ☐ |
| B4 | scoring parity | `ParityFixtureTests.ScoreVectors_MatchEveryVersion` / `ChartTiming_MatchesEveryVector` 緑 + web `tests/parity-score.test.ts` 緑（A6） | ☐ |
| B5 | 負例の実在確認 | 行列 §1 の「負例」列が全 code-verified 項目で非空であることを目視確認 | ☐ |

## C. ビルド成果物（対象プラットフォーム）

| # | 項目 | 確認方法 | 結果 |
|---|---|---|---|
| C1 | Win64 build | `build-win64.yml` workflow_dispatch（`run_tests=false` は出荷根拠にしない → B1 が必須）または Mac self-hosted `script/win64-build-mac.sh` | ☐ |
| C2 | macOS build | `Build → Build macOS` または `build-macos.yml`（同一 SHA の EditMode + build が 1 候補にまとまる） | ☐ |
| C3 | `bundleVersion` = `<VERSION>+<sha>` | `ProjectSettings.asset` / build script の検査 | ☐ |
| C4 | ZIP + SHA-256 + `version.json` | `tools/release/package-win64.ps1` / `package-macos.sh`、公開前に実 ZIP の hash 照合 | ☐ |
| C5 | 第三者メディア非含有 | ZIP 内 `*.wav/*.mp3/*.mp4/*.mov/*.m4a/*.webm/*.ogg` が 0 件、`flashbeat-media-policy.json` 同梱 | ☐ |
| C6 | update channel / tag 形式 | `v<version>-win64-<sha>`、stable は prerelease を自動適用しない | ☐ |

## D. Web（公開・同梱する場合）

| # | 項目 | 確認方法 | 結果 |
|---|---|---|---|
| D1 | unit / type / build | A6 + `vp build` | ☐ |
| D2 | Playwright E2E + visual | `web.yml` の `browser-e2e` を workflow_dispatch で対象 SHA に対し実行 | ☐ |
| D3 | 隔離 D1 のみ使用 | 本番 D1 への書き込みを E2E で行わない | ☐ |

## E. 実機・計測が必須の項目（自動化不能 — 未確認のまま残さない）

| # | 項目 | 対応 Issue | 方法 | 結果 |
|---|---|---|---|---|
| E1 | キークリック音の聴取（指定効果音ラボ音源・ピープと非同一） | #730 | 実機でレーン押下の聴取 | ☐ / 未確認 |
| E2 | クリック音源の由来・配布許可証跡 | #730 | 証跡文書を Issue/PR に記録 | ☐ / 未確認 |
| E3 | Option プレビューの非 MISS 表示 | #732 | 実機 Option → ゲームプレビュー | ☐ / 未確認 |
| E4 | 判定ポップの実描画座標（判定線起点→上昇→下方退場） | #731 | 実描画キャプチャ / `V6RenderParity` 系ハーネス | ☐ / 未確認 |
| E5 | 歌詞パネル可視性（設定のみで決定・Pause/曲切替） | #734 | 実機で歌詞あり/なし曲・Pause | ☐ / 未確認 |
| E6 | Result SS/他グレードの全解像度描画比較 | #737 | 解像度行列での描画確認 | ☐ / 未確認 |
| E7 | Result→Select の選曲復元（Space 遷移） | #738 | 実機で遷移操作 | ☐ / 未確認 |
| E8 | ノーツ移動の実測 fps / frame time / jitter | #739 | `NoteMotionProbe` + `script/mac-fbperf.sh` 等で before/after 計測、条件と数値を記録 | ☐ / 未確認 |
| E9 | scoring v4 のサーバ受理（100 万超）・ランキング版分離・D1 適用 | #736/#758 | 対象 SHA の API/E2E で実証 | ☐ / 未確認 |
| E10 | Unity 全画面 UI の実機観測差異 | #743→#748–750 | 観測差異は次の修正 Issue へ戻す（完了条件にしない） | ☐ / 未確認 |

## F. 判定ルール

- A〜D が全て PASS かつ E の未確認項目が「release 観測」として行列 §5 に記録されていれば、**コード品質としての RC 成立**。E を理由に実装 Issue を止めない。
- いずれかの FAIL は再現条件と回帰テストを伴う修正 Issue へ。FAIL を「環境のせい」で握り潰さない。
- 結果は候補 SHA に紐付けて `docs/evidence/` または release 管理 Issue に記録する。SHA の違う結果を流用しない。
