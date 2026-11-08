---
description: "FlashBeat lean plan — Issues 221 & 222 (v6 Select)"
---

# Plan: v6 選曲画面の疑似ボタン撤去と実データ表示

**Issue**: #221, #222 | **Branch**: `w/w221` | **Date**: 2026-11-08

## 前提

- 監査対象: `Assets/Game/Scripts/UI/V6/Select/View/V6SelectView.cs` (integration/v6-followup 87d723f 時点の指摘だが、作業ブランチ w/w221 の同一ファイルに存在する問題として扱う)。
- Issue #222 はスコープ訂正 (2026-11-08) 済み: プレビュー再生などの新機能は撤回。
  残る作業は「既存の実操作への結線」と「誠実な表示」。

## 設計

### 1. 中央カードの操作ボタン (死んだ操作の解消)

- 純ロジック `V6SelectViewLogic` に `V6SelectControlAction` (MovePrev / Ready /
  MoveNext) と `CenterControls(): V6CenterControlDef[]` を追加。寸法・位置は
  現行の design 値 (56/72/56 px, x=130/220/326) を保持。
- `V6SelectView.BuildControlButton` は定義を受けて switch で実操作に結線:
  - MovePrev → `data.MoveSelection(-1)` (既存の選択移動。Changed → Rebuild)
  - MoveNext → `data.MoveSelection(+1)`
  - Ready → `ReadyAvailable()` を満たすときだけ `onReady`
- `V6SelectControlAction` に None を作らない。View の switch は網羅 (default は
  何も生成しないガード)。

### 2. Ready の有効化条件 (絞り込みと選択の不整合)

- 純ロジック `ReadyAvailable(selected, displaySongs)` を追加。
- View: `DisplaySongs()` (検索 + 未プレイのみの適用済みリスト) に対して判定し、
  フッター Ready ボタン / Space/Enter (MapKey の Confirm) / 中央 play の全入口で
  ゲート。無効時は Ready ボタンを弱い色で描画し onClick を張らない。
- Data: `V6SelectDataSource.CommitReady` にも同名ガード (FilteredSongs 探索) を
  入れ、隠れた選択は `SetError` して songID を確定させない。
  - SelectSceneManager 側は既存の `OnV6Ready` (Selected==null 時に
    FilteredSongs 先頭を選んでから CommitReady → GameScene) をそのまま使う。

### 3. 表示の誠実化

- source タグ: `RankingSourceLabel(song)` — IsCloud で オンライン/ローカル、未選択は "–"。
- ランキング空状態: `RankingEmptyText(song)` — ローカル曲 / エラー / データ無しを
  区分して日本語で表示。
- PLAYERS: "データがありません" (データ源が無い事実を明示)。

### 4. 凡例とキー入力の一致

- `FooterLegend(string confirmKey)` / `FooterLegendWidth(string confirmKey)`。
  項目: 決定 (KeyActions.Confirm) / ↑↓ 曲送り / Tab 検索 / F 絞り込み。
- `Tick`: ↑↓ に `KeyActions.NavPrev/NavNext` を追加、Confirm/Back/Option を
  `KeyActions.IsDown` 経由に戻す。Tab / F は raw のまま (KeyActions に無い v6 固定)。

## 検証

- `script/compile-check.sh --assembly flashbeat` / `--assembly tests`。
- EditMode テスト (統合時に Supervisor が実行):
  - `V6SelectViewLogicTests` — 凡例↔KeyActions 一致 (既定 + 再バインド)、
    ReadyAvailable の可否、CenterControls の全定義が有効操作。
  - `V6SelectDataLogicTests` — CommitReady が隠れた選択を拒否し songID を
    変えない。
  - `V6SelectLayoutTests` — 既存の凡例レイアウト計算 (confirmKey 渡しに更新)。
- 実機画面確認は不可 → `docs/evidence/221-222-select-controls.md` に「未検証」と
  明記し、EditMode テストを代替検証とする。

## リスク

- KeyActions.IsDown は `KeyBindings.GetKeyDownOrInjected` 経由 — EditMode では
  動かないため、キーの割り当て自体は実機確認事項 (凡例表示はテストで担保)。
- FooterLegend のシグネチャ変更により既存テストの呼び出しを更新する (期待値は
  変えない — 既定キー "Space" を渡すだけ)。
