# v0.2.4 RC 検証記録 (#823 クラウド譜面一本化後)

対象: `release-0-2-1` / 候補 SHA `d3533b67` (merge PR #839)。
#823 の最終スコープ — 実曲譜面 JSON 28 本 + `E2EProbeSong.json` の HEAD 撤去、
譜面供給のクラウドカタログ一本化、合成 fixture へのテスト移行 — を含む
最初のリリース候補。

## 候補 SHA と版の同一性

- コード確定: `d3533b67` (VERSION=0.2.4)
- macOS ビルド埋め込み: `0.2.4+d3533b67` (`CFBundleShortVersionString` 実測)
- Win64 ZIP: `FlashBeat-0.2.4-win64-d3533b67.zip` (ビルド元 commit `d3533b67`)

## 自動検証 (同一 SHA 系)

| 項目 | 結果 | 証跡 |
|---|---|---|
| Unity EditMode | **2166/2166 PASS** (merge 前ブランチ `0550ad34` で全件実行。#839 の差分は autotest 読み取りタイミングのみでテスト対象コード不変) | 前セッション TestResults |
| compile-check | 0 errors (248 sources) | — |
| repo-integrity | PASS (実曲 JSON/WAV/MP4/jacket 再混入ゲート含む) | CI run |
| 実機 AutoTest v6songend | **32/32 PASS** — Select→Game→Result、クラウド譜面 130/130 判定、score=1,165,000、chartNotes=runtimeNotes=judged=130 | `autotest/` |
| 実機 AutoTest v6screens | **90/90 PASS** — 21 画面ショット、clip summary `partial=0 outside=0` (Option/Pause の Desc text partial=1 は既知の報告のみ検出で fail 対象外) | `autotest-screens/` |

## クラウド譜面一本化の実機確認

- `autotest-v6songend` はローカル譜面なしで起動し、Select の実カタログから
  `ツイッターランド` を選択 → クラウドから譜面取得 → `PlaySessionSnapshot`
  固定 → 130 ノーツ生成 → YouTube ストリーム (`audioMode=StreamCombined`,
  `video[playing=True frame=4158]`) で完走 → ResultScene。
- `chartNotes=0` 問題: autotest が選択直後 (譜面未到着) にノーツ数を読んで
  いたため。PR #839 でセッション固定後に再読するよう修正し fail=0 を確認。
  ゲーム本体の挙動は不変 (計測タイミングのみ)。

## 実画像レビュー (vision model: opencode-zen / mimo-v2.6-flash-free)

対象 SHA `d3533b67`、3420×2148 の実キャプチャをピクセル確認。
結果は全項目 PASS。詳細はセッション記録; 要点:

1. **歌詞パネル (#789)**: PASS — 演奏中フレームに黒い大パネル・歌詞テキストの
   残像なし。開始前は "Space to start" の細枠のみで不自然な黒塗りなし。
   ※この曲の歌詞区間が撮影窓に十分重ならなかった可能性は残る (下記残件)。
2. **判定ポップ (#793)**: PASS — PERFECT が判定線直上・適正サイズ、
   積み上がり・クリップなし。
3. **コンボボーナス (#784)**: PASS — 桁別アニメのグリフ欠け・ゴースト・
   クリップなし。SCORE/ BONUS の数値は実加点と整合。
4. **Loading→Playing (#824)**: PASS — Loading は楽曲アート全画面で自然、
   遷移後のゲーム背景も動画フレームとして描画。
5. **Select**: PASS — 9 行全て実ジャケット (リモート取得)・`28/28`。
6. **Result**: PASS (narrow) — 0 点スコアの scripted tour のため、
   実スコアのグレード表示は v6songend の `04-result.png` 側で確認要。

### レビュー観察 (要追跡・非ブロッキング判定)

- 一部 burst フレームでゲーム背景のジャケットがブロックモザイク状
  (`burst-43` 等)。YouTube 動画フレームのデコード/シーク由来の可能性が高く、
  UI 欠陥とは断定できない。実機の通常再生で再確認する価値あり。

## 配布物

- `dist/macos/FlashBeat-0.2.4-macos-d3533b67.zip` + `.sha256`
  SHA-256: `d9e25bcdd21a99648dfe6d01d425eff85f9c24523bcdaecdb325b6db963de141`
  mediaPolicy=song-free、macOS 実機で autotest 完走を確認済み。
- `dist/win64/FlashBeat-0.2.4-win64-d3533b67.zip` + `.sha256`
  SHA-256: `49a571cacd92e55a132647328bebe642b596e7f3b79ec6409582d0a1d2cbddfd`
  mediaPolicy=song-free。**Win64 実機起動は未検証** (Mac からのクロスビルド。
  実行検証は Windows 環境が必要)。

## 未実施・残件

- 歌詞パネルの「歌詞区間中の実表示」は本撮影では直接確認できていない
  (パネル自体の不自然さは解消を確認)。歌詞あり区間の連続フレーム確認は
  次回実機レビューで実施。
- Win64 実機での起動・プレイ確認。
- #794: 本番 D1 の実曲データ、Git 履歴/LFS、過去 Release の権利監査は
  Issue #794 comment-7061556271 に残存リスクを記録済み。不可逆操作は未実施。
