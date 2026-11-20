# RC 0.2.0 包括検証記録 (Issue #744 / #728)

- 対象 SHA: `9b61c6dfab61f1dc68064e910174d725afcdd55e` (`release-0-2-0` HEAD, VERSION 0.2.0)
- 実施日: 2026-11-20 / 実施: Devin (自律 RC ゲート)
- 正本チェックリスト: `docs/qa/release-candidate-gate.md`

## A. scriptable preflight — PASS

`script/qa-preflight.sh --with-web`: compile-check 4 アセンブリ、`.meta` 照合、VERSION、
chart schema byte 一致、parity fixtures、web `tsc --noEmit` + `vitest run` 全て PASS。

## B. Unity (同一 SHA)

| 項目 | 結果 | 証跡 |
|---|---|---|
| B1 EditMode | **PASS 2,095/2,095, failed=0, skipped=0** | `TestResults-rc-editmode.xml` (repo root、未コミットの生データ) |
| B2 PlayMode | **PASS 6/6** | `TestResults-rc-playmode.xml` |
| B3 serialized 監査 | PASS (B1 に含有: `SerializedAssetReferenceAuditTests`, `GameSceneSoundBindingTests`) | 同上 |
| B4 scoring parity | PASS (`ParityFixtureTests.ScoreVectors_MatchEveryVersion` 緑 + web `tests/parity-score.test.ts` 26/26) | EditMode + `cd web && node --test tests/parity-score.test.ts` |
| B5 負例実在 | `docs/qa/requirements-traceability.md` §1 の負例列を目視確認 — code-verified 全項目で非空 | 同書 |

EditMode 46 件の失敗を PR #771 で全件 disposition してからの全件再実行。
失敗の内訳: 契約陳腐化による試験更新 (#733/#736/#740/#748/#749/#750) +
実害 3 件の修正 (web v4 finalBonus ゲート、歌詞縁取り TMP NRE、長曲名の auto-size 下限超過)。

## C. ビルド成果物

| 項目 | 結果 |
|---|---|
| C2 macOS build | **PASS** — `FlashBeat.macbuild/Builds/macOS/FlashBeat.app` (Unity batch `BuildScript.BuildStandaloneMacOS`) |
| C3 bundleVersion | **PASS** — `CFBundleShortVersionString = 0.2.0+9b61c6df` (Info.plist 実測) |
| C1 Win64 build | `script/win64-build-mac.sh 9b61c6df` 実行 (結果は追記) |
| C4 ZIP + SHA-256 + version.json | package スクリプト実行後に追記 |
| C5 第三者メディア非含有 | パッケージ後に ZIP 内検査で追記 |
| C6 update channel/tag | パッケージ時に確認 |

## D. Web (対象 SHA)

| 項目 | 結果 |
|---|---|
| D1 unit/type/build | **PASS** — node --test 169/169、vitest 453/453、`vp build` 成功 |
| D2 Playwright E2E + visual | **PASS** — 12/12 specs、visual capture hScroll/console error なし (mobile の clipped=4・smallFonts/smallTargets は計測ファクトとして index.json に記録、要人間視覚レビュー) |
| D3 隔離 D1 | **PASS** — E2E/visual はローカル seed D1 のみ。本番 D1 への書き込みは migration apply のみ (下記 E9) |

## E. 実機・計測

### E8 ノーツ移動 (計測条件つき before/after) — 計測実施

- 計測: `FlashBeat.app` (対象 SHA) を `-autotest v6songend -fbperf -fbnoteprobe` で起動、
  曲「ツイッターランド」GameScene 演奏中の 5 秒窓 3 本 + ResultScene。
- 証跡: `docs/evidence/739-noteprobe/9b61c6df/` (Player.log, probe-summary.txt, autotest PNG 4 枚, result.txt)
- 結果 (after / 対象 SHA):
  - PerfProbe GameScene: `avgFps=60.0 p50=p95=p99=worst=16.7ms`、spike なし、GC 5–6 回/窓・gcKBPerFrame ≈ -4〜+4
  - NoteProbe: `stepHz=29.9` (VideoPlayer 30fps 時計の量子化は残存=仕様どおり)、
    `noteMovePct=100.0`、`noteHz=45.2–50.6`、`stepZeroPct=49.8` (半分のフレームで step 不変→
    MediaRenderClock がフレーム間を補間している証拠)
  - AutoTestV6SongEnd: **pass=32 fail=0**、全 130 ノーツ判定・スコア **2,000,000** (v4 実機確認)・Result 遷移
- before: `1db05d5a` (PR #753 直前) + NoteMotionProbe のみ移植した計測専用ビルドで同条件計測 (結果は追記)

### E9 scoring v4 サーバ受理 — API 層 PASS / 本番デプロイ待ち

- ローカル (対象 SHA): `tests/api.test.ts` が v4 で score=2,000,000 の POST 201、
  leaderboard の scoringVersion 分離、既定 board = 最新版を実証 (169/169 PASS に含有)
- リモート D1: `20261119162405_curly_master_chief.sql` 適用済み・`sqlite_master` で
  `scoringVersion` カラムと v4 CHECK を確認 (docs/operations/d1-migrations.md §10.1)
- **未実施**: 対象 SHA の Worker デプロイ後の本番 API 実証 → release-0-2-0→main 公開時に実施

### E1/E2 クリック音 — 証跡あり / 聴取は人間確認へ

- 素材: `Assets/Game/Audio/決定ボタンを押す31.mp3`、由来 `soundeffect-lab.info`・
  license `free-commercial-no-credit` を .meta userData に記録 (PR #746)
- `GameSceneSoundBindingTests` + `SerializedAssetReferenceAuditTests` が GUID 分離を固定 (EditMode 緑)
- **未実施**: 実機での聴取確認 (人間の受入項目として残す)

### E3–E7, E10 実機 GUI 観測 — 自動証跡 + 人間確認へ

- `-autotest v6songend` が Select→Game→Result を実走行し各画面 PNG を出力
  (docs/evidence/739-noteprobe/9b61c6df/*.png)。判定・スコア・遷移の機械的合格は 32/32
- 実描画の視覚差異 (判定軌道・歌詞パネル・SS レイアウト・全解像度) は
  スクリーンショットを人間レビューへ委ねる。機械的には EditMode の
  `V6RenderParityTests` / `V6ResultGradeFitTests` / `V6TextFitTests` が緑

## 未検証・残件 (正直な一覧)

1. Win64 実機での起動・プレイ (C1 ビルド成果物は得るが実行検証は Windows 環境が必要)
2. 実機聴取 (E1)、実描画の人間レビュー (E3–E7, E10)、パッケージ ZIP の最終 hash 照合
3. #740 Stage B (常時固定フラグの畳み込み) — Issue #740 は open のまま、
   docs/architecture/v5-legacy-ui-references.md の移行計画どおり Unity 実機検証を要する別作業
4. 本番 Worker デプロイ後の E9 実証 (release 公開と同時)
