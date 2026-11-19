# v5 (旧 UI) 実装の参照状況 — Issue #385 Phase 3 / #740

v6 への移植が済んだ画面では旧 v5 View は「フォールバック経路」として残っていたが、
**Issue #740 でフォールバック経路ごと削除した**。製品の画面実装は v6 の 1 系統のみ。
`Assets/Tests/Editor/V5LegacyReferenceAuditTests.cs` が削除状態と残存言及を
ソース走査でピン留めしており、変化があったときにテストが落ちてこの表の更新を促す。

## 型ごとの状態 (Issue #740 時点)

| v5 型 | v6 置き換え | 状態 |
|---|---|---|
| ~~`V5.TitleView`~~ | V6TitleView | 削除済み (Issue #497)。`TitleView.CatalogLabel` の文言契約は `V6TitleLogic.CatalogStatusText` が継承 |
| ~~`V5.SelectView`~~ | V6SelectView | **削除済み (#740)**。`SelectSceneManager` は v6 のみ生成。`CanFetchOnlineRanking` 相当の判定は `V6SelectDataSource.RankingKeyOf` が継承 |
| ~~`V5.OptionView`~~ | V6OptionView | 削除済み (Issue #497)。契約は `V6OptionLogic` + `KeyConfigUI.IsDefaultLayout` が継承 |
| ~~`V5.TitleLayout`~~ | なし | 削除済み (Issue #497) |
| ~~`V5.ResultView`~~ | V6ResultView | **削除済み (#740)**。`RankFor`/`NormalizeName` は `V6ResultRules`、`FormatScore`/`DeltaLabel` は `V6ResultLogic` が継承。`Snapshot`/PlayerPrefs キー "V5PlayerName" の互換は `V6ResultLogic.LocalPlayerNamePrefsKey` / `V6ResultRegistration` が維持 |
| ~~`V5.GameHudView`~~ | V6GameHudView | **削除済み (#740)** |
| ~~`V5.GameLoadingView`~~ | V6LoadingView | **削除済み (#740)** |
| ~~`V5.GameReadyView`~~ | v6 HUD Start プロンプト | **削除済み (#740)** |
| ~~`V5.GameErrorView`~~ | v6 Loading エラー表示 | **削除済み (#740)** |
| ~~`V5.PauseMenuView`~~ | V6PauseView | **削除済み (#740)**。`GameSceneManager.LegacyBackOpensMenu` は v6 ポーズの有無だけを見る 1 引数に縮小 |
| ~~`V5.GameUiExclusivity`~~ | `GamePlayPhaseService` / `GameHudController` | **削除済み (#740)**。フェーズ正本は `HudPlayPhase`、旧歌詞/ロード表示の名前判定は `GameHudController` が継承 |
| `V5.OpeningView` | なし (未移植) | **現役** (v6 化は別 Issue) |

## 補助型 (View ではないが v5 UI 基盤 / 共有ロジック)

- `V5Ui` / `V5GameUi` / `V5Tokens` / `UISizeProfile` / `V5Decor` / `ShortcutKeyCap` /
  `GraphicVisibility` / `ActionKeyHint` / `KeyGuideUI` 等: v6 側も経由して使う
  インフラ層。v6 Canvas の生成自体が `V5GameUi.NewCanvas` を使う契約は維持
  (`V6LegacyV5CanvasHideTests` で固定済み)。責務別の移設は別 Issue の判断。
- `SelectViewLogic` → **`SongScoreLogic`** (`Songs/`、グローバル名前空間):
  `ScoreMax` / `GradeOf` / `DurationText` のみ残し、フィルタ/ソート/スクロール窓は
  v6 側 (`V6SelectDataLogic` / `V6SelectViewLogic`) が担うため撤去 (#740)。
- `ResultView` の静的関数 → `V6ResultRules` (`RankFor`/`NormalizeName`) と
  `V6ResultLogic` (`FormatScore`/`DeltaLabel`) へ移設 (#740)。
- `GameHudLogic` → `FlashBeat.UI.V6.Game.Hud.GameHudLogic` へ移設 (#740)。
  v5 HUD 専用のレール幅/コンボボーナス近似式は v6 HUD (`V6HudScore`/`V6HudLogic`)
  が担うため撤去。残るのは `RankText` / `FormatScore` のみ。
- `PlaySession` → `Songs/PlaySession.cs` (グローバル名前空間) へ移設 (#740)。
  Select→Game の「このプレイの設定」セッション状態で、v6 各画面と
  `V6SettingsCatalog` が共有する。`PlaySessionSnapshot` (Songs/) が
  曲/チャート識別を別途担う。
- `GameHudRuntime.cs` 内の `GameHudController` / `GamePlayPhaseService`:
  クラス名は v5 時代のままだが、中身は「旧 UI の非表示・Raycaster 無効化・
  パイプライン状態公開」のランタイムサービス。v6 表示の生存に必要なため残置
  (改名は別 Issue)。

## 残っている V6UiMode フラグの扱い

`V6UiMode` の画面選択フラグ (`GameHud` / `SelectScreen`) は常に true で
フォールバック分岐は #740 で除去済み。残るフラグは「旧シーン UI (レガシー
Canvas / KeyGuideUI / UpdateNoticeUI) を隠すか」の互換フラグと、
`Playfield` (3D/2D レンダラ選択。UI 重複ではない) など。
詳細は Issue #740 の報告を参照。
