# Spec: 120 — v5 再実装: Title / Opening / Option

Issue: #120 (Refs #41) — branch `120`, base `release-0-1-3` (+ `origin/106` 基盤, `origin/109` KeyConfigUI)。

## 正本

- design/flashbeat-ui.pen **v5** — frames `Screens v5 · layers & shapes (FHD)` の `01 Title` / `06 Option · Play` / `06b Option · Display` / `06c Option · Data` (pen CLI で実測)
- exports: `design/exports/screens/01-title.png`, `06-option-play.png`, `06b-option-display.png`, `06c-option-data.png`
- Opening は v5 に画面フレームが存在しないため、Title の部品 (背景 + エッジ装飾 + ロゴ) で構成する (要報告事項)

## 成果物

1. `Assets/Game/Resources/V5/Common/` — 共有デコ資産 (本 Issue がオーナー):
   `edge-left.png` `edge-right.png` `13.png` `title-bg-hi.png` (design/assets からコピー)。
   他ワーカーは `Resources.Load<Texture2D>("V5/Common/...")` で参照してよい。
2. `Assets/Game/Scripts/UI/V5/V5Decor.cs` — 背景・エッジ装飾・ロゴの共有ビルダ。
   エッジ装飾は高さ連動幅 (380/1080) で左右エッジに固定し、全解像度で歪まない。
3. `Assets/Game/Scripts/UI/V5/TitleView.cs` — 01 Title をコードで再現:
   Bar H (0,424 / 1500x16) + Bar V (1484,440 / 16x640) `#EDEDED`、
   Logo (280,108.5 / 1280x253 contain)、Menu (256,603.5 / 行 332x64 / gap 10 /
   選択 `#525252`・非選択 `#2D2D2D`)、Catalog バッジ (652.5,695.5 / fs22 muted)、
   Legend (659,775.5 / KeyCap)。行は **GameStart / Option / Exit** のみ
   (History/Ranking は遷移先シーンが未実装のため非表示 — 要報告)。
   ナビゲーションはレイアウト非依存: ↑↓ (+W/S) で選択、Enter/Space で決定、
   マウスクリック。旧レター短縮キー (O/E) も維持。左下 (F2 ヒント) と
   右上 (更新通知) には何も描かない。
4. `Assets/Game/Scripts/UI/V5/OptionView.cs` — Option をコードで再現:
   ヘッダ (戻るボタン + OPTION fs52)、タブstrip (560,48 / 800幅 / Play・Display・Sound・Data)、
   左リスト (320,164 / 700幅 / SettingRow 64 + gap 10 / 選択 `#525252`)、
   右側 (1052,164 / 548幅: Preview well 300高 + 説明 fs38/fs30 muted)、
   Legend (48,1000)。Tab/↑↓/←→/Esc キー + マウス。
   - Play: NoteSpeed (±0.1) / NoteTiming (±1) / Key Layout (標準/カスタム) /
     キー設定 (KeyConfigUI.Open() を反射ガード呼び出し、無ければ Toggle()) + 「F2 でも開けます」
   - Display: Flash (On/Off) / FlashTiming (FlashT ±1) / FlashSpeed (FlashBG 1→2→4→8 循環)
   - Sound: Main / Effect / BGM Volume (±0.1, 0..1, %表記)
   - Data: Clear save data (PlayerPrefs の Hiscore + キーバインドを削除)
   - デザインにあるが実データの無い行 (Key Guide/Judge Text/Lyrics/Video Dim/Combo Display/
     Player Name/Ranking Submit/History Keep) は表示しない (偽 UI を作らない — 要報告)
5. `Assets/Game/Scripts/UI/V5/OpeningView.cs` — 暗背景 + 中央ロゴ。既存の
   6.031s 自動遷移は `OpeningSceneManager` に残す。
6. SceneManager 統合: `TitleSceneManager` / `OpeningSceneManager` / `OptionSceneManager`
   の Start で v5 View を構築し、レガシー Canvas を非アクティブ化。
   既存の公開メソッド (SelectScene/OptionScene/EndGame/playSound/SpeedUp/... ) を
   コールバックで再利用。`TitleSceneManager` のキー入力は View へ移管 (二重処理防止)。
7. `Assets/Tests/Editor/TitleOptionV5Tests.cs` — 純ロジックの NUnit テスト
   (カタログラベル、速度/音量のクランプと書式、KeyLayout 値、選択折り返し)。

## 非目標

- Select / Game / Result / Pause 画面 (他ワーカー)
- Judge / KeyBindings / KeyConfigUI / Fader / UpdateChecker / MusicManager の変更
- Cloudflare / 本番 API / Unity Editor 起動
