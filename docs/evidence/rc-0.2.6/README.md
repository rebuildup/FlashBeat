# RC 0.2.6 証跡 (2026-11-22)

`release-0-2-1` のリリース候補。v0.2.5 公開後の実機不具合 8 領域 (#845–#851) の修正と、
#844 破損フレーム計測 (#861) を含む。

- **freeze SHA**: `66a5bf149138541b49b001f611bf36ce4cec7abb`
  (merge PR #861 = `VideoFrameAudit` 計測プローブ。ビルド表示 `0.2.6+66a5bf14`)
- **最終候補 SHA**: `9b369761f54a0a4ca20bb2a9321d81340dcd340d`
  (freeze 後に検出したハーネス計測競合 #863 を PR #864 で修正・統合。
  プロダクトコードへの実変更は audit アクセサ追加のみ)
- 統合済み: #845→#852, #846→#858, #847→#853, #848→#857, #849→#856,
  #850→#859, #851→#854/#855, テスト追従 #855/#860, #844 計測 #861,
  #863 ハーネス修正 #864
- EditMode: 2217/2217 pass (66a5bf14 時点。9b369761 の差分は
  compile-check 全 4 assembly 0 errors + Player 実測で検証)
- `compile-check.sh`: 全 4 assembly 0 errors

## macOS Player AutoTest (FlashBeat.app, 2560x1664)

| ハーネス | 結果 | 証跡 |
|---|---|---|
| `v6songend` ツイッターランド ×2 | pass=34/33 fail=0 | `songend-twitterland-audit/`, `songend-twitterland-audit2/` |
| `v6songend` STAY (editor 追加曲) | pass=35 fail=0 | `songend-stay/` |
| `v6songend` TEST (editor 追加曲) | pass=35 fail=0 | `songend-test/` |
| `v6songend` STAY (audit 付き再実行) | pass=33 fail=0 | `songend-stay-audit/` |
| `v6songend` ツイッターランド (9b369761) | pass=33 fail=0 | `songend-twitterland-9b369761/` |
| `v6interact` (66a5bf14) | pass=95 **fail=1** | `v6interact/` (#863 を検出) |
| `v6interact` (9b369761) | **pass=96 fail=0** | `v6interact-9b369761/` (#863 修正確認) |
| `v6screens` 解像度マトリクス | 5/5 PASS fail=0 | `v6screens-matrix/` |

### songend の内訳 (代表: ツイッターランド)

- judged=130 == chartNotes == runtimeNotes == settled、score=1,165,000、
  songEndState=Completed、forcedByDeadline=False
- 歌詞: `lyrics audit: state=HasLyrics lines=24 hasText=True`、
  `PASS lyric text is rendered during lyric windows`、
  `expectedFrames=808 emptySeconds=0.00` — 枠だけでなく本文が出ている
- STAY/TEST は `state=NoLyrics` で lyric audit を正しく skip (暗黙 pass ではない)
- editor 追加曲の映像経路: `path=remote player='YoutubePlayer_remote'`
  renderMode=MaterialOverride で videoId 解決 (#849)。STAY=`kTJczUoc26U`、
  TEST=`Cdbxt7L8zEM`。scenePlayers=29 に対し index 範囲外でも remote 複製で再生
- loadingObserved=True (STAY 初回 14.90s / TEST 9.62s)。2 回目以降は
  メディアキャッシュ済みで loading が瞬間 (0.00–0.47s) — 計測上の観測であり
  ローディング画面自体の不在ではない

### v6screens UI 監査 (新設 ink/overlap 計測を実動作で確認)

- 全 5 解像度 (1024x768 / 1280x720 / 1920x1080 / 2560x1440 / 2560x1664) で
  全 SCREEN `fail=0`、`[clip] outside=0`、`[overlap] overlaps=0`、
  duplicate Canvas なし
- `[ink]` は `outside=0`。`partial` (インクが RectTransform を一部超過) を
  全解像度で一貫して検出 → **Issue #862** として棚卸し (Result ボーナス行・
  登録モーダル Head・Pause キーキャップ等。大半は行高/グリフ由来の軽微な
  はみ出しで overlap=0 のため実害は未確定。baseline 判定は #862 で実施)

## #844 破損フレーム計測 (VideoFrameAudit)

| 実行 | samples | suspects | maxRatio | 判定 |
|---|---|---|---|---|
| twitterland macOS ×2 | 177 | 12 | 6.89/7.98 | mediaTime 15.2–18.0s の持続区間 |
| twitterland Win64 | 170 | 12 | 7.69 @15.93s | macOS と同一パターン |
| STAY macOS | 41 | 0 | - | - |

- suspect 区間はソース mp4 (ffmpeg デコード) でも勾配エネルギーが
  t≈15.25s に持続ジャンプする**実シーン転換**と一致 → 誤検出と判定
- v0.2.5 のモザイク破損は今回の RC 実行では**再現せず**
- 監査の盲点 (エネルギー低下型破損・4Hz サンプリングの取りこぼし) は
  Issue #844 に記録。破損非再現 ≠ 完全解消として扱い、再発時の定量
  手順を残す

## Win64 (device-windows 実機)

### 66a5bf14 (freeze 時点)

- 成果物: `FlashBeat-0.2.6-win64-66a5bf14.zip`
  (SHA-256 `1abd2f81…814b`、展開 `Desktop\FlashBeat-0.2.6-win64-66a5bf14\`)
- 起動: Interactive タスク `FBVerify026` → `fb-verify-run-026.ps1`
  (SSH session 0 直接起動不可の既知制約を回避)
- 結果 (`win64-66a5bf14/`):
  - `v6songend` pass=34 fail=0 (完走・videoAudit suspects=12 は macOS と同じシーン転換区間)
  - `v6screens` pass=105 fail=0 @2560x1440 (ink/overlap 監査も Windows で動作)
  - `v6` pass=32 fail=0
  - `v6interact` pass=95 fail=1 → **#863 を検出** (jacket fetch キュー計測競合。
    macOS でも同じ FAIL を再現し、ハーネス側の問題と特定 → PR #864 で修正)

### 9b369761 (最終候補)

- 成果物: `FlashBeat-0.2.6-win64-9b369761.zip`
  (SHA-256 `427abb6f…270b`、展開 `Desktop\FlashBeat-0.2.6-win64-9b369761\`)
- 起動: Interactive タスク `FBVerify026b` → `fb-verify-run-026b.ps1`
- 結果 (`win64-9b369761/`): **全グリーン**
  - `v6songend` pass=34 fail=0 (suspects=12 @同一区間、maxRatio=7.04)
  - `v6screens` pass=105 fail=0 @2560x1440
  - `v6interact` **pass=96 fail=0** (#863 修正が Windows でも有効)
  - `v6` pass=32 fail=0

## 音声 (Issue #851)

- コード契約: `GManager.songVolume=1.0` (旧 BGMVolume=0.3 共有を解消)、
  Main/Song/BGM/Effect の 4ch 分離、AudioSource への二重適用なし
  (MusicManager の適用箇所は songVolume 単独乗算、コメントで明記)
- Sound タブ 4 行化に伴うテスト追従済み (PR #855)
- **未検証**: 実際の聴取による音量感・独立性の体感確認 (自動検証は
  設定モデル/永続化/適用経路の単体テストまで)

## 未検証・残件

- スクリーンショットの**目視**による最終確認は人間側で実施が必要
  (エージェントは画像を直接判読できないため、メタデータと監査数値で代替)
- ランキング表示順位: `RankingClient.BoardPosition` の単体テスト +
  Result 画面スクリーンショット取得済み。実 API 値での表示確認は
  結果画面の目視待ち
- #844: 破損非再現だが監査に盲点あり (上記)。Issue open のまま
- #862: ink partial の棚卸し (RC ブロッカーではないが追跡中)
- #863: 修正統合・macOS/Win64 とも v6interact pass=96 fail=0 でクローズ済み
- v6interact / v6 (Win64): 9b369761 で pass=96/32 fail=0 を確認済み
- release PR #787 → main: **未マージ維持** (承認境界)
