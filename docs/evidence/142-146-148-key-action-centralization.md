# #142 / #146 / #148 — メニュー操作キーの一元化 (表示・入力の正本化) 実装証跡

日付: 2026-11-08 / ブランチ: `w/ckeys` / ワークツリー: `/Users/rebui/work/FlashBeat.ckeys`

## 実装内容

正本は既存の `KeyActions` (メニュー操作 13 種) + `KeyBindings` (レーン 8) + `ActionKeyHint`
(表示フォーマッタ)。今回作業は、固定文字列と迂回入力の残骸を正本へ寄せた。

### 表示 (ActionKeyHint 経由に置換)

| ファイル | 置換内容 |
|---|---|
| `V5/GameErrorView.cs` | Esc/R キーキャップ → `Primary(Back)` / `Primary(Retry)` |
| `V5/GameReadyView.cs` | Space キャップ → `Primary(Play)`、Esc 凡例 → `Primary(Back)` |
| `V5/ResultView.cs` | 凡例 R→`Primary(Retry)`、Space→`Primary(Confirm)`、モーダル Skip の Esc → `Primary(Back)`。Name/Save の Enter は #146 T3 の決定どおり生入力 (Return) に対応する固定表示を維持 |
| `V5/PauseMenuView.cs` | Resume/Retire/Retry キャップを `ActionKeyHint` 生成に統一 (Retire/Retry は従来の `KeyConfigUI.GetKeysText` から統合)、Esc 凡例 → `Primary(Back)` |
| `V5/OptionView.cs` | 凡例の Esc → `Primary(Back)` (`V5Decor.BuildLegend` のタプル配列内) |
| `V5/SelectView.cs` | 5 箇所の Esc 凡例 (Ready/リスト/履歴/履歴詳細/ランキング) → `Primary(Back)` |

GameHudView は既に `KeyBindings.GetKeys` を使用しており無変更。TitleView は本タスクの
Owned paths 外 (Issue #148 の残タスクとして Supervisor 側で管理)。

### 入力 (KeyActions.IsDown へ統一)

| ファイル | 変更 |
|---|---|
| `V5/GameErrorView.cs` | Escape→`Back`、R→`Retry` (完全置換。既定は同一キー) |
| `V5/OptionView.cs` | Escape→`Back`、Space→`IsDown(Confirm)` (Return/KeypadEnter は併用キーとして生読み維持) |
| `V5/PauseMenuView.cs` | ポーズ開閉の Escape→`Back` (InputEnabled ガードは維持) |
| `V5/ResultView.cs` | モーダル/本体の Escape→`Back`。モーダル内 Return は名前入力があるため生のまま |
| `V5/SelectView.cs` | Typing/Ready/History/Ranking 各状態の Escape→`Back`、Space→`IsDown(Confirm)` (Enter/KeypadEnter/R は併用キーとして維持) |
| `UI/SelectSceneManager.cs` | V6 History/Ranking の Escape→`Back`、O→`Option`、Space→`IsDown(Confirm)` |
| `UI/ResultRankingOverlay.cs` | 3 箇所の Escape→`Back`。名前入力モーダル内の Backspace/Return はテキスト編集として固定 |

挙動不変: すべて既定割当で今日と同一のキーを受け付ける (Back=Esc, Confirm=Space,
Option=O, Retry=R のみ置換、既定外の生読みは温存)。

## 対象外 (意図的に固定のまま)

- **KeyActions に存在しないショートカット**: F2 (キー設定トグル)、F3/F4 (更新通知、UpdateNoticeUI)、
  F5 (ランキング再取得)、Tab/WASD/S/D/L/K/R (ビュー固有のナビ・モード切替)。
  新規アクションの追加は仕様外のため行わない。
- **Enter/KeypadEnter の生読み**: Confirm の既定は Space のみ (#142 既定表)。Enter は
  併用キーとして生読みを残す (TitleSceneManager の先例と同じ形)。再割当時も Enter は
  常に併用キーとして機能する。
- **名前入力モーダルでの Confirm 読み**: Space が文字入力になるため Confirm を読むと
  Space 入力で誤送信するため意図的に除外。
- **KeyConfigUI / KeyConfigOptionButton**: 設定 UI 自体の F2/Tab/Enter は操作表の外。
- **HudPlayStatus.cs:65 の Escape 直読み**: Owned paths 外 (別タスク)。

## 検証

- `script/compile-check.sh --project . --assembly flashbeat` → **Build succeeded** (138 sources)
- `script/compile-check.sh --project . --assembly tests` → **Build succeeded** (37 sources, Game/Tests asmdef)
- `Assets/Tests/Editor` (FlashBeat.Tests asmdef, 61 ファイル) は compile-check の対象外のため、
  Flash.Tests.csproj テンプレートから one-off csproj を生成し `dotnet build` — **Build succeeded**
  (game + editor + tests 計 215 ソース。検証後 csproj は削除済み)。
- 追加テスト `MenuKeyActionWiringTests` のソース走査 2 件は、同一の正規表現を用いた
  ローカル Perl スキャンで現行ソースに対し違反 0 件を確認済み。
- EditMode テストの実実行 (Unity Test Runner) は Supervisor が実施 (本ワーカーは Editor を起動しない)。

## 残存リスク

1. **Enter/KeypadEnter の併用読み**: ユーザーが Confirm を再割当しても Enter は常に決定として
   動作し、表示 (ActionHint) には現れない。#146 T3 の既定の判断に従う。
2. **TitleView の固定凡例 (`↑↓ Enter`)**: Owned paths 外のため未変更。#148 の別担当分。
3. **HudPlayStatus の Escape 直読み**: 演奏中の Back 再割当がこの表示/入力に反映されない
   (Owned paths 外)。
4. **EditMode テスト未実行**: 上記のとおり Supervisor が実施する。ソース走査テストは
   Unity 上で `Application.dataPath` 基準でファイルを読むため、Editor 実行環境が必要。
