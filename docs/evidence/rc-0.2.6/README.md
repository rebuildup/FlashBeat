# RC 0.2.6 証跡 (2026-11-22)

`release-0-2-1` のリリース候補。v0.2.5 公開後の実機不具合 8 領域 (#845–#851) の修正と、
#844 破損フレーム計測 (#861) を含む。

- **freeze SHA**: `66a5bf149138541b49b001f611bf36ce4cec7abb`
  (merge PR #861 = `VideoFrameAudit` 計測プローブ。ビルド表示 `0.2.6+66a5bf14`)
- **旧候補 SHA**: `9b369761f54a0a4ca20bb2a9321d81340dcd340d`
  (freeze 後に検出したハーネス計測競合 #863 を PR #864 で修正・統合)
- **最終候補 SHA**: `1a504a37f7d653a4ec0b2d420cb5c6418559a324`
  (受入ギャップ対応: #844 検出器 v2 = PR #865、
   #862 ink 監査のクリップ境界対応 + #866 キーボードはみ出し修正 = PR #867。
   ビルド表示 `0.2.6+1a504a37`)
- 統合済み: #845→#852, #846→#858, #847→#853, #848→#857, #849→#856,
  #850→#859, #851→#854/#855, テスト追従 #855/#860, #844 計測 #861,
  #863 ハーネス修正 #864, #844 検出器 v2 #865, #862/#866 #867
- EditMode: 2217/2217 pass (66a5bf14)、**2223/2223 pass**
  (1a504a37 + PR #867 差分の working tree で実測。
  `TestResults-d2437808.xml` — 差分はハーネス/モーダル配置と
  そのテストのみで、製品ロジックへの影響はキーボード配置のみ)
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
| `v6screens` マトリクス (1a504a37) | **5/5 PASS fail=0** | `v6screens-1a504a37/` |
| `v6interact` (1a504a37) | **pass=96 fail=0** | `v6interact-1a504a37/` |
| `v6songend` (1a504a37) | **pass=33 fail=0** (perfect 130/130) | `songend-1a504a37/` |

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
- **1a504a37 で再分類**: `TryClipRectLocal` (祖先 RectMask2D + Canvas の
  クリップ境界) を導入し、旧 partial の大半は `overflow` (描画は完全、
  矩形のみ超過) へ。残る `partial` は Name Field Value のキャレット端
  1〜2 件のみ (マスク境界を 1.8px 越える計測値。視認上の影響は未確認)。
  全件の分類表は Issue #862 コメント参照

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

### 検出器 v2 (1a504a37, PR #865)

- 双方向検出 (エネルギー増大 + 低下)・suspect ランのイベント分類・
  孤立イベント分離・MinRatio 記録を追加。`IsSuspectScore` の
  EditMode テスト 5 件を追加
- `songend-1a504a37` 実測: samples=177 suspects=30 events=4
  isolatedEvents=2 maxRatio=7.27@15.77s (シーン転換) /
  **minRatio=0.07@29.77s** (楽曲終端のフェードアウト〜結果遷移と
  推定されるエネルギー低下。診断値であり FAIL ではない)
- 原理的限界 (4Hz サンプリングの単発取りこぼし・エネルギー中立破損は
  検出不可) は Issue #844 コメントに明記

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

### 客観計測 (Player 内 AudioLevelProbe、同一楽曲 A/B)

- SongVolume=1.0 (twitterland-9b369761): RMS 0.07–0.54、peak 最大 0.726
- SongVolume=0.25 (`audio-gain-025b/`、PlayerPrefs 実測で適用確認):
  RMS 0.03–0.17、peak 最大 0.216 → **約 0.3 倍の減衰**
  (期待 0.25 倍。RMS 窓の時間ジッタを考慮すれば線形応答と整合。
  二重減衰 0.0625 倍ではない。peak 0.22 << 1.0 でクリップなし)
- 計測は Player 内プローブ (最終ミックス後段) の数値。**実デバイス
  出力・聴感の主観評価は人間受入に残す**
- 補足: `audio-gain-025/` は PlayerPrefs を誤ドメイン
  (`unity.KimuraYusuke.FlashBeat` = Editor 側) に書いた無効ラン。
  実効ドメインは `com.KimuraYusuke.FlashBeat` (bundle id)

## UI 視覚レビュー (マルチモーダルモデル独立監査)

- `docs/evidence/rc-0.2.6/` 配下のユニーク画像 53 枚を
  OpenRouter 無料枠の視覚モデル (gemma-4-31b / nemotron-3.5-super-vl /
  ling-3.2-flash) で全件レビュー。Gemini 無料枠は日次クォータ枯渇のため
  代替経路を使用
- **実害 1 件を検出・修正**: キー設定モーダルの JIS キーボードが
  ダイアログ枠から左右にはみ出す → **Issue #866 / PR #867**
  (下段行の実測幅 1038px > LaneRowW 784px。全行一様縮小で収容。
  `KeyboardKeys_StayInsideTheDialog` で契約固定。新ビルドの
  keyconfig-modal / pause-keyconfig スクリーンショットは NO_DEFECTS)
- その他の指摘は全て棄却: 楽曲情報のタイトル/アーティスト ellipsis は
  `V6SongBadge` の意図的仕様、モーダルによるスコアの覆いは正常な
  オーバーレイ、`__no_song_matches__` は autotest が検索欄へ入力した
  クエリ文字列そのもの、SSS グレード・COMB・タイマーの指摘は
  個別検証 (クリップクロップ再レビュー・座標契約) で誤検知と判定
- 未レビュー残り: なし (53/53 判定済み。一部 win64 選曲画面 variant は
  モデルの reasoning 漏出で判定文を取得できず、macOS 同一画面の
  NO_DEFECTS と Player AutoTest pass=105 fail=0 で代替)

## 未検証・残件

- **人間受入が必要な項目** (自動検証で代替不能):
  - 実デバイス出力での音量・音質の聴感 (Song/BGM/Effect/Main の
    体感バランスと「プレイ楽曲が十分聞こえる」の最終判断)
  - 結果画面の実 API ランキング表示の目視 (`rankingMode=Online` で
    実経路は通っているが、1 始まり表示の最終目視は人間側)
  - win64 選曲画面 variant 4 枚はモデル判定文を取得できず
    (macOS 同一画面は NO_DEFECTS、AutoTest も pass)
- #844: 破損非再現だが監査に盲点あり (上記)。検出器 v2 で低下型・
  孤立イベントは拾えるようになったが、単発フレーム・エネルギー中立
  破損は原理的に未対応。Issue open のまま
- #862: 分類完了 (全件誤検知 or #866 へ分離)。検出器は
  `partial` (実クリップ) / `overflow` (非クリップはみ出し) に
  再分類済み。残る partial はキャレット端 1〜2 件
- #863: 修正統合・macOS/Win64 とも v6interact pass=96 fail=0 でクローズ済み
- v6interact / v6 (Win64): 9b369761 で pass=96/32 fail=0 を確認済み
  (1a504a37 の差分はハーネス・モーダル配置のみで Win64 影響なし
  と判断。キーボード修正の Win64 目視は未実施)
- release PR #787 → main: **未マージ維持** (承認境界)
