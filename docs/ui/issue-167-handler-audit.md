# Issue #167: v5 全画面 handler → state effect 監査表 (Game D)

**目的**: Title → Select → Game → Result → Retry の全シーンで、見えている
ボタン / キー / 設定のすべてについて「handler → state effect」の実連結を
コードで監査し、未連結・空ハンドラ・デッドリンクを修正する。

**対象コミット**: 本ドキュメントが入ったコミット (PR #167 系ブランチ `167`)
**監査方法**: コードリーディング (実機検証は別担当。手順は
[issue-167-ui-acceptance.md](issue-167-ui-acceptance.md) のまま実施できる)。

---

## 1. 監査結果サマリ

v5 UI (PR #179 で実動作化済み) の主要ハンドラはすべて実連結だった。
本監査で見つかったのは **オーバーレイと v5 View の入力分離漏れ** と
**設定範囲の不一致**。すべてコード側で修正した (シーン YAML / Canvas 設定は
#169 担当のため触っていない)。

### 修正した欠陥

| # | 症状 (押した結果) | 原因 | 修正 |
|---|---|---|---|
| F1 | Option 画面でキー設定 (F2 / キー設定ボタン) を開いている間、↑↓←→ / Tab / Enter / Esc が **裏の OptionView も同時に動く**。Esc でキー設定を閉じると同時に Option → Title へ遷移し、Enter では Clear save data がアームする | `KeyConfigUI.SuspendScene` の suspend 対象に v5 の `OptionView` が入っていなかった (TitleView / SelectView / ResultView だけ列挙) | `SuspendScene` に `OptionView` を追加。加えて `KeyGuideUI` / `UpdateNoticeUI` も suspend し、開閉フレームの二重処理は `SceneInputGate` で止める |
| F2 | クラウド曲のリザルトでランキング登録モーダルの名前入力中に、`R` / Space / Enter / Esc が **ResultView に届いてリトライ・遷移が発火する** | `ResultRankingOverlay.SuspendScene` がレガシー `*SceneManager` しか止めておらず、v5 の入力を持つ `ResultView` が生きていた (ResultSceneManager は v5 では入力を持たないため実質無防護) | `SuspendScene` に `TitleView` / `SelectView` / `ResultView` / `KeyGuideUI` / `UpdateNoticeUI` を追加。ボード (F5) は Result に加えて Title / Select でも開くため、**全ボードシーンで** suspend するように変更 |
| F3 | Select 画面の `F2` が **2 系統に割り当て**られていた: `SelectView` (履歴) と `KeyConfigUI` (キー設定)。画面の凡例は「F2 履歴」で、押すたびに挙動が実行順で変わる | キー設定オーバーレイ (#128) の `AllowedScenes` に SelectScene が入ったまま | `AllowedScenes` から `SelectScene` を外した。F2 の意味が画面ごとに一意になる: Title = キー設定 / Select = 履歴 (v5 凡例・受入基準 S9 のとおり) / Option = キー設定。ヒント表示 (UpdateHint) も Title のみに変更 |
| F4 | オーバーレイを **Esc で閉じた同じフレーム**に背面シーンが同じ Esc を処理し、Option → Title へ遷移 / Select → Title へ退避する (実行順依存) | suspend は「開いている間」だけ有効で、閉じたフレームの裏渡りが無い | 新規 `SceneInputGate` (開閉フレームを 1 フレームだけブロック)。`OptionView.Update` / `ResultView.Update` / `SelectView.HandleInput` / `KeyGuideUI.Update` の先頭で見る |
| F5 | エラーダイアログ (動画再生失敗) の表示中に HUD 右下の **ポーズボタンが押せて**、エラーの上にポーズメニューが開く。その状態で Esc を押すとポーズを閉じる代わりに選曲へ戻る | `hud.PauseButton.onClick → pause.Open` が `InputEnabled` (= !error.IsShown) を見ていなかった | `PauseMenuView.Open` が `InputEnabled` を見る (Esc と同じ規則) |
| F6 | ポーズの NoteSpeed 行は 1..30 にクランプされており、Ready / Option (1..50) で 40 にした後ポーズで 1 回 ←→ を押すと **値が 30 まで飛ぶ** | `PauseMenuView.Adjust` が固定の 1..30 を使用 | `NextNoteSpeed` (純粋関数) に切り出し、`OptionSettings.NoteSpeedMin..Max` (1..50) に統一 |
| F7 | 選曲画面のカタログ定期更新が、オーバーレイの suspend で **止まったまま再開しない** (MonoBehaviour 無効化はコルーチンも止す) | `SelectSceneManager` に復帰処理が無い | `OnEnable` で更新ループを再開 (`OnDisable` でハンドルを手放す) |
| F8 | 監査と同時に、`V5UiWiringTests` の 3 件が **追加コミット時点から赤のまま** だった (実装とテストの契約不一致): ① Title の Exit 行を `Menu End` という旧名で探す ② Compact 代表キーを「先頭キー (1)」前提で検査 (実装は文書化済みの「文字キー優先」→ Q) ③ `SelectView` は Canvas を**子**として持つのに `GetComponentInParent<Canvas>()` で探して NRE | テストが実装の階層・ラベル規則と不一致 | テストを実装の契約に合わせて修正: `Menu Exit` / 代表キーは「レーンのキー 1 個」で検査 (1/Q/A/Z のいずれか) / Canvas は子方向から探索し、finally はルートごと破棄 |

### 監査済みで問題なかった要素 (実連結確認)

**Title** (`TitleSceneManager` + `TitleView`)
- GameStart 行 → `SelectScene()` → `Initiate.Fade("SelectScene")` (クリック + ↑↓ + Enter/Space = KeyActions)
- Option 行 → `OptionScene()` / Exit 行 → `EndGame()` (Editor では再生停止)
- Ranking 行は `TitleLinks.RankingRequested` が未設定の間 **表示しない** 契約 (受入基準 T7)。フックを設定する実装はまだ無い → 行ごと非表示で契約どおり
- Tab → `KeyGuideUI` (キーガイド) トグル + 下部ヒント表示
- カタログバッジは `GManager.HasRemoteCatalog` / `totalSong` の実値

**Select** (`SelectSceneManager` + `SelectView` / `SelectViewLogic`)
- ← (Back) → `TitleScene()` / 曲行クリック → 選択 or Ready へ / ↑↓・ホイール・レーンキー → 選択移動
- Ready ボタン / Enter → Ready パネル / Start ボタン / Space → `GameScene()` (リモート曲は譜面確定後に `ResetSession` → 遷移)
- Ready の Key Guide / Lyrics → `PlaySession.SetKeyGuide` / `SetLyricsEnabled` (**Game 側の `GameHudView.BuildKeyGuide` / `GameSceneManager.ApplyTextPanelVisibility` が実値を読む**)
- Ready の NoteSpeed → `GManager.noteSpeed` + `OptionSettings.Save()` (1..50 クランプ)
- Sort / Level / Status チップ → `SelectViewLogic.FilterIds` で実フィルタ / 検索 (Tab) → `Input.inputString` / × クリア
- F2 → 履歴 / F3 → ランキング (Local = この端末の Hiscore、Online = cloud 曲のみ `RankingClient.GetLeaderboard`)
- 再生できない曲 (placeholder / unplayable) は Ready / Start を `interactable = false`

**Game** (`GameSceneManager` + `GameHudRuntime` 配下の v5 View 群)
- HUD 右下ポーズボタン → `PauseMenuView.Open` (F5 修正のとおりエラー中は不可)
- Esc → `PauseMenuView.Toggle` (エラー中は `InputEnabled = false` で無効)
- ポーズ Settings タブ: 6 行 ✓ / ←→ ステッパー ✓ / クリック ✓ / 値は `GManager` 実値 + `OptionSettings.Save()`
- ポーズ Display タブ: Key Guide → `PlaySession` + `GameHudView.RebuildKeyGuide()` (即時反映) / Lyrics → `PlaySession` + `GameSceneManager.ApplyLyricsVisibility()` (即時反映)。実装の無い行は表示しない
- ポーズ Ranking / Keys タブ: ローカル実データ / `Judge.LaneKeys` 由来の表示 (押しても変化しない行は無い)
- ポーズ Retire / Retry / Resume → `RetireGame()` / `RetryGame()` (`ResetSession` → GameScene 再読み込み) / Esc
- レガシー MenuPanel は v5 ポーズが開いている間は `GameHudController.LateUpdate` が強制非表示 (二重表示なし)
- GameErrorView: ← 選曲へ / Retry ボタン + Esc / R → `Initiate.Fade` (leaving フラグで二重抑止)
- 歌詞パネル: `PlaySession.LyricsEnabled` && `Menu` && 表示行あり のときだけ出る (空パネルを残さない)

**Result** (`ResultSceneManager` + `ResultView` / `ResultRankingOverlay`)
- Retry ボタン / R → `Retry()` (判定・スコアを 0 に戻して GameScene へ)
- OK ボタン / Space / Esc → `SelectScene()`
- Name ボタン / Enter → 「この端末の名前」モーダル → Save → `PlayerPrefs` (`V5PlayerName`) + 行ラベル即時更新 / Skip = Esc
- クラウド曲のみ 1.2 秒後にランキング登録モーダル → `RankingClient.PostScore` (F5 ボード / 失敗時は再送 or スキップ)
- ハイスコア更新は `Awake` (SaveLoadManager の保存より先)

**Option** (`OptionSceneManager` + `OptionView`)
- Back ボタン / Esc → `TitleScene()`
- Play タブ: NoteSpeed → `SpeedUp/Down` (1..50 + 保存) / NoteTiming → `TimingUp/Down` (+ 各曲 offset へ書き込み) / Key Layout → 標準へ戻す or キー設定を開く / キー設定 → `KeyConfigUI.Open()`
- Display タブ: Flash / FlashTiming / FlashSpeed → `GManager` 実値 + 保存 (BGFlash が読む)
- Sound タブ: Main / Effect / BGM → `GManager` + `AudioListener.volume` + シーン内 AudioSource へ即時反映 + 保存
- Data タブ: Clear save data → 2 ステップ確認 → Hiscore / キー設定を消去
- MediaCacheOptionPanel (キャッシュ削除) / KeyConfigOptionButton (キー設定) / 更新適用ボタン (staging 済み更新があるときだけ出る) — いずれも実ハンドラ付き

**Retry を含む状態リセット**: `GManager.ResetSession()` (Select → Game の両パスとポーズ Retry) と `ResultSceneManager.Retry()` が同じ 9 項目 (perfect/great/bad/miss/score/combo/maxCombo/maxScore/ratioScore) を 0 化することを確認。

### デッドリンク (シーン YAML, 修正は #169 担当)

| 場所 | 内容 | 影響 |
|---|---|---|
| `Assets/Game/Scenes/SelectScene.unity` (行 ~2559) | 非表示のレガシー Canvas 内ボタンが `SelectSceneManager.OptionScene` を呼ぶ。**メソッドは現存しない** (v5 Select 化の a4c7e08 で削除済み) | レガシー Canvas は `SelectView.Create` が `SetActive(false)` するため実行時は不活性。クリックされることは無いが、参照は死んだまま |

`.unity` は #169 の所有物のため本 PR では触らない。削除は #169 側で行うこと
(参考: OptionScene の `ResetHiscore` は `SaveLoadManager.ResetHiscore` 実在メソッドへの参照なので死んではいない)。

---

## 1b. 機能UI契約表 (visible control → handler → state effect → 実装根拠)

実機ゲート (#167 T1 の監査表) のコード側根拠。行は「画面で押せるように見える要素」全件。
根拠は `ファイル:メソッド` (行番号はずれるためメソッド単位)。

### Title

| visible control | handler | state effect | 実装根拠 |
|---|---|---|---|
| GameStart 行 (クリック) | `Button.onClick` | SelectScene へ `Initiate.Fade` | `TitleView.BuildMenu` → `TitleSceneManager.SelectScene` |
| GameStart 行 (Space/Enter) | `KeyActions.IsDown(Start)` | 同上 | `TitleView.Update` → `Activate` |
| Option 行 (クリック / O) | `Button.onClick` / `KeyActions.Option` | OptionScene へ | `TitleView.BuildMenu` / `Update` → `TitleSceneManager.OptionScene` |
| Exit 行 (クリック / E) | `Button.onClick` / `KeyActions.Exit` | Editor=再生停止 / ビルド=終了 | `TitleView.BuildMenu` / `Update` → `TitleSceneManager.EndGame` |
| ↑↓ / W S | `Input.GetKeyDown` | 強調行を移動 + 決定音 | `TitleView.Update` → `SetSelected` |
| Ranking 行 | `TitleLinks.RankingRequested` (未設定) | **行ごと非表示** (押せないものを描かない契約, 受入 T7) | `TitleView.UpdateRankingVisibility` |
| Tab | `KeyGuideUI.Update` | キーガイド (全レーンの割り当て) を開閉 | `TitleSceneManager.CreateKeyGuide` → `KeyGuideUI.Toggle` |

### Select

| visible control | handler | state effect | 実装根拠 |
|---|---|---|---|
| ← (Back) | `Button.onClick` / `KeyActions.Back` (Esc) | TitleScene へ | `SelectView.BuildSelect` → `ToTitle` → `SelectSceneManager.TitleScene` |
| 曲行 (クリック) | `Button.onClick` | 選択更新 / 再クリックで Ready へ | `SelectView.SongRow` (onClick) |
| ↑↓ / ホイール / レーンキー | `Input` / `KeyBindings.IsAnyLaneKeyDown` | `GManager.songID` を移動 | `SelectView.HandleListInput` → `MoveSelection` |
| 検索 (Tab / A-Z) | `Input.inputString` | `SelectViewLogic.FilterIds` で実絞り込み | `SelectView.HandleTypingInput` / `RefreshFilter` |
| 検索クリア × | `Button.onClick` | filterText を空にして再絞り込み | `SelectView.BuildSelect` (Clear) |
| Sort / Level / Status チップ | `Button.onClick` | モード循環 → 実フィルタ | `SelectView.BuildSelect` (FilterChip) → `SelectViewLogic` |
| Ready ボタン / Enter | `Button.onClick` / Enter | Ready パネルを開く (再生不能曲は `interactable=false`) | `SelectView.BuildSelect` (PrimaryButton) → `EnterReady` |
| Start ボタン / Space | `Button.onClick` / `KeyActions.Confirm` | `GManager.ResetSession` → GameScene (リモート曲は譜面確定後) | `SelectView.StartSelected` → `SelectSceneManager.GameScene` |
| Ready: Key Guide ←→ | `Button.onClick` / 矢印 | `PlaySession.SetKeyGuide` → **Game の HUD が実値を読む** | `SelectView.ChangeReadyValue` → `GameHudView.BuildKeyGuide` |
| Ready: Lyrics ←→ | 同上 | `PlaySession.SetLyricsEnabled` → **歌詞パネルが実値を読む** | 同上 → `GameSceneManager.ApplyTextPanelVisibility` |
| Ready: NoteSpeed ←→ | 同上 | `GManager.noteSpeed` (1..50) + `OptionSettings.Save` | `SelectView.ChangeReadyValue` |
| F2 | `Input.GetKeyDown` | 履歴 (記録のある曲を実データで) | `SelectView.HandleListInput` → `EnterHistory` → `BuildHistory` |
| F3 | `Input.GetKeyDown` | ランキング: Local = この端末の Hiscore / Online = cloud 曲のみ取得 | `SelectView.HandleListInput` → `EnterRanking` → `BuildRanking` / `FetchOnlineRanking` |
| Local / Online セグメント | `Button.onClick` | セグメント切替 (Online は取得可能曲のみ表示) | `SelectView.BuildRanking` |

### Game (HUD / Ready / Loading / Error)

| visible control | handler | state effect | 実装根拠 |
|---|---|---|---|
| Space (Ready) | `KeyActions.Play` | `MusicManager.StartPlayback` (再生開始) | `MusicManager.Update` (Gameplay, 参照のみ) |
| HUD ポーズボタン (クリック) | `Button.onClick` | ポーズメニューを開く (**エラー中は開かない**) | `GameHudView.BuildPauseButton` → `GameHudRuntime.Start` → `PauseMenuView.Open` |
| Esc | `PauseMenuView.Update` | ポーズ開閉 (エラー中は `InputEnabled=false` で無効) | `PauseMenuView.Update` / `GameHudController.Update` |
| Pause: Settings 6 行 ←→ | 行 / ステッパー `Button.onClick` + 矢印 | `GManager.noteSpeed/noteTiming/mainVolume/BGMVolume/effectVolume/Flash` + `OptionSettings.Save` + 音量は即時反映 | `PauseMenuView.AddRow` / `Adjust` / `ApplyVolumes` |
| Pause: Display: Key Guide | 行 `Button.onClick` | `PlaySession` 変更 → **HUD のキーガイドを即時作り直し** | `PauseMenuView.BuildDisplayPage` → `ApplyKeyGuideMode` → `GameHudView.RebuildKeyGuide` |
| Pause: Display: Lyrics | 行 `Button.onClick` | `PlaySession` 変更 → **歌詞パネル表示を即時反映** | 同上 → `ApplyLyricsMode` → `GameSceneManager.ApplyLyricsVisibility` |
| Pause: Ranking タブ | タブ `Button.onClick` / S D L K | この曲のベスト + 端末合計 (ローカル実データ) | `PauseMenuView.BuildRankingPage` |
| Pause: Keys タブ | 同上 | `Judge.LaneKeys` 正本のレーン色付きキー配列 | `PauseMenuView.BuildKeysPage` / `BuildLaneKeyMap` |
| Pause: Retire / Retry / Resume | `Button.onClick` / `KeyActions.MenuRetire/MenuRetry` | SelectScene へ / `GManager.ResetSession` → GameScene 再読み込み / 閉じる | `PauseMenuView.BuildActions` → `GameSceneManager.RetireGame` / `RetryGame` |
| Error: ← 選曲へ / Retry | `Button.onClick` / Esc / R | SelectScene / GameScene へ (leaving フラグで二重抑止) | `GameErrorView.Build` / `BackToSelect` / `Retry` |

### Result

| visible control | handler | state effect | 実装根拠 |
|---|---|---|---|
| Retry ボタン / R | `Button.onClick` / `KeyActions.Retry` | 判定・スコア 9 項目を 0 化 → GameScene | `ResultView.BuildActions` → `ActivateAction` → `ResultSceneManager.Retry` |
| OK ボタン / Space / Esc | `Button.onClick` / `KeyActions.Confirm` | SelectScene へ | 同上 → `ResultSceneManager.SelectScene` |
| Name ボタン / Enter | `Button.onClick` / Enter | 「この端末の名前」モーダルを開く | `ResultView.BuildActions` → `OpenModal` |
| モーダル Save / Skip | `Button.onClick` / Enter / Esc | `PlayerPrefs` に保存 + 行ラベル即時更新 / 閉じる | `ResultView.ConfirmRegister` / `CloseModal` |
| ←→ | `Input.GetKeyDown` | ボタンフォーカス移動 | `ResultView.Update` → `SetActionSelected` |
| ランキング登録 (cloud 曲) | `ResultRankingOverlay` (1.2 秒後自動) | `RankingClient.PostScore` → Top10 ボード。**モーダル中は裏の ResultView を suspend** | `ResultRankingOverlay.OpenSubmitModal` / `SuspendScene` |

### Option

| visible control | handler | state effect | 実装根拠 |
|---|---|---|---|
| Back ボタン / Esc | `Button.onClick` | TitleScene へ | `OptionView.BuildHeader` → `OptionSceneManager.TitleScene` |
| タブ 4 個 / Tab | `Button.onClick` / Tab | 行リストを作り直す (Play / Display / Sound / Data) | `OptionView.BuildTabStrip` / `SelectTab` / `BuildRows` |
| NoteSpeed ←→ | ステッパー / 矢印 | `GManager.noteSpeed` (1..50) + 保存 | `OptionView.BuildPlayRows` → `OptionSceneManager.SpeedUp/SpeedDown` |
| NoteTiming ←→ | 同上 | `GManager.noteTiming` (±10) + 保存 + 各曲 offset へ書き込み | 同上 → `TimingUp/TimingDown` |
| Key Layout ←→ | 同上 | 既定へ戻す (`KeyBindings.ResetToDefaults`) / キー設定を開く | `OptionView.BuildPlayRows` / `OpenKeyConfig` |
| Flash / FlashTiming / FlashSpeed ←→ | 同上 | `GManager.Flash/FlashT/FlashBG` + 保存 (BGFlash が読む) | `OptionView.BuildDisplayRows` |
| Main / Effect / BGM Volume ←→ | 同上 | `GManager` + `AudioListener.volume` + シーン内 AudioSource へ即時反映 + 保存 | `OptionView.BuildSoundRows` → `OptionSceneManager.ApplyVolumes` |
| Clear save data / Enter | 行 `Button.onClick` | 2 ステップ確認 → Hiscore + キー設定を消去 | `OptionView.ClearSaveData` |
| キー設定ボタン (KeyConfigOptionButton) | `Button.onClick` / Tab+Enter | `KeyConfigUI.Open` | `KeyConfigOptionButton.BuildUi` / `OpenKeyConfig` |
| Clear media cache | `Button.onClick` | `MediaCacheManager.ClearAll` + 結果表示 | `MediaCacheOptionPanel.BuildUi` / `ClearCache` |
| 更新を適用して再起動 (staging あり時のみ) | `Button.onClick` | `UpdateChecker.RequestApply` | `UpdateChecker.CreateOptionApplyButton` |

### Retry (状態リセットの契約)

| 経路 | handler | state effect | 実装根拠 |
|---|---|---|---|
| Result → Retry | `ResultSceneManager.Retry` | perfect/great/bad/miss/score/combo/maxCombo/maxScore/ratioScore を 0 化 → GameScene | `ResultSceneManager.Retry` |
| Pause → Retry | `GameSceneManager.RetryGame` | `GManager.ResetSession` (同じ 9 項目) → GameScene | `GameSceneManager.RetryGame` / `GManager.ResetSession` |
| Select → Start | `SelectSceneManager.GameScene` | `GManager.ResetSession` → GameScene (ローカル / リモート両パス) | `SelectSceneManager.GameScene` / `StartRemoteSong` |

### ウィンドウ基準 (実Win64: FHD 1920x1080 / 2K 2560x1440)

| 仕組み | 契約 | 実装根拠 | テスト |
|---|---|---|---|
| プロファイル選択 | 16:9 FHD → `fhd` / 2K → `2k` (#169 の規則) | `UISizeProfile.Select` | `V5UiTests.Select_1920x1080_ReturnsFhd` / `Select_2560x1440_Returns2k` |
| Select の design スケール | `u = min(W/1920, H/1080)`。16:9 の 2 条件でプロファイル倍率と一致 | `SelectView.DesignScale` | `WindowBaselineTests` |
| Title のレイアウト | design 実測比率で 2 条件ともウィンドウ内に収まる | `TitleLayout.Compute` | `TitleOptionV5Tests` / `WindowBaselineTests` |
| リサイズ追従 | ウィンドウサイズ変更で Canvas 再適用 / Title は再レイアウト / Select は再構築 | `CanvasConfigurator.Update` / `TitleView.Update` / `SelectView.HandleInput` | — (実機ゲート) |
| スケーリング系統の一致 | HUD/Pause 系 `S = Margin/48`、Result/Option 系 `S = FsM/30` が FHD=1.0 / 2K=1.333 | `GameHudView.S` / `ResultView.S` / `OptionView.S` | `WindowBaselineTests` |
| スケーラー併用 UI | KeyConfigUI / RankingPanel / UpdateNoticeUI / MediaCacheOptionPanel / KeyConfigOptionButton / MediaLoadingOverlay は `ScaleWithScreenSize 1920x1080 match 0.5` で自動追従 | 各 `BuildCanvas` / `BuildUI` | — |
| Mac 2560x1664 | プロファイル選択のみコード対応 (実機はオーナーゲート) | `UISizeProfile.Select` (mac 規則) | `V5UiTests` |

---

## 2. 追加した共有ロジックとテスト

| ロジック | テスト |
|---|---|
| `FlashBeat.UI.SceneInputGate` (新規): オーバーレイ開閉フレームの背面入力を 1 フレームだけブロックする静的ゲート。フレーム番号は `FrameSource` で注入可能 | `Assets/Tests/Editor/SceneInputGateTests.cs` |
| `KeyConfigUI.IsAllowedSceneName(string)` (新規抽出): シーン名でキー設定を開けられるか。SelectScene を不許可に変更 | `Assets/Tests/Editor/KeyConfigSceneAllowTests.cs` |
| `PauseMenuView.NextNoteSpeed(float, int)` (新規抽出): NoteSpeed の増減を OptionSettings (1..50) にクランプ | `Assets/Tests/Editor/PauseMenuViewTests.cs` |
| `SelectView.DesignScale(int, int)` (新規抽出): Select の design スケール `u = min(W/1920, H/1080)`。リサイズで `HandleInput` から `Rebuild` を再実行して追従する | `Assets/Tests/Editor/WindowBaselineTests.cs` |
| `V5UiWiringTests` 3 件の修正 (既存壊れテスト → 実装の契約に整合) | 同ファイル (Title Exit 行 / Compact 代表キー / Select の Canvas 探索方向) |

---

## 3. 検証手順 (コード / テストで完結)

Unity batchmode は他タスク (p7/p8) と排他。実行前に `pgrep -fl "Unity -batchmode"` で確認し、
走っているなら待ってから実行する。

```bash
# 1) EditMode テスト (全件 pass を確認。WindowBaselineTests を含む)
#    (-quit を付けるとテスト実行前に終了するため付けない)
Unity -batchmode -projectPath . -runTests -testPlatform editmode \
  -testResults TestResults.xml -logFile unity-test.log

# 2) Win64 ビルド
Unity -batchmode -quit -projectPath . -executeMethod BuildScript.BuildStandaloneWindows64
```

実機での確認ポイント (既存の手順書 [issue-167-ui-acceptance.md](issue-167-ui-acceptance.md) に追加):

| # | 操作 | 期待される実動作 |
|---|---|---|
| X1 | Option で F2 → キー設定 → ↑↓←→ / Tab / Enter | 裏の Option 画面の行・値が **動かない** |
| X2 | キー設定を Esc で閉じる | Option 画面に **留まる** (Title へ遷移しない) |
| X3 | Select で F2 | 履歴が開く (キー設定は開かない)。左下ヒントは Title のみ |
| X4 | クラウド曲でリザルト → 登録モーダルで `R` / Space / Esc を入力 | 名前入力以外の動作を **しない** (モーダル中はリトライ / 遷移しない) |
| X5 | F5 ボードを Select で開く → 矢印 / Enter / Space | 裏の選曲が動かない。Esc / F5 で閉じたフレームに遷移しない |
| X6 | 動画エラー中表示中に HUD 右下のポーズボタン | 押してもポーズが **開かない** |
| X7 | Ready で NoteSpeed 40 → Game → ポーズ → NoteSpeed ←→ | 40 → 41 → 40 (30 に飛ばない) |
| X8 | タイトル → 選曲 → 動画再生 → リザルト → Retry / OK | 従来どおり (PR #179 の手順をそのまま) |

---

## 4. 制約の遵守

- 判定ロジック (`Assets/Game/Scripts/Gameplay/Judge*`) は未変更 (PR #204 と排他)。
- `CanvasScaler` / `ProjectSettings` / `Assets/**/*.unity` (Canvas 設定含む) は未変更。
- 変更は `Assets/Game/Scripts/UI/**` (SelectSceneManager を含む)、
  `Assets/Tests/Editor/**`、`docs/ui/**` に留まる。
