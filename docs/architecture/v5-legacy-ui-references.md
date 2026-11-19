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
  scoringVersion 1–3 のグレード帯 dispatch は `V6GradeLogic` が `RankFor` を
  経由して担う (#736)。
- `V6VolumeSettings` は v5 `PauseMenuView.ApplyVolumes` と同じ即時反映の
  挙動をコメントで固定しており、監査テストのピン留め対象 (#733)。
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

## 残っているフラグの棚卸しと Stage B 移行計画

### V6UiMode フラグ一覧 (Issue #740 時点)

| フラグ | 既定 | 分岐の実態 | 判定 |
|---|---|---|---|

| `GameHud` | true | `GameSceneManager.Start` (KeyGuideUI 生成の else)、`ApplyTextPanelVisibility` (`!GameHud` で旧歌詞パネル抑止)、`GameLoadingRoute.Current/UsesLegacyOverlay`、`V6GameRuntime.Ensure`、`V6PauseRuntime.Start`、テスト 2 件が代入 | **常に true の製品フラグ。else 側は全て到達不能** → Stage B で畳む |
| `SelectScreen` | true | `AutoTestPlay` の確認分岐のみ (製品コードの分岐は #740 で撤去済み) | **常に true** → Stage B で畳む |
| `Playfield` | false | `Notes.cs` / `PlayfieldLaneGuides.cs` / `V6GameRuntime` / AutoTest `-playfield` / V6PlayfieldNotesTests・V6NoteRenderTests が両値を検証 | **生きたレンダラ選択** (出荷は 3D)。UI 重複ではない → 残す |
| `VideoDim` | false | 動画減光レイヤ | 生きた演出フラグ (テストが両値を検証) → 残す |
| `VerdictRow` | true | `Judge.SuppressJudgementEffects` 連動、VerdictEventPathTests が両値を検証 | 生きた機能フラグ → 残す |
| `TitleTabs` / `TitleLegend` | true | V6 Title の表示分岐 | 正本デザイン決定フラグ。常に true だが V6TitleLayoutTests が値を固定 → 畳むかは要判断 |
| `TitleFooterActions` / `TitleHeaderTitle` | false | V6 Title の非表示要素 | 同上 (常に false) |
| `LegacyOverlaysHidden` | true | `ResultRankingOverlay` / `KeyGuideUI` / `UpdateNoticeUI` の隠蔽 | 旧オーバーレイ互換フラグ。常に true → Stage B 候補 |
| `V6PauseView.Enabled` | true | `V6PauseRuntime.Start`、`GameSceneManager` Esc 契約、`V6PauseRuntimeTests` | 常に true → Stage B |
| `V6TitleView.Enabled` | true | `TitleSceneManager.Start` (`BuildLegacyTitle` else 残存)、`KeyConfigUI`、AutoTest 2 件 | 常に true。**else の BuildLegacyTitle は旧タイトルの死に経路** → Stage B |
| `V6ResultView.Enabled` | true | `ResultRankingOverlay.Update` 早期 return | 常に true → Stage B |


### Stage B 移行計画 (未実施 — Unity 実機検証が必要なため分離)

常に true/false に固定されたフラグを 1 コミットで畳む。相互に連動するため
**GameHud / SelectScreen / 3 つの Enabled / LegacyOverlaysHidden を同時に**
畳まないと中途半端な状態になる。手順:

1. `GameSceneManager.Start`: `if (GameHud) HideLegacyKeyGuide(); else KeyGuideUI.Create(...)`
   → `V6UiMode.HideLegacyKeyGuide()` 固定 (KeyGuideUI.Create の他の呼び出し元を確認)。
2. `GameSceneManager.ApplyTextPanelVisibility`: 旧歌詞 TextPanel は v6 HUD が
   歌詞を描くため**常に非表示**。`!GameHud` 項を消すと show が true になり
   二重表示するので、メソッドは「常に隠す」強制 (SetActive(false)) に縮退させる。
3. `GameLoadingRoute`: `Current` → `GameLoadingScreen.V6` 固定、
   `UsesLegacyOverlay` → `false` 固定。`NotesManager` / `MusicManager` の
   `showLegacyOverlay` / `showLegacyMediaOverlay` 分岐と LoadingOverlay.Show /
   MediaLoadingOverlay の生成・破棄経路を撤去 (呼び出し側の null ガードも整理)。
4. `V6GameRuntime.Ensure` / `V6PauseRuntime.Start` の `GameHud` ガード撤去。
5. `V6PauseView.Enabled` 撤去 → `V6PauseRuntime` は無条件起動、
   `GameSceneManager` の `LegacyBackOpensMenu` は引数なし (常に false =
   レガシー MenuPanel の Esc 経路も撤去候補。MenuPanel 自体の生死要確認)。
6. `V6TitleView.Enabled` 撤去 → `TitleSceneManager` の `BuildLegacyTitle` else
   分岐と旧タイトル UI 一式の撤去可否をシーン参照とともに確認してから削除。
7. `V6ResultView.Enabled` 撤去 → `ResultRankingOverlay.Update` の早期 return
   と旧ランキングボード経路の生死を確認。
8. `V6UiMode.SelectScreen` 撤去 → `AutoTestPlay` の分岐を v6 前提に。
9. `LegacyOverlaysHidden` 撤去 → 隠す側を無条件化 (隠す対象 UI 自体の削除は別判断)。
10. テスト更新: `SongEndAcceptanceRig` / `JudgeSettlementInvariantTests` /
    `V6PauseRuntimeTests` / `V6TitleLayoutTests` / `AutoTestPlay` /
    `AutoTestKeyGuide` のフラグ代入・参照を除去。
11. `Playfield` / `VideoDim` / `VerdictRow` / Title 4 フラグは**畳まない**
    (実レンダラ/演出/デザイン決定のライブ分岐。テストが両値を検証している)。

完了条件: `V6UiMode` に残るのは `Playfield` / `VideoDim` / `VerdictRow` /
Title 系 4 フラグ (計 7) で、画面選択フラグと常時固定の互換フラグが
消えていること。Unity batchmode での EditMode 実行と実機スモーク
(Title→Select→Game→Result→Pause) を必須とする。
