# #415 第2段階 (History/Ranking) before/after 計測 — 2026-11-14

`-autotest v6churn` (本ブランチで追加) を macOS プレイヤーで実行した記録。
同じ計測コードを before/after 両ビルドへ入れ、同一条件で比較する。

## 条件

| 項目 | before | after |
|---|---|---|
| ソース | `391178a2` (merge-base、phase 2 未適用) + v6churn のみ | `457872e3` (`w/415-history-slots`) |
| ビルド | macOS `0.1.26+391178a2` | macOS `0.1.26+457872e3` |
| 起動 | `-autotest v6churn -fbperf`、windowed 3420×2148 | 同左 |
| フェーズ | history → ranking、各 8 秒チャーン + Refresh 60 回バッチ | 同左 |

## 実測 (生ログは各ディレクトリの result / excerpt)

### Refresh 60 回バッチ (壁時計・マネージドヒープ増分)

| | before | after | 倍率 |
|---|---|---|---|
| History avg/refresh | 5.607 ms | 0.037 ms | 約 150× |
| History managed delta | 67,164 KB | 64 KB | 約 1/1000 |
| Ranking avg/refresh | 4.974 ms | 0.051 ms | 約 97× |
| Ranking managed delta | 51,300 KB | 68 KB | 約 1/750 |

### フレームチャーン中の FrameTimeProbe 5 秒窓 (SelectScene)

| | before | after |
|---|---|---|
| History churn avgFps | 45.1 → 29.5 | 58.2 → 59.4 |
| History churn p95 / p99 / worst | 50.0 / 66.7 / 416.7 ms | 17.6 / 33.8 / 50.9 ms |
| Ranking churn avgFps | 37.2 | 58.6 |
| Ranking churn p95 / p99 / worst | 50.0 / 183.2 / 200.1 ms | 17.5 / 33.4 / 49.4 ms |
| チャーン ops/sec | 29.3 (両画面) | 58.9 / 58.3 (ほぼ vsync 上限) |

### センサス

| | before | after |
|---|---|---|
| History View 配下 GameObject 数 | 274 | 275 |
| Ranking View 配下 GameObject 数 | 244 | 260 |
| after の監査カウンタ | (未実装) | rebuild=1、incremental=1497 (history) / 1015 (ranking) |

オブジェクト数が before/after で同程度なのは、before も「破棄して同数を
再生成」するため。差は **更新のコスト** (CPU 時間・アロケーション・
フレーム落ち) に出ている。

## 読み取れること

- Refresh 1 回あたり約 5ms → 0.04ms。毎フレーム更新が走る経路では
  フレーム予算 (16.7ms) の約 1/3 を View 更新だけで消費していたものが、
  ほぼゼロになった。
- 60 回連続 Refresh のマネージドヒープ増分が約 52〜67MB → 約 64〜68KB。
  全再構築で TMP・文字列・クロージャを毎回確保していたことが分かる。
- チャーン中のフレーム落ちは p99 66〜183ms・worst 200〜417ms →
  p99 約 33ms・worst 約 50ms に改善。avgFps は 29〜45 → 58〜59
  (ほぼ上限)。
- 残る p99 ~33ms / worst ~50ms のスパイクは選択移動に伴うジャケット
  ロードやデータソース側の処理が主と見られ、View の再構築ではない
  (after でも rebuild=1 のまま incremental のみ増加)。

## 制約・未実施

- Canvas rebuild 回数はエンジン側カウンタが無いため直接計測していない。
  GameObject 再利用とフレーム時間の改善から実質的な rebuild 削減が
  期待できるが、厳密な回数は Unity Profiler 接続が必要。
- Win64 `-fbperf` は Windows 環境での実施待ち (#381)。
- 計測は macOS windowed。fullscreen / 実機負荷下での値は別途。
