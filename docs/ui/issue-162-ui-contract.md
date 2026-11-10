# 機能UI契約表 (Issue #162 タスク2) — 実装根拠つき監査

- 日付: 2026-11-08
- 対象: worktree `val/pr204` (main + PR #204 head, commit `5e9fdc7` 時点)
- 方法: コード読解による静的監査。**実プレイ証拠は無い** (実機確認は別途 X1〜X8、`pr204-playtest-checkpoints.md`)。
- 対象範囲: EditorBuildSettings の 6 シーン (`Opening / TitleScene / SelectScene / GameScene / ResultScene / OptionScene`, `ProjectSettings/EditorBuildSettings.asset:9-24`) + 常駐オーバーレイ (KeyGuide / KeyConfig / UpdateNotice / MediaCache / Ranking)。
- コード変更: なし (監査のみ)。

## 0. 共通基盤 (全シーンで同じ)

| 仕組み | 実装 | 備考 |
|---|---|---|
| メニュー操作キー | `KeyActions` (`Assets/Game/Scripts/Gameplay/KeyActions.cs`)。既定値: 決定=Space / 戻る=Esc / リトライ=R / オプション=O / 終了=E / メニュー:リトライ=R / 終了=E / リタイア=BS / パネル切替=T / スタート=Space,Enter / 再生開始=Space / 前の曲=← / 次の曲=→ (`KeyActions.cs:41-56`) | 保存先 `FlashBeat-keyconfig.json` (`KeyConfigFile.cs:55-62`) + PlayerPrefs ミラー |
| レーンキー | `KeyBindings` (`Assets/Game/Scripts/Gameplay/KeyBindings.cs`)。プレイ中の実読みは `Judge.cs:202,215` (`KeyBindings.GetKeys(lane)`) | 同じ設定ファイルに保存 |
| 画面遷移 | `Initiate.Fade("シーン名", ...)` (Simple Fade Load System) | 全遷移がこれ経由 |
| 永続化 | `OptionSettings` (PlayerPrefs `FlashBeat.*`) / `SaveLoadManager` (PlayerPrefs `Hiscore`) / `KeyConfigFile` (JSON) / `PlayerPrefs` (`V5PlayerName`, `RankingNickname`) | 系統が 4 種。同名の値の二重管理あり (→ A-8) |
| v5 UI 生成 | 各シーンの `*SceneManager.Start` が `V5` ビューをコード構築し、レガシー Canvas を非表示化 | レガシーのハンドラは接続残留 (→ B-3, C-4) |

---

## 1. Opening (`OpeningSceneManager.cs` 27 行)

| 表示 | 中身 | 入力 | 効果 | 後続 |
|---|---|---|---|---|
| ロゴ画面 | 背景 + エッジ装飾 + 中央ロゴのみ (`OpeningView.cs:26-40`) | **なし** (Input を一切読まない) | なし | 6.031 秒後に `Initiate.Fade("TitleScene")` (`OpeningSceneManager.cs:15-24`) |

- スキップ操作は存在しない (6 秒待つのが唯一の進み方)。

## 2. Title (`TitleSceneManager.cs` + `V5/TitleView.cs`)

レガシー Canvas を非表示化し v5 に置換 (`TitleSceneManager.cs:32-34`)。

| 表示 | 入力 | ハンドラ → 効果 | フィードバック | 根拠 |
|---|---|---|---|---|
| メニュー `GameStart` | ↑↓/W S 選択、Space/Enter (`KeyActions.Start`)、マウスクリック | `TitleSceneManager.SelectScene()` → `Initiate.Fade("SelectScene")` | 決定音 | `TitleView.cs:78,140-157,346-351` / `TitleSceneManager.cs:75-78` |
| メニュー `Ranking` | マウスのみ (キーキャップなし) | `TitleLinks.RankingRequested()` — **未接続 (A-1)** | 決定音 | `TitleView.cs:79,98-104` |
| メニュー `Option` | `KeyActions.Option` (O) で直接ジャンプ (`TitleView.cs:160-164`)、マウス | `OptionScene()` → `Initiate.Fade("OptionScene")` | 決定音 | `TitleSceneManager.cs:80-83` |
| メニュー `Exit` | `KeyActions.Exit` (E)、マウス | `EndGame()` → `Application.Quit()` | 決定音 | `TitleSceneManager.cs:66-73` |
| カタログバッジ `オンライン/ローカル · N曲` | なし (表示) | `GManager.HasRemoteCatalog` / `totalSong` の実値 | — | `TitleView.cs:393-400` |
| 凡例 `↑↓ 選択 / Enter 決定` | なし | — | — | `TitleView.cs:403-412` (Enter 表記は実挙動と不一致 → C-2) |
| キーガイドHint `Tab : キーガイド` | Tab | `KeyGuideUI.Toggle()` | パネル開閉 | `TitleSceneManager.cs:41-64` / `KeyGuideUI.cs:65-79` |
| 更新通知ピル | F3 / クリックで確認、F4 / × で閉じる | `UpdateChecker` → 外部 API | バッジ表示 | `UpdateNoticeUI.cs:79-105` (**v5 タイトルの下に隠れる → A-3**) |
| ランキング板 (F5) | F5 で開閉 | `RankingClient.GetLeaderboard` (GET) | ボード表示 | `ResultRankingOverlay.cs:111-115` |

## 3. Select (`SelectSceneManager.cs` + `V5/SelectView.cs` 1830 行)

5 状態 `List / Typing / Ready / History / Ranking` (`SelectView.cs:36-43`)。入力は `SelectSceneManager.Update` → `view.HandleInput()` (`SelectSceneManager.cs:34-37`)。

### 3.1 List (曲一覧)

| 表示 | 入力 | ハンドラ → 効果 | フィードバック | 根拠 |
|---|---|---|---|---|
| `←` (タイトルへ) | Esc (`KeyActions.Back`)、マウス | `owner.TitleScene()` → Fade TitleScene | 音 | `SelectView.cs:821,531-539,265-269` / `SelectSceneManager.cs:145-148` |
| 検索フィールド `タイプして検索` | **Tab で Typing 状態へ** (`SelectView.cs:140`)。直接入力は効かない (→ C-4) | `filterText` → `RefreshFilter(); Rebuild()` | 文字表示 | `SelectView.cs:824-835` |
| `×` (検索クリア) | マウス | `filterText=""` → 再構築 | なし (音なし) | `SelectView.cs:837-848` |
| `Sort <Recent|Level|Score>` | マウス | `sortMode` 循環 → 再構築 | ラベル変化 | `SelectView.cs:867-872` (Recent 表示は実挙動と不一致 → C-3) |
| `Level <All|1-5|...>` | マウス | `levelMode` 循環 | ラベル変化 | `SelectView.cs:873-878` |
| `Status <All|Played|New>` | マウス | `statusMode` 循環 | ラベル変化 | `SelectView.cs:879-884` |
| 曲行 (ジャケット+題名+レベル) | マウスクリック、ホイール、↑↓ + `NavPrev/NavNext` + レーンキー 0-3/4-7 | 同じ行 → `EnterReady()`。別の行 → `GManager.songID = id; Rebuild()` | 音 + 行ハイライト | `SelectView.cs:721-775,123-136` |
| `N / M` 件数表示 | なし | 実値 (`filteredIds.Count / total`) | — | `SelectView.cs:889-895` |
| ジャケット帯 | なし | ローカル `Resources/V5/select/jacket/` → 無ければ YouTube サムネ URL | 画像 | `SelectView.cs:903,947-979,697` |
| BIGSTAT `BPM / NOTES / LEVEL` | なし | 曲メタデータ実値 | — | `SelectView.cs:1017-1043` |
| ScoreRail + `BEST` | なし | `HiscoreAt(songID)` 実値 | — | `SelectView.cs:1749-1816,1053-1069` |
| `SONG RANKING` 枠 | なし | 自曲ベスト or `スコアがまだありません` | — | `SelectView.cs:1072-1081` |
| `PLAYERS (累計)` 枠 | なし | **常時固定文** `ローカルモードではプレイヤーごとの記録はありません` (→ D-5) | — | `SelectView.cs:1084-1086` |
| 凡例 `[A-Z] 検索 [↑↓] 曲 [Tab] 絞り込み` / `Enter 決定 / F2 履歴 / F3 ランキング / Esc タイトルへ` | なし | — | — | `SelectView.cs:898-900,934-940` (Space/R の表記なし → C-5) |
| `Ready` ボタン | マウス、Enter | `EnterReady()` (`HasPlayableSong()` ガード) | 音 | `SelectView.cs:941-942,137` |

### 3.2 Typing (検索入力オーバーレイ)

| 入力 | 効果 | 根拠 |
|---|---|---|
| `Input.inputString` | `filterText` に追加 → 再構築 | `SelectView.cs:162-173` |
| Backspace | 1 文字削除 | `SelectView.cs:150-153` |
| Enter | List へ戻る (検索語は保持) | `SelectView.cs:154-161` |
| Esc / Tab | List へ戻る | `SelectView.cs:177` |
| ↑↓ / F2 / F3 | 移動 / History / Ranking | `SelectView.cs:175-179` |

### 3.3 Ready (プレイ前設定)

| 表示 | 入力 | ハンドラ → 効果 | 根拠 |
|---|---|---|---|
| `このプレイの設定` 見出し | なし | — | `SelectView.cs:1142` |
| `Key Guide` 行 `Compact|Full|Off` | ←→ / マウス | `PlaySession.SetKeyGuide(...)` (セッション状態、Game HUD が参照) **非永続** (→ D-1) | `SelectView.cs:1154,1172-1212,332` |
| `Lyrics` 行 `Auto|Off` | ←→ / マウス | `PlaySession.SetLyricsEnabled(...)` **非永続** (→ D-1) | `SelectView.cs:1155,340` |
| `NoteSpeed` 行 | ←→ / マウス | `GManager.noteSpeed = Clamp(1..50)` + `OptionSettings.Save()` (永続) | `SelectView.cs:1156,344-346` |
| 注記 `演奏前に変えられる設定です...` | なし | — | `SelectView.cs:1215` |
| `Start` ボタン | マウス、Enter/Space/R | `owner.GameScene()` → (リモート曲は譜面確定後) `GManager.ResetSession()` + Fade GameScene | `SelectView.cs:929-930,188-193,256-262` / `SelectSceneManager.cs:95-136` |
| Esc | キー | List へ戻る | `SelectView.cs:194` |

### 3.4 History (プレイ履歴, F2)

| 表示 | 入力 | ハンドラ → 効果 | 根拠 |
|---|---|---|---|
| `←` / Esc / F2 | マウス / キー | List へ戻る | `SelectView.cs:1233,1443-1447` |
| `N plays` 件数 | なし | **誤ラベル**: N は「記録のある曲数」でプレイ回数ではない (→ C-1) | `SelectView.cs:1243,353-368` |
| 履歴行 | クリック / ↑↓ / ホイール | クリック・↑↓ は `historyIndex` を動かす。**ホイールは `MoveSelection` で `GManager.songID` を動かし詳細とズレる** (→ B-1) | `SelectView.cs:1274-1302,196-200,271-280` |
| 判定内訳 `Perfect/Great/Bad/Miss/Hold/MaxCombo` | なし | **全セル固定 `–`** (内訳未記録のため。空レールのみ) (→ D-3) | `SelectView.cs:1399-1441` |
| `この曲へ` ボタン | マウス、Enter/Space | `GManager.songID = 選択曲` → List へ | `SelectView.cs:1448,214-218,318-325` |

### 3.5 Ranking (F3)

| 表示 | 入力 | ハンドラ → 効果 | 根拠 |
|---|---|---|---|
| `←` / Esc / F3 | マウス / キー | List へ戻る | `SelectView.cs:1458,1485-1487` |
| 曲リスト (左) | ホイール / ↑↓ / クリック | `GManager.songID` 更新 | `SelectView.cs:1468-1483` |
| セグメント `Local` | マウス / Tab | `rankingSegment=0` | `SelectView.cs:1515-1526,233` |
| セグメント `Online` (API 設定時) | マウス | クリックは `FetchOnlineRanking` (GET `/api/v1/leaderboards/...`)。**Tab での切替は取得しない** (→ A-5) | `SelectView.cs:1536-1551,1660-1682,233` |
| `YOU` / `–位 / –人` / `累計` / `プレイ曲` / `S 以上` | なし | `GManager.Hiscore` 集計の実値。順位のみ固定 `–位 / –人` (→ D-4) | `SelectView.cs:1590-1622` |

## 4. Game (`GameSceneManager.cs` + `V5/GameHudView` / `GameHudRuntime` ほか)

v5 は `GameHudRuntime.Ensure()` が Canvas 群を構築し (`GameHudRuntime.cs:70-87`)、レガシー UI を起動時に非表示化 (`GameHudRuntime.cs:216-224`) し毎フレーム強制非表示 (`GameHudRuntime.cs:140-150`)。

### 4.1 HUD (`GameHudView`)

| 表示 | 入力 | ハンドラ → 効果 | 根拠 |
|---|---|---|---|
| ランク `C/B/A/S/SS` | なし | `GManager.score` から毎フレーム | `GameHudView.cs:105-109,429-431` |
| `SCORE` + 値 | なし | `GManager.score` | `GameHudView.cs:116-121,432-433` |
| スコアレール + `BEST` マーカー | なし | `RailFillWidth / RailBestX` — **呼び出し側の単位ミスで負の幅** (→ B-6) | `GameHudView.cs:436-439` / `GameHudLogic.cs:48-66` |
| `+ COMBO BONUS +N` | なし | **近似値を実際の加点のように表示** (→ B-4) | `GameHudView.cs:441-442` / `GameHudLogic.cs:18-23` |
| `COMBO` + 値 / `NO MISS` / `FULL COMBO` | なし | `GManager.combo/miss`。**NO MISS と FULL COMBO が同条件で同時に出る** (→ B-7) | `GameHudView.cs:186-221,446-447` |
| `次のボーナス N / +N` | なし | **仮の式** (milestone×10) (→ B-4) | `GameHudView.cs:224-225,451-452` |
| 判定ポップアップ `PERFECT/GREAT/BAD/MISS` | なし | GManager カウンタ差分 | `GameHudView.cs:232-253,458-486` |
| 一時停止ボタン | マウス | `PauseMenuView.Open()` | `GameHudView.cs:257-283` / `GameHudRuntime.cs:88-91` |
| キーガイド帯 (コンパクト) | なし | `PlaySession.KeyGuide` に応じて再構築 | `GameHudView.cs:292-363` |

### 4.2 Ready (`GameReadyView`, 再生前)

| 表示 | 内容 | 根拠 |
|---|---|---|
| 曲名 / `作曲者 · BPM / Lv` / ジャケット | 実値 + YouTube サムネ (失敗時プレースホルダ) | `GameReadyView.cs:48-111` |
| `[Space] to Start` | 表示のみ (Space は MusicManager が処理) | `GameReadyView.cs:67-75` / `MusicManager.cs:648-652` |
| `[Esc] メニュー (設定・表示・ランキング)` | 表示のみ (Esc は PauseMenuView) | `GameReadyView.cs:78-85` |
| 表示条件 | `!GManager.Start && !GManager.played` の間 | `GameReadyView.cs:17,28-30,90-91` |

### 4.3 Loading (`GameLoadingView` + `MediaLoadingOverlay`)

| 表示 | 内容 | 根拠 |
|---|---|---|
| `Loading youtube video ...` + 不定長バー | **進捗 API は無く PingPong の演出** (コメントで明記) (→ B-5) | `GameLoadingView.cs:10-13,49-83` |
| `読み込み中 / 曲情報を取得中… / 動画をダウンロード中…` 等 | `MusicManager` の段階に応じて表示。失敗時 `音源を取得できませんでした` + 3.5 秒で自動で隠れる | `MediaLoadingOverlay.cs:94-152` / `MusicManager.cs:219-298` |
| 二重表示 | v5 `GameLoadingView` (order 500) と `MediaLoadingOverlay` (order 6000) が同時に出る (→ B-9) | `GameHudRuntime.cs:110-129` / `MusicManager.cs:218-219` |

### 4.4 Error (`GameErrorView`)

| 表示 | 入力 | ハンドラ → 効果 | 根拠 |
|---|---|---|---|
| `動画を再生できません / YouTube エラー 403` | なし (表示) | **発生原因に関係なく 403 固定表示** (→ B-8) | `GameErrorView.cs:52-64` |
| 本文 `通信状態を確認して...譜面と音楽は読み込まれています。` | なし | ストリーム未解決時は**虚偽** (→ B-8) | `GameErrorView.cs:62-64` / `MusicManager.cs:313-319` |
| `← 曲選択へ` + `[Esc]` | Esc / マウス | `Initiate.Fade("SelectScene")` | `GameErrorView.cs:75-77,112-125` |
| `Retry` + `[R]` | R / マウス | `Initiate.Fade("GameScene")` — **`GManager.ResetSession()` なし** (→ A-2) | `GameErrorView.cs:78-83,112-133` |

### 4.5 パズルの旧 UI (レガシー, v5 下で死んでいる)

`MenuPanel / MenuButton / ScoreText / comboText / Gauge / HudStatus / SpacetoStart` は起動時に非表示 (`GameHudRuntime.cs:216-224`) され毎フレーム強制非表示 (`GameHudRuntime.cs:140-150`)。接続されたハンドラ (`GameSceneManager.cs:156-218, 262-288, 392-411`) とシーン配置ボタン (`GameScene.unity` の `もう一度` / `結果をみる` / `曲選択にもどる` / `とじる`) は到達不能 (→ A-4, B-3)。

## 5. ポーズ (`V5/PauseMenuView.cs`, GameScene のみ)

開閉: Esc (`PauseMenuView.cs:793-797`) + HUD 一時停止ボタン (`GameHudRuntime.cs:90`)。**`PAUSE` と表示するが何も一時停止しない** (→ B-2)。

| タブ | 中身 | 入力 | ハンドラ → 効果 | 根拠 |
|---|---|---|---|---|
| `Settings` | `NoteSpeed / NoteTiming / MainVolume / BGMVolume / SEVolume / Flash` の 6 行 | ↑↓ 選択、←→ / `<>` / クリックで調整。タブ切替は **S/D/L/K 固定キー** (`:805-808`) | `GManager.*` を更新 + `OptionSettings.Save()` (即時永続) + 音量は即時適用 | `PauseMenuView.cs:197-269,734-789` |
| `Display` | `Key Guide` / `Lyrics` の 2 行 | 同左 | `PlaySession.*` (セッションのみ、**非永続** → D-1) + HUD 再構築 / 歌詞パ表示切替 | `PauseMenuView.cs:325-372,394-399` |
| `Ranking` | `この曲のベスト (ローカル)` = `1 / YOU / 自分のベスト`、`この端末の合計 (ローカル)` | なし (表示のみ、構築時に 1 回) | `GManager.Hiscore` 実値。**曲ごとでなくスロット共用の問題を引く** (→ A-6) | `PauseMenuView.cs:427-469` |
| `Keys` | `Lane 1..8` 見出し + JIS キーボードの色分け | なし | **`Judge.LaneKeys` (変更されない静的既定値) から色を塗る**。実際の `KeyBindings.Current` ではない (→ C-6) | `PauseMenuView.cs:473-536` / `Judge.cs:38-48` |
| `Retire` | `Backspace` (`KeyActions.MenuRetire`) / クリック | `GameSceneManager.RetireGame()` → Fade SelectScene | `PauseMenuView.cs:627-631,810` / `GameSceneManager.cs:205-209` |
| `Retry` | `R` (`MenuRetry`) / クリック | `RetryGame()` → `GManager.ResetSession()` + Fade GameScene | `PauseMenuView.cs:632-636,809` / `GameSceneManager.cs:210-214` |
| `Resume` | `Esc` (固定表記) / クリック | `Close()` | `PauseMenuView.cs:637,660-677` |

## 6. Option (`OptionSceneManager.cs` + `V5/OptionView.cs`)

| タブ | 行 | ハンドラ → 永続化 | 根拠 |
|---|---|---|---|
| Play | `NoteSpeed` | `GManager.noteSpeed ±0.1` (1..50) + `OptionSettings.Save()` | `OptionView.cs:299-304` / `OptionSceneManager.cs:355-369` |
| Play | `NoteTiming` | `GManager.noteTiming ±1` (−10..10) + 保存 + **全曲の設定 JSON の `offset` を書き換え** (→ C-7) | `OptionSceneManager.cs:318-343` |
| Play | `Key Layout` | `→` でキー設定を開く。**`←` で確認なしに全レーン既定へリセット** (→ C-8) | `OptionView.cs:311-327` |
| Play | `キー設定` | F2 / Enter で `KeyConfigUI.Open()` | `OptionView.cs:328-333,442-466` |
| Display | `Flash / FlashTiming / FlashSpeed` | `GManager.Flash / FlashT / FlashTサイクル` + 保存。**FlashTiming はクランプなし** (→ D-6) | `OptionView.cs:341-358` / `OptionSceneManager.cs:89-99` |
| Sound | `Main / Effect / BGM` | ±0.1 (0..1) + `AudioListener.volume` + 保存 | `OptionView.cs:366-399` / `OptionSceneManager.cs:71-74,194-203` |
| Data | `セーブデータ削除` | 2 段階確認 → `PlayerPrefs["Hiscore"]` 削除 + `KeyBindings.ResetToDefaults()` | `OptionView.cs:407-436` |
| Data | メディアキャッシュ | `MediaCacheManager.ClearAll()` — **パネルは v5 の不透明背景の下で不可視・操作不能** (→ A-3) | `MediaCacheOptionPanel.cs:24-59,96-110` |
| — | 戻る `<` / Esc | `Initiate.Fade("TitleScene")` | `OptionView.cs:473-485,224-228` / `OptionSceneManager.cs:310-316` |

## 7. Result (`ResultSceneManager.cs` + `V5/ResultView.cs` + `ResultRankingOverlay`)

| 表示 | 入力 | ハンドラ → 効果 | 根拠 |
|---|---|---|---|
| `RESULT` / 曲名 / ランク / `SCORE` / `HISCORE (前回 +差分)` / `BPM / LEVEL / NOTES` / 判定内訳 + 比率バー | なし (表示) | `GManager` 実値のスナップショット (`ResultSceneManager.cs:96-111`) | `ResultView.cs:277-502` |
| `Retry` + `[R]` | R / マウス | スコア類をゼロ化して `Initiate.Fade("GameScene")` (`GManager.ResetSession()` と同値の自前実装 → D-7) | `ResultView.cs:661,699-702,214-217` / `ResultSceneManager.cs:130-145` |
| `Name` + `[Enter]` | Enter / マウス | 名前モーダル (`TMP_InputField` 8 文字) → `PlayerPrefs["V5PlayerName"]` 保存。**空入力は `YOU` として保存され `名前あり` になる** (→ B-10) | `ResultView.cs:659,746-760,929-965` |
| `OK` + `[Space]` | Space / Esc / マウス | `Initiate.Fade("SelectScene")` | `ResultView.cs:655,220-230` / `ResultSceneManager.cs:146-151` |
| ランキング登録モーダル (自動, 1.2 秒後) | タイプ / Enter 送信 / Esc スキップ | `RankingClient.PostScore` (POST `/api/v1/scores`, maxCombo を送信) → 成功で Top10 表示、失敗で `送信できませんでした 再送 [Enter]` | `ResultRankingOverlay.cs:117-125,237-313` / `RankingClient.cs:240-268` |
| ランキング板 (F5) | F5 / Esc | `RankingClient.GetLeaderboard` (GET) → Top10 + 自枠ハイライト | `RankingPanel.cs:173-385` / `ResultRankingOverlay.cs:111-115,227-231` |
| モーダル中のキー漏れ | — | **モーダル表示中も ResultView の R / Space / Enter が生きている** (→ A-7) | `ResultRankingOverlay.cs:337-345` / `ResultView.cs:214-235` |

---

## 8. 監査結果一覧 (未接続 / 偽値 / 誤ラベル / 空画面)

### A. 未接続 (表示されるが機能しない / どこからも届かない)

| # | 内容 | 根拠 |
|---|---|---|
| A-1 | **タイトルの `Ranking` メニューは永久に非表示**。`TitleLinks.RankingRequested` は宣言のみで、代入するコードがリポジトリ全体に存在しない (`TitleLinks.cs:14` のみ定義、参照は `TitleView.cs:100,102,237,358` の読み取りのみ)。コメント「ランキング クライアント側が設定する」は未履行。F5 のランキング板は Title で動くので、機能はあるのに入口だけが死んでいる | `TitleView.cs:235-250,356-359` |
| A-2 | **`GameErrorView.Retry` が `GManager.ResetSession()` を呼ばない**。`Hide(); Initiate.Fade("GameScene")` のみ (`GameErrorView.cs:127-133`)。`ResetSession` の呼び出し元は `SelectSceneManager.cs:106,134` / `GameSceneManager.cs:212` / `AutoTestPlay.cs:65` のみ。エラー画面からのリトライは前回のスコア/コンボ/判定カウンタを引きずって再開し、v5 HUD が即座にそれを表示する (`GameHudView.cs:428-450`) | `GameErrorView.cs:127-133` |
| A-3 | **Option のメディアキャッシュパネルが不可視**。`canvas.sortingOrder = 100` (`MediaCacheOptionPanel.cs:59`) に対し v5 Option は order 500 の不透明背景 (`V5GameUi.cs:36` / `V5Decor.cs:49` `V5Tokens.Bg`=#1B1B1B 不透明)。「Clear media cache」ボタンとそのステータス表示は見えず押せない。`Refresh()` は毎フレーム動き続ける (`MediaCacheOptionPanel.cs:46-49`) | 同左 |
| A-4 | **レガシー pause メニュー一式が到達不能**。`MenuPanel/MenuButton` は起動時に非表示 + 毎フレーム強制非表示 (`GameHudRuntime.cs:216-224,140-150`)。`GameSceneManager` の `MenuRetry/MenuEnd/MenuRetire/Back/MenuSwitch` 処理は `MenuPanel.activeSelf` 条件付きで到達不能 (`GameSceneManager.cs:262-288`)。シーン配置の `もう一度 / 結果をみる / 選曲にもどる / とじる` ボタンも死んでいる | 同左 |
| A-5 | **Select のランキングで Tab で `Online` に切ると取得せずに偽の空状態を出す**。Tab は `rankingSegment = 1 - rankingSegment; Rebuild()` のみ (`SelectView.cs:233`)。`FetchOnlineRanking` はマウスクリック時のみ (`SelectView.cs:1536-1551`)。未取得状態 (`onlineEntries==null && !loading && !failed`) では「この曲はまだ順位がありません」と表示 (`SelectView.cs:1697-1701`) — 未取得なのに「順位がない」と表示する | 同左 |
| A-6 | **クラウド追加曲のハイスコアが全曲でスロット 0 を共有**。`GManager.Hiscore` は固定 `int[45]` (`GManager.cs:328`) に対し、クラウド曲は `id = 45+` になり得る。Result は `hiIndex = songID >= Length ? 0 : songID` で**スロット 0 に上書き保存**し (`ResultSceneManager.cs:48-53`, コメント「slot 0 unused」に反して書く)、Select/Pause/Result の表示も同じフォールバックで読む (`SelectView.cs:370-373` / `PauseMenuView.cs:444` / `ResultSceneManager.cs:105`) → 曲 B の Result に曲 A のベストが表示され、履歴・並び替え・統計からも漏れる | 同左 |
| A-7 | **モーダル中のキー漏れ**。`ResultRankingOverlay` の `SuspendScene` は `*SceneManager` しか無効化しない (`ResultRankingOverlay.cs:337-345`)。v5 の入力本体 (`ResultView` / `TitleView` / `SelectView`) は生きたまま: 登録モーダルや F5 板の表示中に R でリトライ (`ResultView.cs:214-217`)、Space で選曲へ (`ResultView.cs:226-230`)、Enter は板の送信と ResultView の名前モーダルが両方発火 (`ResultView.cs:232-235` + `ResultRankingOverlay.cs:249-263`)。Title でも F5 板中表示中に Space で GameStart する (`TitleView.cs:153-157`) | 同左 |
| A-8 | **`KeyGuideUI` のトグルボタンが押せない**。キャンバスが `Canvas + CanvasScaler` のみで `GraphicRaycaster` が無い (`KeyGuideUI.cs:88-95`)。`onClick` は接続済み (`KeyGuideUI.cs:158`) だがレイキャストが届かない。Tab のみ有効 | 同左 |
| A-9 | **TypingScene (`TipingScene].unity`) が孤児**。`TypingSceneManager.cs` を到達させる遷移がコード内に存在せず (`TipingScene`/`TypingScene` の参照は自ファイルのみ)、EditorBuildSettings の 6 シーンにも含まれない (`ProjectSettings/EditorBuildSettings.asset:9-24`)。歌詞タイピング機能は Select 内の Typing 状態 (`SelectView`) が担っている | 同左 |
| A-10 | **`LoadingOverlayHook.Show/Hide` に呼び出し元がない**。差し替えポイントとして作られたが `Bind` されるだけ (`GameHudRuntime.cs:80`、`GameLoadingView.cs:96-114`) | 同左 |

### B. 偽値 (実際と違う値を事実として表示)

| # | 内容 | 根拠 |
|---|---|---|
| B-1 | **HISTORY の判定内訳 6 列が常時 `–`** (`SelectView.cs:1399-1441`、内訳未記録のため。比率バーも常時空)。ラベルだけの列が 6 つ並ぶ | 同左 |
| B-2 | **`PAUSE` が何も一時停止しない**。`Time.timeScale` の書き込みはゲームコード全体に存在せず、`Open()` は UI 状態だけ触る (`PauseMenuView.cs:660-677`)。`Judge.Update` は `GManager.Start` のみがゲート (`Judge.cs:62`) で、ノーツは落ち続け、**92% 黑の幕の下で Miss が蓄積する** (`PauseMenuView.cs:94`)。さらにメニューの S/D/L/K/R は既定ではレーンキーでもあるため (`Judge.cs:40-47`)、タブ操作がそのまま判定を起こす (`Judge.cs:202-205` に一時停止ゲートなし) | 同左 |
| B-3 | **v5 HUD のスコアレールが負の幅で描かれる**。`GameHudLogic.RailFillWidth(score, railWidth)` は px を返す設計 (テストは `RailFillWidth(1_000_000, 600f) == 530f`, `V5GameHudTests.cs:55`) なのに、呼び出し側が `1f` を渡して `RailWidth` を掛ける (`GameHudView.cs:436-439`) → `Clamp01(f) × (1−70) = 負`。Best マーカーも同様、`+` ゾーンは `RailPlusWidth(…,1f) × 70` で最大 70 倍オーバー (`GameHudLogic.cs:54-58` は px を返す) | 同左 |
| B-4 | **`+ COMBO BONUS +N` と `次のボーナス +N` は実在しない加点の表示**。`BonusTotal = combo*50` は近似だとコメントに明記 (`GameHudLogic.cs:18-23`「正確な式は .pen から復元できないため combo*50 を近似に使う (報告済み)」)、`NextBonusReward = milestone*10` (`GameHudLogic.cs:32-35`)。`JudgeScoreboard` の配点にはどちらも存在しない | 同左 |
| B-5 | **ローディングバーは進捗ではなく PingPong 演出** (`GameLoadingView.cs:77-83`、進捗 API が無い旨はコメント明記 `GameLoadingView.cs:10-13`)。ラベル `Loading youtube video ...` のみ英語で他と不整合 (`GameLoadingView.cs:49`) | 同左 |
| B-6 | **エラーダイアログが原因に関係なく `YouTube エラー 403` と表示** (`GameErrorView.cs:60`)。タイムアウト・ストリーム未解決・メディア無しでも 403 表示。本文 `譜面と音楽は読み込まれています` (`:62-64`) は `StreamUnresolved/MediaUnavailable` では虚偽 (`MusicManager.cs:313-319`) | 同左 |
| B-7 | **`NO MISS` と `FULL COMBO` が同一条件で同時に表示** (`GameHudView.cs:446-447` 両方 `miss == 0 && judged`)。曲の途中から FULL COMBO と出る | 同左 |
| B-8 | **Result の `Combo` は最終コンボ (切断後は 0) を表示**し、最大コンボではない (`ResultSceneManager.cs:103` `Combo = GManager.combo`。最大は `maxCombo` だがランキング API にのみ送られる `ResultRankingOverlay.cs:287`)。最後のノーツが Miss だと「Combo 0」と表示される | 同左 |
| B-9 | **Result の名前登録で空入力が `YOU` として保存され `名前あり` になる** (`ResultView.cs:946-953,961`)。ユーザーが入力していない名前が「登録済み」として表示される。またローカル名 (`V5PlayerName`) とオンライン送信用 (`RankingNickname`) は別保存で、片方の設定はもう片方に反映されない (`ResultView.cs:38` / `RankingClient.cs:24`) | 同左 |
| B-10 | **ポーズ `Keys` タブが既定配置の色のまま**。`Judge.LaneKeys` (静的・不変, `Judge.cs:38-48`) から色を塗るため、`KeyConfigUI` で変更した実際の `KeyBindings.Current` と一致しない (`PauseMenuView.cs:512-536`、実プレイは `KeyBindings.GetKeys` を使う `Judge.cs:202`) | 同左 |
| B-11 | **ポーズの `BGMVolume` は GameScene で何にも効かない**。ヘルプは `BGM (動画音声) の音量です` (`PauseMenuView.cs:74`) だが、`ApplyVolumes` は main/effect のみ適用 (`PauseMenuView.cs:772-789`)。`GManager.BGMVolume` の実行時消費者は `VideoTime.cs:20` のみで、`VideoTime` は GameScene に配置されていない。動画音声は実際は `MainVolume` に従う (`MusicManager.cs:139,502`) | 同左 |

### C. 誤ラベル (表示と挙動の不一致)

| # | 内容 | 根拠 |
|---|---|---|
| C-1 | **HISTORY の `N plays` はプレイ回数ではなく記録のある曲数** (`SelectView.cs:1243` + `HistoryIds()` は hiscore>0 の曲 ID 一覧 `SelectView.cs:353-368`) | 同左 |
| C-2 | **Title 凡例 `Enter 決定` に対し実際の決定は Space 主体** (`KeyActions.Start = Space,Return,KeypadEnter` `KeyActions.cs:52`。GameStart 行のキーキャップは Space と表示 `TitleView.cs:378-379` で凡例と食い違い) | 同左 |
| C-3 | **Sort `Recent` は新しい順ではない** (実体は ID 昇順 `SelectViewLogic.cs:129-131`、コメント `Recent=id 昇順`) | 同左 |
| C-4 | **検索フィールド `タイプして検索` に直接入力しても何も起きない** (Typing 状態へは Tab から、`SelectView.cs:140,147`)。しかも Tab はキーガイドの開閉と二重に束縛 (KeyGuideUI が Tab を消費、`KeyGuideUI.cs:65-68`) | 同左 |
| C-5 | **Ready 画面の `←` はタイトルへ飛ぶが Esc は選曲へ戻る** (`SelectView.cs:821` の戻るは常に Title、`SV:194` は List へ)。凡例は `Esc 選曲へ` のみ (`SelectView.cs:923-928`) | 同左 |
| C-6 | **Option の `Key Layout` 行で `←` を押すと確認なしに全レーン割当を初期化** (`OptionView.cs:319-322` `KeyBindings.ResetToDefaults(); KeyBindings.Save();`)。説明文に破壊的操作の記載なし (`:327`)。セーブデータ削除には 2 段階確認がある (`:419-426`) のに不整合 | 同左 |
| C-7 | **Option の `NoteTiming` ±1 が全曲の設定 JSON の `offset` を書き換える** (`OptionSceneManager.cs:322-329`、`i=1..totalSong` をループ保存)。行の説明は「判定タイミングのずれを調整します」のみ (`OptionView.cs:310`) | 同左 |
| C-8 | **KeyConfigUI のフッター `Tab 切替` は行移動の意味** (`KeyConfigUI.cs:1000` が `PgUp/PgDn 切替` の隣に `Tab 切替` と表示するが、実挙動は `MoveSelection(1)` `:363`)。ページ切替は ←→ (`:358-359`) | 同左 |
| C-9 | **`KeyActions` の説明と実挙動の不一致** (リバインドしても効かない): `Back` は「ポーズメニューを開く・閉じる」説明 (`KeyActions.cs:18`) だが v5 ポーズは生 `KeyCode.Escape` を直接読む (`PauseMenuView.cs:793`)。`MenuEnd` (E) と `MenuSwitch` (T) は GameScene では到達不能なレガシー経路でのみ消費 (`GameSceneManager.cs:272-287`)。ポーズのタブ切替は S/D/L/K 固定で設定対象外 (`PauseMenuView.cs:803-808`) | 同左 |
| C-10 | **Result 凡例 `Space 決定` は「選曲へ戻る」の意味** (`ResultView.cs:619` 凡例 vs `:226-230` 実挙動。ボタン選択の決定は Enter `:232-235`)。Esc で戻れることも凡例にない | 同左 |

### D. 空画面 / 非永続 / 状態

| # | 内容 | 根拠 |
|---|---|---|
| D-1 | **Display 系の設定 (Key Guide / Lyrics) が永続化されない**。`PlaySession` の静的フィールドのみ (`PlaySession.cs:43-58`)。`PlaySession.Reset()` の呼び出し元はテストのみ (`V5UiWiringTests.cs:190,234`) → アプリ再起動で初期化。対して Settings タブは即時永続 (`PauseMenuView.cs:768`) | 同左 |
| D-2 | **ポーズ Ranking タブは構築時に 1 回だけ作られ更新されない** (`Open()` は Settings の値のみ更新 `PauseMenuView.cs:664,723-732`) | 同左 |
| D-3 | **`PLAYERS (累計)` 枠が常時固定文** `ローカルモードではプレイヤーごとの記録はありません` (`SelectView.cs:1084-1086`)。毎回描画される常時空パネル | 同左 |
| D-4 | **プレイヤープロフィールの順位が固定 `–位 / –人`** (`SelectView.cs:1600`)。値があるように見える枠に常時プレースホルダ | 同左 |
| D-5 | **検索/フィルタで 0 件でも空メッセージが出ない** (リスト領域が無言で空になる, `SelectView.cs:778-796`) | 同左 |
| D-6 | **Option の `FlashTiming` にクランプがない** (`OptionView.cs:349`、`NoteTiming` は ±10 クランプ付き `OptionSceneManager.cs:320`)。無限に増減し保存される | 同左 |
| D-7 | **Result の Retry が `GManager.ResetSession()` を使わず同値を手書き** (`ResultSceneManager.cs:132-145`)。今日は等価だが、フィールド追加時に二重管理がズレるリスク。対照的にゲーム内 Retry は正本を呼ぶ (`GameSceneManager.cs:212`) | 同左 |
| D-8 | **ポーズ中も Space で曲が始まる** (再生前のポーズ表示中、`MusicManager.cs:648-652` にポーズ状態のチェックがない) と、判定幅の不整合: ポーズの NoteSpeed は 1..30 でクランプ (`PauseMenuView.cs:739`) するが Option/Ready は 1..50 (`OptionSceneManager.cs:44`)。50 に設定した値をポーズで触ると 30 に潰れる | 同左 |
| D-9 | **空のランキングを取得失敗として表示**。`RankingClient` は空配列を `null` (失敗) 扱い (`RankingClient.cs:187`) → 「ランキングをとりにいけませんでした」表示 (`ResultRankingOverlay.cs:179`)。新曲の「0 件」が通信エラーに見える | 同左 |
| D-10 | **オンラインランキングの匿名エントリに `YOU` と表示** (`SelectView.cs:1710` `nickname が空なら YOU`)。他人のスコアが自分のラベルで見える | 同左 |

---

### 8.1 再照合 (2026-11-10, v6 UI 対応)

v6 UI (`Assets/Game/Scripts/UI/V6/`, `V6UiMode` 既定値: GameHud / SelectScreen / Pause / Result / Option / Title すべて有効) の投入後、§8 のリストを現行コードに対して再照合した。判定は「誰がそのビューを生成するか・入力経路が届くか」で行った (クラスが残っていること自体は到達性の根拠にしない)。既定フラグで到達しない v5 専用経路は **moot**、v6 で修正済みは **fixed**、依然到達して誤りのものは **live** とする。

| # | 状態 | 根拠 (2026-11-10 時点) |
|---|---|---|
| A-1 | fixed | v6 タイトルの Ranking タブが実遷移に接続 (`TitleSceneManager.cs:43` → `OnRankingRequested` → `PendingEntry` 経由で Select の Ranking を初期表示)。v5 `TitleView` はどこからも生成されない (`TitleSceneManager.cs:20-22` コメント) |
| A-2 | fixed | v6 モードでは v5 `GameErrorView` は常に非表示 (`GameUiExclusivity.cs:127-146` Resolve: v6 有効時は Error を立てない)。v6 の失敗表示 `V6LoadingView.Retry` は `GameSceneManager.RetryGame` (=`GManager.ResetSession` + Fade) を呼ぶ (`V6LoadingView.cs:373-389`) |
| A-3 | fixed | Option は無条件で v6 `V6OptionView` を構築 (`OptionSceneManager.cs:215-219`)。Media cache は Data タブの行として到達可能 (`V6OptionView.cs:974-980`)。旧 `MediaCacheOptionPanel` の不可視問題の経路は死んだ |
| A-4 | moot | レガシー MenuPanel は起動時非表示 + 0.5 秒ごとに再非表示 (`GameHudRuntime.cs` `LegacyHideNames` / `LateUpdate:176-186`)。Esc は v6 ポーズが担う (`V6GameRuntime.cs` `UpdatePause`) |
| A-5 | fixed | v6 ランキングは選択・表示時に実取得 (`V6SelectDataSource.cs:130,243,562`)。空状態は取得中 / 失敗 / 0 件を区別 (`V6RankingLogic.cs:170-189`) |
| A-6 | **fixed (本再照合)** | `ResultSceneManager.ApplyBestScore` が固定テーブル外 (cloud 追加曲) をスロット 0 に上書きしていたのを、安定キー (remoteId@chartVersion) の保存先へ書くよう修正 (`ResultSceneManager.cs:65-83`)。読み側も V6ResultView (`V6ResultView.cs:1424-1441`) / v6 HUD ゲージ (`V6GameHudView.cs:126-128`) / v6 ポーズ (`V6PauseView.cs:545-555`) で安定キーへフォールバック |
| A-7 | fixed | v6 Result はモーダル表示中・入力ゲート中は背面キーを捨てる (`V6ResultView.cs:240-247`)。旧自動モーダルは v6 で無効化 (`ResultSceneManager.cs:143`)。Title も `SceneInputGate` を確認 (`TitleSceneManager.cs:213`) |
| A-8 | moot | v6 モードでは KeyGuideUI を生成せず、既存も破棄 (`GameSceneManager.cs:89` / `SelectSceneManager.cs:72` / `TitleSceneManager.cs:325` → `V6UiMode.HideLegacyKeyGuide`)。v6 HUD のキーガイド行は `KeyBindings` 実値 (`V6GameHudView.cs:717`) |
| A-9 | moot | TypingScene への遷移は存在しないまま (孤児の到達不能シーン)。プレイヤーから届かない |
| A-10 | moot | `LoadingOverlayHook.Show/Hide` の呼び出し元は無いまま (`GameHudRuntime.cs:100` Bind のみ)。v5 GameLoadingView 自体が v6 モードで非表示 (`GameUiExclusivity.cs:127-146`) |
| B-1 | fixed | v6 履歴の判定内訳は直前プレイの実値 (`V6HistoryView.cs:712-721`)。死んだ HOLD セルは削除済み |
| B-2 | fixed | v6 ポーズは `PauseGate.Enter/Exit` (`V6PauseView.cs:148,164`)。MusicManager (`MusicManager.cs:920`) / Judge (`Judge.cs:110`) / Notes (`Notes.cs:91`) が停止する |
| B-3 | fixed | v5 GameHudView は設計 px を渡すよう修正済み (`GameHudView.cs:450-456`、#162 B-3 コメント)。v6 HUD は px を直接計算 (`V6HudLogic.cs:40-52`)。なお v5 HUD は v6 モードで生成されない (`GameHudRuntime.cs:83-85`) |
| B-4 | **fixed (本再照合)** | v6 HUD の「+ COMBO BONUS +N」「次のボーナス +N」(近似値 / 仮の式。`GameHudLogic.cs:20-35`。`JudgeScoreboard` の配点にコンボボーナスは存在しない) を廃止 (`V6GameHudView.cs:611-615`、`V6HudLogic.ShowComboBonus` 削除)。v5 GameHudView には残るが v6 モードでは生成されない (`GameHudRuntime.cs:83-85`) |
| B-5 | fixed | v6 ローディングは譜面準備の実進捗 (`GameLoadingProgress`、`NotesManager.cs:240`) を出し、未報告時は「不定」と正直に出す (`V6GameRuntime.cs:236-241` / `V6LoadingViewLogic.cs:91`) |
| B-6 | fixed | v6 の失敗表示は原因を断定しない文言 (`V6GameRuntime.cs:243`)。403 固定表示の v5 GameErrorView は v6 モードで非表示 (`GameUiExclusivity.cs:127-146`) |
| B-7 | fixed | v6 の FULL COMBO チップは曲完走 (全ノーツ判定済み) かつノーミスのときだけ出る (`V6GameHudView.cs:139-145`) |
| B-8 | fixed | v6 Result の Combo は最大コンボ (`runMaxCombo`) を出す (`V6ResultView.cs:1134,1147`) |
| B-9 | fixed | v6 はオンラインで空入力を拒否 (`V6ResultLogic.cs:283-307`)、ローカル保存は保存名を明示するメッセージ (`V6ResultView.cs:888` / `V6ResultLogic.cs:378-380`。空入力→`YOU` は v5 互換の意図的既定値)。ローカル名とオンライン名は `InitialName` で統合 (`V6ResultLogic.cs:387-400`) |
| B-10 | fixed | v6 ポーズのキー一覧は `KeyBindings.Current` 実値で、`Changed` で即時更新 (`V6PauseView.cs:1073-1090`) |
| B-11 | **fixed (本再照合)** | v6 ポーズの「BGMVolume」行を廃止 (`V6PauseView.cs:449-455`)。動画音声は MainVolume に従い (`MusicManager.cs:201,754,1184`)、BGMVolume の実行時消費者は GameScene に居ない (`VideoTime.cs:20` のみ)。「押しても効かない行は描画しない」(Key Layout 行廃止と同じ規律) |
| C-1 | fixed | v6 履歴の件数は「記録のある曲数」の文言 (`V6HistoryLogic.cs:239-243`) |
| C-2 | fixed | v6 タイトルのキーキャップは `KeyActions` の実割当から取る (`V6TitleView.cs:574,701-708`)。「Enter 決定」凡例の v5 TitleView は生成されない |
| C-3 | moot | v6 選曲に Sort コントロールは無い (カタログ順固定) |
| C-4 | fixed | v6 は Tab で検索フォーカス → `Input.inputString` を `SetQuery` (`V6SelectView.cs:222,262-268`)。KeyGuideUI が v6 で破棄されるため Tab の二重束縛も解消 (`SelectSceneManager.cs:72`) |
| C-5 | moot | Ready 状態は廃止 (Issue #233)。v6 の Esc はタイトルへ (`SelectSceneManager.cs:149-154` `OnV6Back`) |
| C-6 | **fixed (本再照合)** | v6 Option の Key Layout `←` リセットが確認モーダルを経由する (`V6OptionView.cs:880,1129-1134`)。説明文にも明記 (`V6OptionView.cs:853`)。既に標準のときはモーダルを出さない |
| C-7 | **fixed (本再照合)** | 全曲 offset への書き戻し自体は既存の保存経路 (意図的, `OptionSceneManager.cs:381-399`)。説明文に「変更は全曲の譜面設定 (offset) に書き込まれます」を追記 (`V6OptionView.cs:837`) |
| C-8 | **fixed (本再照合)** | KeyConfigUI フッターを実挙動どおり ←→「切替」/ Tab「選択」に修正 (`KeyConfigUI.cs:1021-1025`。実装は `KeyConfigUI.cs:381-390`) |
| C-9 | fixed | v6 ポーズの開閉は `KeyActions.Back` (`V6GameRuntime.cs` `UpdatePause`)、MenuEnd / MenuSwitch も v6 ポーズ経由で到達可能。v5 の生 `KeyCode.Escape` 読みは v6 モードで非表示 (`V6GameRuntime.cs` `LateUpdate`) |
| C-10 | fixed | v6 Result の凡例は実ボタンと一致 (R=リトライ / Enter=登録 / Space=決定=OK, `V6ResultLogic.cs:163-164` と `V6ResultView.cs` Tick の実装) |
| D-1 | **fixed (2026-11-10 追加対応)** | Key Guide / Lyrics を `PlayerPrefs` へ保存する (`PlaySession.Save`/`Load`, `PlaySession.cs:95,107`、保存先は `IDisplaySettingsStorage` = `Assets/Game/Scripts/Persistence/DisplaySettingsStorage.cs`)。設定変更は即時保存 (`SetKeyGuide`/`SetLyricsEnabled`)、起動時に `TitleSceneManager.Start` が `PlaySession.Load()` で復元する (`TitleSceneManager.cs:51`)。範囲外の保存値は既定値へ寄せる。`Reset()` はセッション値を戻すだけで保存値には触らない (やり直しで利用者の選択を消さない)。テスト: `PlaySessionPersistenceTests.cs` 7 件 |
| D-2 | fixed | v6 ポーズの値行は `Func<string>` Read を開いている間毎フレーム再評価 (`V6PauseView.cs:1024-1040` / `Tick:1142-1150`) |
| D-3 | fixed | v6 選曲の PLAYERS は「データがありません」(`V6SelectView.cs:917-921`)、Ranking 画面は「プレイヤー別の集計には対応していません」(`V6RankingLogic.cs:219-222`) |
| D-4 | fixed | v6 履歴の「順位」セルは削除済み (`V6HistoryView.cs:692-694`) |
| D-5 | fixed | v6 選曲は 0 件で「曲が見つかりません」の空状態を出す (`V6SelectView.cs:571-584`) |
| D-6 | fixed | FlashTiming は負へ寄せない (`V6OptionView.cs:1045-1048` / `OptionSceneManager.cs:412,418`)。上限は意図的に無し (`FlashT/100` = フラッシュ BPM) |
| D-7 | **fixed (本再照合)** | Result の Retry は正本 `GManager.ResetSession` を呼ぶ (`ResultSceneManager.cs:208-223`)。旧自前実装は `settledNoteCount` とプレイセッションの固定 (`BeginPlaySession`) を漏らしていた |
| D-8 | fixed | ポーズ中は `PauseGate` でメディア・判定・Space 開始が停止 (`MusicManager.cs:920-925`)。NoteSpeed クランプは Option/Ready と同じ 1..50 (`PauseMenuView.cs:784-787`) |
| D-9 | fixed | 空配列は取得成功・0 件扱い (`RankingClient.cs:188-195`)。空状態文言は取得中 / 失敗 / 0 件を区別 (`V6RankingLogic.cs:170-189`) |
| D-10 | fixed | v6 はニックネーム一致のときだけ YOU を付ける (`V6SelectDataSource.cs:504` / `V6ResultView.cs:1357`)。匿名エントリは空名で表示される |

実機 (Windows / 実ディスプレイ) でしか検証できない項目は本再照合の対象外 (§0 の X1〜X8 を継続利用)。上記の fixed 判定はコード読解 + EditMode テストによる。

## 9. PR #204 (判定キュー) 実プレイ確認ポイント

→ `docs/ui/pr204-playtest-checkpoints.md` (X1〜X8)
