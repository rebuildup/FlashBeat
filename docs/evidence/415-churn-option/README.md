# #415 第3段階 (Option) before/after 計測 — 2026-11-14

`-autotest v6churn` の Option フェーズ (commit `20d7d19` で追加) を
macOS プレイヤーで実行した記録。History/Ranking と同じ計測系。

## 条件

| 項目 | before | after |
|---|---|---|
| ソース | `8720b905` (main、phase 3 未適用) + v6churn Option フェーズと `MoveSelectionForAudit` のみ | `20d7d19` (`w/415-option-slots`) |
| ビルド | macOS (FlashBeat.diff worktree) | macOS (本 worktree) |
| 起動 | `-autotest v6churn -fbperf`、windowed 3420×2148 | 同左 |
| フェーズ | history → ranking → option、各 8 秒チャーン + Refresh 60 回バッチ | 同左 |

before 側の `MoveSelectionForAudit` は計測用に同名 API を生やしただけで、
内部は変更前の全再構築 `Rebuild()` を呼ぶ。選択移動 op は両側で対称。

## 実測 (生ログは各ディレクトリの result / excerpt)

### Refresh 60 回バッチ (壁時計・マネージドヒープ増分)

| | before | after | 倍率 |
|---|---|---|---|
| Option avg/refresh | 7.709 ms | 0.016 ms | 約 480× |
| Option managed delta | 63,536 KB | 280 KB | 約 1/227 |

参考 (同一ラン内の History/Ranking、phase 2 適用済み両側):

| | before | after |
|---|---|---|
| History avg/refresh | 0.052 ms | 0.042 ms |
| Ranking avg/refresh | 0.039 ms | 0.046 ms |

### フレームチャーン中の FrameTimeProbe 5 秒窓 (OptionScene)

| | before | after |
|---|---|---|
| churn avgFps | 60.0 | 59.3–60.0 |
| churn gcKBPerFrame | **214.8** (チャーン窓) | -4.4 / 0.4 / 1.2 |
| churn p99 / worst | 16.7 / 16.7 ms | 17.7–18.3 / 17.7–67.4 ms |
| チャーン ops/sec | 55.6 | 60.0 |

before は 1 フレームの Refresh 全再構築で約 215KB/frame をアロケート
しており、avgFps は vsync に張り付くものの GC 圧が高い。after は
Refresh が事実上ゼロコストになりアロケーションも消えた。

### センサス

| | before | after |
|---|---|---|
| Option View 配下 GameObject 数 | 730 | 730 |
| after の監査カウンタ | (未実装) | rebuild=1、incremental=1030 |

オブジェクト数が同じなのは before も「破棄して同数を再生成」するため
(phase 2 と同じ構造)。差は更新コストに出ている。

## 読み取れること

- Option の Refresh 1 回あたり約 7.7ms → 0.016ms。行選択・値変更・
  タブ往復を含むチャーンで毎フレーム全再構築していたものが、
  scaffold 保持 + 差分更新でほぼゼロになった。
- 60 回連続 Refresh のマネージドヒープ増分が約 63.5MB → 280KB。
- チャーン中のフレームアロケーションが約 215KB/frame → ほぼ 0。
- タブ切替は構造変更として Settings/Preview のみ再構築する設計で、
  ヘッダー/フッター/タブ帯は保持される (incremental=1030 に対し
  rebuild=1 のまま)。

## 制約・未実施

- Canvas rebuild 回数はエンジン側カウンタが無いため直接計測していない
  (phase 2 と同じ制約。Unity Profiler 接続が必要)。
- Win64 `-fbperf` は Windows 環境での実施待ち (#381)。
- 計測は macOS windowed。fullscreen / 実機負荷下での値は別途。
