# Spec: 122 — Game HUD + Pause (v5)

Issue: #122 (Refs #41) — branch `122`, base `release-0-1-3`.

## 目的

GameScene の UI を design/flashbeat-ui.pen **v5** に合わせてコードで再構築する。
対象 export: `03-game-default`, `03b-game-over-1m`, `03c-game-ready`,
`03d-game-loading`, `03e-game-youtube-error`, `04-pause-settings`,
`04b-pause-display`, `04c-pause-ranking`, `04d-pause-keys`。

## 背景 (実機で壊れている点)

- 右上「メニュー」ラベルが巨大化してクリップ (「ニュー」のみ見える)
- スコアゲージ/上部バーが窮屈
- 判定テキスト (Perfect/Miss 等) がレーン上 (3D 空間) に重なって見える

## 成果物 (すべて新規ファイル, `Assets/Game/Scripts/UI/V5/`)

1. `GameHudRuntime.cs` — 起動と配線。GameScene ロード時に v5 ビュー群を構築し、
   レガシー HUD (SpacetoStart / ScoreText / comboText / Score / Gauge /
   HudStatus / HudBackToSelectButton / MenuPanel / MenuButton / loadingmovie の見た目 /
   Judge の判定ポップアップ clone) を隠す。パイプライン状態は `HudPlayState`
   (#93 の純ロジック) で判定し Loading / Error へ配る。
2. `GameHudView.cs` — プレイ中 HUD: 右上にランク+SCORE+スコアレール
   (Prefab/ScoreRail 相当) + COMBO BONUS、左下に COMBO+NO MISS/FULL COMBO+
   次のボーナス、下中央に判定ポップアップ、右下にポーズボタン、上部に Top Shade
   (レガシー Canvas order 0 より下 = 歌詞が上に来る)。
3. `GameReadyView.cs` — Ready 状態 (03c): 右上に曲情報 (タイトル/アーティスト・
   BPM・Lv + YouTube サムネのジャケット)、中央に [Space] to Start、Esc の説明。
4. `GameLoadingView.cs` + `LoadingOverlayHook` — ロード表示 (03d)。
   進捗 API が無いので不定インジケータ。origin/113 (PR #117) の LoadingOverlay が
   trunk に入ったら Hook を差し替える (明示的フック)。
5. `GameErrorView.cs` — YouTube エラーダイアログ (03e)。
   VideoPlayer.errorReceived を観測して表示。Esc=選曲へ / R=リトライ。
6. `PauseMenuView.cs` — ポーズメニュー (04 系)。Esc で開閉、↑↓/←→ で
   6 行の設定 (NoteSpeed/NoteTiming/MainVolume/BGMVolume/SEVolume/Flash)、
   S/D/L/K でタブ切替、Q=Retire / R=Retry / Esc=Resume。
   Display タブは Key Guide / Lyrics を実動作に接続、Ranking タブは
   GManager.Hiscore から、Keys タブは Judge.LaneKeys (KeyBindings 未着のため) から生成。
7. `V5GameUi.cs` — 本 Issue 専用の uGUI ヘルパ (V5Ui は編集しない)。
8. `GameHudLogic.cs` + `Assets/Tests/Editor/V5GameHudTests.cs` — 純ロジック
   (ランク判定 / レール幅 / ボーナス表示) と EditMode テスト。

## 制約

- レーン/ノーツの 3D 描画 (NotesManager/Notes/Judge) は変更しない。
- 歌詞パネル (LyricsTimeline) はそのまま動かし、Top Shade より手前に置く。
- MusicManager / Judge / Fader は所有外 (観測のみ)。
- 大きな .unity YAML は編集しない。

## 非目標

- Pause Ranking タブのオンライン排行榜 (データ源が存在しない。自分の記録のみ表示)
- 判定ポップアップの ms オフセット表示 (Judge が timeLag を公開していない)
