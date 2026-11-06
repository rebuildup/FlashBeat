# Tasks: 非同期読み込みとローディング表示 (issue #113)

1. [x] Fader を `LoadSceneAsync` + `allowSceneActivation` 方式にし、`Initiate.LoadingFeedback`
   フックと二重起動防御を追加する (`Fader.cs`, `Initiate.cs`)。
2. [x] `LoadingOverlay` (コード生成 uGUI シングルトン) と純粋ロジック
   `LoadingOverlayLogic` を追加する。
3. [x] `NotesManager` の譜面読み込みをコルーチン化 (解析 → 8ms 予算バッチ生成、
   進捗報告、`IsReady`) し、`TimeBudgetBatcher` を追加する。
4. [x] `MusicManager`: 音源を `Resources.LoadAsync` に、Space 開始にノーツ準備完了を
   追加、メディア準備の段階文言を Overlay に出す。`OpeningSceneManager`: 動画初回
   準備を最初の描画後に移動。`[Perf]` ログを全箇所に追加。
5. [x] NUnit テスト (`TimeBudgetBatcherTests`, `LoadingOverlayLogicTests`) と
   spec/plan/tasks を追加する。
