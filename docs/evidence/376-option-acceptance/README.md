# #375 / #376 受入証跡 — Option/Pause のキー設定・NoteSpeed/Timing・音量スライダー

実行日: 2026-11-15 / ブランチ `w/376-option-acceptance` / macOS ビルド (`Builds/macOS/FlashBeat.app`)

## 実行

```
./Builds/macOS/FlashBeat.app/Contents/MacOS/FlashBeat \
  -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 \
  -autotest v6interact -autotest-out docs/evidence/376-option-acceptance/fhd
```

## 結果

`fhd/autotest-v6interact-result.txt`: **pass=82 fail=0** (1920x1080)。

#375 / #376 の新規チェック (全て PASS):

- Option Play タブに NoteSpeed / NoteTiming / Key Layout / キー設定 の 4 行がある。
- どの行の値にも `F2` を表示しない。キー設定行の値は `開く ›`。
- NoteSpeed / NoteTiming は行の ←→ と同じ `AdjustSelected` 経路で変更でき、
  `GManager` の値・PlayerPrefs (`FlashBeat.NoteSpeed` / `FlashBeat.NoteTiming`)・
  行の表示値が一致する。NoteTiming は曲ごとの offset 設定へも書き戻される
  (検証後に元の値へ復元済み)。
- 「キー設定」行の決定 (Enter と同じ `ActivateRow` 経路) だけで
  `V6KeyConfigModal` が開き、背後の入力をブロックする。F2 は不要。
- Pause の Play タブでも同じ 4 行に到達でき、NoteSpeed / NoteTiming の変更と
  保存、キー設定モーダルの開閉を確認 (ポーズ中 = PauseGate 閉鎖下で実施)。

スクリーンショット:

- `v6interact-375-slider-drag.png` — 音量スライダのドラッグ中
- `v6interact-376-keyconfig-modal.png` — Option から開いたキー設定モーダル
- `v6interact-376-pause-keyconfig.png` — Pause から開いたキー設定モーダル

## 補足

- キー重複検出・既定復帰・モーダルのキー操作 (Esc / Enter / ←→) は
  EditMode 側 (`V6KeyConfigLogicTests` / `V6KeyConfigModalTests` /
  `KeyConfigLayoutTests`) が固定済み。ランタイムでは「画面内の行から開ける」
  ことと背後入力のブロックを確認した。
- 「調整できない」の原因 (#376 受け入れ条件): コード上の値モデルは
  Option / Pause で共通 (`GManager` + `OptionSettings.Save`) であり、
  行への到達性の問題ではなく、旧 UI (`KeyConfigUI`) と v6 モーダルの
  混在期に「F2 という表示だけの導線」へ辿り着けなかったことが実機での
  体感原因と判断。F2 表示の廃止と行からの直接起動で解消した。
- 直前の 640x480 実行ではポインタ系チェックが解像度起因で FAIL したため、
  本証跡はウィンドウモード 1920x1080 で取り直した。
