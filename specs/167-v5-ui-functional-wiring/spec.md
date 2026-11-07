---
description: "FlashBeat lean per-issue specification template (project-local override of the Spec Kit core spec template)"
---

# Spec: [Game/P0] Unity 全画面の描画だけのUIを排除し全ボタンを実動作へ接続

**Issue**: #167 | **Branch**: `167` | **PR base**: `release-0-1-11` | **Date**: 2026-11-07 | **Status**: Draft

**Input**: `Issue #167 (親 #162)。v5 UI は「見えるが押せない / 押しても反応しない」状態にある。
全画面 (Title / Select / Game / Pause / Option / Result / Opening) の visible control を
「押した結果が実機能になる」状態へ接続し、機能として成立しない control は画面から除外する。`

> Lean rules (enforced by `docs/agent/spec-kit-workflow.md`):
> one Issue = one branch = one worktree = one Draft PR; ≤5 tasks; no retrospective
> re-specification of the legacy game; no second authority for project governance.

## Objective

オーナーの実機観測「UI は見えるがボタンとして機能せずゲームが進行できない」を解消する。
v5 の Canvas 系統に `GraphicRaycaster` を載せて **マウスクリックが実際に Button を叩く** ようにし、
Select の Ready 画面で選んだ設定 (Key Guide / Lyrics / NoteSpeed) が **GameScene 側に実値で渡る** ようにし、
機能として成立しない行 (Pause の Display タブの表示のみ行、Select の未接続 Ranking Online、
Result の二重登録導線) は **画面から除外するか「ローカル限定」と明記** する。
結果として Title → 選曲 → ゲーム → Result → Retry が一巡でき、各ボタンに実動作がある。

## Owned Paths

- `Assets/Game/Scripts/UI/V5/PlaySession.cs` (新規) — Select(Ready) → Game のセッション設定と、
  Option の実値を永続化する小さな共有ストア。
- `Assets/Game/Scripts/UI/V5/{TitleView,SelectView,SelectViewLogic,OptionView,PauseMenuView,ResultView}.cs`
- `Assets/Game/Scripts/UI/V5/{GameHudView,GameHudLogic,GameHudRuntime,GameReadyView,GameLoadingView,GameErrorView}.cs`
- `Assets/Game/Scripts/UI/V5/{TitleLayout,TitleLinks,V5GameUi,ShortcutKeyCap,ActionKeyHint}.cs`
- `Assets/Game/Scripts/UI/{Title,Select,Game,Result,Option}SceneManager.cs`
- `Assets/Game/Scripts/Gameplay/**` — UI 接続のみ (判定ロジックは触らない)
- `Assets/Tests/Editor/**` — EditMode テスト (新規 `V5UiWiringTests.cs` ほか)
- `specs/167-v5-ui-functional-wiring/**`, `docs/ui/**`

## Exclusions (explicitly out of scope)

- `Assets/Game/Scripts/UI/V5/{UISizeProfile,V5Ui,V5Tokens}.cs` — Worker A 所有 (触らない)。
- `Assets/Game/Scripts/UI/RankingClient.cs` / `RankingPanel.cs` / `ResultRankingOverlay.cs` —
  外部所有。Select の Online セグメントは **既存の `RankingClient.GetLeaderboard` 公共 API を呼ぶだけ** で、
  この 3 ファイル自体は書き換えない。
- `Assets/Game/Scripts/Gameplay/NotesManager.cs` の long note 判定、`Judge.cs` の判定ロジック — Worker D 所有。
- `Assets/Game/Scripts/Songs/GManager.cs` — 他 Issue 所有。共有状態は **新規 `PlaySession.cs`** に逃がす。
- `Assets/**/*.unity` — 本 Issue では **書き換えない**。シーン YAML の EventSystem / CanvasRenderer は
  監査の結果いずれも 1 画面 1 EventSystem・CanvasRenderer 欠損ゼロで要件を満たしていたため、
  変更は最小差分原則に従いコード側 (実行時の Canvas 構築) に集約する。
- Web (`web/**`) — Worker C/D/E/F 所有。
- 見た目のみの変更 (座標 / 色 / スタイル) は本 Issue の成果物にしない。

## User Scenarios & Testing *(mandatory)*

### Scenario 1 — マウスで全画面を操作できる (Priority: P1)

**Given** Title / Select / Option / Result シーンに遷移した (v5 Canvas が表示されている),
**When** v5 のボタン (たとえば Title の「ゲームスタート」, Select の曲行, Option の「戻る」) を
**マウスでクリック**する,
**Then** そのハンドラが **1 回だけ** 呼ばれ, 画面遷移 / 設定変更などの実結果が生じる
(クリックが:none に飲み込まれない)。

**Independent Test**: Title シーンで `GameStart` クリック → `Initiate.Fade("SelectScene")` が走り,
Select シーンで曲行クリック → `GManager.songID` がその曲になり, Ready 画面の `Start` クリック →
`Initiate.Fade("GameScene")` が走る。EditMode テスト `V5UiWiringTests` は実 `Button.onClick` を
`Invoke()` して実ハンドラが呼ばれることを検証する。

### Scenario 2 — Ready 画面の選択が演奏中に効く (Priority: P1)

**Given** Select の Ready 画面で `Key Guide` を `Off` / `Full` に, `Lyrics` を `Off` にする,
**When** `Start` でゲームを開始する,
**Then** GameScene の HUD がその設定で描画される (Key Guide が消る / 全キーが並ぶ / 歌詞が出ない)。
Ready 画面の値は **「画面内に並べるだけ」ではない**。

**Independent Test**: EditMode テスト `V5UiWiringTests.ReadyOptions_AreForwardedToGameHud` が
`PlaySession` の値と HUD の描画結果 (生成オブジェクト数 / キー表示) の一致を検証する。

### Scenario 3 — 機能しない control が残っていない (Priority: P2)

**Given** Pause の Display タブ / Select の Ranking タブ / Result の登録導線,
**When** その画面を開く,
**Then** 押しても実動作しない行 (機能未実装の表示のみ行) は **存在せず**,
online ランキングを持たない / local のみの画面は **「ローカル限定」と明記** されている。

**Independent Test**: EditMode テスト `V5UiWiringTests.PauseDisplay_HasNoDeadRows` /
`V5UiWiringTests.ResultRegister_IsLabelledLocal` が該当行の存在 / ラベルを検証する。

### Edge Cases

- 曲カタログが空 / 対象曲が無編集可能 (placeholder) のとき: Ready / Start ボタンは
  `interactable = false` になり、押しても遷移しない。
- ネットワーク不通 / 対象曲が cloud 曲でないとき: Online セグメントは非表示 (未接続表示を残さない)。
- `noteSpeed` を 0 以下へ下げる操作: 1 にクランプする (ノーツが逆走するのを防ぐ)。
- オプション画面を閉じた後: 設定は `PlayerPrefs` から復元され、次回の起動でも効く。

## Requirements

### 機能 UI 契約表 (T1: 全 visible control の実装根拠)

各行: `画面 / control → 入力 → ハンドラ → 状態・API・永続化効果 → フィードバック → 退出経路`。

#### Opening (`OpeningView` / `OpeningSceneManager`)

| control | 入力 | ハンドラ | 効果 | フィードバック | 退出 |
|---|---|---|---|---|---|
| ロゴ・背景・罫線 | (クリック不可) | `OpeningSceneManager.Update` の経過時間判定 | 経過後 Title へ `Initiate.Fade("TitleScene")` | フェード | Title |

> ボタンのない画面。操作可能な control は 0 (装飾要素は「ボタンのない描画」として許容)。

#### Title (`TitleView` / `TitleSceneManager`)

| control | 入力 | ハンドラ | 効果 | フィードバック | 退出 |
|---|---|---|---|---|---|
| `Menu GameStart` | クリック / `↑↓`+`Enter` / `Space` | `TitleView.Activate` → `owner.GameStart()` | `Initiate.Fade("SelectScene")` | 効果音 + 行ハイライト + フェード | Select |
| `Menu Option` | クリック / `↑↓`+`Enter` | `owner.OptionScene()` | `Initiate.Fade("OptionScene")` | 同上 | Option |
| `Menu End` | クリック / `↑↓`+`Enter` | `owner.EndGame()` | `Application.Quit()` | 同上 | 終了 |
| `Menu Ranking` | — | — | **機能未接続**: `TitleLinks.RankingRequested` が設定された時だけ描画される (既定では描かない) | — | — |
| カタログ徽章 (オンライン/local · N曲) | (クリック不可) | `TitleView.CatalogLabel` | カタログ状態を文字で提示 | — | — |
| 凡例 (`↑↓` 選択 / `Enter` 決定) | (クリック不可) | — | 操作説明 | — | — |

> `Menu Ranking` は外部 Ranking クライアントが `TitleLinks.RankingRequested` を登録するまで
> 描画されない契約であり、本 Issue でこの排他契約は維持する (Ranking 実装は外部所有)。
> 既定のまま「押しても何もしない行」を出さない = 画面の偽値を排除する。

#### Select (`SelectView` / `SelectSceneManager`)

| control | 入力 | ハンドラ | 効果 | フィードバック | 退出 |
|---|---|---|---|---|---|
| `Back ←` | クリック / `Esc` / `Back` | `owner.TitleScene()` | `Initiate.Fade("TitleScene")` | 効果音 + フェード | Title |
| 曲行 `Song <id>` | クリック | `GManager.songID = id` → `Rebuild()` | 選択中の曲が変わる / BEST / グレードが更新 | 行ハイライト | — |
| 曲行 `Song <id>` | `↑↓` / ホイール | `MoveSelection` | `GManager.songID` を前後 | 同上 | — |
| `Ready` (右下) | クリック / `Enter` | `EnterReady()` | Ready パネル表示 (設定 3 行 + Start) | パネル切替 | — |
| `Start` (Ready パネル内) | クリック / `Enter`/`Space`/`R` | `StartSelected()` → `owner.GameScene()` | `GManager.ResetSession()` + `Initiate.Fade("GameScene")` | フェード | Game |
| Ready `SettingRow Key Guide` の `←`/`→` | クリック / `←→` | `ChangeReadyValue(0)` | `PlaySession.KeyGuide` を Compact/Full/Off で切替 → **HUD のガイド表示に反映** | 値テキスト更新 | — |
| Ready `SettingRow Lyrics` の `←`/`→` | クリック / `←→` | `ChangeReadyValue(1)` | `PlaySession.LyricsEnabled` を Auto/Off で切替 → **歌詞パネル表示に反映** | 値テキスト更新 | — |
| Ready `SettingRow NoteSpeed` の `←`/`→` | クリック / `←→` | `ChangeReadyValue(2)` | `GManager.noteSpeed` を 1..50 で clamp して設定 → NotesManager の落下速度に反映 + `PlayerPrefs` 保存 | 値テキスト更新 | — |
| 検索欄 / `× Clear` | クリック | `filterText` を更新 → `RefreshFilter()` | 一覧が絞り込まれる | 再描画 | — |
| フィルタ chip (Sort / Level / Status) | クリック | `SelectViewLogic` のモード切替 → `RefreshFilter()` | 並び替え / 絞り込み | chip の色 | — |
| `Typing` (Tab) | `Tab` / クリック | `HandleTypingInput` | キー入力で曲を検索 | 候補一覧 | — |
| `History` (F2) | `F2` / クリック | `EnterHistory()` | プレイ済み曲の一覧 (ローカル) | 一覧 | — |
| `History` の `この曲へ` | クリック | `SelectHistorySong` | `GManager.songID` を変更 | — | List へ |
| `Ranking` (F3) | `F3` / クリック | `EnterRanking()` | Ranking 画面へ | 画面切替 | — |
| Ranking `曲別ランキング` の `Local` | クリック | `rankingSegment = 0` | この端末のベストスコア表示 (**ローカル限定** と明記) | 行 | — |
| Ranking `曲別ランキング` の `Online` | クリック | `rankingSegment = 1` → `RankingClient.GetLeaderboard` | **cloud 曲 (isRemote && chartVersion>0) のときだけ実データを取得表示**。それ以外は Online セグメントを非表示 | 行 / 取得失敗文言 | — |
| Ranking `登録名ごとのランキング` パネル | (クリック不可) | — | **local のみ**であることを明記 (「この端末の記録のみ (オンライン未接続)」) | — | — |
| `Player Profile` 統計 | (クリック不可) | `GManager.Hiscore` から算出 | 累計 / プレイ曲数 / S 以上 | — | — |

#### Game (`GameHudRuntime` / `GameHudView` / `GameReadyView` / `GameLoadingView` / `GameErrorView` / `GameSceneManager`)

| control | 入力 | ハンドラ | 効果 | フィードバック | 退出 |
|---|---|---|---|---|---|
| Ready パネル `Space` | クリック / `Space` | `GameReadyView` → `MusicManager.Start` | 動画 + 音源の再生開始 | パネル消滅 | — |
| HUD `Pause` ボタン | クリック | `PauseMenuView` を開く | 一時停止メニュー表示 | メニュー | — |
| Pause `Resume` | クリック / `Esc` | `PauseMenuView.Close()` | 一時停止解除 | メニュー消滅 | — |
| Pause `Retry` | クリック | `GameSceneManager.RetryGame()` | `GManager.ResetSession()` + GameScene 再読込 | フェード | Game |
| Pause `Retain` | クリック | `GameSceneManager.RetireGame()` | Select へ | フェード | Select |
| Pause Setting タブの `←→` (NoteSpeed / NoteTiming / Main / BGM / SE / Flash) | クリック / `←→` | `PauseMenuView.Adjust` | `GManager` の各値を更新 → 音量・速度・判定幅に実反映 + `PlayerPrefs` 保存 | 値テキスト更新 | — |
| Pause Display タブの `Key Guide` | クリック / `←→` | `PlaySession.KeyGuide` を切替 | HUD のガイド表示が即時反映 | 値テキスト更新 | — |
| Pause Display タブの `Lyrics` | クリック / `←→` | `PlaySession.LyricsEnabled` を切替 + `GameSceneManager` に通知 | 歌詞パネルの表示が即時反映 | 値テキスト更新 | — |
| Pause Display タブの `Live Rank` / `Hit Timing` / `Judge Text` / `Progress Bar` | — | — | **機能未実装 (表示のみ) → 画面から除外** | — | — |
| Pause Ranking タブ | (クリック不可) | `GManager.Hiscore` から算出 | **「この端末の記録 (ローカル限定)」** と明記して表示 | — | — |
| Pause Keys タブ | (クリック不可) | `KeyBindings` から取得 | 現在のキー割り当てをレーン色付きで表示 | — | — |
| Error ビュー `← 選曲へ` | クリック | `GameErrorView` → Select へ | 遷移 | フェード | Select |
| Error ビュー `Retry` | クリック | `GameSceneManager.RetryGame()` | ゲーム再開始 | フェード | Game |
| HUD の順位 / スコア / レール | (クリック不可) | `GManager` の値を毎フレーム反映 | 実データ表示 | — | — |

#### Pause (`PauseMenuView`)

| control | 入力 | ハンドラ | 効果 | フィードバック | 退出 |
|---|---|---|---|---|---|
| タブ (Setting / Display / Ranking / Keys) | クリック / `←→` | `PauseMenuView.SetTab` | タブcontents 切替 | タブハイライト | — |
| 行の `←` / `→` ステッパー | クリック / `←→` | `PauseMenuView.Adjust` | 値の増減 (実機能のある行のみ) | 値テキスト更新 | — |
| `Resume` / `Retry` / `Retain` | クリック | `GameSceneManager` 側へ委譲 | 遷移 / 再開 | フェード | — |

#### Option (`OptionView` / `OptionSceneManager`)

| control | 入力 | ハンドラ | 効果 | フィードバック | 退出 |
|---|---|---|---|---|---|
| `Back` | クリック / `Esc` | `owner.TitleScene()` | Title へ | フェード | Title |
| タブ (Play / Display / Sound / Data) | クリック | `OptionView.SetTab` | タブ切替 | ハイライト | — |
| `NoteSpeed` `←→` | クリック / `←→` | `OptionSceneManager.SpeedUp/Down` | `GManager.noteSpeed` を **1..50 に clamp** して更新 + 保存 | 値テキスト | — |
| `NoteTiming` `←→` | クリック / `←→` | `OptionSceneManager.TimingUp/Down` | `GManager.noteTiming` を更新 + 保存 (判定幅に反映) | 値テキスト | — |
| `Main Volume` `←→` | クリック / `←→` | `GManager.mainVolume` 更新 → `AudioListener` に即反映 + 保存 | 実音量反映 | 値テキスト | — |
| `Effect Volume` `←→` | クリック / `←→` | `GManager.effectVolume` 更新 + 保存 | SE 音量反映 | 値テキスト | — |
| `BGM Volume` `←→` | クリック / `←→` | `GManager.BGMVolume` 更新 + 保存 | BGM 音量反映 | 値テキスト | — |
| `Flash` / `FlashTiming` / `FlashSpeed` `←→` | クリック / `←→` | `GManager.Flash*` 更新 + 保存 | 背景フラッシュ演出に反映 | 値テキスト | — |
| `Key Layout` | クリック | `KeyConfigOptionButton` → キー設定 UI | 実キー設定画面を開く | UI 切替 | — |
| `Clear save data` | クリック (2 段階確認) | `OptionView.ClearSaveData` | Hiscore とキー割り当てを消去 | 確認ラベル変化 | — |

#### Result (`ResultView` / `ResultSceneManager`)

| control | 入力 | ハンドラ | 効果 | フィードバック | 退出 |
|---|---|---|---|---|---|
| `Retry` | クリック / `Enter` | `owner.Retry()` | `GManager.ResetSession()` + GameScene 再読込 | フェード | Game |
| `OK` (Select へ) | クリック / `Esc` | `owner.SelectScene()` | Select へ | フェード | Select |
| `Register` (名前入力モーダル) | クリック | `ResultView` のモーダル | **この端末の保存名**として保存 (ローカル限定 と明記) | モーダル表示 | モーダルを閉じる |
| 順位 / スコア / 判定内訳 / ベストスコア | (クリック不可) | `GManager` の実データ | 表示のみ | — | — |

## 偽値・未接続・誤ラベルの明示リスト (T1 のFindings)

| # | 箇所 | 現状 (実装根拠) | 本 Issue の扱い |
|---|---|---|---|
| F1 | Title / Select / Option / Result の v5 Canvas | `CanvasConfigurator.Configure` は `CanvasScaler` を付けるが **`GraphicRaycaster` を付けない** (`UISizeProfile.cs:170-240`)。シーンの EventSystem は 1 画面 1 個で EventSystem あり (`TitleScene.unity:138` ほか) → **マウスクリックが v5 Button に届かない** | 各 v5 View の Canvas 構築時に `GraphicRaycaster` を追加 (**修正**) |
| F2 | GameScene のレガシー Canvas | レガシー Canvas は `GraphicRaycaster` あり・sortingOrder 0、v5 HUD は -1 → **レガシーが v5 HUD より手前**でabsorbing する | `GameHudRuntime` がレガシー Canvas の `GraphicRaycaster` を無効化 (**修正**) |
| F3 | Select Ready の Key Guide / Lyrics | `SelectView.cs:56-59` の `private static keyGuideMode / lyricsMode` にしか書かれておらず、**GameScene 側は一切読まない** → 偽値 | `PlaySession` 経由で GameScene へ実値渡し (**修正**) |
| F4 | Select Ready の NoteSpeed | `GManager.noteSpeed` を直接書く (実効果あり) が **永続化なし** | `PlaySession` 経由で保存 (**修正**) |
| F5 | Pause Display タブの 4 行 | `Live Rank` / `Hit Timing` / `Judge Text` / `Progress Bar` は表示のみで **toggle が null** → 押しても変化しない偽行 | **画面から除外** (**修正**) |
| F6 | Pause Ranking タブ | 「登録名ごと (累計)」等の見出しだが実体は `RankRow(..., "YOU", GManager.Hiscore[..])` のローカル総計 | 「この端末の記録 (ローカル限定)」と明記 (**修正**) |
| F7 | Select Ranking の Online セグメント | 常に「オンラインランキングは未接続です」 | cloud 曲では `RankingClient.GetLeaderboard` で実取得、cloud 曲以外では **セグメント非表示** (**修正**) |
| F8 | Select Ranking の「登録名ごとのランキング」 | 常に空 + 「プレイヤーごとの記録はありません」 | 「この端末の記録のみ (オンライン未接続)」と明記 (**修正**) |
| F9 | Title の Ranking メニュー | `TitleLinks.RankingRequested` が未設定 → 描かれない (外部所有の Ranking 実装待ち) | 契約を維持 (= 描かない)。押しても何もしない行を出さない (**据置き**) |
| F10 | Result の `Register` | 見出しが「ランキングに登録」だが実際は `PlayerPrefs` に名前を保存するだけ。**オンライン登録は `ResultRankingOverlay` (外部所有) が担当**し、二重導線になっている | v5 側を「この端末の名前 (ローカル)」と明記 (**修正**) |
| F11 | Option の `Key Layout` 表示 | `PlayerPrefs.HasKey("FlashBeat.KeyBindings")` を見るが、実体は `FlashBeat-keyconfig.json` ファイル → 実状態と不一致の可能性 | 実際の `KeyBindings` と既定値の比較で表示 (**修正**) |
| F12 | Option の音量 | `GManager` だけ更新し **AudioListener / AudioSource への即時反映と永続化がない** → 画面在欧洲に替わっても変化が分からない | 即時反映 + `PlayerPrefs` 保存 (**修正**) |
| F13 | Option の NoteSpeed | `SpeedUp/Down` に clamp がなく **0 以下になり得る** (ノーツが逆走) | 1..50 に clamp (**修正**) |
| F14 | Title 起動時 | `TitleSceneManager.Start` が `GManager.noteTiming--` + `OptionSceneManager.TimingUp()` を実行 → **用户的 NoteTiming 設定が既定値へ戻る** | 削除し、保存済み値で復元 (**修正**) |
| F15 | GameScene の Esc | `GameSceneManager` がレガシー `MenuPanel` を開き、`PauseMenuView` も同時に開く → **二重 handling** | v5 Pause のみが Esc を所有 (**修正**) |
| F16 | Result のベストスコア保存 | `SaveLoadManager.Start` (Start 順) と `ResultSceneManager.Start` の順が不定 → **新記録が保存されない可能性** | 記録更新を `Awake` へ移し保存順序を確定 (**修正**) |
| F17 | Select Ready の `Start` ボタン | パネル内と画面右下に **2 個** ある (重複 control) | パネル内のみ残す (**修正**) |
| F18 | Result の F5 ランキング案内 | `RankingPanel` は F5 を出すが、ローカル固定曲では `ResultRankingOverlay.RankingSong()` が null → **開けない案内** | 外部所有のため **Known Issues** に記録 (本 Issue では修正しない) |

## Requirements

- **R1**: Title / Select / Option / Result の v5 Canvas に `GraphicRaycaster` が付き、マウスクリックが実 Button に届く。
- **R2**: GameScene のレガシー Canvas は `GraphicRaycaster` を無効化され、v5 HUD のみがクリックを受ける。
- **R3**: Select Ready の Key Guide / Lyrics / NoteSpeed は `PlaySession` 経由で GameScene 側へ実値が渡り、
  HUD のガイド表示・歌詞表示・落下速度に反映される。
- **R4**: 機能未実装行 (Pause Display の 4 行) は Pause 画面から存在しない。
- **R5**: ローカル限定の情報 (Pause Ranking / Select Ranking の登録名パネル / Result の名前保存) は
  「ローカル」であることが UI 上に明記される。
- **R6**: Select Ranking の Online セグメントは cloud 曲で実データを表示し、cloud 曲では非表示。
- **R7**: Option の音量 / NoteSpeed / NoteTiming / Flash 系は入力中に実動作 (音・落下速度・判定幅) へ反映され、
  `PlayerPrefs` に保存되어次回起動でも復元される。
- **R8**: 入力が一意に決まる (v5 の自前キー処理と Unity の `Selectable` ナビゲーションが二重に動かない)。
- **R9**: 対象曲が無い / placeholder のとき、遷移ボタンは `interactable = false`。
- **R10**: 既存の判定・動画・音声・永続化ロジックは壊さない (判定値・譜面形式・保存キー名を変えない)。

## Dependencies & Gate

- **Depends on**: #162 (親 Issue) — v5 UI の基礎 (#120/#121/#122/#123/#153) が存在すること。
  `RankingClient.GetLeaderboard` は既存 API を利用するため追加依存なし。
- **Blocks**: #162 の完了 (本 Issue が「UI が実動作する」前半を成立させる)。
- **Gate**: EditMode テストが全件 green、Title→Select→Game→Result→Retry の操作手順書が
  `docs/ui/issue-167-ui-acceptance.md` にあること、Supervisor による実機画面確認。

## Verification Evidence

| # | Check | Command / Method | Expected result |
|---|-------|-------------------|-----------------|
| V1 | EditMode テスト | `Unity -batchmode -quit -projectPath . -runTests -testPlatform editmode -testResults TestResults.xml` | 新規 `V5UiWiringTests` を含む全テストが pass |
| V2 | Canvas 構築監査 | `V5UiWiringTests` 内の `GraphicRaycaster` 存在アサーション | v5 Canvas 4 種すべてに `GraphicRaycaster` あり |
| V3 | 実操作手順 | `docs/ui/issue-167-ui-acceptance.md` の手順を Supervisor が実機で実行 | 各手順で期待どおりの遷移 / 変化が起きる |
| V4 | 既存テスト非劣化 | V1 と同じコマンド (回帰) | 既存 `JudgeLogicTests` / `SongDataTests` が pass |

## 実装結果 (Worker B, 2026-11-08)

### Findings の決着

| # | 決着 | 実装 |
|---|---|---|
| F1 | **修正** | `V5GameUi.NewScreenCanvas` を追加し、Title / Select / Option / Result の Canvas に `GraphicRaycaster` を付与 (`V5GameUi.cs`, 各 View の Build) |
| F2 | **修正** | `GameHudRuntime.DisableLegacyRaycaster` でレガシー Canvas の `GraphicRaycaster` を無効化 (v5 HUD のみがクリックを受ける) |
| F3 | **修正** | 新規 `PlaySession.cs` に `KeyGuide` / `LyricsEnabled` を持ち、Select (Ready) → `GameHudView` / `GameSceneManager` が実値で参照 |
| F4 | **修正** | Ready の NoteSpeed は `OptionSettings.Save()` で `PlayerPrefs` に保存、`TitleSceneManager.Start` で復元 |
| F5 | **修正** | Pause の Display タブから `Live Rank` / `Hit Timing` / `Judge Text` / `Progress Bar` を削除 (Key Guide / Lyrics の 2 行だけ残す) |
| F6 | **修正** | Pause Ranking の見出しを「この端末の合計 (ローカル)」「この曲のベスト (ローカル)」へ変更 |
| F7 | **修正** | Select の Online セグメントは cloud 曲 (isRemote && chartVersion>0 && 有効 songId) のときだけ出し、`RankingClient.GetLeaderboard` の実データを表示。非 cloud 曲ではセグメントを出さない |
| F8 | **修正** | Select の登録名パネルを「この端末の記録 (ローカル)」と明記し、**この端末のベストスコア順の実データ** (記録のある曲をスコア降順で最大 9 件) を表示。空パネルにしない |
| F9 | **据置き** | `TitleLinks.RankingRequested` の排他契約を維持 (既定では描かない) |
| F10 | **修正** | Result の action を `Register` → `Name`、モーダル見出しを「Rankingに登録」→「この端末の名前」、補助ラベルを「名前あり / 名前未設定」に変更 |
| F11 | **修正** | `OptionView.KeyLayoutLabel` を実際の割り当てと既定値の比較 (`Judge.LaneKeys`) で判定するよう変更 (旧 PlayerPrefs キー有無の判定は削除) |
| F12 | **修正** | `OptionSceneManager.ApplyVolumes` を追加し、音量変更を `AudioListener` / シーン内 `AudioSource` へ即時反映 + `PlayerPrefs` 保存 + Title / Option 起動時に復元 |
| F13 | **修正** | `OptionSceneManager.SpeedUp/SpeedDown` を 1..50 にクランプ、`TimingUp/Down` を -10..10 にクランプ |
| F14 | **修正** | `TitleSceneManager.Start` の `GManager.noteTiming--` + `TimingUp()` を削除し、`OptionSettings.Load()` で保存値を復元 |
| F15 | **修正** | `GameSceneManager.HasV5PauseMenu()` を追加し、v5 ポーズがある間はレガシー `MenuPanel` を開かない (Esc の二重処理を解消) |
| F16 | **修正** | `ResultSceneManager` のベストスコア更新を `Awake` へ移動 (全 `Start` より先に更新 → `SaveLoadManager` の保存に必ず載る) |
| F17 | **修正** | Ready パネル内の `Start` ボタンを削除し、右下の `Start` 1 個に統一 |
| F18 | **Known Issue** | `RankingPanel` の F5 案内は cloud 曲以外では開けない (`ResultRankingOverlay` は他 Worker 所有のため本 Issue では触らない) |

### T4: Scene / EventSystem / CanvasRenderer 監査 (2026-11-08 実測)

| Scene | EventSystem | InputModule | TMP テキスト | CanvasRenderer | TMP で CanvasRenderer 欠損 |
|---|---|---|---|---|---|
| TitleScene | 1 | 1 | 10 | 26 | **0** |
| SelectScene | 1 | 1 | 36 | 83 | **0** |
| GameScene | 1 | 1 | 19 | 32 | **0** |
| ResultScene | 1 | 1 | 30 | 58 | **0** |
| OptionScene | 1 | 1 | 26 | 65 | **0** |
| Opening | 1 | 1 | 0 | 0 | **0** |
| TypingScene | 1 | 1 | 0 | 0 | **0** |
| MakeFileScene | 0 | 0 | 0 | 0 | (譜面作成ツール。対象外) |

**結論**: 1 画面 1 EventSystem、TMP の CanvasRenderer 欠損ゼロ (Issue #153 の修正済み)。
二重 EventSystem も無いため、**シーン YAML の変更は不要**と判断し、最小差分原則に従って
`Assets/**/*.unity` は一切書き換えていない。Canvas 側の wiring (Raycaster / 順序 / レガシー
Raycaster 無効化) は実行時コードで解決した。

### オフラインコンパイル検証 (Unity 起動なし)

`Assets/Game/Scripts/**` と `Assets/Tests/Editor/{V5UiWiringTests,TitleOptionV5Tests}.cs` を
Unity 6.0.5.6f1 の `UnityEngine.*` DLL と `Library/ScriptAssemblies/{UnityEngine.UI, Unity.TextMeshPro,
SimpleFadeSystem, NoteEditor, YoutubePlayer}.dll` を参照にして `dotnet build` で型検査 →
**0 エラー** (テストコードを含む)。

## Rollback

本 Issue の変更は Owned Paths 内に閉じており、他ブランチの成果物と競合しない。
`git revert` 相当 (merge 取り消し) で戻す: 追加は `PlaySession.cs` と EditMode テスト、
変更は v5 View / SceneManager に限定されるため、revert すると*v5 Canvas に GraphicRaycaster が無い*元の
「クリックが効かない」状態に戻るだけで他機能への影響はない。

## Success Criteria

- **SC1**: Title / Select / Option / Result で **マウスクリック**が実 Button を起動し、
  Title→Select→Game→Result→Retry が一巡できる (Scenario 1)。
- **SC2**: Ready 画面の Key Guide / Lyrics / NoteSpeed が演奏中の HUD・歌詞・落下速度に実反映される (Scenario 2)。
- **SC3**: 押しても実動作しない行が Pause / Select Ranking / Result に残っていない (Scenario 3)。
