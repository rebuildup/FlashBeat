# Evidence — Issue 226: 曲終了の状態機械 (SongEndMachine)

日付: 2026-11-08 / Branch: `w/b226` / Unity Editor は未起動 (他ワーカーが Library を所有のため)

## 実装

| ファイル | 変更 |
| --- | --- |
| `Assets/Game/Scripts/Gameplay/SongEndStateMachine.cs` (新規) | 純粋状態機械 `SongEndMachine` (`Playing → Finishing → Completed / Failed`)、`SongEndTick` 入力、`SongEndFailure` (MediaError / MediaStalled)。`SettleGraceSeconds = 2s` |
| `Assets/Game/Scripts/Gameplay/MediaTimeline.cs` | `IsUsableDuration()` 追加、`HasSongEnded()` に不正 duration (≤0 / NaN / Infinity) ガード、`FallbackSongEnd()` 追加 (メディアなし時の終端位置) |
| `Assets/Game/Scripts/Gameplay/MusicManager.cs` | `loopPointReached` 購読 (`OnSongVideoLoopReached`)、`DetectMediaEnd()` (isPlaying 立ち下がり / wav 終端 / 分離音声停止の補完経路)、メディア終端後は `GManager.StartTime` の再計算を止めて judge 時計を継続、`SongEndMachine.Update` で Completed を受理してから従来どおり `Initiate.Fade("ResultScene", Color.black, 1.0f)`、再生中エラー / 30 秒ストールで `FailSongEnd()` |
| `Assets/Tests/Editor/SongEndStateMachineTests.cs` (新規) | `SongEndStateMachineTests` (13 テスト) + `MediaTimelineSongEndContractTests` (7 テスト) |

編集していないもの: `Judge.cs` / `JudgeQueue.cs` / `NotesManager.cs` / `PlaybackRecovery.cs` / UI 側 (競合回避・指示どおり)。

## 動作の要点

1. **終端条件** = `mediaEnded`(実メディアの終端: loop point / isPlaying 立ち下がり / wav 終端) **または** `MediaTimeline.HasSongEnded`(有効 duration − 1s リードアウト、旧契約を維持)。メディアなしのフォールバック時計では `mediaTime >= FallbackSongEnd(duration)`。
2. **末尾ノーツの確定**: メディア終端後も judge の時計 (`Time.time - StartTime`) を進めるため、Judge 既存の Miss 窓 (`BAD_WINDOW = 0.2s`) と HOLD の中断 / 期限切れ (`GREAT_WINDOW`) で全ノーツが確定する。判定ロジックは無変更。
3. **遷移**: `Finishing` 中は未確定ノーツ (pending = `noteNum − (GManager.nextNotes − 1)`) が 0 になるまで Result へ行かない。judge 時刻が `SettleDeadline + 2s` を過ぎても残る場合のみ `ForcedByDeadline` 付きで強制完了 (ログに警告、合計不一致はランキング側の合計チェックで拒否される = 成功偽装なし)。
4. **Failed**: 再生中の VideoPlayer エラー → `SongEndFailure.MediaError`、30 秒時計停止 → `SongEndFailure.MediaStalled`。Completed を報告せず自動遷移しない。VideoPlayer エラーは GameHudRuntime が自前で errorReceived を購読しており GameErrorView が表示される (既存経路)。開始前の準備失敗の既存経路 (`HandlePlaybackFailure` → `SelectReturnPending` → GameErrorView) は無変更。

## 検証

1. **コンパイル** (Unity Editor 不使用):
   - `script/compile-check.sh --project /Users/rebui/work/FlashBeat.b226 --assembly flashbeat` → `Build succeeded` (139 sources)
   - `script/compile-check.sh --assembly tests` → `Build succeeded` (37 sources)
2. **純粋ロジックの .NET ハーネス** (issue の最小確認と同じ方式。`MediaTimeline.cs` + `SongEndStateMachine.cs` を UnityEngine の Mathf スタブでコンパイルし実行、14 チェックすべて PASS):
   - Issue の再現シナリオ: duration=10, lead 1 → 9s で Finishing にはなるが完了せず、10.1s (Miss 窓内) も待ち、確定後 (10.3s) に Completed。旧実装は 9s でフェードしていた。
   - 動画 60s / duration 120s: duration 条件は成立せず、メディア終端で Finishing → 確定後に Completed (旧実装は Result に進めなかった)。
   - duration 不正: 0 / NaN / Infinity では duration で終わらず、メディア終端で完了。メディアなし + duration 0 は即終端 (旧挙動どおり、閉じ込めなし)。
   - 極端に長い duration (1e9) + メディア終端 → 完了。極端に短い duration (0.001s) でもノーツ確定を待ってから完了。
   - 期限強制完了: SettleDeadline+2s を過ぎて未確定が残ると `ForcedByDeadline=true` で完了。猶予内は待つ。
   - Failed 経路: Failed は Update を何回重ねても Completed を報告しない。Completed 後の Fail は無視。
3. **EditMode テスト**: `Assets/Tests/Editor/SongEndStateMachineTests.cs` に上記と同等の契約を固定 (17 テスト: 状態機械 9 + MediaTimeline duration 契約 8)。実行は Supervisor が統合時に実施 (Editor 未起動のため本ワーカーでは未実行)。
4. **既存テストへの影響**: `MediaTimelineTests` の `HasSongEnded` の既存アサート (47/48s 系) は有効 duration のみで、ガード追加により不変。`HudPlayStateTests` / `PlaybackRecoveryTests` が観測する `SelectReturnPending` / `HandlePlaybackFailure` 経路は無変更。

## 残存リスク

1. **pending の会計が `GManager.nextNotes` 経由の間接観測**。Judge が確定 1 件ごとに `message()` → `nextNotes++` する既存会計に依存する。無効レーンでスキップされたノーツは永遠に確定しないため、期限 + 2s の強制完了が上界を保障する (その場合判定合計がノーツ数に届かず、ランキング送信は既存の合計チェックで拒否される — 偽装はしない)。
2. **ストール → Failed 中の可視フィードバック**: VideoPlayer エラーは GameErrorView が出るが、ストール (error イベントなし) は HUD に出ない (`HudPlayState.Evaluate` が playbackStarted 中の pipelineFailed を Hidden にするため)。回復導線は Esc ポーズメニュー → 選曲へ戻り。HUD 側の変更は所有外のため未実施。
3. **メディアなし + 極端に長い不正 duration** (例: 1e9) では壁時計での終端を待つことになり、実質 Esc 回復頼み。成功偽装を避けるため意図的に上限を設けていない。
4. **分離音声 (StreamSeparate) で映像が終端に達した後も音声プレイヤーが鳴り続ける**可能性 (既存挙動のまま)。フェードまで最大数秒の音の余韻があり得る。
5. **実機検証未実施**: Unity 実機 (VideoPlayer イベント / フレームタイミング、ランキング送信込みの完走) は Supervisor の統合時検証が必要。EditMode / ハーネスは純粋ロジックのみ。

## 必要な API (JudgeQueue 側・他担当向けメモ)

`MusicManager` は `GManager.nextNotes` (確定 1 件ごとに +1) と `NotesManager.noteNum` から pending を間接算出している。次のいずれかが Judge/JudgeQueue 側に生えると、この間接会計と強制完了の猶予 (2s) を排除できる:

- `JudgeQueue.SettledCount` (判定済み + Miss 確定 + HOLD 確定の合計) と `JudgeQueue.UnsettledCount` (pending + 保持中) — または
- `Judge.UnsettledNoteCount` (queue への委譲プロパティ)

併せて、無効レーン除外ノーツを「確定扱い」にカウントするかどうかの合意があると、強制完了パスが実質不要になる。

## 完了条件の確認

- [x] コード (MusicManager / MediaTimeline / SongEndStateMachine 新規)
- [x] テスト (Assets/Tests/Editor/SongEndStateMachineTests.cs)
- [x] spec / plan / tasks (`specs/226-song-end-state-machine/`)
- [x] compile-check (flashbeat / tests とも Build succeeded)
