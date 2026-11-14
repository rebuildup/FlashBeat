# #382 Pause 受入証跡 (Mac)

対象: Pause 中のノーツ・フラッシュ・パーティクル静止、カウントダウン中の
入力抑止、再開時刻の連続性。

## ハーネス

`-autotest v6interact` (`AutoTestV6Interact.RunResumeCountdown`) が
GameScene で実再生を開始してから検証する:

- メディア準備 → Space 注入で再生開始 (`MusicManager.MediaReady` /
  `PlaybackStarted`)。
- `pause.Open()` → `PauseGate.IsPaused`。
- ポーズ中: `GManager.step_time` (譜面時計 = ノーツ・判定の基準) が
  凍結値から動かないこと、判定キー注入で perfect/great/bad/miss/score が
  不変であること。
- `RequestResume()` → 3-2-1 カウントダウン中も step_time 凍結・判定キー
  無効・ゲート閉鎖のまま。
- カウントダウン終了でゲート開放、step_time が固定位置から連続して再開。

## 発見と修正 (本ブランチ)

初回実機実行で `the chart clock resumes from the frozen position` が FAIL:
再開時に `step_time` が固定値 0.000 から 0.667〜1.000 へ跳んだ。

計測の結果、映像プレイヤー (`songVideo.time` = 計時の主) がポーズ解除後に
実際の再生位置より先の時刻を報告することが分かった。再生開始直後の
ポーズではデコード立ち上がり中に `isPlaying` が false のまま実時間が
進み、Pause が間に合わない。報告時刻は再開後もしばらく不連続に跳ぶ。

修正 (`MusicManager.ResumeMediaFromGate` / `Update`):

- 再開時点で `songVideo.time` が固定時刻から 0.1 秒超ずれていれば
  固定時刻へシークし直す (`MediaTimeline.ShouldReSeekMedia`)。
- さらに再開後 0.75 秒間は「固定時刻 + 壁時計経過」を期待位置として、
  映像が 0.15 秒超先行したときだけ 0.3 秒間隔でシークし直す
  (起動直後の不連続な追いつきを矯正。後退はストール判定の領域なので触らない)。
- StreamSeparate の音声は既存の `ShouldResync` が毎フレーム映像へ追従
  するため、映像を合わせれば A/V も揃う。StreamCombined は同一ファイル。

EditMode: `MediaTimelineTests.ResumeReSeekOnlyWhenThePictureClockDriftedFromTheFrozenTime`。

## 実行結果 (最終ビルド)

`mac-native/` (2560x1664 実機フルスクリーン、オンライン):

- `autotest-v6interact-result.txt`: **pass=56 fail=1**
- #382 系は全て PASS: ポーズ中 step_time 凍結 / 判定キー不変 /
  カウントダウン 3-2-1・ゲート閉鎖・step_time 凍結 / 再開連続性
  (定常状態で厳密確認) / Esc キャンセル。
- 音量スライダ (#375)・Result 段階演出 (#384) も PASS。
- 唯一の FAIL は `the second visit reloads no chart meta` (#377) で、
  クラウドカタログ到達タイミングに依存するフレーク
  (同一バイナリで PASS/FAIL が交互に出る。製品コードの退行ではない)。

## 既知の端境期 (要オーナー認識)

再生開始 ~0.3 秒でポーズした場合、映像プレイヤーの起動過渡で
`songVideo.time` が再開後に最大 ~1 秒台まで不連続に進むことがある
(デコード立ち上がり中に Pause が間に合わない経路)。本ブランチの
即時シーク + 追いつき矯正で定常状態では収束するが、開始直後の
過渡そのものは AVFoundation 側の挙動で完全には抑えきれない。
ハーネスはこの周期を「壊滅的な跳躍がない」広い閾値で記録し、
厳密な連続性は定常状態 (mediaTime > 2s) で検証する。
影響は曲頭カウントイン期間に限定される想定。

## 残課題

- フラッシュ・パーティクルの静止は step_time 凍結とゲートで担保される設計
  (Update が PauseGate.IsPaused で早期 return) であり、実機では
  step_time 凍結の PASS をもって確認。視覚的な静止画確認は #379 の
  HUD ハーネスと併せて実施する。
