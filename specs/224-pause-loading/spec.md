# Spec: 224 — ポーズ中に時間・音声が止まらない (ポーズゲート)

Issue: #224 ([P1/v6] Game Pause・楽曲ローディングをPencilデザインに置換 — ポーズ部分の未達) — branch `w/wpause`。

## 目的

ポーズ中に **メディア時計・判定・譜面進行・音声 / 映像** を止め、再開時に
ノーツ位置が飛ばないことを保証する。

## 背景 (監査結果 / integration HEAD 87d723f 時点)

- `V6PauseView.Open()` / `Close()` は表示の切替のみで、ゲーム側の時計を止めていない。
- リポジトリ全体に `timeScale` / `isPaused` / `AudioListener.pause` の記載が 0 件。
- `MusicManager.Update` は毎フレーム `GManager.StartTime` / `GManager.step_time` を更新 (ポーズ考慮なし)。
- `Judge.Update` は `Time.time - GManager.StartTime` を判定時刻として計算 (ポーズ判定なし)。
- `Notes.Update` は `GManager.step_time` 基準で常時進行。
- 結果として、ポーズ中にノーツが流れ続け・判定が成立し・ポーズ時間ぶんの itime が消費される。

## 要件

1. **共有のポーズゲート**: `FlashBeat.Gameplay.PauseGate` (`IsPaused` / `FrozenMediaTime`) を
   新設する。v6 (`V6PauseView`) と v5 (`PauseMenuView`) の両方のポーズ画面が
   Open / Close で `Enter()` / `Exit()` を呼ぶ (同じ挙動に揃える)。
   開閉状態の正本は各ビューの `IsOpen` のまま (ゲートは 1 ビットだけ持つ)。
2. **メディア時計の停止**: `MusicManager.Update` はポーズ中は丸ごとスキップし、
   `GManager.StartTime` / `GManager.step_time` を更新しない (`step_time` は固定値のまま)。
   実メンバー (`songVideo` / `streamAudioPlayer` / `audioS`) を `Pause()` し、
   再開時に `Play()` / `UnPause()` する。メディア終端後のポーズでは再再生しない。
3. **判定の停止**: `Judge.Update` はポーズ中は判定・スコア加算を行わない。
   判定時刻の基準は `Time.time` の直接読みから `GManager.step_time` (メディア時刻)
   へ寄せる — 通常再生中は両者は同一 (`MusicManager` が毎フレーム同期する) なので
   既存の判定タイミングの仕様は不変。
4. **譜面進行の停止**: `Notes.Update` に明示的なガードを入れる
   (`step_time` 固定により自然に止まるが、明示的に止める)。
5. **再開の整合**: `PauseGate.Exit()` が `GManager.StartTime` を
   `wallClock - frozenMediaTime` へ補正する。ポーズ前後でノーツ位置が飛ばない。
6. **キー凡例の真実化** (`V6PauseView` フッタ): 凡例は実際に動くキーと一致させる。
   - ←→ は `V6GameRuntime.UpdatePause` が `MoveSelection` (行選択) に配線済み → 凡例「選択」
   - タブ切替の実キーは `KeyActions.MenuSwitch` (既定 **T**) → 凡例「Tab」を「T」へ
   - 値の変更は行のステッパー / クリック (マウス) のみ。呼び出し元ゼロの
     `AdjustSelected(int)` はマウス操作の公開 API として維持し、←→ には配線しない
     (配線先の未使用キーがなく、`V6GameRuntime` は他担当所有のため)。
7. **純ロジックの分離**: 時間計算・判定停止の可否は `PauseGateLogic`
   (Unity の静的状態に触らない static クラス) に分離し、EditMode テストから直接検証する。
8. **シーン越えのリセット**: `MusicManager.Start` で `PauseGate.HardReset()` を呼ぶ
   (ポーズ中のリトライ / リタイアで次のプレイが止まったままにならない)。

## 局限

- ポーズ中も `Time.time` (壁時計) は進む。判定の基準は `GManager.step_time` 相当
  (ポーズ中固定) を使うことで吸収する。
- 実機プレイ検証は不可 (Unity Editor を起動しない)。代替検証は EditMode テスト。
- キーボードでの値変更は未配線のまま (マウスのみ)。凡例は実態に合わせて削除した。

## Out of scope

- Issue #224 本体の v6 Pause / Loading UI のデザイン実装 (別担当)。
- `V6GameRuntime.cs` / `NotesManager.cs` / `KeyActions.cs` の変更 (他担当所有)。
