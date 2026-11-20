# 要求トレーサビリティ行列 (Issue #744)

Epic [#728](https://github.com/rebuildup/FlashBeat/issues/728) の「オーナー要求 → 正本 → 実装 → 独立 oracle / 負例 → 検証レベル → 候補証跡」の連鎖を 1 表に固定する。**テストが緑であることと要求が満たされたことを分ける**のが目的であり、実機・性能・視覚の未観測項目は `未確認` のまま残す（`未測定を PASS にしない`、正本: `docs/operations/development-convergence.md`）。

- 対象ブランチ: `release-0-2-0`（RC freeze SHA `86a843ba2451a5aca564fdf12c38c5be8a55c2d3`、VERSION 0.2.0）
- 検証レベルは development-convergence §2 の L0（局所）/ L1（cheap CI）/ L2（Unity batch compile + EditMode）/ L3（candidate freeze 後の包括検証）に対応
- 「負例」列は **欠陥を意図的に再導入した時に失敗するテスト** の有無。`—` は未着手を意味し、緑を装わない
- 証跡状態: `code-verified` = 実装 + 自動テストが release ブランチにあり局所検証済 / `rc-verified` = RC freeze SHA で包括検証済 / `unverified` = 自動化で担保できない残件あり / `not-started` = 未着手
- RC 包括検証の記録: `docs/evidence/rc-0.2.0/README.md`、`docs/qa/release-candidate-gate.md`

## 1. #728 症状 → チケット → 検証の対応

| # | 症状（オーナー観測） | Issue | 実装（PR / commit） | 正本・仕様 | 独立 oracle / テスト | 負例（再導入で失敗） | 検証レベル | 証跡状態 |
|---|---|---|---|---|---|---|---|---|
| 1 | キークリックがピープ音（hitSound/clickSound 同一 GUID） | #730 (open) | PR #746 `42f56556`, `34e4dbe4` | `Judge.cs` の hit/click 分離契約 | `GameSceneSoundBindingTests`（3 件）+ `SerializedAssetReferenceAuditTests`（#744 で全 Scene/Prefab へ一般化） | 同一 GUID 再代入・GUID 解決不能・Audio 以外への差し替えで失敗 | L0/L2（EditMode） | code-verified。**音源の由来・配布許可の証跡は .meta userData に記録済み。実機聴取のみ未確認（人間の受入項目）** |
| 2 | Option プレビュー判定が全 MISS | #732 (closed) | PR #755 `0f001169` | `V6OptionPreviewRig` のオートプレイ + 時計リベース契約 | `V6OptionPreviewClockTests`（`AutoPlay_ProducesEveryJudgementKind` 等 3 件）、`V6OptionPreviewExperienceTests`（判定・歌詞・セッション非汚染） | `Clock_SongChange_RebasesWithoutMissBurst` は旧実装の MISS バーストを直接再現する反例 | L0/L2 + L3（実描画確認） | code-verified。実機での非 MISS 表示は人間レビュー待ち |
| 3 | 判定アニメーションが旧軌道（上→落下停止） | #731 (closed) | PR #751 `4ea76c13` | `docs/design/v6/review-2026-11-11-decisions.md` §10.3、`docs/architecture/v6-ui.md`、`docs/ui/v6-screen-contract.md` を要求から再制定 | `HudFeedbackTests.VerdictPose_*`（時系列境界・中間状態）、`V6RenderParityTests`（本編/プレビュー同一軌道） | `VerdictPose_RejectsOldSpawnHighFallToRestPath` が旧軌道を明示拒否 | L0/L2 + L3（実描画座標） | code-verified。実描画スクショの人間レビュー待ち |
| 4 | Option キー設定の矢印がテキストでテーマ不一致 | #733 (closed) | PR #763 | `V6Legend.IconItem` + `V6Icons`（chevron プリミティブ） | `V6HierarchyTests.OptionRebuild_DoesNotDuplicateTheFooterLegend`（アイコン凡例の存在・旧グリフ keycap 0 件を assert） | テキスト矢印への回帰で失敗 | L0/L2 | rc-verified（EditMode 2090 緑） |
| 5 | 歌詞パネルが歌詞有無で出没 | #734 (closed) | PR #754 `e7d16e34` | `PlaySession.LyricsEnabled` を唯一の visibility policy に固定 | `V6GameHudViewTests.Lyrics_*`（`BoxFollowsOnlyTheSettingDuringPlayback`、`KeepTheBoxWhenTheChartHasNoLyrics`、`BoxStaysVisibleBeforeLyricDataArrives` 等） | 設定 OFF で本文に関わらず非表示 / 歌詞なし・空区間でも枠が残ることを検査 | L0/L2 + L3 | code-verified。実機 Pause/曲切替は人間レビュー待ち |
| 6 | ノーツが暗い（Shade/Shader/合成） | #735 (closed) + #760 (closed) | PR #759, #766, #769 | flat-rendering 契約 + 歌詞帯カットアウト | note-render 系 EditMode + `V6NoteRenderTests` | シェード/alpha/queue の再導入で失敗 | L0/L2 + L3 | code-verified。実描画の人間レビュー待ち |
| 7 | Pause と Option が別実装・プレビュー不一致 | #733 (closed) | PR #763 | `V6SettingsCatalog` / `V6VolumeSettings` 共通モデル + 共通 UI 機構 | Option/Pause の設定項目・適用経路の一致を検査する EditMode 群 | 二重実装への回帰で失敗 | L0/L2 | rc-verified |
| 8 | コンボ追加スコアが読めない + 100 万超の意味論 | #736 (closed) / #758 (closed) | PR #757 `d148f138`、PR #764、ADR-0033、D1 migration `20261119162405` | ADR-0033（v4: 判定ベース上限 100 万 + 加算ボーナス）、`contracts/parity/score-vectors.json` | `ScoringV4Tests`（5 件）、`ParityFixtureTests.ScoreVectors_MatchEveryVersion`、`ScoringV2Tests` 境界、`RankingSubmissionJudgementSumTests`、`JudgeSettlementInvariantTests`、web `tests/parity-score.test.ts` | v4 式の改変・v3 暗黙改算・ボーナス未確定時の先行加算で失敗。web/Unity の parity vector ずれも検出 | L0/L1（web 単体）/ L2（EditMode）/ L3 | **rc-verified** — 実機 AutoTest で score=2,000,000 (v4) を確認、API 層で 100 万超 POST 201・board 分離を実証、リモート D1 migration 適用・schema 確認済み。本番 Worker デプロイ後の実証は公開時 |
| 9 | Result の SS グレードでレイアウト崩れ | #737 (closed) | PR #752 `4497513f` | `V6ResultLogic`/`V6ResultStats` の fit 規約 | `V6ResultGradeFitTests`（`GradeLetter_AllGrades_FitInsideGradeBox`、`SS_ShrinksToFit`、`NumericTexts_ExtremeValues`、`RegisterModal_*`） | SS で字が箱をはみ出す変更・極端桁数で失敗 | L0/L2 + L3（全解像度の描画比較） | code-verified。実描画比較は人間レビュー待ち |
| 10 | Result→Select( Space )で選曲コンテキスト初期化 | #738 (closed) | PR #747 `e89a9c6d` | `SelectReturnContext`（唯一の SelectionContext） | `SelectReturnContextTests`（復元・1 回消費・fallback・本番配線のソース検査 `ProductionWiring_ResultToSelectAndTitleBoundary`） | 復元を Record のみ/Consume しない/曲削除時 fallback なしで失敗 | L0/L2 + L3 | code-verified。実機遷移は人間レビュー待ち |
| 11 | 全画面の細かい UI 崩れ（多数） | #743 (closed) → #748/#749/#750 (closed) | PR #762, #761, #767 | #748: 解像度/pan、#749: テキスト溢れ、#750: グリフ/色トークン | `V6CompactScreen`/`V6UiProfile`/`V6ScreenAudit` 系 + 各子 Issue の追加テスト | 子 Issue 毎の負例テストで再発検出 | L0/L2 + L3 | rc-verified（EditMode 緑）。実機観測差異は人間レビュー待ち |
| 12 | ノーツ移動がリフレッシュより粗い（体感 <20fps、未計測） | #739 (closed) | PR #753 `8d2d55a5`（`NoteMotionProbe`、`MediaTimeline` 描画時計） | `MediaRenderClock` の単調・補間契約 | `MediaRenderClockTests`（量子化時計の平滑化・停滞・逆シーク・ギャップの 6 件） | 量子化時計へ戻す変更で `AQuantizedMediaClockStillRendersSmoothly` が失敗 | L0/L2 + **L3 実測必須** | **rc-verified** — 実機計測: before `noteMovePct≈51`/`noteHz≈27` (1db05d5a) → after `98–100`/`45–53` (86a843ba)、avgFps 60.0・spike なし。証跡 `docs/evidence/739-noteprobe/` |

## 2. 構造的要件（#728 後半）

| 要件 | Issue | 状態 | 正本 / 証跡 | 未確認事項 |
|---|---|---|---|---|
| V5/V6 恒久二重構造の廃止・単一実装 | #729 (closed) → #740 (closed) | **rc-verified** | `specs/729-unified-runtime-architecture/spec.md`（PR #745）、Stage A PR #768 + Stage B PR #776、`docs/architecture/v5-legacy-ui-references.md` 棚卸し | なし（`V6UiMode` 残 7 フラグはライブ分岐のみ。監査テストで再発検出） |
| 命名・directory・重複定数の機械検証 | #740 (closed) | code-verified | `V5LegacyReferenceAuditTests` の pin リスト + `StageBRemoved*` テスト、責務別移設（PR #768） | 意味のない一括リネームは実施していない（計画どおり） |
| Web 現行スタック維持 + modern-web-guidance 評価 | #741 (closed) | rc-verified | `docs/adr/ADR-0030-web-guidance-and-studio-structure.md`、`web/AGENTS.md` 更新、`request-guard.ts`/`catalog.ts` 重複統合 | なし |
| Web 情報優先度・導線再設計 | #742 (closed) | rc-verified | `docs/design/web-studio-ia.md`、`sidebar.tsx` 再編、`sidebar.test.tsx` | 実ブラウザでの操作性の人間レビューは残件 |
| 「テスト緑 ≠ 要求充足」の QA 連鎖再構築 | #744 (closed) | rc-verified | 本ディレクトリ、`SerializedAssetReferenceAuditTests`、`script/qa-preflight.sh`、`release-candidate-gate.md`、`docs/evidence/rc-0.2.0/` | 人間受入項目（E1, E3–E7, E10）は release 観測へ |

## 3. チケット単位の状態一覧

| Issue | タイトル要約 | Issue 状態 | release 上の証跡 | 証跡状態 |
|---|---|---|---|---|
| #729 | 単一アーキテクチャ仕様 | CLOSED | PR #745 `specs/729-unified-runtime-architecture/spec.md` | 仕様確定（実装は #740 で完了） |
| #730 | クリック音源バインド | OPEN | PR #746 + `GameSceneSoundBindingTests` + #744 監査一般化 | code-verified / **聴取確認待ち（人間受入）** |
| #731 | 判定ポップ軌道 | CLOSED | PR #751 + `HudFeedbackTests`/`V6RenderParityTests` | code-verified / 実描画レビュー待ち |
| #732 | プレビュー全 MISS | CLOSED | PR #755 + `V6OptionPreviewClockTests`/`ExperienceTests` | code-verified / 実機表示レビュー待ち |
| #733 | Option/Pause 統一 + 矢印 | CLOSED | PR #763 + `V6SettingsCatalog` + icon legend | rc-verified |
| #734 | 歌詞パネル可視性固定 | CLOSED | PR #754 + `V6GameHudViewTests.Lyrics_*` | code-verified / 実機レビュー待ち |
| #735 | ノーツ白色の最終合成 | CLOSED | PR #759 + #766/#769（歌詞帯カットアウト・TMP ガード） | code-verified / 実描画レビュー待ち |
| #736 | scoring v4 | CLOSED | PR #757 + `ScoringV4Tests`/`ParityFixtureTests`/ADR-0033/D1 migration/web parity test | rc-verified |
| #737 | Result SS レイアウト | CLOSED | PR #752 + `V6ResultGradeFitTests` | code-verified / 実描画レビュー待ち |
| #738 | Result→Select 復元 | CLOSED | PR #747 + `SelectReturnContextTests` | code-verified / 実機レビュー待ち |
| #739 | ノーツ移動パフォーマンス | CLOSED | PR #753 + `MediaRenderClockTests` + `NoteMotionProbe` + before/after 実測 | **rc-verified** |
| #740 | V5/V6 廃止・命名整理 | CLOSED | PR #768 (Stage A) + PR #776 (Stage B) + 監査台帳 | **rc-verified** |
| #741 | Web architecture | CLOSED | PR #756 + ADR-0030 | rc-verified |
| #742 | Web UX 導線 | CLOSED | PR #756 + `web-studio-ia.md` + sidebar tests | rc-verified / 実ブラウザレビュー待ち |
| #743 | Unity 全画面 UI 監査 | CLOSED | 監査完了 → #748/#749/#750 へ分割 | 監査のみ（修正は子 Issue で完了） |
| #744 | QA 再構築（本 Issue） | CLOSED | 本書、`SerializedAssetReferenceAuditTests`、`script/qa-preflight.sh`、`release-candidate-gate.md`、`docs/evidence/rc-0.2.0/` | rc-verified |
| #748 | HUD 解像度/pan 根本修正 | CLOSED | PR #762 | rc-verified |
| #749 | テキスト溢れ系統修正 | CLOSED | PR #761 | rc-verified |
| #750 | グリフ/色トークン統一 | CLOSED | PR #767 | rc-verified |
| #758 | scoring v4 グレード閾値・ゲージ・D1 残件 | CLOSED | PR #764 + D1 migration 適用・schema 確認 | rc-verified（本番 API 実証は公開時） |
| #760 | 歌詞パネル帯のノーツ減光 | CLOSED | PR #766 + #769 | code-verified / 実描画レビュー待ち |

## 4. カバレッジ集計（RC freeze 時点）

- #728 症状 12 項目すべてに実装 + 独立 oracle + 負例テストが存在。**not-started は 0**
- 「欠陥を再導入すると失敗する負例テスト」を持つ症状: **12/12**
- serialized reference 監査: `SerializedAssetReferenceAuditTests` が Game/Scenes 全 Scene + Assets/Game 配下 Prefab 全件 + EditorBuildSettings カバレッジ + Scene 必須バインディングを検査。Stage B で `StageBRemovedUiFiles_StayDeleted` / `StageBRemovedFlags_StayRemoved` を追加し旧 UI 経路の再導入を検出可能化
- `.cs` の `.meta` 欠落は `script/qa-preflight.sh` が FAIL にする
- RC 包括検証（freeze SHA `86a843ba`）: EditMode 2,090/2,090、PlayMode 6/6、web 全件、macOS build + bundleVersion、Win64 ZIP+SHA-256、実機 AutoTest 32/32（score 2,000,000 v4）、ノーツ移動 before/after 実測 — `docs/evidence/rc-0.2.0/README.md`

## 5. 未確認・残件の正直な一覧（release 観測 / 人間受入）

1. **実機聴取**: #730 クリック音源の実際の聞こえ（素材・バインド・ライセンス証跡は完備。Issue open のまま人間受入待ち）
2. **実描画の人間レビュー**: #731 判定軌道の実座標、#732 プレビュー非 MISS 表示、#734 Pause/曲切替、#735/#760 ノーツ白色・歌詞帯、#737 全解像度 SS、#738 実機遷移、#743→#748–750 全画面観測差異 — AutoTest の画面 PNG (`docs/evidence/739-noteprobe/86a843ba/*.png`) をレビューへ
3. **Windows 実機**: Win64 ZIP は生成・メディア非含有を検査済みだが、起動・プレイの実行検証は Windows 環境が必要
4. **本番サーバ**: 対象 SHA の Worker デプロイ後に E9（v4 100 万超受理・ランキング分離）を本番 API で実証 — release 公開と同時に実施
5. **パッケージ最終 hash 照合**: 公開直前に実 ZIP の SHA-256 を再照合
