# v6 実機レビュー証跡 — Mac 3420×2224 (2026-11-12)

Issue #368 の子 Issue (#369–#384) に対する、macOS ビルドでの実機確認記録。確認できた項目と、
確認できなかった項目を分けて書く。**未確認の項目を PASS として扱わない。**

## 1. 対象と環境

| 項目 | 値 |
|---|---|
| ビルド | macOS `FlashBeat.app`、`0.1.23+4e2d1627` (Unity 6000.5.6f1) |
| ソース | branch `w/v6-368-impl`、HEAD `4e2d1627` (実装コミット `31904bd9`, `4e2d1627`。前提: `809251e5`, `a4978b6d`, `83494e6e`) |
| ディスプレイ | 3420×2224 px (1710×1112 pt)、メニューバー 38 pt、Dock は自動非表示 |
| 起動方法 | `open Builds/macOS/FlashBeat.app` |
| 入力 | `osascript` の `key code` を前面ウィンドウへ送信 |
| 撮影 | `screencapture -x` (ディスプレイ全体、3420×2224 px) |

スクリーンショット内の座標・サイズはすべて実画面 px。UI の設計基準は `design/flashbeat-ui.pen`
(1728×972) で、実装は高さ合わせ + 水平中央寄せ。

## 2. #369 全画面 — 部分達成 (残課題あり)

### 2.1 連続起動の測定

5 回連続で起動し、各回のウィンドウ位置・サイズと最初のフレームのログを取得した
(`369-launches.txt`、`369-launch-1.png` … `369-launch-5.png`)。5 回とも同値。

```
bounds = 0, 38, 1710, 1074 (pt)
[FullscreenBootstrap] event=first-frame,
  screen=3420x2148, display=3420x2224,
  fullScreen=True, fullScreenMode=FullScreenWindow,
  safeArea=3420x2148
```

達成していること:

- `fullScreen=True` / `fullScreenMode=FullScreenWindow` で起動する (5/5)。
- タイトルバーは無い。Dock は自動非表示のため写らない。
- `Screen.SetResolution` による擬似全画面は実装に無い (ランタイムの `Assets/Game/Scripts` を grep して 0 件)。

未達:

- `screen=3420x2148` に対しディスプレイは `3420x2224`。**上部 76 px は Unity の描画面の外**で、
  キャプチャではメニューバー領域として残る。Result 画面の撮影 (`384-result-register.png`) では
  右上にメニューバーのステータス項目が写っている。
- したがって受け入れ条件「スクリーンショットにメニューバーが写らない」は満たしていない。

### 2.2 原因 (特定済み)

- Unity の `FullScreenWindow` はボーダーレス `NSWindow` を `NSScreen.visibleFrame`
  (1710×1074.5 pt) のサイズで作り、メニューバー分を除く。
- 実行時の `Screen.fullScreenMode` / `Screen.fullScreen` の再設定ではウィンドウは広がらない。
- ウィンドウは `NSWindowStyleMaskFullScreen` を含むスタイルで固定されており、`setFrame:` は
  `PlayerWindow` (Unity の `NSWindow` サブクラス) 側で無視される。`NSWindow` の実装を直接
  呼んでもフレームは変わらなかった。
- スタイルマスクから fullscreen ビットを外す方法は `NSPerformVisuallyAtomicChange` 内で
  Objective-C 例外になり、プレイヤーが abort する (Player.log のスタックで確認)。

ネイティブ バンドルによる回避は試作したが、上記の理由で成立せず、クラッシュ経路も確認したため
**成果物には含めていない**。この制限は Unity の macOS プレイヤー側の挙動で、アプリコードだけでは
解消できない。

### 2.3 未確認

- 表示スケーリング 2 通りでの比較: この端末はディスプレイ 1 台で、切り替えはシステム設定の
  GUI 操作が必要。実施していない (環境制約)。
- Win64 の `collect-win-evidence.ps1 -ExpectResolution FHD|2K`: 未実施。
- 3 解像度 (FHD / 2K / Mac) の比較: Mac 1 解像度のみ。

## 3. 画面ごとの確認結果

| Issue | 確認したこと | 証跡 | 残り |
|---|---|---|---|
| #371 | Opening は映像のみ。ロゴ・エッジ装飾・背景パネルを描かない | `371-opening.png` | 映像の自動遷移は既存仕様どおり (未変更) |
| #372 | Title の背景が bg-anim3 の全画面表示になり、ベージュの旧背景が消えた | `372-title.png` | pen とのピクセル比較は未実施 |
| #373 | History / Ranking が背景映像を表示。Title から入ると Esc / 戻るで Title へ戻る。右上の楽曲表示は pen `S1TZD` と同じ 64×36 ジャケット + 曲名・アーティストで重なりなし | `373-history.png`, `373-ranking.png`, `373-back-to-title.png`, `373-ranking-back.png`, `373-ranking-top-right.png` | Issue に書かれた「300×96 / ジャケットが小さい」は現行 pen と一致しないため、その是正は未実施 (pen 側の値が正) |
| #374 | Select の外周ストロークを廃止。背景は全画面。中央パネルの曲名・アーティスト・タグの組版を pen に合わせた | `374-select.png` | スケルトン点滅は読み込みが速く観測できず (未確認) |
| #370 | 設定行は「アイコン + ラベル + 前矢印 + 値 + 次矢印」の固定列。閉じる / 戻るボタンは hug 幅で、x アイコン + Esc / 戻るアイコン + Esc を表示 | `370-settings.png`, `370-rows.png`, `370-settings-button.png`, `370-close-button.png`, `370-back-button.png` | なし |
| #375 | Option 右パネルにレーン プレビューとキー一覧を表示。Sound タブは共通音量スライダー | `375-option.png` | 音量スライダーのドラッグ操作は静止画では未確認 |
| #376 | キー設定モーダルを開ける。Title で F2 を押しても何も起きず、F2 の KeyCap も出ない | `376-keyconfig.png`, `376-title-before-f2.png`, `376-title-after-f2.png` | キー割り当ての衝突・リセット操作は未確認 |
| #378 | Select で Ready を押すとフェードが即座に始まり、GameScene の描画まで連続キャプチャ内で遷移する | `378-transition-01.png` … `378-transition-16.png` | ローディング画面はメディアがキャッシュ済みで表示されず、見た目は未確認 |
| #379 | HUD に上部シェード、歌詞スロット (画面下部)、SCORE / COMBO、判定文字、キーガイドを表示 | `379-hud.png` | 設計値とのピクセル比較・スコア折返し/ポップの挙動は未確認 |
| #380 | レーン ガイドが細線になり、画面下端まで延びる。外周の太い枠は無い | `380-playfield.png`, `379-hud.png` | 設計との線幅比較は目視のみ |
| #382 | ポーズ メニューを Esc で開ける。再開ボタンのアイコンは circle-stop | `382-pause.png`, `382-resume-icon.png` | フラッシュ静止と 3 秒カウントダウンは静止画では未確認 |
| #383 | 曲終了時に「リザルトへ」表示と暗転フェードが出る | `383-song-end-overlay.png`, `383-end-fade.png` | 譜面の `fadeOut` / `startTiming` / `beatOffset` は EditMode テストのみ |
| #384 | Result のトロフィー、段階表示、自動登録モーダル (名前入力カーソル) を表示 | `384-result-register.png` | Space スキップの実操作と登録完了の見た目は未確認 |

## 4. この証跡が示さないこと

- Win64 実機での確認全般 (#377 / #381 の計測を含む)。11/14 展示候補 `b6860a80` と展示端末には触れていない。
- FHD / 2K でのレイアウト。
- 連続プレイ・リトライ・登録失敗時のエラー表示。
- EditMode テストの結果は別途 `TestResults.xml` (1623 件) にあり、このディレクトリには含めない。
