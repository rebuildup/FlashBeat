# Plan

- `MusicManager` に `SelectReturnPending` (static) を追加: `HandlePlaybackFailure` で true、`PrepareMediaCoroutine` 冒頭と `StartPlayback` で false。
- `HudPlayState.Evaluate` を 5 引数 (純粋) にし、既存の 4 引数オーバーロードは `MusicManager.SelectReturnPending` を観測して委譲。`HudPlayStatus` 側は無変更で良い (シグネチャ互換)。
- 音源決定のインライン if 群を `PlaybackRecovery.DecideAudioSource` に抽出し MusicManager から呼ぶ。
- クロック: 復帰経路はすべて毎フレーム `GManager.StartTime = JudgeStartTime(Time.time, mediaTime)` で再計算されるためメディアクロックと同期が保たれる (変更不要、確認のみ)。
- テスト: `PlaybackRecoveryTests` に音源決定 4 ケースを追加、`HudPlaybackFailureHookTests` を同ファイルに追加 (HudPlayStateTests は他オーナーのため触らない)。
