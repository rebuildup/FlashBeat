# v5 (旧 UI) 実装の参照状況 — Issue #385 Phase 3

v6 への移植が済んだ画面では旧 v5 View は「フォールバック経路」として残っている。
削除は別 Issue の判断だが、**どの型がどこから生きて参照されているか**をここに固定する。
`Assets/Tests/Editor/V5LegacyReferenceAuditTests.cs` が配線サイトをソース走査で
ピン留めしており、参照が消えた/増えたときにテストが落ちてこの表の更新を促す。

## 型ごとの状態 (2026-11-15 時点, Issue #497 削除反映後)

| v5 型 | v6 置き換え | 生きている参照 (UI/V5 以外) | 判定 |
|---|---|---|---|
| ~~`V5.TitleView`~~ | V6TitleView | **削除済み** (Issue #497)。生成経路ゼロだったため除去。`TitleView.CatalogLabel` の文言契約は `V6TitleLogic.CatalogStatusText` が継承し `V6TitleLogicTests` が固定 | 削除済み |
| `V5.SelectView` | V6SelectView | `SelectSceneManager` (`V6UiMode.SelectScreen == false` の退避先) | フォールバックとして存続 |
| ~~`V5.OptionView`~~ | V6OptionView | **削除済み** (Issue #497)。`AdjustVolume`/`FormatVolume`/`FormatSpeed`/`CycleFlashSpeed`/`WrapIndex`/`KeyLayoutLabel` の契約は `V6OptionLogic` + `KeyConfigUI.IsDefaultLayout` が継承し `V6OptionLogicTests`/`KeyConfigLayoutTests` が固定 | 削除済み |
| ~~`V5.TitleLayout`~~ | なし (v6 は固定 1728×972 フレーム) | **削除済み** (Issue #497)。v5 の可変レイアウト計算で、v6 側に相当物なし | 削除済み |
| `V5.ResultView` | V6ResultView | `ResultSceneManager` (`V5.ResultView.Build` + `RankFor`/`NormalizeName`/`Snapshot`) | フォールバック + 純関数の借用 |
| `V5.GameHudView` | V6GameHudView | `V5.GameHudRuntime` 内部 (`V6UiMode.GameHud == false` のときだけ生成) | フォールバックとして存続 |
| `V5.GameLoadingView` | V6LoadingView | `V5.GameHudRuntime` 内部 (同上) | フォールバックとして存続 |
| `V5.GameReadyView` | v6 HUD Start プロンプト | `V5.GameHudRuntime` 内部 + `AutoTestV6Screens` (コメント/監査) | フォールバックとして存続 |
| `V5.GameErrorView` | v6 Loading エラー表示 | `V5.GameHudRuntime` 内部 + `MusicManager`/`SongEndStateMachine` (コメント) | フォールバックとして存続 |
| `V5.PauseMenuView` | V6PauseView | `V5.GameHudRuntime` 内部 + `V6GameRuntime` (legacyPause 検索) + `GameSceneManager` (cachedPauseMenu) + `PauseGate` | フォールバックとして存続 |
| `V5.OpeningView` | なし (未移植) | `OpeningSceneManager` (`OpeningView.Build`) | **現役** (v6 化は別 Issue) |

## 補助型 (View ではないが v5 UI 基盤)

- `V5Ui` / `V5GameUi` / `V5Tokens` / `UISizeProfile` / `V5Decor` 等: v6 側も一部を
  経由して使う (`V5GameUi.NewCanvas` は v6 Canvas の生成にも使われ、
  "V6" 名は登録対象外という契約が `V5LegacyV5CanvasHideTests` で固定済み)。
- `KeyGuideUI` / `UpdateNoticeUI`: シーン配置の MonoBehaviour。v6 では
  `V6UiMode.HideLegacyKeyGuide` / `HideLegacyUpdateNotice` が隠す側。
- `SelectViewLogic` / `ResultView` の静的関数 (`ScoreMax`, `DurationText`,
  `GradeOf`, `RankFor`, `NormalizeName`): v6 の Logic 層が**値の正本として借用**
  している (`V6SelectDataLogic`, `V6ResultLogic`)。View ごと消す場合は
  これらの関数の移設が先に必要。

## Phase 3 で行った構造変更

- `V6UiMode.HideLegacyV5Canvases`: 走査を v5 Canvas 生成番号の変化時 +
  `SafetyScanIntervalFrames` (120f) ごとの保険に限定 (#396 の TitleUi と同方式)。
- `V6UiMode.HideLegacyUpdateNotice`: `FindObjectsByType<Canvas>` の走査を
  `UpdateNoticeScanIntervalFrames` (30f) に 1 回までに限定。見つかるまで
  毎フレーム呼ばれる契約 (戻り値) は維持。
- `HideLegacyTitleUi` は #396 で既に同方式済み。`HideLegacyKeyGuide` /
  `HideLegacyV5CanvasesOnce` はシーン構築時の 1 回呼び出しで変更不要。
- `V6GameRuntime.LateUpdate` の `legacyPause` 監視は legacyPause 用の
  別経路として維持 (PauseMenuView は Update で自開き直しし得るため)。
