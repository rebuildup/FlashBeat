# RC 0.2.0 包括検証記録 (Issue #744 / #728)

- 対象 SHA: `86a843ba2451a5aca564fdf12c38c5be8a55c2d3` (`release-0-2-0` HEAD, VERSION 0.2.0)
  - 旧 freeze `9b61c6df` からの差分は #740 Stage B (PR #776: 常時固定フラグ畳み込み + 死に経路削除) のみ。コード差分はあるため B/C/E は本 SHA で再実施した
- 実施日: 2026-11-20 / 実施: Devin (自律 RC ゲート)
- 正本チェックリスト: `docs/qa/release-candidate-gate.md`

## A. scriptable preflight — PASS

`script/qa-preflight.sh --with-web`: compile-check 4 アセンブリ、`.meta` 照合、VERSION、
chart schema byte 一致、parity fixtures、web `tsc --noEmit` + `vitest run` 全て PASS。
(web は Stage B で無変更のため `9b61c6df` 時点の結果を流用)

## B. Unity (対象 SHA)

| 項目 | 結果 | 証跡 |
|---|---|---|
| B1 EditMode | **PASS 2,090/2,090, failed=0, skipped=0** | `test-results/TestResults-rc2-editmode.xml`（対象 SHA の release-0-2-0 上で実行） |
| B2 PlayMode | **PASS 6/6** | `test-results/TestResults-rc2-playmode.xml`（対象 SHA `86a843ba` で実行） |
| B3 serialized 監査 | PASS (B1 に含有: `SerializedAssetReferenceAuditTests`, `GameSceneSoundBindingTests`, `StageBRemovedUiFiles_StayDeleted`, `StageBRemovedFlags_StayRemoved`) | 同上 |
| B4 scoring parity | PASS (`ParityFixtureTests.ScoreVectors_MatchEveryVersion` 緑 + web parity 26/26) | EditMode + `cd web && node --test` |
| B5 負例実在 | `docs/qa/requirements-traceability.md` §1 の負例列を目視確認 — code-verified 全項目で非空 | 同書 |

旧 freeze `9b61c6df` では EditMode 2,095/2,095 PASS (PR #771 で 46 件の失敗を全件
disposition: 契約陳腐化の試験更新 + 実害 3 件修正)。Stage B (PR #776) でテスト 5 件減・
監査テスト 2 件増のため総数は 2,090 (`test-results/` に旧 SHA・新 SHA 両方の XML を保存)。

## C. ビルド成果物

| 項目 | 結果 |
|---|---|
| C2 macOS build | **PASS** — `FlashBeat.macbuild/Builds/macOS/FlashBeat.app` (Unity batch `BuildScript.BuildStandaloneMacOS`, 対象 SHA で再ビルド) |
| C3 bundleVersion | **PASS** — `CFBundleShortVersionString = 0.2.0+86a843ba` (Info.plist 実測) |
| C1 Win64 build | **PASS** — `script/win64-build-mac.sh 86a843ba` → `dist/win64/FlashBeat-0.2.0-win64-86a843ba.zip` + SHA-256 sidecar。公開 Release `v0.2.0-win64-86a843ba` に添付済み（実機起動は Windows 環境が必要で未確認） |
| C4 ZIP + SHA-256 + version.json | **PASS** — 両 ZIP の SHA-256 を sidecar と再照合済み。win64 `version.json`/`update-channel.txt` 同梱を監査で確認 |
| C5 第三者メディア非含有 | **PASS** — `tools/release/audit-release-media.py` で公開 ZIP を再監査: 両 Release `song-free`、ルーズメディア 0 件（`docs/operations/release-media-audit.md` §3.2） |
| C6 update channel/tag | **PASS** — tag 形式 `v0.2.0-{win64,macos}-86a843ba`、stable (prerelease=false)、`update-channel.txt=stable` |

## D. Web (対象 SHA — Stage B で web 無変更)

| 項目 | 結果 |
|---|---|
| D1 unit/type/build | **PASS** — `npm test` 全件: node --test 169/169、vitest 453/453 (51 files)、e2e harness 22/22。`vp build` 成功 (4,096 modules) |
| D2 Playwright E2E + visual | **PASS** — 12/12 specs、visual capture hScroll/console error なし (mobile の clipped=4・smallFonts/smallTargets は計測ファクトとして index.json に記録、要人間視覚レビュー) |
| D3 隔離 D1 | **PASS** — E2E/visual はローカル seed D1 のみ。本番 D1 への書き込みは migration apply のみ (下記 E9) |

## E. 実機・計測

### E8 ノーツ移動 (計測条件つき before/after) — PASS

- 計測: `FlashBeat.app` (`0.2.0+86a843ba`) を `-autotest v6songend -fbperf -fbnoteprobe` で起動、
  曲「ツイッターランド」GameScene 演奏中の 5 秒窓 + ResultScene。
- 証跡: `docs/evidence/739-noteprobe/86a843ba/` (Player.log, probe-summary.txt, autotest PNG 4 枚, result.txt, summary.txt)
- 結果 (after / 対象 SHA):
  - PerfProbe GameScene: `avgFps=60.0`、p50/p95=16.7ms、p99=17.7ms、spike (>250ms) なし
  - NoteProbe: `stepHz≈29.9` (VideoPlayer 30fps 時計の量子化は残存=仕様どおり)、
    `noteMovePct=98.3–100.0`、`noteHz=45–53`、`stepZeroPct≈49.8` (半分のフレームで step 不変→
    MediaRenderClock がフレーム間を補間している証拠)、`avDriftMaxMs=16.7`
  - AutoTestV6SongEnd: **pass=32 fail=0**、全 130 ノーツ判定・スコア **2,000,000** (v4 実機確認)・Result 遷移
- before: `docs/evidence/739-noteprobe/1db05d5a-baseline/` (PR #775 でコミット済み)。
  同一曲・同一ハーネスで `noteMovePct≈51` / `noteHz≈27` / スコア 1,000,000 (v3)。
  **ノーツが描画フレームの約半分でしか移動していなかった状態が、補間時計の導入で
  全フレーム移動へ改善したことを before/after の実測で確認**

### E9 scoring v4 サーバ受理 — **PASS（公開済み）**

- ローカル (対象 SHA): `tests/api.test.ts` が v4 で score=2,000,000 の POST 201、
  leaderboard の scoringVersion 分離、既定 board = 最新版を実証 (169/169 PASS に含有)
- リモート D1: `20261119162405_curly_master_chief.sql` 適用済み・`sqlite_master` で
  `scoringVersion` カラムと v4 CHECK を確認 (docs/operations/d1-migrations.md §10.1)
- **本番 deploy 済み (2026-11-20)**: `void deploy --platform cloudflare` で
  `flashbeat-web` を公開。`https://flashbeat.rebuildup.dev` で catalog 200・
  rankings/leaderboards の `scoringVersion` 受理を実測。v4 スコアの実 POST は
  実プレイで自然発生する（テスト投稿でランキングを汚さないため未実施）
- **データ保全の実測 (2026-11-20, 公開後フォローアップ)**: テーブル再構築 migration
  (`-- void:allow-destructive`) の適用後に本番 D1 を照合 —
  `scores` 27 行・id 1–27 連番・distinct (欠損なし)、`scoringVersion` 内訳
  v1×26 + v3×1 (全て 1,000,000 以下で旧上限を維持)、`scores_leaderboard_idx` 存在、
  v4 CHECK 制約あり、`PRAGMA foreign_key_check` 違反 0 件、
  `songs`=30 / `chart_versions`=41 (API の catalogVersion=41 と一致)。
  移行前の件数スナップショットは未取得のため「27 件が移行前と同一」の直接証明は
  ないが、AUTOINCREMENT id の連番保持と FK チェック通過から行欠損はないと判断。
  バックアップ/復旧経路は D1 Time Travel (PITR) + `void db restore --bookmark`

### E1/E2 クリック音 — 証跡あり / 聴取は人間確認へ

- 素材: `Assets/Game/Audio/決定ボタンを押す31.mp3`、由来 `soundeffect-lab.info`・
  license `free-commercial-no-credit` を .meta userData に記録 (PR #746)
- `GameSceneSoundBindingTests` + `SerializedAssetReferenceAuditTests` が GUID 分離を固定 (EditMode 緑)
- **未実施**: 実機での聴取確認 (人間の受入項目として残す)

### E3–E7, E10 実機 GUI 観測 — 自動証跡 + 人間確認へ

- `-autotest v6songend` が Select→Game→Result を実走行し各画面 PNG を出力
  (docs/evidence/739-noteprobe/86a843ba/*.png)。判定・スコア・遷移の機械的合格は 32/32
- 実描画の視覚差異 (判定軌道・歌詞パネル・SS レイアウト・全解像度) は
  スクリーンショットを人間レビューへ委ねる。機械的には EditMode の
  `V6RenderParityTests` / `V6ResultGradeFitTests` / `V6TextFitTests` が緑

## 公開記録 (2026-11-20、フォローアップ追記)

- `release-0-2-0 → main` merge: PR #778 (`534b527e`)、記録 PR #779
- GitHub Releases (stable): `v0.2.0-win64-86a843ba` / `v0.2.0-macos-86a843ba`、
  ZIP + SHA-256 添付・sidecar 再照合済み、メディア監査 song-free
- Web Worker: `void deploy --platform cloudflare` で本番公開、D1 migrations 最新

## 未検証・残件 (正直な一覧)

1. Win64 実機での起動・プレイ (C1 ビルド成果物は公開済みだが実行検証は Windows 環境が必要)
2. 実機聴取 (E1)、実描画の人間レビュー (E3–E7, E10)、パッケージ ZIP の最終 hash 照合
   — **動画・実聴での確認が必要**（静止スクリーンショットでは判定アニメーションや
   音声の受入は完了しない）
3. 本番での v4 スコア実 POST（初回実プレイで自然発生。API 層・D1 スキーマは検証済み）
4. #730 は聴取確認待ちで open のまま (素材・バインド・ライセンス証跡は完備)
5. `noteHz` (45–53) は移動検出の計測値であり描画 FPS ではない — 実画面での
   滑らかさ・更新タイミングの揺らぎは人間レビューで確認する
