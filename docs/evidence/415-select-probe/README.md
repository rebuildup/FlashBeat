# #415 第4段階 (Select) 要否判断プローブ — 2026-11-14

`V6SelectView.RefreshVolatile` (#399) は Refresh ごとに可変ゾーン
(Header / Song List / Selected Song Panel / Ranking Column /
Settings Column) を破棄→再構築する。行スロット再利用を入れるかを
per-call コストで判断するための計測。行移動の公開 API が無いため
選択 op は no-op、Refresh が主計測対象。

## 条件

| 項目 | 内容 |
|---|---|
| ソース | `3e288712` (main、phase 4 未適用) + v6churn Select フェーズ |
| ビルド | macOS、windowed 3420×2148 |
| 起動 | `-autotest v6churn -fbperf` |

## 実測 (`autotest-v6churn-result.txt` / `Player.log.excerpt`)

### Refresh 60 回バッチ

| phase | avg/refresh | managed delta (60回) |
|---|---|---|
| **select** | **4.549 ms** | **49,008 KB** |
| history (参考) | 0.043 ms | 108 KB |
| ranking (参考) | 0.043 ms | 88 KB |
| option (参考、phase 3 適用済み) | 0.015 ms | 224 KB |

select の 1 Refresh あたり約 818KB のマネージドアロケーション。
Option 修正前 (7.7ms / 63.5MB per 60) と同オーダー。

### フレームチャーン (8 秒、Refresh 毎フレーム)

- ops/sec 59.4 (ほぼ vsync 上限。Refresh 4.5ms は 16.7ms 予算に収まる)
- SelectScene の該当 5 秒窓: avgFps 54.8、p99 83.9ms、worst 183.2ms
  — 他窓 (avgFps 59-60、p99 ~33ms) より明確に悪い。
  RefreshVolatile のゾーン再構築がフレームスパイクとして出ている。

### センサス

objects=289、rebuild=1、incremental=1525 (#399 のカウンタは機能中。
RefreshVolatile は incremental として計上されるが、内部では
ゾーン破棄→再構築を行っている)。

## 結論

**phase 4 (Select の scaffold/行スロット再利用) は実施する。**
History/Ranking/Option で確認したのと同じ構造的コスト
(数 ms/回・数十 MB/60回・チャーン時のフレームスパイク) が
Select にも存在する。

## 実装上の注意

- `w/415-diff-update` の Select 実装 (1919 行) は旧 base
  (`2d9b7107`) 由来で main (1387 行) との差分が大きい。
  History/Ranking/Option と同様、WIP は参照のみとし
  current-main の #399 実装・検索/フィルター/クイック設定・
  スクロール追従・ジャケットローダの挙動を保持して移植する。
- 優先対象は Song List の可視行スロット再利用と
  Header/Center/Right の in-place 更新。構造変化
  (settingsOpen 開閉、選択曲 null↔非 null、行数変化) のみ
  部分再構築に落とす。
