# Plan: 非同期読み込みとローディング表示 (issue #113)

## Approach

既存の `Initiate.Fade` 経路 (~12 呼び出し site) は触らず、`Fader` 内部だけを
同期ロードから `LoadSceneAsync` に置き換える。UI シーンマネージャは一切変更しない。

進捗の受け渡しは `Initiate.LoadingFeedback` (Begin/Progress/End デリゲート) で行う。
SimpleFadeSystem アセンブリは FlashBeat を参照できない (循環参照) ため、逆方向の
通知はデリゲートフックで行い、`LoadingOverlay` が `RuntimeInitializeOnLoadMethod`
で差し込む。

## Design decisions

- **オーナー方式の Overlay**: シーン遷移 (排他的, owner=null)、メディア準備
  (owner "media")、譜面準備 (owner "notes") が重なることがあるため、`Show/Hide`
  にオーナーキーを持たせる。owner=null は他オーナーをリセットする排他表示。
- **純粋ロジックの分離**: 表示遅延/最短表示/進捗写像は `LoadingOverlayLogic`
  (Unity API なし)、バッチ予算は `TimeBudgetBatcher` に切り出し、NUnit から
  直接検証できるようにする。
- **ノーツ生成の適応バッチ**: `NextBatchSize(remaining)` → 実測 → `Report(count, seconds)`
  のループで 1 フレーム 8ms 予算に収める。譜面の時刻計算は純粋数値処理なので
  一括 (1 フレーム以内) で行い、Instantiate のみバッチ化。
- **開始タイミングの保護**: `MusicManager.Update` の Space 許可に
  `NotesManager.IsReady` を追加。判定時計はメディア時計基準のままなので、
  準備が終わってからの開始にすることでタイミングへの影響を zero に保つ。
- **音源の非同期化**: `Resources.LoadAsync` にして、`audioMode` 決定の直前に
  完了を待つ (決定ロジック `PlaybackRecovery.DecideAudioSource` は不変)。
- **Opening**: オープニング動画は PlayOnAwake でシーンロード直後に初回準備が
  走り最初のフレームを止める。`OpeningSceneManager` がコードから一度 Pause し、
  最初の描画の後に再再生する (シーンファイルは編集しない)。

## Risks

- `LoadingSceneAsync` の progress はアクティベーション禁止中 0.9 で止まる →
  `MapProgress` で 0.9 を 100% に写像する。
- `Initiate.Fade` の呼び出し site は変更不要だが、Fader の二重起動防御
  (`loadRoutineRunning` + 既存 `startedLoading`) を入れて連打耐性を確保する。

## Verification

- NUnit: `TimeBudgetBatcherTests`, `LoadingOverlayLogicTests` (EditMode)。
- Unity 実行は不可のため、`[Perf]` ログによる Windows ビルド計測は Supervisor が
  Player.log で確認する。
