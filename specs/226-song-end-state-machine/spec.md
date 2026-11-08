---
description: "FlashBeat lean spec — Issue 226"
---

# Spec: 曲終了の状態機械 (SongEndMachine) — duration 依存の終了判定を直す

**Issue**: #226 | **Branch**: `w/b226` | **Date**: 2026-11-08 | **Status**: Draft

**Input**: `[Gameplay/P1] 曲終了を duration だけで決めるため、末尾ノーツが未判定のまま確定し、動画終端でも Result へ進めない`

## Objective

曲の終了を「カタログの durationSec だけ」で決めるのをやめ、
**(1) 終端条件 (メディア終端 または 曲長) と (2) 全ノーツの確定** の両方を
取りまとめる状態機械 `SongEndMachine` を導入する。これにより:

- (a) 末尾ノーツが判定窓 (BAD 窓 / HOLD 猶予) を流れる前に Result へ遷移し、
  判定合計がノーツ数に届かずランキング送信が拒否される問題を直す。
- (b) 動画がメタデータより先に終わって時計が止まったとき、Result に進めず
  プレイヤーが閉じ込められる問題を直す。
- (c) メディアの破綻 (再生中エラー、長時間ストール) を Completed に偽らず、
  Failed として既存のエラー導線へ流す。

## Decisions (凍結する設計)

| 項目 | 決定 |
| --- | --- |
| 状態機械 | 新規純粋クラス `SongEndMachine` (`Assets/Game/Scripts/SongEndStateMachine.cs`)。状態は `Playing → Finishing → Completed / Failed`。Input / Time / MonoBehaviour 非依存で EditMode テストから直接検証する |
| Finishing への遷移 (終端条件) | `メディア終端 (実メディアの loop point / 再生停止)` **または** `曲長到達`。duration が使用可能なときは既存のリードアウト (`MediaTimeline.HasSongEnded`, lead 1s) を維持 |
| 時計の信頼順位 | **実メディア時計が正** (譜面は映像に合わせて作られている)。動画が duration より先に終わればそこが終端 (case b の修正)。duration が先なら duration が終端 (旧挙動を維持しつつノーツ確定を待つ)。duration が不正 (0 以下 / NaN / Infinity) の場合、実メディアありなら duration を終了条件に使わず、メディアなし (壁時計フォールバック) では `FallbackSongEnd` = 0 で即終端 (旧挙動どおり、閉じ込めない) |
| 末尾ノーツの確定 | メディア終端後、MusicManager は `GManager.StartTime` の再計算を止める (固定)。judge の時計 `Time.time - StartTime` は終端時刻から壁時計で進み続け、Judge 既存の Miss 窓 (`BAD_WINDOW`) / HOLD の中断・期限切れ (`GREAT_WINDOW`) で全ノーツが確定する。判定ロジック (Judge / JudgeQueue) は変更しない |
| Finishing → Completed | 未確定ノーツ数 (pending) が 0 になったら完了。pending は「ノーツ総数 (NotesManager.noteNum) − 確定数 (GManager.nextNotes − 1)」で数える (Judge が確定 1 件ごとに `message()` 経由で nextNotes を +1 する既存会計を利用) |
| 強制完了 | judge 時刻が `SettleDeadline + SettleGraceSeconds(2s)` を過ぎても pending が残る場合 (無効レーンで除外されたノーツなど会計の揺れ) は強制的に Completed にする。`ForcedByDeadline` 印を付けログに出す。合計が合わない結果は Result / ランキング側の合計チェックで拒否される (成功偽装をしない)。`SettleDeadline` は譜面の最後のノーツの確定時刻 (タップ: 時刻 + BAD 窓 / HOLD: 終端 + GREAT 猶予) |
| Failed | `Fail(reason)` は再生中の VideoPlayer エラー (`OnVideoError`) と長時間ストール (メディア時計が `StallFailSeconds = 30s` 進まない) で呼ぶ。Failed は Completed にならず、Result へ自動遷移しない。表示と回復は既存経路: VideoPlayer エラーは GameHudRuntime が errorReceived を購読して GameErrorView を表示、ストールは Esc ポーズメニューから選曲へ戻る |
| 既存挙動の維持 | Result への遷移は従来どおり `Initiate.Fade("ResultScene", Color.black, 1.0f)` (完了時のみ)。開始前のメディア失敗経路 (`HandlePlaybackFailure` → `SelectReturnPending` → GameErrorView) は無変更。`MediaTimeline.HasSongEnded` の既存契約 (有効 duration + lead 1s) も維持 |

## 非目標 (Out of scope)

- Judge / JudgeQueue / NotesManager / PlaybackRecovery / UI 側の変更 (他担当と競合するため読むだけ)。
- ランキング送信側の合計チェック変更。
- 実機 (展示 PC) での完走テスト — Supervisor の統合時に実施。

## Acceptance criteria

1. 末尾ノーツが未判定のまま終端条件が成立しても、Result へ遷移せず、Miss 窓で確定した後に遷移する。
2. 全ノーツ確定 + 終端条件成立で Result へ遷移する (旧挙動より遅くならない)。
3. duration が 0 / 極端に短い / 極端に長い場合でも、状態機械が破綻せず、実メディア終端を正として完了できる。
4. Failed (再生中エラー / 長時間ストール) は Completed を報告せず、Result へ自動遷移しない。
5. EditMode テスト (`Assets/Tests/Editor/SongEndStateMachineTests.cs`) が上記を固定する。
6. `script/compile-check.sh --assembly flashbeat / --assembly tests` が通る。
