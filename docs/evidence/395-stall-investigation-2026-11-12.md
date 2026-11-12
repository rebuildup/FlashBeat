# #395 調査 — メディア準備後の「停止」はレンダリング停止ではなく時計の飛び (2026-11-12)

Issue #395 は「メディア準備 → Game の 8.4 秒停止」の原因特定を求めている。
本調査では `script/mac-fbperf.sh` で `571bd357` (修正前 main) を実測し、
**停止の正体がフレーム描画の停止ではない**ことをログから特定した。
修正はまだ入れていない (原因が分かった段階)。

## 1. 記録された停止

```
[PerfProbe] event=window scene=GameScene frames=302 avgFps=60.2 p50=16.7 p95=16.7 p99=16.7 worst=16.7
[PerfProbe] event=spike scene=GameScene frameMs=47855.6 t=56.32
[PerfProbe] event=window scene=GameScene frames=47 avgFps=1.0 p50=16.7 ... worst=47855.6
```

`FrameTimeProbe` は `Time.unscaledDeltaTime` を記録する。47,855.6 ms の
「フレーム」が 1 回あった、と読める。

## 2. 同時刻の MusicManager ログ

```
t≈5.53s
[MusicManager] STATUS mediaTime=5.53 audioMode=StreamCombined rms=0.0000 peak=0.4839
               video[playing=True frame=4158 prepared=True] perfect=3 ...

   ← ここで frameMs=47855.6

t≈55.97s
[MusicManager] STATUS mediaTime=55.97 audioMode=StreamCombined rms=0.0000 peak=0.4839
               video[playing=True frame=4158 prepared=True] perfect=3 ...
```

読み取れること:

1. **`video frame=4158` が 5.53 秒時点と 55.97 秒時点で同一**。
   映像は 1 フレームも進んでいない。
2. **`mediaTime` は 5.53 → 55.97 へ飛んでいる** (50.4 秒分)。
3. `isPlaying=True` / `prepared=True` のまま。プレイヤーは「再生中」と報告している。
4. `rms=0.0000` (音声レベルは 0) が 5.53 秒以降ずっと続いている。
   `peak=0.4839` は 5.53 秒以降変化していない (過去のピークの残り)。

## 3. 結論 (現時点)

**この 47.9 秒は「描画が 48 秒かかった」のではない。**

- 映像フレームは止まったまま (`frame=4158` で不変)。
- メディア時計だけが 50 秒進んだ。
- 音声は無音 (`rms=0.0000`)。

つまり **メディアのデコードが止まり、時計だけが進んだ**状態である。
`FrameTimeProbe` の `unscaledDeltaTime` が 47.8 秒になったのは、
おそらくメディアのデコード待ちでメインスレッドがブロックしたためだが、
**その内訳 (誰が待っているか) はまだ Profiler で取っていない。**

これは「レンダリングの最適化 (#394)」では直らない種類の問題である。
#394 の対象 (ノーツ数依存の uGUI コスト) とは別の層。

## 4. 副次的に分かったこと (より重要)

同じ実行で、**曲が 130 ノーツ中 3 ノーツしか判定されず**、期限切れで強制完了している。

```
[MusicManager] song end forced with 127 unsettled note(s) ... the result total may not match
[MusicManager] song completed: judged 3/130, mediaEnded=False, state=Completed
```

`mediaTime=55.97` は曲長 44 秒を超えている。つまり

- メディア時計が曲長を超えて進み、
- 判定は 3 ノーツで止まり、
- `SongEndMachine` が期限切れで強制完了した。

**これは「演奏が成立していない」状態**で、フレームレート以前の問題である。
ハーネスがノーツを送っていない (hitsSent=3) ので判定が増えないのは
ハーネス側の設計どおりだが、`mediaTime` が曲長を超えて進むのは
`MusicManager` の時計の扱いの問題である。

## 5. 次に必要なこと

1. **Profiler で停止中の Main Thread を取る**。`VideoPlayer` のデコード待ちか、
   `MediaFoundation`/`AVFoundation` の同期呼び出しかを特定する。
2. `mediaTime` が曲長を超えて進む経路の確認 (`CurrentMediaTime` と
   `SongEndMachine` の関係)。曲長で頭打ちにするのが正しい可能性がある。
3. 8.4 秒 / 47.9 秒の差が「キャッシュの有無」か「実行ごとのばらつき」かを、
   同一条件で複数回測って分離する。

## 6. 未計測

- Profiler (Main Thread / Render Thread / Video / GC) の内訳: **未取得**
- 再現回数: **1 回のみ** (ばらつき未評価)
- Win64: 未計測

**この文書は原因の特定ではなく、原因の層を切り分けた記録である。**
修正と再計測は次の段階。
