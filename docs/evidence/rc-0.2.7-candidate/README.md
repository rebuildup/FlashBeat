# RC evidence — v0.2.7 candidate (Program #869)

Build under test: `Builds/macOS/FlashBeat.app` built from release-0-2-1
tree at `f544091f` (content identical to merge `ce4b2cfe`; VERSION/stamp
bumps land after this evidence). Unity 6000.5.6f1, macOS 実機・実ディスプレイ全画面。

## v6songend — `songend-f544091f/` (ツイッターランド, 実 API)

- `RESULT pass=34 fail=0`: judged=130 == chartNotes == runtimeNotes == settled、
  score=1,100,015、songEndState=Completed、forcedByDeadline=False
- 歌詞 (#871 受入): `lyrics audit: state=HasLyrics lines=24 hasText=True`、
  `PASS lyric text is rendered during lyric windows`、
  `expectedFrames=775 emptySeconds=0.00`。catalog version drift 下でも
  held-chart フォールバックで本文が出ている。18 枚の lyrics burst PNG と
  `04-result.png` を保存
- グレード/ゲージ (#870 受入): Result 画面は grade **S** + score 1,100,015 +
  判定 124P/2G/1B/3M (=130)。vision review (nemotron-3.5-super-vl) で
  grade と score/判定の整合を確認。ゲージ脇の小さな "C" グリフは
  rc-0.2.6 の同曲結果画面とピクセル一致する既存要素 (回帰ではない)
- 歌詞パネル描画: `77-06-lyrics-13.png` を vision review — 本文描画・
  可読性・ジャッジ/スコア/ゲージとの非重なりすべて PASS

## v6screens 解像度マトリクス — `v6screens-f544091f/`

- 5/5 PASS (1024x768 / 1280x720 / 1920x1080 / 2560x1440 / 2560x1664)、
  全 SCREEN `fail=0`、`[clip] outside=0`、`[overlap] overlaps=0`
- ResultScene (最終状態 + 登録モーダル再現) と GameScene (HUD/Pause/Loading)
  を各解像度で監査

## EditMode

- 2240/2240 PASS (`/tmp/editmode-802.xml`, release tree, 6000.5.6f1 batch)
