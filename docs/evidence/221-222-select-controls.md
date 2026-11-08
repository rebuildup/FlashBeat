# Evidence: #221 / #222 — v6 選曲画面の疑似ボタン撤去と実データ表示

- **Branch**: `w/w221`
- **Date**: 2026-11-08
- **Spec**: `specs/221-222-select-v6/`
- **基准**: 監査は integration/v6-followup HEAD 87d723f 基準。本ブランチの同一箇所に対して実装した。

## 実装

### 1. 中央カードの操作ボタン — 死んだ操作の解消 (#221 Task 3)

- `Assets/Game/Scripts/UI/V6/Select/View/V6SelectViewLogic.cs`
  - `V6SelectControlAction { MovePrev, Ready, MoveNext }` を追加。**None メンバなし**
    (死んだ操作を型で表現できなくする)。
  - `CenterControls(): V6CenterControlDef[]` を追加。寸法・位置は現行 design 値
    (skip-back 56px x130 / play 72px x220 / skip-forward 56px x326) を保持。
- `Assets/Game/Scripts/UI/V6/Select/View/V6SelectView.cs`
  - `BuildControlButton(V6CenterControlDef)` に書き換え。switch で実操作へ結線:
    - skip-back → `data.MoveSelection(-1)` (既存の選択移動。スクロールボタンと同じ経路)
    - play → `ReadyAvailable()` を満たすとき `onReady` (フッター Ready と同じ経路)
    - skip-forward → `data.MoveSelection(+1)`
  - default ケースは **ボタンを生成しない** (onClick=null の飾りは作れない)。
  - プレビュー再生などの新操作は作っていない (Issue #222 スコープ訂正 2026-11-08 を遵守)。

### 2. 絞り込みで隠れた曲の Ready 禁止 (#221/#222)

- `V6SelectViewLogic.ReadyAvailable(selected, displaySongs)` (純ロジック) を追加。
- View 側: フッター Ready ボタン / Space・Enter (Confirm) / 中央 play の全入口を
  `ReadyAvailable()` でゲート。無効のときは Ready ボタンを弱い色
  (`V5Tokens.Key1` / `TextDim`) で描画し onClick を張らない。
- Data 側: `V6SelectDataSource.CommitReady` にガードを追加 — 選択が
  `FilteredSongs` (検索絞り込み後) に無ければ
  `SetError("選択中の曲が絞り込みで隠れています (検索を変更してください)")`
  して留まる。`GManager.songID` の確定 (= 開始) も起きない。
- 「未プレイのみ」絞り込みは View 表示フィルタのまま。Ready 判定は View の
  表示リスト基準なので、この絞り込みで隠れた曲も Ready 不可。
- `SelectSceneManager.cs` は無変更 (他担当所有。Ready → CommitReady → GameScene の
  既存経路をそのまま使う)。

### 3. 表示の誠実化 (#221)

- ランキング source タグ: `RankingSourceLabel(song)` — `IsCloud` で
  「オンライン / ローカル」、未選択は「–」。ローカル曲に「オンライン」と
  表示する固定値を削除。
- ランキング空状態: `RankingEmptyText(song)` — ローカル曲 →
  「ローカル曲のランキングはありません」、エラー → `StatusMessage`、
  それ以外 → 「ランキングのデータがありません」。固定の "NO DATA" を削除。
- PLAYERS パネル: 「データがありません」(プレイヤー集計のデータ源が無い事実を
  明示。固定の "NO DATA" を削除)。

### 4. 凡例とキー入力の一致 (#221 / #222 Task 2・5)

- `FooterLegend(string confirmKey)` / `FooterLegendWidth(string confirmKey)` に変更。
  項目: **決定 (KeyActions.Confirm) / ↑↓ 曲送り / Tab 検索 / F 絞り込み**。
  design 記載の「Tab 絞り込み」は実装 (Tab = 検索フォーカス / F = 絞り込み) と
  矛盾していたため修正。
- 決定キーのキャップはビューから `ActionKeyHint.Primary(KeyActions.KeyAction.Confirm)`
  を渡す (commit 411caee の v5 方式と同じ一元化)。キー設定を変えると
  Ready ボタンの Space キャップ・凡例が追従する。
- 戻る / 設定ボタンのキーキャップも `ActionKeyHint.Primary(KeyActions.KeyAction.Back /
  Option)` 経由に統一 ("Esc" / "O" のハードコードを削除)。
- `V6SelectView.Tick` の raw KeyCode を KeyActions 経由に戻した
  (v5 SelectView.cs:150-157 と同じ規約):
  - ↑↓ + `KeyActions.NavPrev/NavNext` (←→) で曲送り
  - `KeyActions.Confirm` (既定 Space) + Enter/KeypadEnter で Ready
  - `KeyActions.Back` (既定 Esc) で戻る
  - `KeyActions.Option` (既定 O) で設定
  - Tab (検索フォーカス) / F (絞り込み) は KeyActions に該当操作が無い v6 固定
    ショートカットのため raw KeyCode のまま

## 検証

### compile-check (実施済み)

```
$ script/compile-check.sh --project /Users/rebui/work/FlashBeat.w221 --assembly flashbeat
compile-check: 140 sources -> FlashBeat.compilecheck.csproj
Build succeeded.
$ script/compile-check.sh --project /Users/rebui/work/FlashBeat.w221 --assembly tests
compile-check: 37 sources -> FlashBeat.compilecheck.csproj
Build succeeded.
```

### EditMode テスト (追加・更新)

- `Assets/Game/Tests/V6SelectViewLogicTests.cs`
  - `FooterLegend_ConfirmKey_FollowsKeyActionsDefault` — 決定キャップが
    ActionKeyHint (KeyActions の正本) と一致。
  - `FooterLegend_FilterToggleKey_MatchesImplementation` — Tab=検索 / F=絞り込み。
  - `FooterLegend_ConfirmKey_FollowsUserRebinding` — Confirm を F8 に再バインドすると
    凡例キャップが F8 になる (MemoryKeyStorage 注入)。
  - `ReadyAvailable_SelectionVisibleInList_IsTrue` /
    `ReadyAvailable_SelectionHiddenByFilter_IsFalse` / `ReadyAvailable_NoSelection_IsFalse`。
  - `CenterControls_EveryButtonMapsToAnExistingOperation` — 3 定義が
    MovePrev/Ready/MoveNext (None 無しの列挙) に対応。
- `Assets/Game/Tests/V6SelectDataLogicTests.cs`
  - `CommitReady_WhenSelectionHiddenByQuery_ReportsErrorAndKeepsSongId` —
    絞り込みで隠れた選択の CommitReady がエラーを設定し `GManager.songID` を
    変えない (開始されない)。
- `Assets/Game/Tests/V6CanvasTests/V6SelectLayoutTests.cs` —
  `FooterLegend()` / `FooterLegendWidth()` の呼び出しを confirmKey 渡しに更新
  (期待値・アサーション内容は不変。既定キー "Space" を渡すだけ)。
- 既存テストの期待値は壊していない
  (`SetQuery_NarrowsTheList_AndKeepsSelectionWhenHidden` など選択維持のピン留めは
  変更せず、Ready 経路だけを塞いだ)。

**注**: EditMode テストの実行は Unity Editor を必要とするため本ブランチでは未実施
(作業方法の制約: Unity Editor を起動しない)。統合時に Supervisor が
Test Runner (EditMode) で実行する。コンパイルは上記 compile-check で両アセンブリとも成功。

### 実機での画面確認

**未検証**。展示 PC (Windows) は使用できないため、以下は実機確認事項として残す:

- 曲送りボタン (skip-back / skip-forward) のクリックで選択が移動し、中央カードと
  曲リストが追従すること。
- play ボタン / Space / Enter で Ready → GameScene へ遷移すること。
- 検索で選択曲を隠した状態で Space を押すと開始されず、ステータスメッセージが
  出ること (Ready ボタンが弱い色で描画されること)。
- キー設定で Confirm を変更したとき、凡例と Ready ボタンのキャップが追従すること。

代替検証として、上記 EditMode テストでロジック層 (凡例文言↔キー割り当ての一致、
Ready ゲート、操作定義の有効性、CommitReady ガード) を固定した。

## 残存リスク

- **タブキー / F キーの実機挙動**: Tab (検索フォーカス) と F (絞り込み) は
  KeyActions 外の固定割り当て。キー設定画面からは変更できない (仕様どおり)。
- **←→ 曲送りの追加**: v5 に合わせ `NavPrev/NavNext` (←→) を曲送りに追加した。
  design のキャップは「↑↓」のまま (←→ は補助入力)。実機で違和感があれば
  design 更新が必要。
- **PLAYERS パネル**: データ源が無いため常に「データがありません」。将来
  プレイヤー集計 API が入ったときは `IV6SelectDataSource` の拡張が必要
  (本 issue では対応しない)。
- **KeyActions.IsDown の注入経路**: `KeyBindings.GetKeyDownOrInjected` 経由。
  EditMode では検証できないため、実機で Confirm/Back/Option/←→ の動作確認を
  推奨 (v5 と同じ仕組みなのでリスクは低い)。
