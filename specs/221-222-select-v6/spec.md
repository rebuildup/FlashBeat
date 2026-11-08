---
description: "FlashBeat lean spec — Issues 221 & 222 (v6 Select の疑似ボタンと嘘の凡例)"
---

# Spec: v6 選曲画面の「死んだ操作」と「嘘の表示」をなくす (#221 / #222)

**Issue**: #221, #222 | **Branch**: `w/w221` | **Date**: 2026-11-08 | **Status**: Draft

**Input**: 監査結果 (integration/v6-followup HEAD 87d723f 基準) — 中央カードの一時操作
ボタン 3 個が onClick=null で生成される疑似ボタン、PLAYERS パネルの "NO DATA"
ハードコード、ランキング source タグの "オンライン" ハードコード、凡例
「Tab 絞り込み」と実装 (Tab=検索フォーカス / F=絞り込み) の矛盾、raw KeyCode への
置換で v5 短縮キー (KeyActions) が失われたこと、絞り込みで隠れた曲が Ready され
開始されてしまうこと。

## Objective

v6 選曲画面 (`V6SelectView` / `V6SelectDataSource`) を **表示しているものと
できることを一致させる**:

- (a) 押しても何も起きない疑似ボタンをなくす。ボタンは実在する操作に結線するか、
  要素ごと描画しない。
- (b) データが無いことを示す表示は誠実にする。固定文字列を実データに見せかけない。
- (c) 凡例 (キーキャップ) は実際のキー割り当てと一致させ、キー設定 (KeyActions)
  の正本から取る。
- (d) 絞り込み (検索 / 未プレイのみ) で隠れた曲は Ready できず、開始されない。

## Decisions (凍結する設計)

| 項目 | 決定 |
| --- | --- |
| 中央カードの操作ボタン | 3 個 (skip-back / play / skip-forward) は **実在する操作に結線する**: skip-back → `data.MoveSelection(-1)`、skip-forward → `data.MoveSelection(+1)` (既存の曲送り。スクロールボタンと同じ経路)、play → Ready (フッターの Ready ボタンと同じ `onReady` 経路)。**新しい操作 (試聴・シーク・プレビュー再生) は作らない** (Issue #222 スコープ訂正 2026-11-08 により撤回済み) |
| 操作定義の正本 | `V6SelectViewLogic.CenterControls()` が `V6CenterControlDef[]` (Icon / Size / X / Primary / Action) を返す。`Action` は `V6SelectControlAction { MovePrev, Ready, MoveNext }` — `None` メンバを持たない (死んだ操作を型で表現できないようにする)。View はこの定義だけからボタンを生成し、switch を網羅させる (default で何も作らない) |
| Ready の有効性 | `V6SelectViewLogic.ReadyAvailable(selected, displaySongs)` (純ロジック): 選択中の曲が **表示リスト** に見えるときだけ true。View 側はフッター Ready ボタン・Space/Enter・中央 play のすべての入口でこの判定を通し、無効なら onClick を張らず弱い色で描画する。データ側は `V6SelectDataSource.CommitReady` で `FilteredSongs` に選択が無ければ `SetError("選択中の曲が絞り込みで隠れています …")` して留まる (二重に守る。遷移の確定もしない) |
| 「未プレイのみ」絞り込み | View 側の表示フィルタのまま (データ契約は変更しない)。Ready 判定は View の表示リスト基準なので、この絞り込みで隠れた曲も Ready 不可になる |
| ランキング source タグ | `RankingSourceLabel(song)`: `song.IsCloud` → "オンライン"、ローカル → "ローカル"、未選択 → "–"。ローカル曲に "オンライン" を表示しない |
| ランキング空状態 | ローカル曲 → "ローカル曲のランキングはありません" (オンラインランキングは存在しないという事実)。クラウド曲 → エラー時は `StatusMessage`、それ以外 → "ランキングのデータがありません"。取得中は既存どおりスケルトン行 |
| PLAYERS パネル | プレイヤー集計のデータ源は存在しない (#222)。実データを出せないので "データがありません" と明示する (偽のデータも "NO DATA" の英語固定も使わない)。パネル自体は design (dRztp) の構成要素として残す |
| 凡例キー | `FooterLegend(string confirmKey)`。確定キーはビューから `ActionKeyHint.Primary(KeyActions.KeyAction.Confirm)` を渡す (commit 411caee の v5 方式と同じ一元化。キー設定変更がキャップに反映される)。Tab (検索フォーカス) と F (絞り込み) は v6 固定ショートカットなので KeyActions に入れずリテラルのまま — 凡例は「Space 決定 / ↑↓ 曲送り / Tab 検索 / F 絞り込み」を出す |
| 既存キー入力の復元 | `Tick` の raw `KeyCode` 読みを KeyActions 経由に戻す: ↑↓ + `NavPrev/NavNext` (←→)、Confirm (既定 Space) + Enter/KeypadEnter、Back (既定 Esc)、Option (既定 O)。Tab (検索フォーカス) と F (絞り込み) は raw のまま (KeyActions に該当操作が無いため) |
| SelectSceneManager | 触らない (他担当所有)。Ready 判定は View / DataSource 側で完結させる |

## 非目標 (Out of scope)

- 試聴・シーク・プレビュー再生など新操作の追加 (Issue #222 スコープ訂正で撤回)。
- `SelectSceneManager.cs` / `V6UiMode` / v5 `SelectView.cs` の変更。
- プレイヤー集計データ源の新設。
- 実機 (展示 PC) での画面確認 — 未検証として evidence に明記し、EditMode テストで代替する。

## Acceptance criteria

1. 中央カードの 3 ボタンはすべて実操作に結線される (定義上 `Action` が必ず有効)。
2. 凡例の 4 項目のキー表示が実装の割り当てと一致し、決定キーは KeyActions の
   設定に追従する。
3. 絞り込みで選択曲が隠れた状態では、どの入口 (Space / Enter / Ready ボタン /
   中央 play) からも開始されず、エラーメッセージが出るか操作が無効になる。
4. ランキングの source タグと空状態、PLAYERS パネルが嘘の固定データを表示しない。
5. 既存の EditMode テスト (V6SelectLayoutTests / V6SelectDataLogicTests /
   V6SelectCenterScrollTests) が壊れない。
