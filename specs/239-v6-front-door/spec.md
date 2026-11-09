# 239 — [P0/v6] Title / History / Ranking の UI 整合 (既存機能を保つ)

Parent: #239 (parent #217)。Design: flashbeat-ui.pen の Title QJtTv / History q9JZcH / Ranking S1TZD / 共通フレーム rw7zx。

## 目的

v6 の Title・History・Ranking で、**既に存在する記録・操作・データ経路**がそのまま使えるようにし、表示 (ラベル・凡例・キーキャップ) と実値・実キーを一致させる。新機能 (プレイ履歴ストア、プレイヤー集計 REST API など) は本 spec の要件から外す ( Issue 本文の「撤回」項)。

## 固定した仕様決定

### D1. 入口導線の一致 (タイトルタブとキー)

- Title のタブは design QJtTv どおり 4 枚: `GameStart` / `History` / `Ranking` / `Option`。
  - History / Ranking は遷移先が Title シーンに無いため `V6TitleLinks` 経由で接続する。
    `HistoryRequested` / `RankingRequested` を TitleSceneManager が Start で設定し、
    遷移先 (Select シーン) は静的な `V6TitleLinks.PendingEntry` を Start で消費して
    History / Ranking モードで開く。
  - タブの KeyCap 表記と実際に poll するキーは `V6TitleLogic` の単一ソース
    (`HistoryTabKey` = "H" / `RankingTabKey` = "R" と対応する KeyCode) を使う。
    TitleSceneManager は `KeyCode.H` / `KeyCode.R` を poll し、KeyActions に
    割当の無い固定キーである (KeyActions に新設しない)。
- Select 画面内の入口は既存の `L` → History / `R` → Ranking を維持する (既存操作の保護)。
  Select 画面の凡例は V6SelectView (#221 所有) のため本 issue では触れない。
  残差は evidence に記録する。
- spec 167 の F2/F3 入口は v5 向けの表記とし、v6 では採用しない (実装済みの L/R と
  タイトルタブが実入口。キーを増やしても凡例に載せられないため)。

### D2. History のラベルを実値に合わせる

- ヘッダー右端の件数表示: 「N plays」→「記録 N 曲」(値はベスト記録のある曲数であり
  プレイ回数ではない)。
- プレイヤー情報パネル:
  - 累計 = 記録済みベストスコアの合計 (変更なし)
  - 「プレイ曲」→「プレイ済み」値 `記録曲数 / 全曲数` (意味を明確化)
  - S以上 = BestGrade が S / SS の曲数 (変更なし)
  - 「順位」セルは削除 (総合順位 API は既存に無い。design 差分として evidence に記録)
- プレイ情報パネル: HOLD セルを削除 (ホールド成功数の集計は既存コードに無く、
  ホールドは先頭押下時に通常判定へ加算済み。常に "–" の死んだセルは残さない)。
  行は PERFECT / GREAT / BAD / MISS / MAX COMBO の 5 セル。

### D3. History の「曲ランキング」パネルの状態網羅

空のときの本文は `V6RankingLogic.SongRankingEmptyMessage` に集約する:

- cloud 曲: 取得中 → 「ランキングを取得しています…」、失敗 → データソースの
  `StatusMessage`、取得成功で 0 件 → 「NO DATA」。
- cloud 以外 (ローカル曲): 「記録がありません」(オンラインの状態メッセージを
  流さない)。

### D4. Ranking の整合

- フッター凡例を実キーに一致させる: `↑↓ 曲送り / Space 決定 / S 切替 / F5 再試行 / Esc 戻る`
  (「Tab 切替」は実装と不一致のため削除、F5 再試行を追加)。
- Online セグメントは cloud 曲だけ表示 (spec 167:216 を踏襲)。cloud 以外の曲では
  Online セグメントを描かず、表示は Local に寄せる。セグメント切替 (S) も
  cloud 以外では何もしない (見えないセグメントへの切替でキーを死なせない)。
- PLAYERS パネル: 総合集計 API は既存に無いため、左右の矢印ボタン (無機能) と
  メトリックラベルを削除し、本文を「プレイヤー別の集計は未対応です」にする。
  パネル自体は design 要素として残し、未対応であることを偽らない。

### D5. 設定ボタン / O キーを History・Ranking でも有効化

History / Ranking の「設定」ボタンと O キーは、選曲画面のクイック設定を開く
(クイック設定は選曲画面の右カラム差し替えのため、一度選曲モードへ戻してから開く)。
Select 以外で警告して捨てていた既存の early-return を削除する。

## 既存機能の保護

- History はベストスコア順の「プレイ済み曲の記録一覧」(spec 121/167 の基準) のまま。
  時系列履歴・PlayHistoryStore は作らない。
- ランキングの取得経路 (V6SelectDataSource → RankingClient) は触らない
  (読み取りのみ)。エラー状態 (`HasError` / `StatusMessage` / `IsLoading`) の表示に
  使うのは許可された読み取り。
- 保存方式 (ScorePersistence / PlayerPrefs) は変更しない。

## 未確定事項 (design 差分として記録、実装しない)

- PLAYERS の総合集計 (API 無し) — 空状態のみ。
- 履歴パネル中央の skip/play コントロール (プレビュー再生 API 無し)。
  V6SelectView と同じ扱いで装飾として残置 (evidence に記録)。
- 順位 cell / HOLD 判定の内訳はデータが存在しないためセルごと削除。
