# Plan: 122 — Game HUD + Pause (v5)

設計ソース: design/flashbeat-ui.pen v5 + design/exports/screens/{03*,04*}.png
(実体は /Users/rebui/work/FlashBeat/design/exports/)。

## 構成

- `Assets/Game/Scripts/UI/V5/GameHudRuntime.cs` — GameScene ロード時に v5 UI を
  構築する単一の入口 ([RuntimeInitializeOnLoadMethod(AfterSceneLoad)])。
  レガシー HUD の隠蔽、VideoPlayer 購読、HudPlayState による Loading/Error 判定、
  ポーズメニューの開閉をここに集約する。
- 画面別ビュー (GameHudView / GameReadyView / GameLoadingView / GameErrorView /
  PauseMenuView) はそれぞれ自分の Canvas を持つ:
  - HUD: order -1 (Top Shade がレガシー Canvas order 0 の歌詞より下に来る)
  - Ready / Loading / Error: order 1 (レガシー Canvas より上)
  - Pause: order 500 (全面スクリム)
  - 全 Canvas: CanvasConfigurator (Constant Pixel Size x1) + GraphicRaycaster

## 画面ごとの要点

| export | 要点 |
|---|---|
| 03-game-default | ランク (SS=1,000,000 / S=950,000 / A=900,000 / B=700,000) は FsL*2.5、色は SS/S=WarnLight、A/B/C=Text。スコアレール 600x14 r4、fill=score/1M*530、+ゾーン 70px (Warn)。COMBO は FsCombo。判定ポップアップは FsL、色は Perfect=AccentLight / Great=Text / Bad=WarnLight / Miss=DangerLight。Top Shade は 260px 高・黒 50% |
| 03b-game-over-1m | 03 と同じ HUD + FULL COMBO タグ (WarnTint 背景 / WarnLight 文字)。フルコンボ時のみ表示 |
| 03c-game-ready | 曲情報右上 (タイトル FsL*1.37 / メタ FsL*0.79)、ジャケット 192x108 r8 (YouTube サムネ、失敗時は Key2 の角丸)、中央 [Space] to Start (FsL*1.79)、Esc 凡例 |
| 03d-game-loading | 全面 Bg + 中央ローディング (不定バー)。loadingmovie は GameObject 残置・RawImage/VideoPlayer だけ無効化し、出し分けはこのビュー |
| 03e-game-youtube-error | ダイアログ 800 幅 r16 Raised2、▲ (Danger) + タイトル FsL + 本文 FsM、ボタンは Retry (primary) / 選曲へ (secondary + KeyCap) |
| 04-pause-settings | PAUSE ラベル FsM muted 左上、タブ 4 枚 (Settings/Display/Ranking/Keys) 800x48 r12 #222222、設定行 640 幅 (SettingRow 相当: アイコン+ラベル+値+ステッパ 40x40 #3A3A3A r8) |
| 04b-pause-display | 表示トグル 6 項目。Key Guide=KeyGuideUI、Lyrics=TextPanel、Progress Bar=gauge、Judge Text=判定ポップアップ、Live Rank/Hit Timing はデータ源が無いので表示のみ (報告済み) |
| 04c-pause-ranking | 左: 曲別 (自分の記録のみ)、右: 累計 (Hiscore 合計 + 記録曲数)。他プレイヤーのデータ源が無いので自分の行だけ |
| 04d-pause-keys | レーン見出し 8 本 (LaneColor) + JIS キーボード (KeyCap 72x72、レーンの代表キーは Judge.LaneKeys から) |

## 決定事項

- 固定ピクセル値 (rail 600x14, combo 180, judge 38, prompt 68 など) は
  `S` (= Margin/48) 倍してプロファイルに合わせる。
- 色/フォントは V5Tokens から。フォントは TMP_Settings.defaultFontAsset →
  Resources.Load(V5Tokens.FontAssetPath) のフォールバック。
- 判定ポップアップは Judge の Instantiate を止められないため、clone を毎フレーム
  非表示にして一本化する (GameHudView.SuppressJudgeClones)。
- 不定ロード表示: 進捗 API が無いため PingPong で幅を往復させる。

## 検証

Unity 実行は不可 (Supervisor が単一 Windows Editor を保有)。C# コンパイラで
構文チェック + 目視レビュー。実行確認は未検証として報告する。
