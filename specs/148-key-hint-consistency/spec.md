# Spec: v5 キー表示を実操作と設定に統一 (#148)

**Issue**: #148 | **Branch**: `148` | **PR base**: `expo-ui`

## Objective
v5 UI (Title / Select 各状態 / Option / Pause 各タブ / Result) の画面上のキーヒントを、
実際に受け付けるキーと一致させる。入力の正本は `KeyActions` / `KeyBindings`
(`Assets/Game/Scripts/Gameplay/`)、表示整形は `ActionKeyHint` (`Assets/Game/Scripts/UI/V5/ActionKeyHint.cs`, #147)。
新しい独立キーテーブルは作らない。KeyActions/KeyBindings の意味論は変更しない。

## Owned paths
- `Assets/Game/Scripts/UI/V5/TitleView.cs`
- `Assets/Game/Scripts/UI/V5/SelectView.cs`
- `Assets/Game/Scripts/UI/V5/OptionView.cs`
- `Assets/Game/Scripts/UI/V5/PauseMenuView.cs`
- `Assets/Game/Scripts/UI/V5/ResultView.cs` (固定 cap の誤り修正のみ)
- `Assets/Tests/Editor/KeyHintConsistencyTests.cs` (+ .meta)
- `specs/148-key-hint-consistency/**`

## Audit matrix (T1)

凡例: 「表示」= 画面のキャプ / 「受付」= 実際にその動作を起こすキー / 「導出」= ActionKeyHint 経由で設定値から生成。

### Title
| 要素 | 表示 (現状 → 変更後) | 受付 | 動作 |
|---|---|---|---|
| メニュー移動 | `↑` `↓` (固定) → 固定のまま | ↑/↓ 矢印 + W/S (ハードコード) | 選択移動 |
| 決定 (凡例) | `Enter` (固定) → **導出** `Primary(Start)` | KeyActions.Start (Space / Enter / keypad Enter) | 項目実行 |
| (非表示) | — | KeyActions.Option (O) / Exit (E) | Option / Exit ショートカット |

### Select — List
| 要素 | 表示 | 受付 | 動作 |
|---|---|---|---|
| 検索 | `A-Z` (固定) → 固定のまま | `Input.inputString` (KeyCode 非依存) | 絞り込み |
| 曲移動 | `↑ ↓` (固定) → 固定のまま | 矢印 + NavPrev/NavNext + レーン半分 | 選択移動 |
| 決定 | `Enter` (固定) → 固定のまま | Return / KeypadEnter (ハードコード) | Ready へ |
| 再生開始 (非表示) | — | KeyActions.Confirm | 直接 GameScene |
| 戻る | `Esc` (固定) → **導出** `Primary(Back)` | KeyActions.Back | Title へ |
| 絞り込み | `Tab` (固定) → 固定のまま | Tab (ハードコード) | typing 状態へ |
| 履歴 / ランキング | `F2` / `F3` (固定) → 固定のまま | F2 / F3 (ハードコード) | History / Ranking へ |

### Select — Typing
| 要素 | 表示 | 受付 | 動作 |
|---|---|---|---|
| 文字入力 | (検索フィールド) | inputString | filterText |
| 確定 | — | Return (固定) | List へ |
| 戻る | (List 凡例を踏む) → **導出** `Primary(Back)` + Tab 固定 | KeyAction.Back または Tab | List へ |

### Select — Ready
| 要素 | 表示 | 受付 | 動作 |
|---|---|---|---|
| 行移動 / 値変更 | `← →` (固定) → 固定のまま | 矢印 (ハードコード) | 行 / 値 |
| スタート | `Enter` (固定) → 固定のまま | Return / KeypadEnter / Space / R (ハードコード) | GameScene |
| 選曲へ | `Esc` (固定) → **導出 `Primary(Back)`** | KeyAction.Back に経路変更 (既定 Esc、挙動不変) | List へ |

### Select — History
| 要素 | 表示 | 受付 | 動作 |
|---|---|---|---|
| 選択 | `↑ ↓` (固定) → 固定のまま | 矢印 / ホイール | 行選択 |
| 戻る | `Esc` (固定) → **導出** `Primary(Back)` | KeyAction.Back に経路変更 + F2 固定のまま | List へ |
| この曲へ | `Enter` (ボタン) | Return / Space (ハードコード) | 曲を選んで List へ |

### Select — Ranking
| 要素 | 表示 | 受付 | 動作 |
|---|---|---|---|
| 曲移動 | `↑ ↓` (固定) | 矢印 / ホイール | 選択移動 |
| 列切替 | `Tab` (固定) → 固定のまま | Tab (ハードコード) | Local/Online |
| 戻る | `Esc` (固定) → **導出** `Primary(Back)` | KeyAction.Back に経路変更 + F3 固定のまま | List へ |
| 決定 (非表示) | — | Return / Space (ハードコード) | Ready へ |

### Option (全タブ共通凡例)
| 要素 | 表示 | 受付 | 動作 |
|---|---|---|---|
| 項目移動 | `↑ ↓` (固定) | 矢印 + W/S | 行選択 |
| 値変更 | `← →` (固定) | 矢印 | 値 ±1 |
| タブ切替 | `Tab` (固定) → 固定のまま | Tab (ハードコード) | Play/Display/Sound/Data |
| 戻る | `Esc` (固定) → **導出** `Primary(Back)` | KeyAction.Back に経路変更 (既定 Esc) | Title へ |
| キー設定行の値 | `F2` (固定) → 固定のまま | F2 (KeyConfigUI、OptionScene は許可シーン) | キー設定オーバーレイ |
| 決定 (非表示) | — | Return / Space | 行 Activate |

### Pause (タブ: Settings / Display / Ranking / Keys)
| 要素 | 表示 | 受付 | 動作 |
|---|---|---|---|
| 開閉 / 再開 | `Esc` (固定) → **導出** `Primary(Back)` | KeyAction.Back に経路変更 (既定 Esc)。HUD の ▮▮ ボタンでも開く | ポーズ切替 |
| 項目 / 値 | `↑↓` / `←→` (固定) → 固定のまま | 矢印 (ハードコード) | 行 / 値 |
| タブ切替 | `S D L K` (固定) → 固定のまま | S/D/L/K (ハードコード、設定対象外の固定ショートカット) | タブ切替 |
| Retry | cap = `KeyConfigUI.GetKeysText(全キー)` → **導出** `Primary(MenuRetry)` | KeyActions.MenuRetry | GameScene 再読込 |
| Retire | cap = `GetKeysText(全キー)` → **導出** `Primary(MenuRetire)` | KeyActions.MenuRetire | SelectScene へ |
| Resume | `Esc` (固定) → **導出** `Primary(Back)` | KeyAction.Back | 閉じる |

### Result (+ Register モーダル)
| 要素 | 表示 | 受付 | 動作 |
|---|---|---|---|
| Retry 凡例 | `R` (固定) → **導出** `Primary(Retry)` | KeyActions.Retry | リトライ |
| 決定 凡例 | `Space` (固定) → **導出** `Primary(Confirm)` | KeyActions.Confirm | 選曲へ (OK ボタンと同一動作) |
| 登録 凡例 / ボタン | `Enter` (固定) → 固定のまま | Return / KeypadEnter (ハードコード、選択中アクション実行) | Register モーダル |
| (非表示) | — | Esc (固定) | 選曲へ |
| Retry / OK ボタン cap | #147 済: `Primary(Retry)` / `Primary(Confirm)` → 変更なし | KeyActions.Retry / Confirm | 同左 |
| モーダル Skip | `Esc` (固定) → 固定のまま | Escape (ハードコード) | モーダルを閉じる |
| モーダル Register | `Enter` (固定) → 固定のまま | Return / KeypadEnter (ハードコード) | 登録 |
| 名前入力 | `A-Z 0-9` cap (固定) → 固定のまま | TMP_InputField 英数字制限 | 登録名 |

### Game HUD / Ready / Error (所有外 — 本 Issue では変更しない)
- レーンキーガイド: #146 済み。`KeyBindings` から導出 ✓。
- `GameReadyView`: `Space` / `Esc` の cap を固定表示。入力は MusicManager=KeyAction.Play
  (既定 Space)、ポーズ開=KeyAction.Back (既定 Esc) のため**既定バインドでは一致**するが、
  変更時は陳腐化する。所有外ファイルのため本 Issue では触らず既知ギャップとして記録。
- `GameErrorView`: `Esc` / `R` cap はハードコード入力 (`KeyCode.Escape` / `KeyCode.R`) と
  完全一致 → 表示は実操作と一致している (固定キー、設定不可)。

## Requirements
- R1: 設定可能な操作 (KeyActions) を示す cap は `ActionKeyHint.Primary(action)` から生成する。
  幅固定 cap 内に収まる短名を使い、ボタン幅を広げない。
- R2: 意図的に固定の入力 (矢印 / W S / Tab / F2 / F3 / S D L K / Return / Space-ready / R-ready /
  inputString 検索) は、実入力と一致する固定キャプのまま残す (上の表に記録)。
- R3: Select (Ready/Typing/History/Ranking)・Option・Pause の「戻る/再開」は
  `KeyActions.Back` 経由に統一する (既定 Esc なので既定の挙動は不変)。
- R4: cap は `KeyActions.Changed` (Option では追加で `KeyBindings.Changed`) で再描画する。
  Title/Select/Result はシーン再入場で全体再構築されるため再入場 = 再描画。加えて
  Changed 購読でライブ更新する (Select は Rebuild()、他は cap の TMP text を差し替え)。
- R5: EditMode テスト: 表示 cap 生成メソッドが `ActionKeyHint.Primary(Get(action))` と一致、
  `All()` が `Get()` の非 None キー集合と同数、既定 / 記号キーのカスタム / 空割当の 3 状態。

## Out of scope
- GameReadyView / GameErrorView / GameHudView の cap (所有外、上記既知ギャップ)。
- KeyConfigUI 自体、シーン/Prefab、Windows 実機検証 (本 PR では GUI 未検証)。

## Verification
- EditMode 全 suite を batchmode で実行し、結果 XML の total/passed/failed を報告する。
- 既知の上流欠陥 `JapaneseFontCoverageTests.BuildScenes_UseTheJapaneseFont`
  (TitleScene の TMP が CanvasRenderer 無し。branch 153 で修正予定) は本 PR の失敗とみなさない。
