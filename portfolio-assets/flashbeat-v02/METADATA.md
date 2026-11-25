# Portfolio assets — flashbeat-v02

## main.png（主画像）

- 種別: main（主画像）
- パス: `portfolio-assets/flashbeat-v02/main.png`
- 寸法: 2560×1664 px（≈15:9.7、製品の実画面解像度そのまま。トリミング・加工なし）
- 内容: FlashBeat v0.2.8 の実機プレイ画面。楽曲「ツイッターランド」
  （YouTube videoId `e_qQEU_uGjw`）の再生中、mediaTime≈24 秒時点のフレーム。
  8 レーンに落ちるノーツ、判定線、スコア / コンボ HUD、歌詞パネルが同時に見える。
- alt: `FlashBeat v0.2.8 の実機ゲームプレイ画面。YouTube 楽曲の再生中に 8 レーンへ落ちるノーツ・判定線・スコアとコンボの HUD・歌詞表示が見えている macOS 全画面キャプチャ。`
- caption: `FlashBeat v0.2.8 — YouTube 動画を再生しながら 8 レーンのノーツをキーボードで判定する自作リズムゲーム。公開版 macOS ビルドの実プレイ画面（2560×1664）。`
- 撮影対象版/SHA: GitHub Release `v0.2.8-macos-35569543`
  （tag commit = `3556954315a35b9ae2f8d22c8b4358af40faa19c`、VERSION=0.2.8、
  `version.json`: buildSha `3556954315a35b9ae2f8d22c8b4358af40faa19c`、
  mediaPolicy `song-media-excluded`）。
  アセット `FlashBeat-0.2.8-macos-35569543.zip`（332,032,833 bytes、
  sha256 `3183e47249cdb8a71f3877d278fd91c252f0052abea597fdd5ff1864014cd62a`、
  GitHub API の digest と一致確認済み）。
- 再現手順:
  1. 上記 Release アセットをダウンロードして展開（`FlashBeat.app`）。
  2. 収録の実機ハーネス（`-autotest v6songend`、docs/evidence/233-song-end-harness.md
     参照）で起動:
     `FlashBeat.app/Contents/MacOS/FlashBeat -screen-width 2560 -screen-height 1664 -screen-fullscreen 1 -autotest v6songend -autotest-out <dir> -logFile <dir>/Player.log`
  3. ハーネスが SelectScene → 「ツイッターランド」を選択 → メディア準備
     （yt-dlp 経由の YouTube ストリーミング、要ネットワーク）→ 自動キー注入で
     1 曲を完走 → ResultScene まで遷移し、途中経過を連番 PNG で保存する。
  4. 本画像はその連番の `autotest-v6songend-50-06-lyrics-08.png`
     （`06-lyrics-NN` は mediaTime 10 秒以降 2 秒毎の定期キャプチャ。
     lyrics-08 ≈ mediaTime 24 秒）。実行ログ `Player.log` と
     `autotest-v6songend-result.txt`（pass=38 fail=0、judged=130/130、
     score=752753、maxCombo=36、songEndState=Completed、
     forcedByDeadline=False）を同時取得済み。
- 真正性: スクリーンショットは公開版アプリの `ScreenCapture.CaptureScreenshot`
  による無加工キャプチャ。合成・レタッチなし。画像内のスコア/コンボは
  実プレイの HUD 実値。
- フレーム選定の根拠: 全 81 枚の証跡フレームをレーン帯の画素解析
  （ノーツ色 #EDEDED の無彩色ハイライト画素を 8 レーン矩形で集計）で比較し、
  上位フレームを目視相当の構造確認（輝度 ASCII 化）で選定。
  `05-burst-*` は判定開始直後約 4 秒の連写、`06-lyrics-*` は曲中盤以降の
  定期キャプチャ。選定フレームは複数レーン・複数深度にノーツが分布し、
  判定線と HUD が判読可能なバランスの良い構図。
- 権利・公開境界: **要監督者判断**。プレイフィールド背景には、製品仕様どおり
  YouTube 動画「ツイッターランド」（videoId `e_qQEU_uGjw`、公開 MV）の
  再生フレームが約 50% 減光（Video Dim #00000080）された状態で表示されている。
  本ゲームは YouTube 動画再生が中核機能であり、映像を完全に消す公開モードは
  存在しないため、実プレイ画面には必ず第三者映像のフレームが含まれる。
  カタログ内に自作・許諾確認済みの楽曲は無い（全曲が YouTube 動画）。
  個人情報・秘密情報・非公開情報は含まない（選曲は公開カタログ、
  スコアはローカル実行値）。リポジトリは public。
- 承認状態: 監督者の視覚レビュー待ち（本 Worker 環境では画像の直接目視が
  できないため、選定は定量解析＋構造確認に基づく。最終的な構図判定は
  監督側レビューに委ねる）。

## supplement-select.png（補足）

- 種別: supplement（補足）
- パス: `portfolio-assets/flashbeat-v02/supplement-select.png`
- 寸法: 2560×1664 px（main と同一画面・同一実行セッション）
- 内容: v0.2.8 の選曲画面（SelectScene）。クラウドカタログ適用後の
  楽曲一覧が表示され、「ツイッターランド」が選択状態。
  （Player.log: `[GManager] cloud catalog applied: local=29 merged=31 remote=28`）
- alt: `FlashBeat v0.2.8 の選曲画面。クラウドカタログとローカル楽曲の一覧が表示され、「ツイッターランド」が選択されている macOS 全画面キャプチャ。`
- caption: `FlashBeat v0.2.8 の選曲画面 — クラウドカタログ経由で配信される楽曲一覧。`
- 出典: main.png と同一の `v6songend` ハーネス実行の
  `autotest-v6songend-01-01-select.png`（無加工）。
- 権利・公開境界: main.png と同じ実行・同じ公開版。画像内は公開カタログの
  曲名・アーティスト名のみで個人情報・秘密情報なし。選曲画面の背景は
  静止画/UI であり再生中の MV フレームは含まない。
