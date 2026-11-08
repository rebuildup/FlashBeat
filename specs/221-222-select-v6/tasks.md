---
description: "FlashBeat lean tasks — Issues 221 & 222 (v6 Select)"
---

# Tasks: v6 選曲画面の疑似ボタン撤去と実データ表示

順序依存: T1 → T2 → T3 → T4 → T5 (T6 は最後)。

## T1. 中央カードの 操作ボタンを実操作へ結線 (#221 Task 3)

- [x] `V6SelectViewLogic` に `V6SelectControlAction { MovePrev, Ready, MoveNext }`
  (None 無し) と `CenterControls(): V6CenterControlDef[]` を追加 (寸法は現行 design 値を保持)。
- [x] `V6SelectView.BuildControlButton` を定義駆動に書き換え、MovePrev/MoveNext →
  `data.MoveSelection(±1)`、Ready → `ReadyAvailable()` を満たすとき `onReady`。
  default ケースはボタンを生成しない。
- [x] テスト: `CenterControls_EveryButtonMapsToAnExistingOperation`
  (Assets/Game/Tests/V6SelectViewLogicTests.cs)。

## T2. Ready の絞り込みガード (#221/#222)

- [x] `V6SelectViewLogic.ReadyAvailable(selected, displaySongs)` を追加。
- [x] `V6SelectView` — フッター Ready ボタン / Confirm キー / 中央 play を
  `ReadyAvailable()` でゲート。無効時は Ready ボタンを弱い色で描画し onClick を張らない。
- [x] `V6SelectDataSource.CommitReady` — FilteredSongs に選択が無ければ
  `SetError` して songID を確定させない。
- [x] テスト: `ReadyAvailable_*` 3 件 (View ロジック) +
  `CommitReady_WhenSelectionHiddenByQuery_ReportsErrorAndKeepsSongId` (Data)。

## T3. 表示の誠実化 (#221)

- [x] ランキング source タグ: `RankingSourceLabel(song)` (IsCloud → オンライン /
  ローカル / 未選択 → –)。
- [x] ランキング空状態: `RankingEmptyText(song)` (ローカル曲 / エラー / データ無し)。
- [x] PLAYERS パネル: "データがありません" (固定の "NO DATA" を撤去)。

## T4. 凡例とキー入力の一致 (#221 / #222 Task 2・5)

- [x] `FooterLegend(string confirmKey)` / `FooterLegendWidth(string confirmKey)` に
  変更。項目: 決定 (KeyActions.Confirm) / ↑↓ 曲送り / Tab 検索 / F 絞り込み。
- [x] `Tick` の raw KeyCode を KeyActions 経由に戻す
  (↑↓ + NavPrev/NavNext、Confirm + Enter/KeypadEnter、Back、Option)。
  Tab / F は v6 固定のまま。
- [x] Ready ボタン / 戻る / 設定のキーキャップも ActionKeyHint.Primary 経由に統一。
- [x] `V6SelectLayoutTests` の呼び出しを confirmKey 渡しに更新 (期待値は不変)。

## T5. テスト追加

- [x] `FooterLegend_ConfirmKey_FollowsKeyActionsDefault` (既定キーが正本と一致)
- [x] `FooterLegend_FilterToggleKey_MatchesImplementation` (Tab=検索 / F=絞り込み)
- [x] `FooterLegend_ConfirmKey_FollowsUserRebinding` (再バインドが凡例に反映)
- [x] `ReadyAvailable_*` (可否 3 件)
- [x] `CenterControls_EveryButtonMapsToAnExistingOperation`
- [x] `CommitReady_WhenSelectionHiddenByQuery_ReportsErrorAndKeepsSongId`

## T6. 仕様・エビデンス

- [x] `specs/221-222-select-v6/{spec,plan,tasks}.md`
- [x] `docs/evidence/221-222-select-controls.md` (実装・検証・残存リスク、
  実機確認は「未検証」明記)
- [x] `script/compile-check.sh --assembly flashbeat` / `--assembly tests` 成功
