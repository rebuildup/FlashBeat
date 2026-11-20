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

## 残っているフラグの棚卸し (Stage B 完了後)

`V6UiMode` に残るのは実レンダラ / 演出 / 正本デザイン決定のライブ分岐 **7 個だけ**。
画面選択フラグと常時固定の互換フラグは Stage B で全て畳んだ。

| フラグ | 既定 | 分岐の実態 | 判定 |
|---|---|---|---|
| `Playfield` | false | `Notes.cs` / `PlayfieldLaneGuides.cs` / `V6GameRuntime` / AutoTest `-playfield` / V6PlayfieldNotesTests・V6NoteRenderTests が両値を検証 | **生きたレンダラ選択** (出荷は 3D)。UI 重複ではない → 残す |
| `VideoDim` | false | 動画減光レイヤ | 生きた演出フラグ (テストが両値を検証) → 残す |
| `VerdictRow` | true | `Judge.SuppressJudgementEffects` 連動、VerdictEventPathTests が両値を検証 | 生きた機能フラグ → 残す |
| `TitleTabs` / `TitleLegend` | true | V6 Title の表示分岐 | 正本デザイン決定フラグ。V6TitleLayoutTests が値を固定 → 残す |
| `TitleFooterActions` / `TitleHeaderTitle` | false | V6 Title の非表示要素 | 同上 → 残す |

畳んだフラグ (復活防止は `V5LegacyReferenceAuditTests.StageBRemovedFlags_StayRemoved` が固定):

| フラグ | Stage B での処理 |
|---|---|
| `V6UiMode.GameHud` | 撤去。`GameSceneManager.Start` は `HideLegacyKeyGuide()` 無条件、`ApplyTextPanelVisibility` と歌詞更新系は削除 (旧 TextPanel は `LegacyHideNames` の名前指定で常時非表示)、`V6GameRuntime.Ensure` / `V6PauseRuntime.Ensure` のガード撤去 |
| `V6UiMode.SelectScreen` | 撤去。`AutoTestPlay` は v6 Select 前提に |
| `V6UiMode.LegacyOverlaysHidden` | 撤去。`UpdateChecker` の Option 適用ボタン生成経路ごと削除、`ResultRankingOverlay` / `KeyConfigOptionButton` / `MediaCacheOptionPanel` はファイルごと削除 |
| `V6PauseView.Enabled` | 撤去。`V6PauseRuntime` は無条件起動。`GameSceneManager` の Esc → MenuPanel 経路 (`LegacyBackOpensMenu` / `OpenMenu` / `CloseMenu` / `SwitchPanel`) も削除 |
| `V6TitleView.Enabled` | 撤去。`TitleSceneManager.BuildLegacyTitle` / `HandleLegacyInput` / `CreateKeyGuide` を削除し `BuildV6Title` 無条件 |
| `V6ResultView.Enabled` | 撤去。`ResultSceneManager` は `BuildV6View` 無条件、`ResultRankingOverlay` ファイルごと削除 (v6 の登録モーダルとランキング表示は `V6ResultModal` / `V6ResultEntry` が担う) |

### Stage B で削除したファイル / 経路

- `UI/ResultRankingOverlay.cs` — 旧リザルトのランキングボード + 自動登録モーダル。
  v6 は `V6ResultView` / `V6ResultModal` / `V6ResultEntry` が同等機能を持つ。
  `ResultSceneManager` が起動時に `enabled=false` にしていたため実質死に経路だった。
- `UI/KeyConfigOptionButton.cs` / `UI/MediaCacheOptionPanel.cs` — 旧 Option 画面の
  コード生成ボタン/パネル。Option は v6 (`V6OptionView`) が唯一の実装。
- `UI/MediaLoadingOverlay.cs` — 旧メディア読み込み表示。`MusicManager` の生成経路を
  撤去した時点で製品から到達不能になったためファイルごと削除。
  `AutoTestLoading` は `V6LoadingView` を対象に書き換え、
  `MediaLoadingOverlayTests` は削除 (v6 側は `V6LoadingViewTests` 等が担保)。
- `GameLoadingRoute` (旧 `LoadingOverlay.cs` 内) — 旧/新ローディングの所有者分岐。
  `LoadingOverlay` 本体は生きている (後述)。
- `GameSceneManager` の旧 UI API — `OpenMenu` / `CloseMenu` / `SwitchPanel` /
  `TextPanelOn` / `TextPanelOff` / `MenuTUp` / `MenuTDown` / `playSound` /
  `ApplyLyricsVisibility` / `LegacyBackOpensMenu` / 歌詞更新系
  (`UpdateLyrics` / `HideLyrics` / `ApplyLyricLine` / `ClearLyricText`) /
  シリアライズ欄 (`MenuPanel` / `MenuButton` / `TextPanel` / `menuT` / `sound` /
  `furiganaText` / `mondaiText` / `romajiText` / `OnText` / `OffText` /
  `_animator` / `Menu`)。歌詞データ (`InputJson`) は v6 HUD (V6HudLyrics) が読む。
- `GamePlayPhaseService.KeepLegacyMenuClosed` / `RestoreLyricsVisibility` —
  Esc 経路の撤去で不要になった旧メニュー閉じ処理。
- `TitleSceneManager.BuildLegacyTitle` / `HandleLegacyInput` / `CreateKeyGuide` —
  旧タイトル UI の入力経路。
- `UpdateChecker` の Option 適用ボタン — `CreateOptionApplyButton` /
  `CreateOptionButtonNextFrame` / `OnSceneLoaded` / `UpdateApplyButtonMarker` /
  `optionApplyButton`。更新導線は Title ヘッダー + `UpdateNoticeUI.Trigger` →
  `RequestApply` が担う。`ApplyStagedUpdateNow` (public 入口) は残す。
- `KeyConfigUI` の F2 ヒント (`BuildHint` / `UpdateHint` / `hintLabel` / `hintCap`) と
  `FindJapaneseFont` の public 化 (唯一の外部利用者 KeyConfigOptionButton が消えた)。

### Stage B で残したものと理由

- `KeyGuideUI` クラス — 製品の生成経路 (`KeyGuideUI.Create`) は全て撤去したが、
  `V6UiMode.HideLegacyKeyGuide()` が stray インスタンスを掃除する安全網として残す。
  EditMode テスト (`KeyGuideTests` / `KeyGuideInputTests` / `KeyBindingsTrunkTests`)
  も生きている。削除は別 Issue の判断 (隠す対象 UI 自体の削除は Stage B の範囲外)。
- `UpdateNoticeUI` — v6 Title の更新ボタンが `UpdateNoticeUI.Trigger()` を呼ぶ
  生きた依存。`LegacyOverlaysHidden` ガードを外し、見た目のピル非表示は
  `V6UiMode.HideLegacyUpdateNotice()` が担う (コンポーネントは無効化しない)。
- `LoadingOverlay` — シーン遷移 (`Initiate.Fade` / `SelectSceneManager`) の
  全画面ローディングとして現役。曲準備の表示だけが V6LoadingView に移った。
- `LoadingOverlayLogic.MapProgress` — 呼び出し側は無くなったが、
  `LoadingOverlayLogicTests` が契約として固定している純粋関数。残置。
- `GameScene.unity` のシーン配置オブジェクト — `MenuPanel` / `MenuButton` /
  `TextPanel` 等は削除せず、`GamePlayPhaseService.HideLegacyHud` が名前指定で
  常時非表示に保つ (`LegacyHideNames` に `TextPanel` を追加)。ボタンの
  onClick に残る削除済みメソッド (`OpenMenu` 等) への永続呼び出しは
  非表示・Raycaster 無効化済みで到達不能。シーン YAML の手編集は行わない。
- `GameHudController` / `GamePlayPhaseService` (`UI/V5/GameHudRuntime.cs`) —
  クラス名は v5 時代のままだが、中身は「旧 UI の非表示・Raycaster 無効化・
  パイプライン状態公開」のランタイムサービス。v6 表示の生存に必要 (改名は別 Issue)。

### Stage B の完了条件 (達成)

`V6UiMode` に残るのは `Playfield` / `VideoDim` / `VerdictRow` /
Title 系 4 フラグ (計 7)。画面選択フラグと常時固定の互換フラグは消え、
`script/compile-check.sh` 全 4 アセンブリ 0 エラー。
EditMode 全量実行は実行環境のディスク容量不足で未実施 (下記「検証」参照)。
