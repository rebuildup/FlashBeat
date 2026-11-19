# 要求トレーサビリティ行列 (Issue #744)

Epic [#728](https://github.com/rebuildup/FlashBeat/issues/728) の「オーナー要求 → 正本 → 実装 → 独立 oracle / 負例 → 検証レベル → 候補証跡」の連鎖を 1 表に固定する。**テストが緑であることと要求が満たされたことを分ける**のが目的であり、実機・性能・視覚の未観測項目は `未確認` のまま残す（`未測定を PASS にしない`、正本: `docs/operations/development-convergence.md`）。

- 対象ブランチ: `release-0-2-0`（本書作成時 HEAD `75a2f192` + #744 作業分）
- 検証レベルは development-convergence §2 の L0（局所）/ L1（cheap CI）/ L2（Unity batch compile + EditMode）/ L3（candidate freeze 後の包括検証）に対応
- 「負例」列は **欠陥を意図的に再導入した時に失敗するテスト** の有無。`—` は未着手を意味し、緑を装わない
- 証跡状態: `code-verified` = 実装 + 自動テストが release ブランチにあり局所検証済 / `unverified` = 自動化で担保できない残件あり / `not-started` = 未着手

## 1. #728 症状 → チケット → 検証の対応

| # | 症状（オーナー観測） | Issue | 実装（PR / commit） | 正本・仕様 | 独立 oracle / テスト | 負例（再導入で失敗） | 検証レベル | 証跡状態 |
|---|---|---|---|---|---|---|---|---|
| 1 | キークリックがピープ音（hitSound/clickSound 同一 GUID） | #730 (open) | PR #746 `42f56556`, `34e4dbe4` | `Judge.cs` の hit/click 分離契約 | `GameSceneSoundBindingTests`（3 件）+ `SerializedAssetReferenceAuditTests`（#744 で全 Scene/Prefab へ一般化） | 同一 GUID 再代入・GUID 解決不能・Audio 以外への差し替えで失敗 | L0/L2（EditMode） | code-verified。**音源の由来・配布許可の証跡と実機聴取は未確認**（Issue open のまま） |
| 2 | Option プレビュー判定が全 MISS | #732 (closed) | PR #755 `0f001169` | `V6OptionPreviewRig` のオートプレイ + 時計リベース契約 | `V6OptionPreviewClockTests`（`AutoPlay_ProducesEveryJudgementKind` 等 3 件）、`V6OptionPreviewExperienceTests`（判定・歌詞・セッション非汚染） | `Clock_SongChange_RebasesWithoutMissBurst` は旧実装の MISS バーストを直接再現する反例 | L0/L2 + L3（実描画確認） | code-verified。実機での非 MISS 表示は未確認 |
| 3 | 判定アニメーションが旧軌道（上→落下停止） | #731 (closed) | PR #751 `4ea76c13` | `docs/design/v6/review-2026-11-11-decisions.md` §10.3、`docs/architecture/v6-ui.md`、`docs/ui/v6-screen-contract.md` を要求から再制定 | `HudFeedbackTests.VerdictPose_*`（時系列境界・中間状態）、`V6RenderParityTests`（本編/プレビュー同一軌道） | `VerdictPose_RejectsOldSpawnHighFallToRestPath` が旧軌道を明示拒否 | L0/L2 + L3（実描画座標） | code-verified。実描画スクショによる座標検査は未確認 |
| 4 | Option キー設定の矢印がテキストでテーマ不一致 | #733 (open) | — | 未制定（#733 でデザイントークン準拠アイコンへ） | — | — | — | **not-started** |
| 5 | 歌詞パネルが歌詞有無で出没 | #734 (closed) | PR #754 `e7d16e34` | `PlaySession.LyricsEnabled` を唯一の visibility policy に固定 | `V6GameHudViewTests.Lyrics_*`（`BoxFollowsOnlyTheSettingDuringPlayback`、`KeepTheBoxWhenTheChartHasNoLyrics`、`BoxStaysVisibleBeforeLyricDataArrives` 等） | 設定 OFF で本文に関わらず非表示 / 歌詞なし・空区間でも枠が残ることを検査 | L0/L2 + L3 | code-verified。Pause/曲切替を含む実機確認は未確認 |
| 6 | ノーツが暗い（Shade/Shader/合成） | #735 (open) | — | 未制定 | — | — | — | **not-started** |
| 7 | Pause と Option が別実装・プレビュー不一致 | #733 (open) | — | #733 で共通設定モデル + 共通 UI 機構へ | — | — | — | **not-started** |
| 8 | コンボ追加スコアが読めない + 100 万超の意味論 | #736 (closed) / 残件 #758 (open) | PR #757 `d148f138`、ADR-0033、D1 migration `20261119162405` | ADR-0033（v4: 判定ベース上限 100 万 + 加算ボーナス）、`contracts/parity/score-vectors.json` | `ScoringV4Tests`（5 件）、`ParityFixtureTests.ScoreVectors_MatchEveryVersion`、`ScoringV2Tests` 境界、`RankingSubmissionJudgementSumTests`、`JudgeSettlementInvariantTests`、web `tests/parity-score.test.ts` | v4 式の改変・v3 暗黙改算・ボーナス未確定時の先行加算で失敗。web/Unity の parity vector ずれも検出 | L0/L1（web 単体）/ L2（EditMode）/ L3（サーバ受理・ランキング分離の実証） | code-verified（Unity/Web/契約）。**サーバ側 100 万超受理・グレード閾値・ゲージ表示は #758 で未完** |
| 9 | Result の SS グレードでレイアウト崩れ | #737 (closed) | PR #752 `4497513f` | `V6ResultLogic`/`V6ResultStats` の fit 規約 | `V6ResultGradeFitTests`（`GradeLetter_AllGrades_FitInsideGradeBox`、`SS_ShrinksToFit`、`NumericTexts_ExtremeValues`、`RegisterModal_*`） | SS で字が箱をはみ出す変更・極端桁数で失敗 | L0/L2 + L3（全解像度の描画比較） | code-verified。実描画比較は未確認 |
| 10 | Result→Select( Space )で選曲コンテキスト初期化 | #738 (closed) | PR #747 `e89a9c6d` | `SelectReturnContext`（唯一の SelectionContext） | `SelectReturnContextTests`（復元・1 回消費・fallback・本番配線のソース検査 `ProductionWiring_ResultToSelectAndTitleBoundary`） | 復元を Record のみ/Consume しない/曲削除時 fallback なしで失敗 | L0/L2 + L3 | code-verified。実機遷移確認は未確認 |
| 11 | 全画面の細かい UI 崩れ（多数） | #743 (closed) → #748/#749/#750 (open) | 監査のみ完了。修正は子 Issue へ分割 | #748: 解像度/pan、#749: テキスト溢れ、#750: グリフ/色トークン | 既存 `V6CompactScreen`/`V6UiProfile`/`V6ScreenAudit` 系 + 各子 Issue で追加予定 | 子 Issue 未着手のため負例なし | L0/L2（子 Issue 毎）+ L3 | 監査 code-verified / **修正群は not-started** |
| 12 | ノーツ移動がリフレッシュより粗い（体感 <20fps、未計測） | #739 (closed) | PR #753 `8d2d55a5`（`NoteMotionProbe`、`MediaTimeline` 描画時計） | `MediaRenderClock` の単調・補間契約 | `MediaRenderClockTests`（量子化時計の平滑化・停滞・逆シーク・ギャップの 6 件） | 量子化時計へ戻す変更で `AQuantizedMediaClockStillRendersSmoothly` が失敗 | L0/L2 + **L3 実測必須** | code-verified（ロジック）。**実測 fps / frame pacing / jitter は未確認 — RC で計測必須** |

## 2. 構造的要件（#728 後半）

| 要件 | Issue | 状態 | 正本 / 証跡 | 未確認事項 |
|---|---|---|---|---|
| V5/V6 恒久二重構造の廃止・単一実装 | #729 (closed) → #740 (open) | 仕様のみ確定 | `specs/729-unified-runtime-architecture/spec.md`（PR #745）、ADR 群 | #740 の実装・削除条件の履行は未着手 |
| 命名・directory・重複定数の機械検証 | #740 (open) | not-started | `code-naming` Skill 原則 | 検証器自体が未整備 |
| Web 現行スタック維持 + modern-web-guidance 評価 | #741 (closed) | code-verified | `docs/adr/ADR-0030-web-guidance-and-studio-structure.md`、`web/AGENTS.md` 更新、`request-guard.ts`/`catalog.ts` 重複統合 | E2E/visual は RC |
| Web 情報優先度・導線再設計 | #742 (closed) | code-verified | `docs/design/web-studio-ia.md`、`sidebar.tsx` 再編、`sidebar.test.tsx` | 実ブラウザでの操作性確認は RC |
| 「テスト緑 ≠ 要求充足」の QA 連鎖再構築 | #744 (本 Issue) | 本書 + 監査テスト + preflight で対応 | 本ディレクトリ、`SerializedAssetReferenceAuditTests`、`script/qa-preflight.sh` | RC 実施は別途 |

## 3. チケット単位の状態一覧

| Issue | タイトル要約 | Issue 状態 | release 上の証跡 | 証跡状態 |
|---|---|---|---|---|
| #729 | 単一アーキテクチャ仕様 | CLOSED | PR #745 `specs/729-unified-runtime-architecture/spec.md` | 仕様確定のみ（実装は #740） |
| #730 | クリック音源バインド | OPEN | PR #746 + `GameSceneSoundBindingTests` + #744 監査一般化 | code-verified / 権利証跡・実機聴取 未確認 |
| #731 | 判定ポップ軌道 | CLOSED | PR #751 + `HudFeedbackTests`/`V6RenderParityTests` | code-verified / 実描画 未確認 |
| #732 | プレビュー全 MISS | CLOSED | PR #755 + `V6OptionPreviewClockTests`/`ExperienceTests` | code-verified / 実機表示 未確認 |
| #733 | Option/Pause 統一 + 矢印 | OPEN | — | not-started |
| #734 | 歌詞パネル可視性固定 | CLOSED | PR #754 + `V6GameHudViewTests.Lyrics_*` | code-verified / 実機 未確認 |
| #735 | ノーツ白色の最終合成 | OPEN | — | not-started |
| #736 | scoring v4 | CLOSED | PR #757 + `ScoringV4Tests`/`ParityFixtureTests`/ADR-0033/D1 migration/web parity test | code-verified / #758 残件・サーバ実証 未確認 |
| #737 | Result SS レイアウト | CLOSED | PR #752 + `V6ResultGradeFitTests` | code-verified / 実描画 未確認 |
| #738 | Result→Select 復元 | CLOSED | PR #747 + `SelectReturnContextTests` | code-verified / 実機 未確認 |
| #739 | ノーツ移動パフォーマンス | CLOSED | PR #753 + `MediaRenderClockTests` + `NoteMotionProbe` | code-verified / **実測性能 未確認** |
| #740 | V5/V6 廃止・命名整理 | OPEN | — | not-started |
| #741 | Web architecture | CLOSED | PR #756 + ADR-0030 | code-verified / E2E RC |
| #742 | Web UX 導線 | CLOSED | PR #756 + `web-studio-ia.md` + sidebar tests | code-verified / 実ブラウザ RC |
| #743 | Unity 全画面 UI 監査 | CLOSED | 監査完了 → #748/#749/#750 へ分割 | 監査のみ |
| #744 | QA 再構築（本 Issue） | OPEN | 本書、`SerializedAssetReferenceAuditTests`、`script/qa-preflight.sh`、`release-candidate-gate.md` | — |
| #748 | HUD 解像度/pan 根本修正 | OPEN | — | not-started（#743 由来） |
| #749 | テキスト溢れ系統修正 | OPEN | — | not-started（#743 由来） |
| #750 | グリフ/色トークン統一 | OPEN | — | not-started（#743 由来） |
| #758 | scoring v4 グレード閾値・ゲージ・D1 残件 | OPEN | — | not-started（#736 由来） |

## 4. カバレッジ集計（本書作成時点）

- #728 症状 12 項目中: **code-verified 8**（#1,2,3,5,8,9,10,12）、**not-started 3**（#4,6,7 — #733 が 2 症状を兼務）、**監査済み・修正待ち 1**（#11 → #748–750）
- 「欠陥を再導入すると失敗する負例テスト」を持つ症状: **8/8 の code-verified 分すべて**。ただし #12 はロジックのみで実測性能は別途必須
- serialized reference 監査: #730 の単一 Scene 検査を `SerializedAssetReferenceAuditTests` で **Game/Scenes 全 7 Scene + Assets/Game 配下 Prefab 全件 + EditorBuildSettings カバレッジ + Scene 必須バインディング**へ一般化（#744 で追加。初回実行で MakeFileScene の dangling script GUID `6f0c8a32…` を検出・修正済み）
- `.cs` の `.meta` 欠落（ScoringV4Tests.cs, V6ResultGradeFitTests.cs）も #744 で補完 — `script/qa-preflight.sh` が今後の欠落を FAIL にする

## 5. 未確認・残件の正直な一覧（RC で必ず回収）

1. **実機・実描画**: #1 聴取、#2 プレビュー非 MISS 表示、#3 判定軌道の実座標、#5 Pause/曲切替、#9 全解像度描画比較、#10 実機遷移
2. **性能実測**: #12 の fps / frame time / jitter（`NoteMotionProbe` + `script/mac-fbperf.sh` 系ハーネス）
3. **サーバ・契約**: #8 の 100 万超スコア受理・ランキング分離・D1 適用（#758）
4. **権利・素材**: #1 クリック音源の由来・配布許可証跡
5. **Web**: #741/#742 の E2E・visual（`workflow_dispatch` の browser-e2e）
6. **未着手**: #733（症状 4,7）、#735（症状 6）、#740（構造）、#748–750（症状 11 の修正）、#758（症状 8 の残件）
