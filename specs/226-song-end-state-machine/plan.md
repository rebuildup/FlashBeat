---
description: "FlashBeat lean implementation plan — Issue 226"
---

# Plan: 曲終了の状態機械 (SongEndMachine) — Issue #226

**Issue**: #226 | **Branch**: `w/b226` | **Spec**: [`spec.md`](spec.md) | **Date**: 2026-11-08

## Architecture

```
MusicManager (MonoBehaviour, 所有: 今回改修)
  Update (playbackStarted 後)
    ├─ mediaTime = CurrentMediaTime()
    ├─ DetectMediaEnd()            … loop point / isPlaying 立ち下がり / wav 終端
    ├─ 時計:
    │   mediaEnded  → StartTime を固定し、step_time = Time.time - StartTime
    │   (judge の時計が終端時刻から継続し、末尾ノーツが Miss 窓で確定する)
    │   それ以外    → StartTime = JudgeStartTime(wall, media) (従来どおり)
    ├─ ストール監視 (mediaEnded 以外) … 30s 進まなければ Fail(MediaStalled)
    └─ SongEndMachine.Update(in tick)
            true  → Initiate.Fade("ResultScene")   (従来どおり 1s フェード)
            Failed → 自動遷移なし (GameErrorView / Esc ポーズ経由で回復)

SongEndMachine (純粋, 新規)      Playing → Finishing → Completed / Failed
MediaTimeline (追記のみ)          IsUsableDuration / HasSongEnded(ガード) / FallbackSongEnd
Judge / JudgeQueue / NotesManager  無変更 (確定数は GManager.nextNotes 経由で観測)
```

## Data flow

1. **終端条件** (`MusicManager.Update`):
   - `mediaEnded` = loop point イベント (`OnSongVideoLoopReached`) または
     `DetectMediaEnd()` (映像 `isPlaying` 立ち下がり / BundledWave の clip 終端 /
     分離音声の停止)。実メディアが正。
   - `durationEnded` = 実メディアあり → `MediaTimeline.HasSongEnded(mediaTime, duration)`
     (不正 duration では常に false) / メディアなし → `mediaTime >= FallbackSongEnd(duration)`。
   - `EndReached = mediaEnded || durationEnded`。
2. **確定会計**: pending = `noteNum − (GManager.nextNotes − 1)`。Judge は確定 1 件
   (判定 / Miss / HOLD 中断差し替え) ごとに `message()` で `nextNotes` を +1 する
   既存会計をそのまま観測に使う (Judge / JudgeQueue は編集しない)。
3. **期限**: `SettleDeadline()` が譜面の最後の確定時刻 (タップ: 時刻 + BAD 窓、
   HOLD: 終端 + GREAT 猶予) を返す。judge 時刻が期限 + 2s を過ぎても pending が
   残れば強制完了 (`ForcedByDeadline`)。
4. **破綻**: `OnVideoError` (再生中) と 30s ストールで `FailSongEnd()` →
   `SongEndMachine.Fail()`。`stage = Failed`。Result への自動遷移は起きない。

## リスクと決定

- **pending の会計が Judge の実装に依存** (nextNotes = 確定 1 件ごと +1)。無効
  レーンで除外されたノーツは永遠に確定しないため、期限 + 2s の強制完了で上界を
  保障する。正確な pending が欲しければ Judge/JudgeQueue 側の API 追加が必要
  (evidence の「必要 API」参照)。
- **ストール → Failed は片方向**。回復しても Completed にはならない (稀なケース;
  Esc で戻って再試行)。誤検出を避けるためローカル再生前提の 30s を採用。
- **メディアなし + 極端に長い不正 duration** では壁時計が曲げられない (成功偽装
  を避けるため上限を設けない)。Esc / ポーズメニューで回復できる。残存リスクとして
  evidence に記録。
- Unity Editor は起動しない (Library 競合)。検証は compile-check と純粋ロジックの
  .NET ハーネスで実施する。
