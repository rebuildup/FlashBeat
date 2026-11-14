# #375 / #376 受入証跡 — Option/Pause のキー設定・NoteSpeed/Timing・音量スライダー

実行日: 2026-11-15 / ブランチ `w/376-option-acceptance` / macOS ビルド (`Builds/macOS/FlashBeat.app`)

## 実行

```
./Builds/macOS/FlashBeat.app/Contents/MacOS/FlashBeat \
  -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 \
  -autotest v6interact -autotest-out docs/evidence/376-option-acceptance/fhd
```

## 結果

`fhd/autotest-v6interact-result.txt`: **pass=84 fail=0** (1920x1080)。

#375 / #376 の新規チェック (全て PASS):

- Option Play タブの行が `NoteSpeed / NoteTiming / Key Layout / キー設定`
  の 4 行・この順序と完全一致する (部分一致ではなく全列を検証)。
- Pause Play タブも同じ 4 行・同じ順序。
- どの行の値にも `F2` を表示しない。キー設定行の値は `開く ›`。
- NoteSpeed / NoteTiming は行の ←→ と同じ `AdjustSelected` 経路で変更でき、
  `GManager` の値・PlayerPrefs (`FlashBeat.NoteSpeed` / `FlashBeat.NoteTiming`)・
  **実際に描画されている TMP テキスト** (`RenderedRowValueForAudit`) が一致する。
  NoteTiming は曲ごとの offset 設定へも書き戻される (検証後に元の値へ復元済み)。
- Pause の NoteSpeed は Option と同じ 0.1 刻み (`OptionSceneManager.AdjustNoteSpeed`)
  で、1 回の ←→ で +0.1 になることを値で検証する。
- 「キー設定」行の決定 (Enter と同じ `ActivateRow` 経路) だけで
  `V6KeyConfigModal` が開き、背後の入力をブロックする。F2 は不要。
- Pause のモーダル表示中にメニュー切替キー (T) を実際の入力経路
  (`KeyBindings.InjectKeyDown` → `KeyActions.IsDown` → `UpdatePause`) で注入し、
  背後のタブが動かないことを確認する (フラグだけでなく実入力で検証)。

スクリーンショット:

- `v6interact-375-slider-drag.png` — 音量スライダのドラッグ中
- `v6interact-376-keyconfig-modal.png` — Option から開いたキー設定モーダル
- `v6interact-376-pause-keyconfig.png` — Pause から開いたキー設定モーダル

## 補足

- キー重複検出・既定復帰・モーダルのキー操作 (Esc / Enter / ←→) は
  EditMode 側 (`V6KeyConfigLogicTests` / `V6KeyConfigModalTests` /
  `KeyConfigLayoutTests`) が固定済み。ランタイムでは「画面内の行から開ける」
  ことと背後入力のブロックを確認した。
- 「調整できない」の原因 (#376 受け入れ条件): レビューで 2 件の実差分を
  確認し修正した。(a) Pause の「キー設定」行はマウスクリック専用で、
  キーボード (決定キー) では開けなかった → `KeyAction.Confirm` (Space)
  で選択行の Click を実行する `ActivateSelected` を追加。
  (b) Pause の NoteTiming は `GManager` / PlayerPrefs だけ更新し、
  Option が行う曲ごとの offset 書き戻し (`WriteNoteTimingToSongOffsets`)
  をしていなかった → 同じ呼び出しを追加し、画面間で保存先を一致させた。
  (c) Pause の NoteSpeed は `PauseMenuView.NextNoteSpeed` による整数 ±1 刻みで、
  Option の 0.1 刻み (`OptionSceneManager.AdjustNoteSpeed`) と異なっていた
  → Pause 側も同じ `AdjustNoteSpeed` 経路に統一した。
  併せて「値欄に F2 だけが見える旧導線」も廃止済み (決定 §4.1)。
- 直前の 640x480 実行ではポインタ系チェックが解像度起因で FAIL したため、
  本証跡はウィンドウモード 1920x1080 で取り直した。
- #377 のキャッシュ計測は、クラウドカタログの定期再取得 (約 8 秒周期) が
  計測の合間に remote 曲を追加すると `chartMeta loads` が増えて FAIL する
  競合があった (実測 +28)。`V6ContentCaches.SuspendCatalogRefresh` で
  計測中は `SelectSceneManager` / `V6SelectDataSource` の両経路の
  カタログ再取得を止めるようにし、決定的にした。
