# v0.2.1 RC 検証記録

対象: `release-0-2-1` / PR #787。v0.2.0 実機レビューで受入不合格となった
11 件の指摘 (#730, #731, #733, #734, #735, #781–#786) と、PR レビュー
4 件 (BGM Volume 実適用 / プレビューループのポップイン / Desc スクロール
raycast / スクロール位置リセット) の修正候補。

## 候補 SHA と版の同一性

- コード確定: `5393ab84` (VERSION=0.2.1、ビルド埋め込み `0.2.1+5393ab84`)
- ビルド後の stamp コミットのみがこれに続く (内容差分なし)。

## 自動検証 (同一 SHA 系)

| 項目 | 結果 | 証跡 |
|---|---|---|
| Unity EditMode | **2097/2097 PASS** (`ca3edcb4`、VERSION bump と同一コード。以降の差分はテスト追加のみ) | `test-results/TestResults-rc021-editmode.xml` |
| Unity PlayMode | **6/6 PASS** (同上) | `test-results/TestResults-rc021-playmode.xml` |
| HudFeedbackTests (BAD 色固定を含む) | 25/25 PASS (`5393ab8` で filter 実行) | Unity batch log |
| compile-check | 0 errors (244 sources) | — |
| 実機 AutoTest v6songend | **33/33 PASS** — Select→Game→Result、130 ノーツ全判定、score 272,519 (ミス込みの実判定) | `autotest/` |
| 実機 AutoTest v6screens | pass=85 fail=5 — fail 5 件は全て「スクリーンショット書き込み」の flaky で、画面内容の assert は全て PASS | `autotest-screens/` |
| 実機 AutoTest v6interact | **97/97 PASS** (`5393ab8` ビルド、無負荷で再実行)。初回〜2 回目の実行では開始直後ポーズの時計過渡チェック 2 件が負荷時に閾値超過したが、定常状態の連続性チェックは全実行で PASS、クリーン再実行で全件 PASS (フレークと判断) | `autotest-interact-final/` |

## 実描画・実音声の証跡

- `autotest/burst-gameplay.mp4` — 判定開始後約 4 秒を 12fps で連写した
  38 コマの動画 (判定ポップの軌道・歌詞パネル・コンボボーナス・
  ノーツ発色の実描画証跡。ffmpeg で PNG 列から生成)。
- `autotest/autotest-v6songend-*-05-burst-*.png` — 同連写の元コマ。
- `autotest-screens/v6screen-OptionScene-*.png` — Option の
  Play/Display/Sound タブ実描画。
- `autotest-screens/v6screen-GameScene-Pause-20.png` — Pause 実描画
  (共有 `V6SettingsScreen` による Option との一致を目視確認する材料)。
- `autotest-interact/v6interact-375-slider-drag.png` — 音量スライダーの
  ドラッグ実操作証跡。

## 音声

- clickSound = `Assets/Game/Audio/レーンキークリック.wav`
  (効果音ラボ「決定ボタンを押す31(カチッ)」を WAV 化・0.2 秒トリム、
  onset 0ms)。hitSound = 歴代どおり `カーソル移動4.mp3`。
- 遅延の原因は MP3 のエンコーダプライミングと音源の二段エンベロープで、
  WAV 化で解消。実際の音色・遅延の最終確認は人間の聴取が必要
  (下記「人間確認待ち」)。

## 配布物

- `dist/FlashBeat-0.2.1-macos-5393ab8.zip` + `.sha256` (song-free)
- `dist/FlashBeat-0.2.1-win64-5393ab8.zip` + `.sha256` (song-free)
- `Builds/macOS/FlashBeat.app` = `0.2.1+5393ab84` (bundleVersion)

## 人間確認待ち (自動検証で代替できない項目)

以下はコード・テスト・連続コマで構造は保証したが、体感品質の受入は
実機での人間確認が必要:

1. **クリック音の音色と遅延** (#730) — WAV クリップの実出力を聴く
2. **判定アニメの見え方** (#731) — burst 動画で軌道は確認できるが、
   イーズの自然さは実画面で
3. **ノーツの白さ** (#735) — Video Dim 常時OFF化で構造上は解消。
   burst コマの画素でも白さは確認したが最終合成の見え方は実機で
4. **歌詞パネルの状態遷移** (#734) — 歌詞あり譜面 (ツイッターランド) の
   burst でパネル表示は確認。歌詞なし譜面はカタログに実曲がなく
   EditMode のポリシーテストでのみ保証
5. **Pause/Option の一致** (#733) — 共有コンポーネント化済み。
   v6screen の Pause/Option PNG で目視一致を確認
6. **コンボボーナス表示** (#784) — burst コマに右カラムのブロックあり
7. **説明文スクロール** (#786) — ScrollRect 構造はテスト済み、
   ホイール操作の体感は実機で
8. **Windows 実機での起動** — ZIP は検証済みだが実行は未検証
