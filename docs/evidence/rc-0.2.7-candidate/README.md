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

## v6songend — `songend-fa1af78f/` (stamp 後の最終候補ビルド 0.2.7+1f498134)

- `RESULT pass=33 fail=0` (loadingObserved=False — メディアキャッシュ済みのため
  loading check が対象外。判定・歌詞・動画監査は全て実施)
- 歌詞: `state=HasLyrics lines=24 hasText=True`、`PASS lyric text is rendered`
- judged=130 == chartNotes、score=1,088,846、Completed

## Result 境界スコア描画証跡 — `result-boundary-8610f1b5/` (#870)

`AutoTestV6Screens` に `-autotest-result-score` / `-autotest-result-chart-notes` /
`-autotest-result-{perfect,great,bad,miss}` を追加し、ResultScene ロード前に
GManager へ注入 (run* 値は初回 Refresh で固定されるため)。139 ノーツ
(v6 max=1,174,000) で 5 点を実描画:

| score | 期待 | 表示 grade | gauge | 判定 |
|---|---|---|---|---|
| 645,699 | C | C | ~55% | vision review MATCH |
| 645,700 | B | B | ~55% | MATCH |
| 1,056,599 | A | A | ~90% | MATCH |
| 1,056,600 | S | S | ~90% | MATCH |
| 1,174,000 | SS | SS | full | MATCH |

全件 `fail=0`。vision review (nemotron-3.5-super-vl) で grade 文字・SCORE・
ゲージ充填の一致を確認。不一致なし。

## v6screens 解像度マトリクス — `v6screens-f544091f/`

- 5/5 PASS (1024x768 / 1280x720 / 1920x1080 / 2560x1440 / 2560x1664)、
  全 SCREEN `fail=0`、`[clip] outside=0`、`[overlap] overlaps=0`
- ResultScene (最終状態 + 登録モーダル再現) と GameScene (HUD/Pause/Loading)
  を各解像度で監査

## EditMode

- 2240/2240 PASS (`/tmp/editmode-802.xml`, release tree, 6000.5.6f1 batch)

## 最終成果物 (commit `f8e8b3a6`, stamp `0.2.7+f8e8b3a6`)

- macOS: `dist/macos/FlashBeat-0.2.7-macos-f8e8b3a6.zip`
  SHA-256 `ef298f50849edfb2ffc1de55ea5b644433f8fbcce44fc9a5ddcf1a5d30940251`
- Win64: `dist/win64/FlashBeat-0.2.7-win64-f8e8b3a6.zip`
  SHA-256 `4b5331038e9c32446be2ed70ac050040d73cfc0669fdd954b14817be2ebea1d4`
  (entries=223 / managedDlls=131 / mediaPolicy=song-free)

## 未実施 (PASS として扱わない)

- Win64 実機での起動・プレイ確認 (Mac 環境では実行不可。Owner 作業)
- CI `browser-e2e` は本 PR では skip (release-0-2-1 向け check は pass)
- `release-0-2-1 → main` 統合・GitHub Release 公開は未実施 (明示承認待ち)
