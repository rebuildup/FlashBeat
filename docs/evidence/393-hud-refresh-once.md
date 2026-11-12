# #393 Game HUD の二重 Refresh を一本化 — 実装と計測

## 1. 問題 (確認済み)

`571bd357` の時点で、同じフレームに HUD の `Refresh()` が 2 回走っていた。

| 呼び出し元 | 箇所 |
|---|---|
| `V6GameRuntime.Update()` | `hud.Refresh()` |
| `V6GameHudView.Update()` | `Refresh()` |

`Refresh()` はスコア・ゲージ・コンボ・歌詞・判定行・レーンガイドの更新を含む。
文字列設定は値が変わったときだけ書く差分ガードがあるが、**その判定自体が 2 回走る**。

## 2. 修正

更新の所有者を `V6GameHudView` 自身に一本化した。

- `V6GameRuntime.Update()` は `hud.RequestRefresh()` を呼ぶ (表示の出し分けだけを持つ)。
- `RequestRefresh()` は **同じフレームで既に更新済みなら何もしない**。
  したがって、呼び出し順に依存せず 1 フレーム 1 回が保証される。
- `V6GameHudView.Update()` も `RequestRefresh()` を通す。

「統合側が呼ばないこと」に依存しない設計にしたのは、将来 `V6GameRuntime` 以外から
更新を要求されても二重にならないようにするため。

## 3. 回帰テスト

`Assets/Tests/Editor/HudRefreshOncePerFrameTests.cs` (4 件):

| テスト | 固定すること |
|---|---|
| `RequestRefreshTwiceInOneFrameRunsOnce` | 同一フレームで 3 回呼んでも `Refresh` は 1 回 |
| `RequestRefreshRunsAgainOnTheNextFrame` | 別インスタンス/次フレームでは通常どおり走る |
| `DirectRefreshCallsAreVisibleInTheAuditCounters` | 直接呼び出しは重複として観測できる |
| `TheRuntimeDoesNotCallRefreshDirectly` | `V6GameRuntime` が `Refresh` を直接呼ばない |

`TheRuntimeDoesNotCallRefreshDirectly` は、修正を戻すと **FAIL する**ことを確認した。

観測点として `RefreshCountForAudit` / `DuplicateRefreshFramesForAudit` を追加した。
これらは実行時の計測にも使える (ハーネスから読める)。

## 4. 計測

### 4.1 定常状態 (ベースライン、`571bd357`)

`script/mac-fbperf.sh` / ツイッターランド / Mac 3420×2148:

```
avgFps=60.2 p50=16.7 p95=16.7 p99=16.7 worst=16.7 gc0=5 gcKBPerFrame=3.5
```

### 4.2 修正後

修正後のビルドで同条件を再計測し、`docs/evidence/381-fbperf-*` に追記する。
**この節は計測が終わるまで「未計測」である。**

## 5. 注意 (改善を誇張しない)

ベースラインが既に `avgFps=60.2 / p95=16.7ms` で 60fps の上限に張り付いている。
**この曲では HUD 更新の重複がフレーム時間に現れていない。**
つまり #393 の効果は「フレームレートが上がった」ではなく、

- 同じフレームに同じ仕事を 2 回しない (CPU 時間の無駄の除去)、
- 重い場面 (ノーツが多い曲、エフェクトが多い場面) での余裕を増やす、

として評価するのが正しい。数値で差が出ない場合は「差が出なかった」と書く。

## 6. 計測結果 (A/B、負荷つき)

| 実行 | load avg | avgFps (median) | p95 | p99 | gc0 | gcKB/frame |
|---|---|---|---|---|---|---|
| before | 11.3 | 9.0 | 122.8 ms | 174.8 ms | 29 | 6.1 |
| after | 7.9 | 9.1 | 121.5 ms | 124.2 ms | 27 | 12.9 |

**フレームレートの改善は確認できなかった。** この負荷帯ではマシン側が天井で、
どちらも 60fps を出していない。負荷の低い時間帯の before は既に 60.2fps (§4.1)。

したがって #393 の成果は「フレームレートが上がった」ではなく、
**同じフレームに同じ仕事を 2 回しないことを保証した**ことにある。
数値での改善を示すには、負荷の低い時間帯に重い曲で複数回測る必要がある (未実施)。
