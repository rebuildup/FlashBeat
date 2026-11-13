# #415 第4段階 (Select) before/after 計測 — 2026-11-14

`-autotest v6churn` の Select フェーズを macOS プレイヤーで実行した記録。
History/Ranking (#455) / Option (#456) / Select プローブ (#457) と同じ計測系。

## 条件

| 項目 | before | after |
|---|---|---|
| ソース | `3e288712` + v6churn Select フェーズ (#457 プローブ、`docs/evidence/415-select-probe/`) | `94bafec` (`w/415-select-slots`) |
| ビルド | macOS、windowed 3420×2148 | macOS、同左 |
| 起動 | `-autotest v6churn -fbperf` | 同左 |
| フェーズ | history → ranking → select → option、各 8 秒チャーン + Refresh 60 回バッチ | 同左 |

before 側の `V6SelectView.cs` は `3e288712..916139bf` で無変更
(#458 はテストのみ) のため、#457 プローブの計測値を同一コードの
before として使う。after 側は `RefreshVolatile` を scaffold 保持 +
行スロット再利用の差分更新へ移した実装。

## 実測 (生ログは各ディレクトリの result / excerpt)

### Refresh 60 回バッチ (壁時計・マネージドヒープ増分)

| | before | after | 倍率 |
|---|---|---|---|
| **Select avg/refresh** | **4.549 ms** | **0.054 ms** | **約 84×** |
| **Select managed delta** | **49,008 KB** | **8 KB** | **約 1/6100** |

参考 (同一ラン内の他フェーズ):

| | before (#457) | after |
|---|---|---|
| History avg/refresh | 0.043 ms | 0.041 ms |
| Ranking avg/refresh | 0.043 ms | 0.043 ms |
| Option avg/refresh | 0.015 ms | 0.015 ms |

### フレームチャーン中の FrameTimeProbe 5 秒窓 (SelectScene)

| | before | after |
|---|---|---|
| churn avgFps | 54.8 | 59.8–60.0 |
| churn p95 | 17.7 ms | 17.6 ms |
| churn p99 | 83.9 ms | 17.7–17.9 ms |
| churn worst | 183.2 ms | 17.7–33.5 ms |
| churn gcKBPerFrame | -15.5〜13.7 (変動大) | -1.7〜2.2 |
| churn ops/sec | 59.4 | 60.0 |

before は RefreshVolatile がゾーンを破棄→再構築するたびに
マネージド約 818KB/回をアロケートし、チャーン中のフレーム
スパイク (p99 83.9ms / worst 183.2ms) として出ていた。
after は Refresh が事実上ゼロコストになり、スパイクは消えた。

### センサス

| | before | after |
|---|---|---|
| Select View 配下 GameObject 数 | 289 | 289 |
| 監査カウンタ | rebuild=1、incremental=1525 | rebuild=1、incremental=1546 |

オブジェクト数が同じなのは before も「破棄して同数を再生成」
するため (phase 2/3 と同じ構造)。差は更新コストに出ている。

## 読み取れること

- Select の Refresh 1 回あたり約 4.5ms → 0.054ms。
  60 回連続 Refresh のマネージドヒープ増分が約 49MB → 8KB。
- 曲選択・検索・スクロール・設定値変更で可視行スロットと
  ゾーン scaffold を再利用し、構造変化 (設定開閉・解像度・
  選択曲 null↔非 null・リスト/ランキングのモード遷移・
  Ready 可否の反転) のみ該当部分を再構築する。
- 表示差分: センサスの GameObject 数は同一 (289) で、
  EditMode の再利用テスト (`V6SelectViewReuseTests`) が
  行同一性と表示値 (曲名・選択色・BEST・検索語・件数) の
  更新を固定している。

## 制約・未実施

- Canvas rebuild 回数はエンジン側カウンタが無いため直接計測して
  いない (phase 2/3 と同じ制約。Unity Profiler 接続が必要)。
- Win64 `-fbperf` は Windows 環境での実施待ち (#381)。
- 計測は macOS windowed (FullscreenBootstrap が FullScreenWindow
  へ昇格、解像度は 3420×2148 で before と同一)。
- before は #457 プローブの記録を再利用 (同一 `V6SelectView`
  コード)。after は本ブランチの macOS ビルドで新規計測。
